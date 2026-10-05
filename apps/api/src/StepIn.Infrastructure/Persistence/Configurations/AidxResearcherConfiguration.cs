using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StepIn.Domain.Aidx;
using StepIn.Domain.Users;

namespace StepIn.Infrastructure.Persistence.Configurations;

public sealed class AidxResearcherConfiguration : IEntityTypeConfiguration<AidxResearcher>
{
    public void Configure(EntityTypeBuilder<AidxResearcher> builder)
    {
        builder.ToTable("AidxResearchers", "aidx");

        builder.Property(r => r.Slug).HasMaxLength(160).IsRequired();
        builder.Property(r => r.DisplayName).HasMaxLength(150).IsRequired();
        builder.Property(r => r.Category).HasConversion<string>().HasMaxLength(30);
        builder.Property(r => r.Position).HasMaxLength(200);
        builder.Property(r => r.Biography).HasMaxLength(5000);
        builder.Property(r => r.ProfileImageKey).HasMaxLength(500);
        builder.Property(r => r.OrcidUrl).HasMaxLength(500);
        builder.Property(r => r.GoogleScholarUrl).HasMaxLength(500);
        builder.Property(r => r.LinkedInUrl).HasMaxLength(500);
        builder.Property(r => r.WebsiteUrl).HasMaxLength(500);

        builder.HasIndex(r => r.Slug).IsUnique();
        builder.HasIndex(r => r.UserId).IsUnique().HasFilter("\"UserId\" IS NOT NULL");

        // Optional link to a StepIn account. Deleting the account only unlinks the
        // researcher profile; it never removes lab content.
        builder.HasOne<ApplicationUser>()
            .WithMany()
            .HasForeignKey(r => r.UserId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
