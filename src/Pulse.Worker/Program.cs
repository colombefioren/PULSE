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

    builder.Services.AddHostedService<HeartbeatService>();

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
