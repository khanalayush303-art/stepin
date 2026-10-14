using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StepIn.Domain.Aidx;
using StepIn.Domain.Users;

namespace StepIn.Infrastructure.Persistence.Configurations;

public sealed class AidxEditorialAuditEntryConfiguration : IEntityTypeConfiguration<AidxEditorialAuditEntry>
{
    public void Configure(EntityTypeBuilder<AidxEditorialAuditEntry> builder)
    {
        builder.ToTable("AidxEditorialAuditEntries", "aidx");

        builder.Property(e => e.ContentType).HasConversion<string>().HasMaxLength(20);
        builder.Property(e => e.Action).HasConversion<string>().HasMaxLength(30);
        builder.Property(e => e.Comment).HasMaxLength(5000);

        builder.HasIndex(e => new { e.ContentType, e.SubjectId });

        builder.HasOne<AidxContentSnapshot>()
            .WithMany()
            .HasForeignKey(e => e.SnapshotId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne<ApplicationUser>()
            .WithMany()
            .HasForeignKey(e => e.PerformedByUserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<AidxMembership>()
            .WithMany()
            .HasForeignKey(e => e.PerformedByMembershipId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
