using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StepIn.Domain.Jobs;
using StepIn.Domain.Profiles;

namespace StepIn.Infrastructure.Persistence.Configurations;

public sealed class SavedJobConfiguration : IEntityTypeConfiguration<SavedJob>
{
    public void Configure(EntityTypeBuilder<SavedJob> builder)
    {
        builder.ToTable("SavedJobs");

        // Database-level guarantee against duplicate saves, independent of the
        // endpoint's own pre-check — same reasoning as JobApplicationConfiguration's
        // (CandidateProfileId, JobId) index.
        builder.HasIndex(s => new { s.CandidateProfileId, s.JobId }).IsUnique();

        // Foreign-key-only, same reasoning as JobApplicationConfiguration's
        // relationship to CandidateProfile.
        builder.HasOne<CandidateProfile>()
            .WithMany()
            .HasForeignKey(s => s.CandidateProfileId)
            .OnDelete(DeleteBehavior.Cascade);

        // Real navigation (needed to project Job details for the saved-jobs
        // list), Cascade — same as JobApplicationConfiguration's relationship to Job.
        builder.HasOne(s => s.Job)
            .WithMany()
            .HasForeignKey(s => s.JobId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
