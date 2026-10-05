using Microsoft.EntityFrameworkCore;
using StepIn.Application.Common.Interfaces;
using StepIn.Domain.Aidx;
using StepIn.Domain.Applications;
using StepIn.Domain.Common;
using StepIn.Domain.Companies;
using StepIn.Domain.Jobs;
using StepIn.Domain.Profiles;
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

    public DbSet<CandidateProfile> CandidateProfiles => Set<CandidateProfile>();

    public DbSet<CandidateEducation> CandidateEducations => Set<CandidateEducation>();

    public DbSet<CandidateExperience> CandidateExperiences => Set<CandidateExperience>();

    public DbSet<CandidateCertification> CandidateCertifications => Set<CandidateCertification>();

    public DbSet<RecruiterProfile> RecruiterProfiles => Set<RecruiterProfile>();

    public DbSet<Company> Companies => Set<Company>();

    public DbSet<Job> Jobs => Set<Job>();

    public DbSet<JobApplication> JobApplications => Set<JobApplication>();

    public DbSet<SavedJob> SavedJobs => Set<SavedJob>();

    public DbSet<AidxResearchArea> AidxResearchAreas => Set<AidxResearchArea>();

    public DbSet<AidxProject> AidxProjects => Set<AidxProject>();

    public DbSet<AidxProjectResearchArea> AidxProjectResearchAreas => Set<AidxProjectResearchArea>();

    public DbSet<AidxProjectTechnology> AidxProjectTechnologies => Set<AidxProjectTechnology>();

    public DbSet<AidxResearcher> AidxResearchers => Set<AidxResearcher>();

    public DbSet<AidxProjectResearcher> AidxProjectResearchers => Set<AidxProjectResearcher>();

    public DbSet<AidxPublication> AidxPublications => Set<AidxPublication>();

    public DbSet<AidxPublicationAuthor> AidxPublicationAuthors => Set<AidxPublicationAuthor>();

    public DbSet<AidxPublicationResearchArea> AidxPublicationResearchAreas => Set<AidxPublicationResearchArea>();

    public DbSet<AidxPublicationProject> AidxPublicationProjects => Set<AidxPublicationProject>();

    public DbSet<AidxNews> AidxNews => Set<AidxNews>();

    public DbSet<AidxEvent> AidxEvents => Set<AidxEvent>();

    IQueryable<ApplicationUser> IApplicationDbContext.Users => Users;

    IQueryable<AidxResearchArea> IApplicationDbContext.AidxResearchAreas => AidxResearchAreas;

    IQueryable<AidxProject> IApplicationDbContext.AidxProjects => AidxProjects;

    IQueryable<AidxResearcher> IApplicationDbContext.AidxResearchers => AidxResearchers;

    IQueryable<AidxPublication> IApplicationDbContext.AidxPublications => AidxPublications;

    IQueryable<AidxPublicationAuthor> IApplicationDbContext.AidxPublicationAuthors => AidxPublicationAuthors;

    IQueryable<AidxNews> IApplicationDbContext.AidxNews => AidxNews;

    IQueryable<AidxEvent> IApplicationDbContext.AidxEvents => AidxEvents;

    IQueryable<CandidateProfile> IApplicationDbContext.CandidateProfiles => CandidateProfiles;

    IQueryable<CandidateEducation> IApplicationDbContext.CandidateEducations => CandidateEducations;

    IQueryable<CandidateExperience> IApplicationDbContext.CandidateExperiences => CandidateExperiences;

    IQueryable<CandidateCertification> IApplicationDbContext.CandidateCertifications => CandidateCertifications;

    IQueryable<RecruiterProfile> IApplicationDbContext.RecruiterProfiles => RecruiterProfiles;

    IQueryable<Company> IApplicationDbContext.Companies => Companies;

    IQueryable<Job> IApplicationDbContext.Jobs => Jobs;

    IQueryable<JobApplication> IApplicationDbContext.JobApplications => JobApplications;

    IQueryable<SavedJob> IApplicationDbContext.SavedJobs => SavedJobs;

    void IApplicationDbContext.Add<TEntity>(TEntity entity) => Set<TEntity>().Add(entity);

    void IApplicationDbContext.RemoveRange<TEntity>(IEnumerable<TEntity> entities) => Set<TEntity>().RemoveRange(entities);

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
