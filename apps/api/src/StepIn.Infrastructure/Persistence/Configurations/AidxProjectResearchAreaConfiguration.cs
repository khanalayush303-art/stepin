using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StepIn.Domain.Aidx;

namespace StepIn.Infrastructure.Persistence.Configurations;

public sealed class AidxProjectResearchAreaConfiguration : IEntityTypeConfiguration<AidxProjectResearchArea>
{
    public void Configure(EntityTypeBuilder<AidxProjectResearchArea> builder)
    {
        builder.ToTable("AidxProjectResearchAreas", "aidx");

        builder.HasKey(x => new { x.ProjectId, x.ResearchAreaId });

        builder.HasOne(x => x.ResearchArea)
            .WithMany()
            .HasForeignKey(x => x.ResearchAreaId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(x => x.ResearchAreaId);
    }
}
