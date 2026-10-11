using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StepIn.Domain.Career;

namespace StepIn.Infrastructure.Persistence.Configurations;

public sealed class RoadmapItemConfiguration : IEntityTypeConfiguration<RoadmapItem>
{
    public void Configure(EntityTypeBuilder<RoadmapItem> builder)
    {
        builder.Property(i => i.Title).IsRequired().HasMaxLength(200);
        builder.Property(i => i.Description).HasMaxLength(2000);
        builder.Property(i => i.Priority).HasConversion<string>().HasMaxLength(10);
        builder.Property(i => i.Status).HasConversion<string>().HasMaxLength(20);

        builder.HasIndex(i => i.CareerGoalId);
        // Supports "see upcoming deadlines and completed milestones" without a full table scan.
        builder.HasIndex(i => new { i.CareerGoalId, i.Status });
    }
}
