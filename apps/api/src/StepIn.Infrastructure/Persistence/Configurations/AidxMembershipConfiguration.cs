using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StepIn.Domain.Aidx;
using StepIn.Domain.Users;

namespace StepIn.Infrastructure.Persistence.Configurations;

public sealed class AidxMembershipConfiguration : IEntityTypeConfiguration<AidxMembership>
{
    public void Configure(EntityTypeBuilder<AidxMembership> builder)
    {
        builder.ToTable("AidxMemberships", "aidx");

        builder.Property(m => m.Role).HasConversion<string>().HasMaxLength(20);
        builder.Property(m => m.Status).HasConversion<string>().HasMaxLength(20);

        // One membership row per user, ever. Role/scope changes and revocation
        // mutate this same row so every audit entry and CreatedByMembershipId
        // link always points at a stable id.
        builder.HasIndex(m => m.UserId).IsUnique();

        builder.HasOne<ApplicationUser>()
            .WithMany()
            .HasForeignKey(m => m.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne<AidxResearchArea>()
            .WithMany()
            .HasForeignKey(m => m.ScopedResearchAreaId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne<ApplicationUser>()
            .WithMany()
            .HasForeignKey(m => m.GrantedByUserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
