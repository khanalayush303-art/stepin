using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StepIn.Domain.Aidx;

namespace StepIn.Infrastructure.Persistence.Configurations;

public sealed class AidxPublicationResearchAreaConfiguration : IEntityTypeConfiguration<AidxPublicationResearchArea>
{
    public void Configure(EntityTypeBuilder<AidxPublicationResearchArea> builder)
    {
        builder.ToTable("AidxPublicationResearchAreas", "aidx");

        builder.HasKey(x => new { x.PublicationId, x.ResearchAreaId });

        builder.HasOne<AidxPublication>()
            .WithMany(p => p.ResearchAreas)
            .HasForeignKey(x => x.PublicationId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.ResearchArea)
            .WithMany()
            .HasForeignKey(x => x.ResearchAreaId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(x => x.ResearchAreaId);
    }
}
