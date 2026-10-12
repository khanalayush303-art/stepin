using StepIn.Domain.Interviews;

namespace StepIn.Api.Endpoints;

/// <summary>
/// A curated, maintained set of general interview-preparation questions, grouped by category.
/// There is no verified role-specific or company-specific question content in this phase — every
/// session draws from the same category-based bank regardless of the target role a candidate
/// enters, and the frontend says so plainly rather than implying the questions are tailored.
///
/// Selection is deterministic: the first N questions in a category's fixed list order, never
/// randomized — the same request always produces the same session, which keeps this testable and
/// means two candidates comparing notes see a predictable, explainable set.
/// </summary>
public static class InterviewQuestionBank
{
    public const int MinQuestionCount = 3;
    public const int MaxQuestionCount = 8;

    private static readonly IReadOnlyList<string> GeneralQuestions =
    [
        "Tell me about yourself and why you're interested in this kind of role.",
        "What are your greatest strengths, and how have you used them at work or in study?",
        "What's an area you're actively working to improve, and how are you doing that?",
        "Why do you want to work in this field or industry?",
        "Where do you see yourself professionally in the next few years?",
        "What kind of work environment helps you do your best work?",
        "What questions do you have for us about the role or the team?",
        "How do you prioritize when you have several things competing for your time?",
    ];

    private static readonly IReadOnlyList<string> BehavioralQuestions =
    [
        "Tell me about a time you had to work with someone whose working style was very different from yours.",
        "Describe a situation where you made a mistake. What happened, and what did you do about it?",
        "Tell me about a time you had to meet a tight deadline. How did you approach it?",
        "Describe a time you received critical feedback. How did you respond?",
        "Tell me about a time you had to persuade someone to see things your way.",
        "Describe a situation where you took initiative without being asked.",
        "Tell me about a time you had to learn something new quickly to get a task done.",
        "Describe a time you disagreed with a decision made by your team or manager.",
    ];

    private static readonly IReadOnlyList<string> TechnicalQuestions =
    [
        "Walk through how you would approach debugging an issue you've never seen before.",
        "Describe how you'd explain a technical concept you understand well to someone non-technical.",
        "How do you decide between two reasonable approaches to solving the same problem?",
        "Describe a time you had to balance code quality against a deadline. What trade-offs did you make?",
        "How do you approach testing your own work before considering it done?",
        "Describe how you'd investigate a performance problem in a system you're responsible for.",
        "How do you keep your technical skills current, and what have you learned recently?",
        "Describe a technical decision you made that you'd reconsider if you could do it again.",
    ];

    private static readonly IReadOnlyList<string> SituationalQuestions =
    [
        "You've been given a task with an unclear requirement and the person who could clarify it is unavailable. What would you do?",
        "A teammate consistently misses deadlines that affect your own work. How would you handle it?",
        "You discover a mistake in work that's already been delivered. What would you do?",
        "You're asked to take on an additional responsibility with no reduction in your current workload. How would you respond?",
        "You strongly disagree with an approach your manager wants to take. How would you raise it?",
        "You're the newest person on a team and notice a process that seems inefficient. What would you do?",
        "A project you're on is at risk of missing its deadline. What steps would you take?",
        "You receive conflicting instructions from two people you report to. How would you proceed?",
    ];

    private static readonly IReadOnlyDictionary<InterviewCategory, IReadOnlyList<string>> QuestionsByCategory =
        new Dictionary<InterviewCategory, IReadOnlyList<string>>
        {
            [InterviewCategory.General] = GeneralQuestions,
            [InterviewCategory.Behavioral] = BehavioralQuestions,
            [InterviewCategory.Technical] = TechnicalQuestions,
            [InterviewCategory.Situational] = SituationalQuestions,
        };

    public static int AvailableQuestionCount(InterviewCategory category) => QuestionsByCategory[category].Count;

    /// <summary>
    /// The first <paramref name="count"/> questions for <paramref name="category"/>, in fixed
    /// order. The caller is expected to have already validated
    /// <see cref="MinQuestionCount"/> &lt;= count &lt;= <see cref="AvailableQuestionCount"/>.
    /// </summary>
    public static IReadOnlyList<string> SelectQuestions(InterviewCategory category, int count) =>
        QuestionsByCategory[category].Take(count).ToList();

    public static IReadOnlyList<InterviewCategory> Categories { get; } = Enum.GetValues<InterviewCategory>();
}
