using StepIn.Domain.Common;

namespace StepIn.Domain.Interviews;

/// <summary>
/// A candidate's own practice session. <see cref="CandidateProfileId"/> is set once, server-side,
/// from the authenticated candidate's own profile — never from client input — the same FK-only
/// ownership pattern as <see cref="StepIn.Domain.Career.CareerGoal.CandidateProfileId"/>.
///
/// <see cref="TargetRole"/> is free-text metadata only: this phase has no verified role-specific
/// question bank, so every session draws from the same curated, category-based question set
/// (see StepIn.Api.Endpoints.InterviewQuestionBank) regardless of what role is entered here.
///
/// Feedback is never persisted on this entity or its questions — it is a pure, deterministic
/// function of a question's Category and AnswerText (StepIn.Api.Endpoints.InterviewFeedbackEngine),
/// recomputed on every read. A template wording update can therefore never retroactively change a
/// historical session's feedback, because each question snapshots its own wording at creation time
/// (see <see cref="InterviewPracticeQuestion.QuestionText"/>) and feedback only ever depends on the
/// category plus the candidate's own saved answer text, both of which are already stored verbatim.
/// </summary>
public sealed class InterviewPracticeSession : Entity
{
    public required Guid CandidateProfileId { get; set; }

    public required InterviewCategory Category { get; set; }

    public string? TargetRole { get; set; }

    public InterviewSessionStatus Status { get; set; } = InterviewSessionStatus.InProgress;

    /// <summary>Set server-side exactly on the transition to Completed; never trusted from client
    /// input and never cleared (no reopen-to-InProgress workflow exists in this phase).</summary>
    public DateTimeOffset? CompletedAt { get; set; }

    public List<InterviewPracticeQuestion> Questions { get; set; } = [];
}
