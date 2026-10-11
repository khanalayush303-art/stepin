using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StepIn.Domain.Career;

namespace StepIn.Infrastructure.Persistence.Configurations;

public sealed class CareerGoalConfiguration : IEntityTypeConfiguration<CareerGoal>
{
    public void Configure(EntityTypeBuilder<CareerGoal> builder)
    {
        builder.Property(g => g.TargetRole).IsRequired().HasMaxLength(150);
        builder.Property(g => g.Description).HasMaxLength(2000);
        builder.Property(g => g.Status).HasConversion<string>().HasMaxLength(20);

        builder.HasIndex(g => g.CandidateProfileId);
        builder.HasIndex(g => g.TargetJobId);

        // Optional link to a target job. SetNull so nothing about a job's own lifecycle can ever
        // remove a candidate's own goal — Jobs are never deleted today (only published/
        // unpublished), but this matches the same defensive pattern Job.AidxProjectId uses.
        builder.HasOne(g => g.TargetJob)
            .WithMany()
            .HasForeignKey(g => g.TargetJobId)
            .OnDelete(DeleteBehavior.SetNull);

        // Deleting a goal takes its own roadmap items with it — there is no independent use for
        // a roadmap item once its goal is gone.
        builder.HasMany(g => g.RoadmapItems)
            .WithOne()
            .HasForeignKey(i => i.CareerGoalId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
