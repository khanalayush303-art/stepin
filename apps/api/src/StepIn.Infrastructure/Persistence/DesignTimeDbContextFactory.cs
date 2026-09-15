using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using StepIn.Application.Common.Interfaces;

namespace StepIn.Infrastructure.Persistence;

/// <summary>
/// Lets <c>dotnet ef migrations add</c> construct the context without booting the
/// whole web host. Reads the same environment variable Compose sets, so the
/// design-time and runtime connection strings never drift.
/// </summary>
public sealed class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<ApplicationDbContext>
{
    public ApplicationDbContext CreateDbContext(string[] args)
    {
        var connectionString =
            Environment.GetEnvironmentVariable("ConnectionStrings__Postgres")
            ?? "Host=localhost;Port=5432;Database=stepin;Username=stepin;Password=stepin";

        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseNpgsql(connectionString, npgsql =>
                npgsql.MigrationsHistoryTable("__ef_migrations_history", ApplicationDbContext.Schema))
            .Options;

        return new ApplicationDbContext(options, new DesignTimeClock());
    }

    private sealed class DesignTimeClock : IDateTimeProvider
    {
        public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
    }
}
