using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StepIn.Domain.Interviews;

namespace StepIn.Infrastructure.Persistence.Configurations;

public sealed class InterviewPracticeSessionConfiguration : IEntityTypeConfiguration<InterviewPracticeSession>
{
    public void Configure(EntityTypeBuilder<InterviewPracticeSession> builder)
    {
        builder.Property(s => s.Category).HasConversion<string>().HasMaxLength(20);
        builder.Property(s => s.TargetRole).HasMaxLength(150);
        builder.Property(s => s.Status).HasConversion<string>().HasMaxLength(20);

        builder.HasIndex(s => s.CandidateProfileId);
        // Supports "see your previous sessions, newest first" without a full table scan.
        builder.HasIndex(s => new { s.CandidateProfileId, s.CreatedAt });

        // Deleting a session takes its own questions (and their answers) with it — there is no
        // independent use for a question once its session is gone.
        builder.HasMany(s => s.Questions)
            .WithOne()
            .HasForeignKey(q => q.SessionId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
