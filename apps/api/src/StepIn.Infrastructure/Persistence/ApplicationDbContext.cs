using Microsoft.EntityFrameworkCore;
using StepIn.Application.Common.Interfaces;
using StepIn.Domain.Common;
using StepIn.Domain.Users;

namespace StepIn.Infrastructure.Persistence;

/// <summary>The single EF Core context for the platform.</summary>
public sealed class ApplicationDbContext(
    DbContextOptions<ApplicationDbContext> options,
    IDateTimeProvider clock)
    : DbContext(options), IApplicationDbContext
{
    /// <summary>All tables live under this schema rather than <c>public</c>.</summary>
    public const string Schema = "stepin";

    public DbSet<ApplicationUser> Users => Set<ApplicationUser>();

    IQueryable<ApplicationUser> IApplicationDbContext.Users => Users;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);

        modelBuilder.HasDefaultSchema(Schema);

        // Entity configurations are discovered from this assembly, so adding a
        // new IEntityTypeConfiguration<T> is all a later phase needs to do.
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ApplicationDbContext).Assembly);
    }

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        ArgumentNullException.ThrowIfNull(configurationBuilder);

        // Postgres stores timestamptz; keep offsets rather than flattening to local time.
        configurationBuilder.Properties<DateTimeOffset>().HaveColumnType("timestamptz");
        configurationBuilder.Properties<string>().HaveMaxLength(512);

        base.ConfigureConventions(configurationBuilder);
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        StampTimestamps();
        return base.SaveChangesAsync(cancellationToken);
    }

    public override int SaveChanges()
    {
        StampTimestamps();
        return base.SaveChanges();
    }

    private void StampTimestamps()
    {
        var now = clock.UtcNow;

        foreach (var entry in ChangeTracker.Entries<Entity>())
        {
            switch (entry.State)
            {
                case EntityState.Added:
                    entry.Entity.MarkCreated(now);
                    break;
                case EntityState.Modified:
                    entry.Entity.MarkUpdated(now);
                    break;
                default:
                    break;
            }
        }
    }
}
