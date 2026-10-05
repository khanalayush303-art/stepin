using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StepIn.Domain.Aidx;

namespace StepIn.Infrastructure.Persistence.Configurations;

public sealed class AidxProjectTechnologyConfiguration : IEntityTypeConfiguration<AidxProjectTechnology>
{
    public void Configure(EntityTypeBuilder<AidxProjectTechnology> builder)
    {
        builder.ToTable("AidxProjectTechnologies", "aidx");

        builder.Property(t => t.Name).HasMaxLength(100).IsRequired();

        builder.HasIndex(t => new { t.ProjectId, t.Name }).IsUnique();
    }
}
