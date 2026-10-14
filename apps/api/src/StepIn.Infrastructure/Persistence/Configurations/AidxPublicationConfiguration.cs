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
        builder.Property(p => p.Status).HasConversion<string>().HasMaxLength(20);

        builder.HasIndex(p => new { p.Published, p.Year });
        // A revision copies the canonical row's DOI too (full field copy) — excluded here for the
        // same reason canonical/revision pairs never collide on slug uniqueness elsewhere.
        builder.HasIndex(p => p.Doi).IsUnique().HasFilter("\"Doi\" IS NOT NULL AND \"RevisionOfId\" IS NULL");
        builder.HasIndex(p => p.CreatedByMembershipId);
        builder.HasIndex(p => p.RevisionOfId)
            .IsUnique()
            .HasDatabaseName("IX_AidxPublications_OneOpenRevisionPerCanonical")
            .HasFilter("\"RevisionOfId\" IS NOT NULL AND \"MergedAt\" IS NULL AND \"DiscardedAt\" IS NULL");

        builder.HasOne<AidxMembership>()
            .WithMany()
            .HasForeignKey(p => p.CreatedByMembershipId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne<AidxPublication>()
            .WithMany()
            .HasForeignKey(p => p.RevisionOfId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Property<uint>("xmin")
            .HasColumnName("xmin")
            .HasColumnType("xid")
            .ValueGeneratedOnAddOrUpdate()
            .IsConcurrencyToken();
    }
}
