using StepIn.Domain.Common;

namespace StepIn.Domain.Interviews;

/// <summary>
/// One question within an <see cref="InterviewPracticeSession"/>. <see cref="SessionId"/> is set
/// once at creation, only after the session itself was just created by this same request — never
/// trusted from client input. <see cref="OrderIndex"/> is stable and assigned once, 0-based, in
/// the order the question bank returned it.
///
/// <see cref="QuestionText"/> is a snapshot taken from the curated bank at session-creation time —
/// deliberately copied rather than referenced by a template id, so a later wording change to the
/// bank can never silently alter what an existing session shows a candidate reviewing it later.
/// </summary>
public sealed class InterviewPracticeQuestion : Entity
{
    public required Guid SessionId { get; set; }

    public required int OrderIndex { get; set; }

    public required InterviewCategory Category { get; set; }

    public required string QuestionText { get; set; }

    public string? AnswerText { get; set; }

    /// <summary>Set server-side exactly when an answer is saved; cleared if the answer is ever
    /// cleared back to blank. Never trusted from client input.</summary>
    public DateTimeOffset? AnsweredAt { get; set; }
}
