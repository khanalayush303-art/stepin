using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StepIn.Domain.Aidx;

namespace StepIn.Infrastructure.Persistence.Configurations;

public sealed class AidxEventConfiguration : IEntityTypeConfiguration<AidxEvent>
{
    public void Configure(EntityTypeBuilder<AidxEvent> builder)
    {
        builder.ToTable("AidxEvents", "aidx");

        builder.Property(e => e.Slug).HasMaxLength(220).IsRequired();
        builder.Property(e => e.Title).HasMaxLength(300).IsRequired();
        builder.Property(e => e.Description).HasMaxLength(10000).IsRequired();
        builder.Property(e => e.Location).HasMaxLength(300);
        builder.Property(e => e.RegistrationUrl).HasMaxLength(500);
        builder.Property(e => e.SpeakerName).HasMaxLength(200);
        builder.Property(e => e.ImageKey).HasMaxLength(500);
        builder.Property(e => e.ImageAltText).HasMaxLength(300);
        builder.Property(e => e.Status).HasConversion<string>().HasMaxLength(20);

        builder.HasIndex(e => e.Slug).IsUnique().HasFilter("\"RevisionOfId\" IS NULL");
        builder.HasIndex(e => new { e.Status, e.StartsAt });
        builder.HasIndex(e => e.CreatedByMembershipId);
        builder.HasIndex(e => e.RevisionOfId)
            .IsUnique()
            .HasDatabaseName("IX_AidxEvents_OneOpenRevisionPerCanonical")
            .HasFilter("\"RevisionOfId\" IS NOT NULL AND \"MergedAt\" IS NULL AND \"DiscardedAt\" IS NULL");

        builder.HasOne<AidxMembership>()
            .WithMany()
            .HasForeignKey(e => e.CreatedByMembershipId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne<AidxEvent>()
            .WithMany()
            .HasForeignKey(e => e.RevisionOfId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Property<uint>("xmin")
            .HasColumnName("xmin")
            .HasColumnType("xid")
            .ValueGeneratedOnAddOrUpdate()
            .IsConcurrencyToken();
    }
}
