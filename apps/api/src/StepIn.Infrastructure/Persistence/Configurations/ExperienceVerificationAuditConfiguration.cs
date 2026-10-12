using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StepIn.Domain.Experience;

namespace StepIn.Infrastructure.Persistence.Configurations;

public sealed class ExperienceVerificationAuditConfiguration : IEntityTypeConfiguration<ExperienceVerificationAudit>
{
    public void Configure(EntityTypeBuilder<ExperienceVerificationAudit> builder)
    {
        builder.Property(a => a.ActorRole).IsRequired().HasMaxLength(20);
        builder.Property(a => a.Action).HasConversion<string>().HasMaxLength(20);
        builder.Property(a => a.PreviousStatus).HasConversion<string>().HasMaxLength(20);
        builder.Property(a => a.NewStatus).HasConversion<string>().HasMaxLength(20);
        builder.Property(a => a.Note).HasMaxLength(1000);

        // Supports the per-record timeline, in order, and the reviewer queue's "oldest submission
        // first" ordering.
        builder.HasIndex(a => new { a.ExperienceRecordId, a.CreatedAt });
    }
}
