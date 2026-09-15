using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using StepIn.Application.Common.Interfaces;
using StepIn.Domain.Common;
using StepIn.Infrastructure.Identity;

namespace StepIn.Infrastructure.Persistence;

/// <summary>
/// The single EF Core context for the platform, and the Identity store for
/// <see cref="ApplicationUser"/>/<see cref="IdentityRole{TKey}"/>.
///
/// Later phases add their own <c>DbSet</c> and an <c>IEntityTypeConfiguration</c>
/// beside it.
/// </summary>
public sealed class ApplicationDbContext(
    DbContextOptions<ApplicationDbContext> options,
    IDateTimeProvider clock)
    : IdentityDbContext<ApplicationUser, IdentityRole<Guid>, Guid>(options), IApplicationDbContext
{
    /// <summary>All tables live under this schema rather than <c>public</c>.</summary>
    public const string Schema = "stepin";

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);

        base.OnModelCreating(modelBuilder);

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

        // ApplicationUser can't inherit the Domain Entity base (it already
        // inherits IdentityUser<Guid>), so it's stamped separately here.
        foreach (var entry in ChangeTracker.Entries<ApplicationUser>())
        {
            switch (entry.State)
            {
                case EntityState.Added:
                    entry.Entity.CreatedAt = now;
                    break;
                case EntityState.Modified:
                    entry.Entity.UpdatedAt = now;
                    break;
                default:
                    break;
            }
        }
    }
}
