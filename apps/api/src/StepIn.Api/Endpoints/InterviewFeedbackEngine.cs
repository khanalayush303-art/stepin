using StepIn.Domain.Interviews;

namespace StepIn.Api.Endpoints;

/// <summary>One rubric criterion and whether the answer appears to address it.</summary>
public sealed record InterviewFeedbackCriterion(string Name, bool Met, string Note);

/// <summary>
/// Rubric-based practice feedback for one answer — never persisted (see
/// <see cref="StepIn.Domain.Interviews.InterviewPracticeSession"/>), always recomputed from the
/// question's <see cref="InterviewCategory"/> and the candidate's own saved answer text, so it is
/// always consistent with whatever text is currently saved and never goes stale.
/// </summary>
public sealed record InterviewFeedbackResult(
    bool Answered,
    IReadOnlyList<InterviewFeedbackCriterion> Criteria,
    int CriteriaMetCount,
    int CriteriaTotalCount,
    string Band,
    IReadOnlyList<string> Improvements,
    string SuggestedStructure,
    string Disclaimer);

/// <summary>
/// Deterministic, keyword-based heuristics — not natural-language understanding, not AI, and not
/// a verified hiring assessment. The same answer text always produces the same result. Every
/// public entry point returns an <see cref="InterviewFeedbackResult"/> whose
/// <see cref="InterviewFeedbackResult.Disclaimer"/> makes this explicit, so nothing calling this
/// class needs to repeat the caveat itself.
///
/// Each category's criteria look for a small number of simple textual signals (specific words or
/// phrases, and a minimum length) that a reasonably structured answer in that category tends to
/// contain. Missing a criterion is reported as a concrete, actionable suggestion — never as a
/// penalty on tone, grammar, length beyond the stated minimums, or anything about the candidate
/// personally. A blank answer is reported honestly as unanswered, never scored.
/// </summary>
public static class InterviewFeedbackEngine
{
    public const string Disclaimer =
        "This is rubric-based practice feedback from simple, documented heuristics — not an AI-generated " +
        "assessment and not a verified hiring evaluation. Use your own genuine examples; don't invent experience you haven't had.";

    private const int SubstantiveLength = 40;
    private const int DetailedLength = 100;

    public static InterviewFeedbackResult Evaluate(InterviewCategory category, string? answerText)
    {
        var trimmed = answerText?.Trim() ?? string.Empty;
        if (trimmed.Length == 0)
        {
            return new InterviewFeedbackResult(
                Answered: false,
                Criteria: [],
                CriteriaMetCount: 0,
                CriteriaTotalCount: 0,
                Band: "Not yet answered",
                Improvements: ["Save an answer to see feedback here."],
                SuggestedStructure: SuggestedStructureFor(category),
                Disclaimer: Disclaimer);
        }

        var lower = trimmed.ToLowerInvariant();
        var criteria = category switch
        {
            InterviewCategory.Behavioral => EvaluateBehavioral(lower),
            InterviewCategory.Technical => EvaluateTechnical(lower),
            InterviewCategory.Situational => EvaluateSituational(lower),
            _ => EvaluateGeneral(lower),
        };

        var met = criteria.Count(c => c.Met);
        var improvements = criteria.Where(c => !c.Met).Select(c => c.Note).ToList();
        if (improvements.Count == 0)
        {
            improvements.Add("This answer addresses every criterion in this rubric — consider tightening it so the strongest points stand out.");
        }

        return new InterviewFeedbackResult(
            Answered: true,
            Criteria: criteria,
            CriteriaMetCount: met,
            CriteriaTotalCount: criteria.Count,
            Band: BandFor(met, criteria.Count),
            Improvements: improvements,
            SuggestedStructure: SuggestedStructureFor(category),
            Disclaimer: Disclaimer);
    }

    private static string BandFor(int met, int total) => met switch
    {
        0 => "Needs more detail",
        _ when met == total => "Well-structured",
        _ => "Covers the basics",
    };

    private static string SuggestedStructureFor(InterviewCategory category) => category switch
    {
        InterviewCategory.Behavioral =>
            "Situation → Action → Result (STAR): briefly set the scene, describe specifically what you did, and close with the outcome or what you learned.",
        InterviewCategory.Technical =>
            "State your approach, explain the reasoning behind the key decisions, and mention any trade-offs, edge cases, or complexity you considered.",
        InterviewCategory.Situational =>
            "Describe the plan you'd take, who else is involved or affected, and the outcome or risk you'd be managing for.",
        _ =>
            "Give a direct, specific answer grounded in a real example rather than a general statement.",
    };

    private static bool ContainsAny(string lower, params string[] phrases) => phrases.Any(lower.Contains);

    private static List<InterviewFeedbackCriterion> EvaluateBehavioral(string lower)
    {
        var describesSituation = ContainsAny(lower, "when ", "during ", "while ", "situation", "project", "at my", "in my role", "once i", "there was a time");
        var describesAction = ContainsAny(lower, "i did", "i led", "i decided", "i created", "i organized", "i organised", "i implemented",
            "i managed", "i resolved", "i handled", "i communicated", "i coordinated", "i took", "my approach was", "i chose to");
        var describesOutcome = ContainsAny(lower, "result", "outcome", "achieved", "improved", "reduced", "increased", "led to", "learned", "impact", "as a result");
        var isSubstantive = lower.Length >= SubstantiveLength;

        return
        [
            new InterviewFeedbackCriterion("Sets the scene", describesSituation,
                "Add a sentence on the situation or context — when and where this happened."),
            new InterviewFeedbackCriterion("Describes your own action", describesAction,
                "Be explicit about what you personally did, not just what the team or situation involved."),
            new InterviewFeedbackCriterion("States the outcome", describesOutcome,
                "Close with what resulted from your action, or what you learned from it."),
            new InterviewFeedbackCriterion("Has enough detail to assess", isSubstantive,
                $"The answer is quite short — aim for at least a few sentences (roughly {SubstantiveLength} characters)."),
        ];
    }

    private static List<InterviewFeedbackCriterion> EvaluateTechnical(string lower)
    {
        var explainsApproach = ContainsAny(lower, "approach", "steps", "first,", "first i", "then i", "algorithm", "method", "strategy", "process");
        var explainsReasoning = ContainsAny(lower, "because", "since ", "due to", "the reason", "trade-off", "tradeoff", "i chose", "i decided");
        var considersComplexity = ContainsAny(lower, "edge case", "complexity", "scalability", "performance", "big o", "o(n", "corner case", "fails if", "breaks if");
        var isSubstantive = lower.Length >= SubstantiveLength;

        return
        [
            new InterviewFeedbackCriterion("Explains the approach", explainsApproach,
                "Walk through the steps or method you'd take, not just the end result."),
            new InterviewFeedbackCriterion("Explains the reasoning", explainsReasoning,
                "Say why you'd choose that approach over an alternative."),
            new InterviewFeedbackCriterion("Considers edge cases or trade-offs", considersComplexity,
                "Mention at least one edge case, limitation, or trade-off you'd watch for."),
            new InterviewFeedbackCriterion("Has enough detail to assess", isSubstantive,
                $"The answer is quite short — aim for at least a few sentences (roughly {SubstantiveLength} characters)."),
        ];
    }

    private static List<InterviewFeedbackCriterion> EvaluateSituational(string lower)
    {
        var describesPlan = ContainsAny(lower, "i would", "i'd ", "my plan", "first i", "i would start", "the steps i");
        var considersOthers = ContainsAny(lower, "team", "stakeholder", "manager", "deadline", "constraint", "resources", "communicate", "colleague");
        var consideredOutcome = ContainsAny(lower, "risk", "outcome", "result", "if that", "in case", "contingency", "to avoid");
        var isSubstantive = lower.Length >= SubstantiveLength;

        return
        [
            new InterviewFeedbackCriterion("Describes a concrete plan", describesPlan,
                "Describe specifically what you'd do, not just how you'd feel about the situation."),
            new InterviewFeedbackCriterion("Considers people or constraints involved", considersOthers,
                "Mention who else is affected, or what constraints (time, resources) shape your plan."),
            new InterviewFeedbackCriterion("Anticipates risk or outcome", consideredOutcome,
                "Note what you're managing for, or what could go wrong with your plan."),
            new InterviewFeedbackCriterion("Has enough detail to assess", isSubstantive,
                $"The answer is quite short — aim for at least a few sentences (roughly {SubstantiveLength} characters)."),
        ];
    }

    private static List<InterviewFeedbackCriterion> EvaluateGeneral(string lower)
    {
        var trivialFillers = new[] { "i don't know", "idk", "not sure", "n/a", "none", "no comment" };
        var isRelevant = !trivialFillers.Contains(lower);
        var isSubstantive = lower.Length >= SubstantiveLength;
        var isDetailed = lower.Length >= DetailedLength;

        return
        [
            new InterviewFeedbackCriterion("Gives a real answer", isRelevant,
                "Try answering directly rather than with a placeholder — even a brief, honest attempt is more useful to practice with."),
            new InterviewFeedbackCriterion("Has enough detail to assess", isSubstantive,
                $"The answer is quite short — aim for at least a few sentences (roughly {SubstantiveLength} characters)."),
            new InterviewFeedbackCriterion("Includes specific detail", isDetailed,
                "Ground the answer in a specific example or detail rather than speaking generally."),
        ];
    }
}
