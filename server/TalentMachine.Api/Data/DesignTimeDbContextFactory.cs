using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;
using TalentMachine.Api.Auth;

namespace TalentMachine.Api.Data;

/// <summary>
/// Used only by the EF Core tools (`dotnet ef migrations` / `database update`).
/// AppDbContext requires an <see cref="ICurrentTenant"/>; design time has no
/// request, so we hand it an empty one. Migrations don't need a live tenant.
///
/// Reads the same configuration the app uses at runtime (appsettings.json +
/// user-secrets + the gitignored local override), so `dotnet ef` targets whatever
/// database you've configured locally rather than a hardcoded connection string.
/// </summary>
public class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        var config = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: true)
            .AddJsonFile("appsettings.Development.json", optional: true)
            .AddUserSecrets<AppDbContext>(optional: true)
            // Same override the app loads last — see Program.cs for why.
            .AddJsonFile("appsettings.Development.local.json", optional: true)
            .AddEnvironmentVariables()
            .Build();

        var connectionString = config.GetConnectionString("Default")
            ?? "Host=localhost;Port=5433;Database=talentmachine;Username=postgres;Password=postgres";

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(connectionString)
            .Options;
        return new AppDbContext(options, new CurrentTenant());
    }
}
