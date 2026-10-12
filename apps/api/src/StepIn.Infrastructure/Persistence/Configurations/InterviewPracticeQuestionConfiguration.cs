using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StepIn.Domain.Interviews;

namespace StepIn.Infrastructure.Persistence.Configurations;

public sealed class InterviewPracticeQuestionConfiguration : IEntityTypeConfiguration<InterviewPracticeQuestion>
{
    public void Configure(EntityTypeBuilder<InterviewPracticeQuestion> builder)
    {
        builder.Property(q => q.Category).HasConversion<string>().HasMaxLength(20);
        builder.Property(q => q.QuestionText).IsRequired().HasMaxLength(1000);
        // Matches StepIn.Api.Endpoints.InterviewEndpoints.MaxAnswerLength — the actual limit is
        // enforced there (returning a 400), this is defense-in-depth, not the source of truth.
        builder.Property(q => q.AnswerText).HasMaxLength(4000);

        builder.HasIndex(q => q.SessionId);
        // Stable, unique per session — guards against two questions ever claiming the same slot.
        builder.HasIndex(q => new { q.SessionId, q.OrderIndex }).IsUnique();
    }
}
