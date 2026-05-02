using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Keydral.Storage;

/// <summary>
/// Design-time factory for creating ApplicationDbContext instances.
/// Used by EF Core tools for migrations without needing a full DI container.
/// </summary>
public class ApplicationDbContextFactory : IDesignTimeDbContextFactory<ApplicationDbContext>
{
    /// <summary>
    /// Creates an ApplicationDbContext instance for design-time operations (migrations).
    /// </summary>
    public ApplicationDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<ApplicationDbContext>();

        // Default connection string for local development
        // Can be overridden by setting ASPNETCORE_ENVIRONMENT and appsettings.Development.json
        var connectionString = Environment.GetEnvironmentVariable("DATABASE_CONNECTION_STRING")
            ?? "Server=localhost;Port=5432;Database=keydral;User Id=keydral;Password=keydral_dev_password;";

        optionsBuilder.UseNpgsql(connectionString);

        return new ApplicationDbContext(optionsBuilder.Options);
    }
}
