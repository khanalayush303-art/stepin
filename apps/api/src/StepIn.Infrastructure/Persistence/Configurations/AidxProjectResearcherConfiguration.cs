using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StepIn.Domain.Aidx;

namespace StepIn.Infrastructure.Persistence.Configurations;

public sealed class AidxProjectResearcherConfiguration : IEntityTypeConfiguration<AidxProjectResearcher>
{
    public void Configure(EntityTypeBuilder<AidxProjectResearcher> builder)
    {
        builder.ToTable("AidxProjectResearchers", "aidx");

        builder.HasKey(x => new { x.ProjectId, x.ResearcherId });

        builder.Property(x => x.Role).HasMaxLength(150);

        builder.HasOne(x => x.Researcher)
            .WithMany()
            .HasForeignKey(x => x.ResearcherId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(x => x.ResearcherId);
    }
}
