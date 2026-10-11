namespace StepIn.Api.Endpoints;

/// <summary>
/// An existing, already-published Research opportunity (a StepIn <c>Job</c> with
/// <c>Category = Research</c>) linked to the project. Its presence is what distinguishes "this
/// project is open for applicants" from "this project is informational only" — a project with no
/// entries here has nothing to apply to, and this response never adds an apply action of its own.
/// </summary>
public sealed record ResearchOpportunityLinkResponse(Guid JobId, string Title);

/// <summary>
/// A published AIDX project, scored against the signed-in candidate's recorded skills using the
/// exact same deterministic rule as <see cref="JobMatchScorer"/> (reused, not reimplemented),
/// compared against the project's recorded technologies. <see cref="SkillCoverage"/> is null
/// exactly when <see cref="MatchQuality"/> is "Unscored" (no technologies recorded). This is a
/// relevance explanation for research content, never an invented AI-generated score, and never an
/// application action by itself — see <see cref="OpenOpportunities"/>.
/// </summary>
public sealed record ResearchMatchResponse(
    Guid ProjectId,
    string Title,
    string Slug,
    string ShortDescription,
    IReadOnlyList<string> ResearchAreas,
    IReadOnlyList<string> MatchedSkills,
    IReadOnlyList<string> SkillsNotYetDemonstrated,
    double? SkillCoverage,
    string MatchQuality,
    IReadOnlyList<ResearchOpportunityLinkResponse> OpenOpportunities);
