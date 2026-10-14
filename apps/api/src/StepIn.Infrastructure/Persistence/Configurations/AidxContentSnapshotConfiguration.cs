using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StepIn.Domain.Aidx;

namespace StepIn.Infrastructure.Persistence.Configurations;

public sealed class AidxContentSnapshotConfiguration : IEntityTypeConfiguration<AidxContentSnapshot>
{
    public void Configure(EntityTypeBuilder<AidxContentSnapshot> builder)
    {
        builder.ToTable("AidxContentSnapshots", "aidx");

        builder.Property(s => s.ContentType).HasConversion<string>().HasMaxLength(20);
        builder.Property(s => s.ContentJson).HasColumnType("jsonb").HasMaxLength(50000);

        // A subject row can have many snapshots over time (one per submission round), but never
        // two with the same number — the atomic-numbering guarantee as an actual DB constraint,
        // not just trust in the locked transaction that assigns it.
        builder.HasIndex(s => new { s.SubjectId, s.SnapshotNumber }).IsUnique();

        builder.HasOne<AidxMembership>()
            .WithMany()
            .HasForeignKey(s => s.SubmittedByMembershipId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
