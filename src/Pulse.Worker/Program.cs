using Pulse.Application;
using Pulse.Application.Common;
using Pulse.Infrastructure;
using Pulse.Worker;
using Serilog;
using Serilog.Formatting.Compact;

Log.Logger = new LoggerConfiguration()
    .WriteTo.Console(new RenderedCompactJsonFormatter())
    .CreateLogger();

try
{
    var builder = Host.CreateApplicationBuilder(args);

    builder.Services.AddSerilog((services, configuration) => configuration
        .ReadFrom.Configuration(builder.Configuration)
        .ReadFrom.Services(services)
        .Enrich.FromLogContext()
        .Enrich.WithProperty("Service", "pulse-worker")
        .WriteTo.Console(new RenderedCompactJsonFormatter()));

    builder.Services.Configure<AppOptions>(builder.Configuration.GetSection(AppOptions.Section));
    builder.Services.AddApplication();
    builder.Services.AddInfrastructure(builder.Configuration);
    builder.Services.AddCheckEngine(builder.Configuration);
    builder.Services.AddHostedService<PartitionMaintenanceService>();
    builder.Services.AddHostedService<CheckEngineService>();
    builder.Services.AddHostedService<AlertDispatcherService>();
    builder.Services.AddHostedService<RollupService>();

    builder.Build().Run();
}
catch (Exception ex) when (ex is not HostAbortedException)
{
    Log.Fatal(ex, "Pulse worker terminated unexpectedly");
    throw;
}
finally
{
    Log.CloseAndFlush();
}
