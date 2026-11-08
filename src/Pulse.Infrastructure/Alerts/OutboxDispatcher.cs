using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Pulse.Application.Abstractions;
using Pulse.Application.Alerts;
using Pulse.Application.StatusPages;
using Pulse.Domain.Alerts;
using Pulse.Infrastructure.Persistence;

namespace Pulse.Infrastructure.Alerts;

/// <summary>
/// Sends the alerts written to the outbox. A message is claimed with <c>FOR UPDATE SKIP LOCKED</c> and leased for a
/// few minutes before it is sent, so any number of workers can run together and none holds a database lock during a
/// slow network call. A failed send is retried after 1 minute, 5, 15 and 60, then given up on. A worker that dies
/// mid-send leaves a lease that expires, after which the message is tried again: delivery is at least once.
/// </summary>
public sealed class OutboxDispatcher(IServiceScopeFactory scopes, TimeProvider clock, ILogger<OutboxDispatcher> logger)
{
    public const int Batch = 20;
    public static readonly TimeSpan LeaseDuration = TimeSpan.FromMinutes(5);
    private const int Concurrency = 8;

    public async Task RunAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(2), clock);
        try
        {
            do
            {
                try
                {
                    while (await DispatchOnceAsync(stoppingToken) == Batch)
                    {
                        // A full batch means there is probably more waiting.
                    }
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    logger.LogError(ex, "Alert dispatch pass failed");
                }
            }
            while (await timer.WaitForNextTickAsync(stoppingToken));
        }
        catch (OperationCanceledException)
        {
            // Shutting down.
        }
    }

    /// <summary>Claims due messages and sends them. Returns how many were claimed.</summary>
    public async Task<int> DispatchOnceAsync(CancellationToken cancellationToken)
    {
        var ids = await ClaimAsync(cancellationToken);
        await Parallel.ForEachAsync(ids, new ParallelOptions { MaxDegreeOfParallelism = Concurrency, CancellationToken = cancellationToken }, async (id, token) =>
        {
            try
            {
                await ProcessAsync(id, token);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "Could not process outbox message {Message}", id);
            }
        });

        return ids.Count;
    }

    private async Task<List<Guid>> ClaimAsync(CancellationToken cancellationToken)
    {
        await using var scope = scopes.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PulseDbContext>();
        var now = clock.GetUtcNow();

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var due = await db.OutboxMessages
            .FromSql($"""
                SELECT * FROM outbox_messages
                WHERE processed_at IS NULL AND next_attempt_at <= {now}
                ORDER BY next_attempt_at
                LIMIT {Batch}
                FOR UPDATE SKIP LOCKED
                """)
            .ToListAsync(cancellationToken);

        foreach (var message in due)
        {
            message.Lease(now, LeaseDuration);
        }

        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return due.Select(m => m.Id).ToList();
    }

    private async Task ProcessAsync(Guid messageId, CancellationToken cancellationToken)
    {
        await using var scope = scopes.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PulseDbContext>();
        var protector = scope.ServiceProvider.GetRequiredService<ISecretProtector>();

        var message = await db.OutboxMessages.FirstOrDefaultAsync(m => m.Id == messageId, cancellationToken);
        if (message is null || message.ProcessedAt is not null)
        {
            return;
        }

        if (message.Type == SubscriberNotifier.OutboxType)
        {
            await SendToSubscriberAsync(db, scope.ServiceProvider.GetRequiredService<SubscriberMailer>(), message, cancellationToken);
            return;
        }

        var deliveryId = JsonDocument.Parse(message.Payload).RootElement.GetProperty("deliveryId").GetGuid();
        var delivery = await db.AlertDeliveries.FirstOrDefaultAsync(d => d.Id == deliveryId, cancellationToken);
        var channel = delivery is null ? null : await db.AlertChannels.FirstOrDefaultAsync(c => c.Id == delivery.ChannelId, cancellationToken);
        if (delivery is null || channel is null || delivery.Status != DeliveryStatus.Pending)
        {
            // The channel or delivery was deleted, or it was already sent: there is nothing left to do.
            message.Complete(clock.GetUtcNow());
            await db.SaveChangesAsync(cancellationToken);
            return;
        }

        try
        {
            var config = AlertJson.Deserialize<AlertConfig>(protector.Unprotect(channel.ProtectedConfig));
            // Resolved per message: the transport sends mail through a scoped sender, so it must not outlive its scope.
            var transport = scope.ServiceProvider.GetRequiredService<IAlertTransport>();
            await transport.SendAsync(channel.Type, config, AlertJson.Deserialize<AlertPayload>(delivery.Payload), delivery.Id, cancellationToken);

            var now = clock.GetUtcNow();
            delivery.MarkSent(now);
            message.Complete(now);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            var reason = ex is AlertSendException ? ex.Message : "Sending failed unexpectedly.";
            if (ex is not AlertSendException)
            {
                logger.LogError(ex, "Alert {Delivery} failed unexpectedly", delivery.Id);
            }

            message.Fail(reason, clock.GetUtcNow());
            delivery.MarkAttemptFailed(reason, willRetry: message.ProcessedAt is null);
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    /// <summary>An incident email to one status page subscriber. A subscriber who left in the meantime simply drops out.</summary>
    private async Task SendToSubscriberAsync(PulseDbContext db, SubscriberMailer mailer, OutboxMessage message, CancellationToken cancellationToken)
    {
        using var payload = JsonDocument.Parse(message.Payload);
        var root = payload.RootElement;

        try
        {
            await mailer.SendAsync(root.GetProperty("subscriberId").GetGuid(), root.GetProperty("incidentId").GetGuid(), root.GetProperty("updateId").GetGuid(), cancellationToken);
            message.Complete(clock.GetUtcNow());
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "Subscriber email {Message} failed", message.Id);
            message.Fail("Sending the email failed.", clock.GetUtcNow());
        }

        await db.SaveChangesAsync(cancellationToken);
    }
}
