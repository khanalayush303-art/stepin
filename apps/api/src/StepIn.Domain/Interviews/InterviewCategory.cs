namespace StepIn.Domain.Interviews;

/// <summary>
/// The rubric used to evaluate an answer is chosen by this category — see
/// StepIn.Api.Endpoints.InterviewFeedbackEngine. There is no "Unscored" category: every question
/// in the curated bank (StepIn.Api.Endpoints.InterviewQuestionBank) belongs to exactly one of
/// these four.
/// </summary>
public enum InterviewCategory
{
    General,
    Behavioral,
    Technical,
    Situational,
}
