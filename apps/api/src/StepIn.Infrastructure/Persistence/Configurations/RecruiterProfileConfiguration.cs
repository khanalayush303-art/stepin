using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StepIn.Domain.Profiles;
using StepIn.Domain.Users;

namespace StepIn.Infrastructure.Persistence.Configurations;

public sealed class RecruiterProfileConfiguration : IEntityTypeConfiguration<RecruiterProfile>
{
    public void Configure(EntityTypeBuilder<RecruiterProfile> builder)
    {
        builder.ToTable("RecruiterProfiles");

        builder.Property(r => r.JobTitle).HasMaxLength(150);
        builder.Property(r => r.PhoneNumber).HasMaxLength(30);
        builder.Property(r => r.PhotoUrl).HasMaxLength(500);

        builder.HasIndex(r => r.UserId).IsUnique();

        // Foreign-key-only, same reasoning as CandidateProfileConfiguration.
        builder.HasOne<ApplicationUser>()
            .WithOne()
            .HasForeignKey<RecruiterProfile>(r => r.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        // Restrict, not Cascade: a company shouldn't vanish because a single
        // recruiter profile referencing it is deleted.
        builder.HasOne(r => r.Company)
            .WithMany()
            .HasForeignKey(r => r.CompanyId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
