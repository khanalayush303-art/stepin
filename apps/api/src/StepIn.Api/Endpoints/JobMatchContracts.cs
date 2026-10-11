namespace StepIn.Api.Endpoints;

/// <summary>
/// A published Career job, scored against the signed-in candidate's recorded skills (see
/// <see cref="JobMatchScorer"/>). <see cref="SkillCoverage"/> is a deterministic fraction of
/// matched required skills, never a probability of being hired, and is null exactly when
/// <see cref="MatchQuality"/> is "Unscored" (the job recorded no skills to compare against).
/// </summary>
public sealed record JobMatchResponse(
    Guid Id,
    string Title,
    string CompanyName,
    string EmploymentType,
    string WorkplaceType,
    string Location,
    string? Compensation,
    DateTimeOffset PublishedAt,
    IReadOnlyList<string> MatchedSkills,
    IReadOnlyList<string> SkillsNotYetDemonstrated,
    double? SkillCoverage,
    string MatchQuality);
