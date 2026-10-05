using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StepIn.Domain.Aidx;

namespace StepIn.Infrastructure.Persistence.Configurations;

public sealed class AidxPublicationConfiguration : IEntityTypeConfiguration<AidxPublication>
{
    public void Configure(EntityTypeBuilder<AidxPublication> builder)
    {
        builder.ToTable("AidxPublications", "aidx");

        builder.Property(p => p.Title).HasMaxLength(400).IsRequired();
        builder.Property(p => p.Abstract).HasMaxLength(10000);
        builder.Property(p => p.PublicationType).HasConversion<string>().HasMaxLength(30);
        builder.Property(p => p.Venue).HasMaxLength(300);
        builder.Property(p => p.Doi).HasMaxLength(200);
        builder.Property(p => p.ExternalUrl).HasMaxLength(500);
        builder.Property(p => p.PdfKey).HasMaxLength(500);

        builder.HasIndex(p => new { p.Published, p.Year });
        builder.HasIndex(p => p.Doi).IsUnique().HasFilter("\"Doi\" IS NOT NULL");
    }
}
