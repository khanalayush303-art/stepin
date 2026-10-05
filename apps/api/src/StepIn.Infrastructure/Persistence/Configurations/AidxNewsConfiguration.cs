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

        builder.HasIndex(n => n.Slug).IsUnique();
        builder.HasIndex(n => new { n.Status, n.PublishedAt });

        // Keep the news item if its author's researcher profile is removed.
        builder.HasOne<AidxResearcher>()
            .WithMany()
            .HasForeignKey(n => n.AuthorResearcherId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
