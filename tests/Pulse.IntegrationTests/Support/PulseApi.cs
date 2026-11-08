using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Npgsql;
using Pulse.Application.Abstractions;
using Testcontainers.PostgreSql;

namespace Pulse.IntegrationTests.Support;

/// <summary>
/// The whole API running in-process against a real PostgreSQL. One instance is shared by every test class in
/// the "api" collection, so the database starts once. Tests keep apart by using unique emails.
/// <para>
/// By default PostgreSQL comes from a Testcontainers container (what CI uses). Set
/// <c>PULSE_TEST_POSTGRES</c> to a connection string for an already-running server (for example
/// <c>Host=localhost;Port=5432;Username=postgres;Password=...</c>) to skip Docker: the fixture creates a
/// scratch database on it and drops it afterwards.
/// </para>
/// </summary>
public sealed class PulseApi : WebApplicationFactory<Program>, IAsyncLifetime
{
    public const string ExternalServerVariable = "PULSE_TEST_POSTGRES";

    private readonly string? _externalServer = Environment.GetEnvironmentVariable(ExternalServerVariable);
    private readonly PostgreSqlContainer? _container;
    private string _scratchDatabase = string.Empty;

    public PulseApi()
    {
        if (string.IsNullOrWhiteSpace(_externalServer))
        {
            _container = new PostgreSqlBuilder("postgres:16-alpine").Build();
        }
    }

    public CapturingEmailSender Emails { get; } = new();

    public string ConnectionString { get; private set; } = string.Empty;

    public async Task InitializeAsync()
    {
        if (_container is not null)
        {
            await _container.StartAsync();
            ConnectionString = _container.GetConnectionString();
            return;
        }

        _scratchDatabase = $"pulse_test_{Guid.NewGuid():N}";
        await using (var admin = new NpgsqlConnection(_externalServer))
        {
            await admin.OpenAsync();
            await using var create = new NpgsqlCommand($"create database {_scratchDatabase}", admin);
            await create.ExecuteNonQueryAsync();
        }

        ConnectionString = new NpgsqlConnectionStringBuilder(_externalServer) { Database = _scratchDatabase }.ConnectionString;
    }

    async Task IAsyncLifetime.DisposeAsync()
    {
        if (_container is not null)
        {
            await _container.DisposeAsync();
            return;
        }

        NpgsqlConnection.ClearAllPools();
        await using var admin = new NpgsqlConnection(_externalServer);
        await admin.OpenAsync();
        await using var drop = new NpgsqlCommand($"drop database if exists {_scratchDatabase} with (force)", admin);
        await drop.ExecuteNonQueryAsync();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseSetting("ConnectionStrings:Default", ConnectionString);
        builder.UseSetting("Database:MigrateOnStartup", "true");
        builder.UseSetting("RateLimiting:AuthPermitsPerMinute", "10000");
        builder.UseSetting("App:PublicUrl", "https://pulse.test");
        builder.UseSetting("RateLimiting:PublicReadPermitsPerMinute", "100000");
        builder.UseSetting("RateLimiting:PublicWritePermitsPerMinute", "100000");
        // Tests look at a page right after changing it, so the public page is not cached unless a test asks for it.
        builder.UseSetting("StatusPage:CacheSeconds", "0");
        builder.UseSetting("Badge:CacheSeconds", "0");
        builder.UseSetting("Demo:Enabled", "true");

        builder.ConfigureServices(services =>
        {
            services.RemoveAll<IEmailSender>();
            services.AddSingleton<IEmailSender>(Emails);

            // No DNS in tests: names resolve to nothing, and one reserved name is treated as internal.
            services.RemoveAll<IMonitorUrlGuard>();
            services.AddSingleton<IMonitorUrlGuard>(new FakeUrlGuard());
        });
    }

    /// <summary>Runs SQL against the test database, for setting up states the API cannot reach (like an old invite).</summary>
    public async Task Execute(string sql, params object[] parameters)
    {
        await using var source = new NpgsqlDataSourceBuilder(ConnectionString).Build();
        await using var command = source.CreateCommand(sql);
        foreach (var parameter in parameters)
        {
            command.Parameters.AddWithValue(parameter);
        }

        await command.ExecuteNonQueryAsync();
    }

    /// <summary>Reads one value, for facts only the database knows (like a token that is only ever sent by email).</summary>
    public async Task<T?> Scalar<T>(string sql, params object[] parameters)
    {
        await using var source = new NpgsqlDataSourceBuilder(ConnectionString).Build();
        await using var command = source.CreateCommand(sql);
        foreach (var parameter in parameters)
        {
            command.Parameters.AddWithValue(parameter);
        }

        var value = await command.ExecuteScalarAsync();
        return value is null or DBNull ? default : (T)value;
    }

    /// <summary>A new client with its own cookie jar, like a separate browser.</summary>
    public ApiClient NewClient() => new(CreateClient());

    /// <summary>A client that does not follow redirects, to look at where a link sends a visitor.</summary>
    public ApiClient NewClientWithoutRedirects() => new(CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false }));
}

[CollectionDefinition(Name)]
public sealed class ApiCollection : ICollectionFixture<PulseApi>
{
    public const string Name = "api";
}
