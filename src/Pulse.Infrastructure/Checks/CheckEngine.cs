using System.Threading.Channels;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Npgsql;
using Pulse.Application.Abstractions;
using Pulse.Application.Incidents;
using Pulse.Domain.Monitors;
using Pulse.Infrastructure.Monitoring;
using Pulse.Infrastructure.Persistence;

namespace Pulse.Infrastructure.Checks;

public sealed class CheckEngineOptions
{
    public const string Section = "Checks";

    /// <summary>How many checks run at the same time.</summary>
    public int Concurrency { get; set; } = 32;

    /// <summary>How many claimed checks may wait for a free runner. The scheduler stops claiming when it is full.</summary>
    public int QueueCapacity { get; set; } = 1000;

    /// <summary>The most monitors claimed in one pass.</summary>
    public int ClaimBatch { get; set; } = 200;

    public int TickMilliseconds { get; set; } = 1000;
}

/// <summary>
/// The check engine: a scheduler that claims due monitors once a second, a bounded queue, and a fixed pool of
/// runners that perform the checks and record what they find.
/// <para>
/// Claiming uses <c>FOR UPDATE SKIP LOCKED</c> and moves <c>next_check_at</c> forward in the same transaction, so
/// any number of worker instances can run side by side and no monitor is ever checked twice for the same slot.
/// </para>
/// </summary>
public sealed class CheckEngine
{
    private readonly IServiceScopeFactory _scopes;
    private readonly IHttpChecker _checker;
    private readonly TimeProvider _clock;
    private readonly CheckEngineOptions _options;
    private readonly ILogger<CheckEngine> _logger;
    private readonly Channel<CheckTarget> _queue;

    public CheckEngine(
        IServiceScopeFactory scopes,
        IHttpChecker checker,
        TimeProvider clock,
        IOptions<CheckEngineOptions> options,
        ILogger<CheckEngine> logger)
    {
        _scopes = scopes;
        _checker = checker;
        _clock = clock;
        _options = options.Value;
        _logger = logger;
        _queue = Channel.CreateBounded<CheckTarget>(new BoundedChannelOptions(_options.QueueCapacity)
        {
            SingleWriter = true,
            FullMode = BoundedChannelFullMode.Wait,
        });
    }

    /// <summary>Runs until cancelled: starts the runners, then claims due monitors every tick.</summary>
    public async Task RunAsync(CancellationToken stoppingToken)
    {
        var runners = Enumerable.Range(0, _options.Concurrency)
            .Select(_ => Task.Run(() => RunnerAsync(stoppingToken), CancellationToken.None))
            .ToArray();

        using var timer = new PeriodicTimer(TimeSpan.FromMilliseconds(_options.TickMilliseconds), _clock);
        try
        {
            do
            {
                try
                {
                    await ScheduleOnceAsync(stoppingToken);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    // A database blip must not stop the engine; the next tick tries again.
                    _logger.LogError(ex, "Scheduling pass failed");
                }
            }
            while (await timer.WaitForNextTickAsync(stoppingToken));
        }
        catch (OperationCanceledException)
        {
            // Shutting down.
        }
        finally
        {
            _queue.Writer.TryComplete();
            await Task.WhenAll(runners);
        }
    }

    /// <summary>Claims due monitors and queues them. Returns how many were queued.</summary>
    public async Task<int> ScheduleOnceAsync(CancellationToken cancellationToken)
    {
        var room = _options.QueueCapacity - _queue.Reader.Count;
        var batch = Math.Min(room, _options.ClaimBatch);
        if (batch <= 0)
        {
            return 0;
        }

        var claimed = await ClaimDueAsync(batch, cancellationToken);
        foreach (var target in claimed)
        {
            await _queue.Writer.WriteAsync(target, cancellationToken);
        }

        return claimed.Count;
    }

    /// <summary>How many claimed checks are waiting for a runner.</summary>
    public int Waiting => _queue.Reader.Count;

    /// <summary>Claims up to <paramref name="batch"/> due monitors and moves each one's next check forward, in one transaction.</summary>
    public async Task<IReadOnlyList<CheckTarget>> ClaimDueAsync(int batch, CancellationToken cancellationToken)
    {
        await using var scope = _scopes.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PulseDbContext>();
        var now = _clock.GetUtcNow();

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);

        // "m.*" leaves out PostgreSQL's hidden xmin column, which the model uses as its concurrency token.
        var due = await db.Monitors
            .FromSql($"""
                SELECT m.*, m.xmin FROM monitors m
                WHERE m.state <> 'Paused' AND m.next_check_at <= {now}
                ORDER BY m.next_check_at
                LIMIT {batch}
                FOR UPDATE OF m SKIP LOCKED
                """)
            .ToListAsync(cancellationToken);

        foreach (var monitor in due)
        {
            monitor.ScheduleNext(now, Random.Shared.NextDouble());
        }

        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return due.Select(CheckTarget.From).ToList();
    }

    private async Task RunnerAsync(CancellationToken stoppingToken)
    {
        await foreach (var target in _queue.Reader.ReadAllAsync(CancellationToken.None))
        {
            try
            {
                var outcome = await _checker.CheckAsync(target, stoppingToken);
                await RecordAsync(target, outcome, CancellationToken.None);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                // Shutting down: this check is simply not recorded, and the monitor is picked up again on restart.
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Check of {Monitor} failed to complete", target.MonitorId);
            }
        }
    }

    /// <summary>Stores one result and folds it into the monitor, retrying if an edit landed at the same moment.</summary>
    public async Task RecordAsync(CheckTarget target, CheckOutcome outcome, CancellationToken cancellationToken)
    {
        for (var attempt = 1; ; attempt++)
        {
            await using var scope = _scopes.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<PulseDbContext>();
            var now = _clock.GetUtcNow();

            var monitor = await db.Monitors.FirstOrDefaultAsync(m => m.Id == target.MonitorId, cancellationToken);
            if (monitor is null || monitor.IsPaused)
            {
                return; // deleted or paused while the check was running
            }

            db.CheckResults.Add(new CheckResult(monitor.Id, now, outcome));
            var change = monitor.Apply(outcome, now);

            // A state change may open or resolve an incident; its alerts are staged here and saved with the result.
            IncidentChange? incident = change is null
                ? null
                : await scope.ServiceProvider.GetRequiredService<IncidentWorkflow>().ApplyAsync(monitor, change, now, cancellationToken);

            try
            {
                await db.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateConcurrencyException) when (attempt < 3)
            {
                continue;
            }
            catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.CheckViolation } && attempt < 3)
            {
                // No partition for this month yet: make them and try again.
                await scope.ServiceProvider.GetRequiredService<CheckPartitions>().EnsureUpcomingAsync(now, cancellationToken);
                continue;
            }

            await PublishAsync(scope.ServiceProvider.GetRequiredService<ILiveEvents>(), monitor, outcome, now, change, incident, cancellationToken);
            return;
        }
    }

    private async Task PublishAsync(ILiveEvents events, HttpMonitor monitor, CheckOutcome outcome, DateTimeOffset at, StateChange? change, IncidentChange? incident, CancellationToken cancellationToken)
    {
        try
        {
            await events.PublishAsync(
                new LiveEvent("CheckRecorded", monitor.OrganizationId, new
                {
                    monitorId = monitor.Id,
                    checkedAt = at,
                    success = outcome.Success,
                    statusCode = outcome.StatusCode,
                    responseMs = outcome.ResponseMs,
                    errorType = outcome.ErrorType,
                    errorMessage = outcome.ErrorMessage,
                    state = monitor.State,
                }),
                cancellationToken);

            if (change is not null)
            {
                _logger.LogInformation("Monitor {Monitor} went {From} -> {To}", monitor.Id, change.From, change.To);
                await events.PublishAsync(
                    new LiveEvent("MonitorStatusChanged", monitor.OrganizationId, new
                    {
                        monitorId = monitor.Id,
                        name = monitor.Name,
                        from = change.From,
                        to = change.To,
                        at,
                    }),
                    cancellationToken);
            }

            if (incident is not null)
            {
                await events.PublishAsync(new LiveEvent(incident.Type, monitor.OrganizationId, IncidentEvents.Data(incident.Incident)), cancellationToken);
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // The result is already stored; a missed live update is not worth failing the check for.
            _logger.LogWarning(ex, "Could not announce the result for {Monitor}", monitor.Id);
        }
    }
}
