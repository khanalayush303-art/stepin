using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StepIn.Domain.Aidx;
using StepIn.Domain.Users;

namespace StepIn.Infrastructure.Persistence.Configurations;

public sealed class AidxMembershipAuditEntryConfiguration : IEntityTypeConfiguration<AidxMembershipAuditEntry>
{
    public void Configure(EntityTypeBuilder<AidxMembershipAuditEntry> builder)
    {
        builder.ToTable("AidxMembershipAuditEntries", "aidx");

        builder.Property(e => e.Action).HasConversion<string>().HasMaxLength(20);
        builder.Property(e => e.PreviousRole).HasConversion<string>().HasMaxLength(20);
        builder.Property(e => e.NewRole).HasConversion<string>().HasMaxLength(20);

        builder.HasIndex(e => e.MembershipId);

        builder.HasOne<AidxMembership>()
            .WithMany()
            .HasForeignKey(e => e.MembershipId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne<ApplicationUser>()
            .WithMany()
            .HasForeignKey(e => e.PerformedByUserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
