using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StepIn.Domain.Companies;
using StepIn.Domain.Jobs;
using StepIn.Domain.Aidx;
using StepIn.Domain.Profiles;

namespace StepIn.Infrastructure.Persistence.Configurations;

public sealed class JobConfiguration : IEntityTypeConfiguration<Job>
{
    public void Configure(EntityTypeBuilder<Job> builder)
    {
        builder.ToTable("Jobs");

        builder.Property(j => j.Title).HasMaxLength(200).IsRequired();
        builder.Property(j => j.Description).HasMaxLength(5000).IsRequired();
        builder.Property(j => j.EmploymentType).HasConversion<string>().HasMaxLength(20);
        builder.Property(j => j.WorkplaceType).HasConversion<string>().HasMaxLength(20);
        builder.Property(j => j.Location).HasMaxLength(200).IsRequired();
        builder.Property(j => j.Compensation).HasMaxLength(100);
        builder.Property(j => j.Skills).HasColumnType("text[]");
        builder.Property(j => j.Status).HasConversion<string>().HasMaxLength(20);
        builder.Property(j => j.Category).HasConversion<string>().HasMaxLength(20).HasDefaultValue(JobCategory.Career);

        builder.HasIndex(j => j.RecruiterProfileId);
        builder.HasIndex(j => new { j.Category, j.Status, j.PublishedAt });

        // Optional link for AIDX research opportunities. SetNull so deleting an AIDX
        // project never deletes the job or any applicant history attached to it.
        builder.HasOne(j => j.AidxProject)
            .WithMany()
            .HasForeignKey(j => j.AidxProjectId)
            .OnDelete(DeleteBehavior.SetNull);

        // Foreign-key-only, same reasoning as CandidateProfileConfiguration: no
        // navigation back from RecruiterProfile, so a job's owner is resolved by
        // querying, never by loading a RecruiterProfile.Jobs collection.
        builder.HasOne<RecruiterProfile>()
            .WithMany()
            .HasForeignKey(j => j.RecruiterProfileId)
            .OnDelete(DeleteBehavior.Cascade);

        // Real navigation (for displaying the company name), Restrict on delete —
        // same pattern and reasoning as RecruiterProfileConfiguration's Company FK.
        builder.HasOne(j => j.Company)
            .WithMany()
            .HasForeignKey(j => j.CompanyId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
