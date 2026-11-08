using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Pulse.Infrastructure.Persistence;

/// <summary>Lets `dotnet ef` build the model without starting the API. Only used for generating migrations.</summary>
public sealed class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<PulseDbContext>
{
    public PulseDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__Default")
            ?? "Host=localhost;Port=5432;Database=pulse;Username=pulse;Password=pulse_dev_password";

        var options = new DbContextOptionsBuilder<PulseDbContext>()
            .UseNpgsql(connectionString)
            .UseSnakeCaseNamingConvention()
            .Options;

        return new PulseDbContext(options);
    }
}
