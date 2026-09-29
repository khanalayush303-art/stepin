using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StepIn.Domain.Applications;
using StepIn.Domain.Jobs;
using StepIn.Domain.Profiles;

namespace StepIn.Infrastructure.Persistence.Configurations;

public sealed class JobApplicationConfiguration : IEntityTypeConfiguration<JobApplication>
{
    public void Configure(EntityTypeBuilder<JobApplication> builder)
    {
        builder.ToTable("JobApplications");

        builder.Property(a => a.CoverLetter).HasMaxLength(5000);
        builder.Property(a => a.ResumeStorageKey).HasMaxLength(300).IsRequired();
        builder.Property(a => a.ResumeOriginalFileName).HasMaxLength(255).IsRequired();
        builder.Property(a => a.ResumeContentType).HasMaxLength(100).IsRequired();
        builder.Property(a => a.Status).HasConversion<string>().HasMaxLength(20);

        // Release-blocking: the database-level guarantee against duplicate
        // applications, independent of the endpoint's own pre-check.
        builder.HasIndex(a => new { a.CandidateProfileId, a.JobId }).IsUnique();

        // Foreign-key-only, same reasoning as CandidateProfileConfiguration/JobConfiguration.
        builder.HasOne<CandidateProfile>()
            .WithMany()
            .HasForeignKey(a => a.CandidateProfileId)
            .OnDelete(DeleteBehavior.Cascade);

        // Real navigation (needed to project Job.Title/Job.Company.Name), Cascade.
        builder.HasOne(a => a.Job)
            .WithMany()
            .HasForeignKey(a => a.JobId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
