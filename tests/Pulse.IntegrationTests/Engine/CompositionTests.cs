using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Pulse.Application;
using Pulse.Infrastructure;

namespace Pulse.IntegrationTests.Engine;

/// <summary>
/// The worker is a plain host, not the web app. In Development the container checks every registration when it is
/// built, so a service that needs something only the API provides stops the worker from starting at all.
/// </summary>
public class CompositionTests
{
    [Fact]
    public void The_worker_container_builds_with_every_registration_checked()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["ConnectionStrings:Default"] = "Host=localhost;Database=unused" })
            .Build();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IConfiguration>(configuration);
        services.AddApplication();
        services.AddInfrastructure(configuration);
        services.AddCheckEngine(configuration);

        var build = () => services.BuildServiceProvider(new ServiceProviderOptions { ValidateOnBuild = true, ValidateScopes = true });

        build.Should().NotThrow();
    }
}
