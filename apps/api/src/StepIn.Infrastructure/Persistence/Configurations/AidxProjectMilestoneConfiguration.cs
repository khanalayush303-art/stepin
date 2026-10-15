using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StepIn.Domain.Aidx;

namespace StepIn.Infrastructure.Persistence.Configurations;

public sealed class AidxProjectMilestoneConfiguration : IEntityTypeConfiguration<AidxProjectMilestone>
{
    public void Configure(EntityTypeBuilder<AidxProjectMilestone> builder)
    {
        builder.ToTable("AidxProjectMilestones", "aidx");

        builder.Property(m => m.Title).HasMaxLength(300).IsRequired();
        builder.Property(m => m.Description).HasMaxLength(5000);

        builder.HasIndex(m => m.ProjectId);

        builder.HasOne<AidxProject>()
            .WithMany(p => p.Milestones)
            .HasForeignKey(m => m.ProjectId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
