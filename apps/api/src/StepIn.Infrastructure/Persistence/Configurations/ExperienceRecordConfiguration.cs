using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StepIn.Domain.Experience;

namespace StepIn.Infrastructure.Persistence.Configurations;

public sealed class ExperienceRecordConfiguration : IEntityTypeConfiguration<ExperienceRecord>
{
    public void Configure(EntityTypeBuilder<ExperienceRecord> builder)
    {
        builder.Property(r => r.RecordType).HasConversion<string>().HasMaxLength(20);
        builder.Property(r => r.Title).IsRequired().HasMaxLength(200);
        builder.Property(r => r.Organization).HasMaxLength(200);
        builder.Property(r => r.Description).HasMaxLength(5000);
        builder.Property(r => r.Skills).HasColumnType("text[]");
        builder.Property(r => r.EvidenceUrl).HasMaxLength(500);
        builder.Property(r => r.Visibility).HasConversion<string>().HasMaxLength(10);
        builder.Property(r => r.Status).HasConversion<string>().HasMaxLength(20);

        builder.HasIndex(r => r.CandidateProfileId);
        // Supports "see your own records, newest first".
        builder.HasIndex(r => new { r.CandidateProfileId, r.CreatedAt });
        // Supports the recruiter-visible lookup and the candidate's own status filtering.
        builder.HasIndex(r => new { r.CandidateProfileId, r.Status });
        // Supports the admin reviewer queue: records awaiting review, oldest first.
        builder.HasIndex(r => new { r.Status, r.SubmittedAt });

        // Audit history is never cascade-deleted by an ordinary record operation — the only hard
        // DELETE this feature exposes is restricted to Draft records, which by construction have
        // never been submitted and so have zero audit rows. Restrict is defense-in-depth: even a
        // future bug in that application-level check could never silently take audit rows with
        // it, the database itself refuses the delete.
        builder.HasMany(r => r.AuditEntries)
            .WithOne()
            .HasForeignKey(a => a.ExperienceRecordId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
