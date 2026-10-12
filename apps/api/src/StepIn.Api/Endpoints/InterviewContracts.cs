namespace StepIn.Api.Endpoints;

/// <summary>What the setup page shows before a candidate starts a session — reflects
/// InterviewQuestionBank exactly, so the frontend never hardcodes its own copy of question counts
/// or category names.</summary>
public sealed record InterviewCategoryOption(string Category, int AvailableQuestionCount);

public sealed record InterviewTemplatesResponse(
    IReadOnlyList<InterviewCategoryOption> Categories, int MinQuestionCount, int MaxQuestionCount);

/// <summary>
/// Deliberately has no CandidateProfileId: ownership is always resolved server-side from the
/// authenticated principal. TargetRole is free-text metadata only — it does not change which
/// questions are selected (see InterviewQuestionBank).
/// </summary>
public sealed record CreateInterviewSessionRequest(string Category, string? TargetRole, int QuestionCount);

public sealed record SaveInterviewAnswerRequest(string? AnswerText);

public sealed record InterviewFeedbackCriterionResponse(string Name, bool Met, string Note);

public sealed record InterviewFeedbackResponse(
    bool Answered,
    IReadOnlyList<InterviewFeedbackCriterionResponse> Criteria,
    int CriteriaMetCount,
    int CriteriaTotalCount,
    string Band,
    IReadOnlyList<string> Improvements,
    string SuggestedStructure,
    string Disclaimer);

public sealed record InterviewQuestionResponse(
    Guid Id,
    int OrderIndex,
    string Category,
    string QuestionText,
    string? AnswerText,
    DateTimeOffset? AnsweredAt,
    InterviewFeedbackResponse Feedback);

public sealed record InterviewSessionResponse(
    Guid Id,
    string Category,
    string? TargetRole,
    string Status,
    int QuestionCount,
    int AnsweredCount,
    DateTimeOffset CreatedAt,
    DateTimeOffset? CompletedAt,
    IReadOnlyList<InterviewQuestionResponse> Questions);

/// <summary>Listing shape — no question/answer bodies, so a candidate's history loads in one
/// lightweight query.</summary>
public sealed record InterviewSessionSummaryResponse(
    Guid Id,
    string Category,
    string? TargetRole,
    string Status,
    int QuestionCount,
    int AnsweredCount,
    DateTimeOffset CreatedAt,
    DateTimeOffset? CompletedAt);
