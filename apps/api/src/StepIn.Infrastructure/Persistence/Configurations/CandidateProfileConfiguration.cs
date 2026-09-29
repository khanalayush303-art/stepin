using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StepIn.Domain.Profiles;
using StepIn.Domain.Users;

namespace StepIn.Infrastructure.Persistence.Configurations;

public sealed class CandidateProfileConfiguration : IEntityTypeConfiguration<CandidateProfile>
{
    public void Configure(EntityTypeBuilder<CandidateProfile> builder)
    {
        builder.ToTable("CandidateProfiles");

        builder.Property(p => p.PhoneNumber).HasMaxLength(30);
        builder.Property(p => p.Location).HasMaxLength(200);
        builder.Property(p => p.Headline).HasMaxLength(200);
        builder.Property(p => p.Bio).HasMaxLength(2000);
        builder.Property(p => p.PhotoUrl).HasMaxLength(500);
        builder.Property(p => p.LinkedInUrl).HasMaxLength(300);
        builder.Property(p => p.PortfolioUrl).HasMaxLength(300);
        builder.Property(p => p.GitHubUrl).HasMaxLength(300);
        builder.Property(p => p.Skills).HasColumnType("text[]");

        builder.HasIndex(p => p.UserId).IsUnique();

        // Foreign-key-only: ApplicationUser has no navigation back here, so
        // StepIn.Domain.Users stays decoupled from StepIn.Domain.Profiles.
        builder.HasOne<ApplicationUser>()
            .WithOne()
            .HasForeignKey<CandidateProfile>(p => p.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(p => p.Education)
            .WithOne()
            .HasForeignKey(e => e.CandidateProfileId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(p => p.Experience)
            .WithOne()
            .HasForeignKey(e => e.CandidateProfileId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(p => p.Certifications)
            .WithOne()
            .HasForeignKey(e => e.CandidateProfileId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
