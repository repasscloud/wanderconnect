using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;

namespace Swoovr.Infrastructure.Persistence;

public static class PersistenceSetup
{
    /// <summary>
    /// Registers the database when a connection string is configured. Without one nothing is
    /// registered and callers must treat persistence as unavailable (fail closed).
    /// </summary>
    public static bool AddSwoovrPersistence(this IServiceCollection services, string? connectionString)
    {
        if (string.IsNullOrWhiteSpace(connectionString)) return false;
        services.AddDbContext<SwoovrDbContext>((provider, options) => options.UseNpgsql(connectionString,
            npgsql => npgsql.MigrationsHistoryTable("__ef_migrations_history")).AddInterceptors(provider.GetServices<ISaveChangesInterceptor>()));
        return true;
    }
}

/// <summary>Lets `dotnet ef migrations add` build the model; it never connects.</summary>
public sealed class DesignTimeFactory : IDesignTimeDbContextFactory<SwoovrDbContext>
{
    public SwoovrDbContext CreateDbContext(string[] args) => new(new DbContextOptionsBuilder<SwoovrDbContext>()
        .UseNpgsql("Host=design-time-only", npgsql => npgsql.MigrationsHistoryTable("__ef_migrations_history")).Options);
}
