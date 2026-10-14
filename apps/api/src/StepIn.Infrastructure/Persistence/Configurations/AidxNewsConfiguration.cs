using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StepIn.Domain.Aidx;

namespace StepIn.Infrastructure.Persistence.Configurations;

public sealed class AidxNewsConfiguration : IEntityTypeConfiguration<AidxNews>
{
    public void Configure(EntityTypeBuilder<AidxNews> builder)
    {
        builder.ToTable("AidxNews", "aidx");

        builder.Property(n => n.Slug).HasMaxLength(220).IsRequired();
        builder.Property(n => n.Title).HasMaxLength(300).IsRequired();
        builder.Property(n => n.Summary).HasMaxLength(500).IsRequired();
        builder.Property(n => n.Body).HasMaxLength(20000).IsRequired();
        builder.Property(n => n.ImageKey).HasMaxLength(500);
        builder.Property(n => n.Status).HasConversion<string>().HasMaxLength(20);

        builder.HasIndex(n => n.Slug).IsUnique().HasFilter("\"RevisionOfId\" IS NULL");
        builder.HasIndex(n => new { n.Status, n.PublishedAt });
        builder.HasIndex(n => n.CreatedByMembershipId);
        builder.HasIndex(n => n.RevisionOfId)
            .IsUnique()
            .HasDatabaseName("IX_AidxNews_OneOpenRevisionPerCanonical")
            .HasFilter("\"RevisionOfId\" IS NOT NULL AND \"MergedAt\" IS NULL AND \"DiscardedAt\" IS NULL");

        // Keep the news item if its author's researcher profile is removed.
        builder.HasOne<AidxResearcher>()
            .WithMany()
            .HasForeignKey(n => n.AuthorResearcherId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne<AidxMembership>()
            .WithMany()
            .HasForeignKey(n => n.CreatedByMembershipId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne<AidxNews>()
            .WithMany()
            .HasForeignKey(n => n.RevisionOfId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Property<uint>("xmin")
            .HasColumnName("xmin")
            .HasColumnType("xid")
            .ValueGeneratedOnAddOrUpdate()
            .IsConcurrencyToken();
    }
}
