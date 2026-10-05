using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StepIn.Domain.Aidx;

namespace StepIn.Infrastructure.Persistence.Configurations;

public sealed class AidxResearchAreaConfiguration : IEntityTypeConfiguration<AidxResearchArea>
{
    public void Configure(EntityTypeBuilder<AidxResearchArea> builder)
    {
        builder.ToTable("AidxResearchAreas", "aidx");

        builder.Property(a => a.Name).HasMaxLength(150).IsRequired();
        builder.Property(a => a.Slug).HasMaxLength(160).IsRequired();
        builder.Property(a => a.Description).HasMaxLength(2000);

        builder.HasIndex(a => a.Slug).IsUnique();
        builder.HasIndex(a => a.Name).IsUnique();
    }
}
