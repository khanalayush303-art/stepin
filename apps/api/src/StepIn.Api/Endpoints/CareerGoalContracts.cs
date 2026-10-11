namespace StepIn.Api.Endpoints;

/// <summary>
/// Deliberately has no CandidateProfileId: ownership is always resolved server-side from the
/// authenticated principal, never accepted from the client — same shape discipline as
/// CreateJobRequest. TargetJobId, when provided, must be a published Career job; the endpoint
/// validates that, not this record.
/// </summary>
public sealed record CreateCareerGoalRequest(string TargetRole, string? Description, DateOnly? TargetDate, Guid? TargetJobId);

public sealed record UpdateCareerGoalRequest(string TargetRole, string? Description, DateOnly? TargetDate, Guid? TargetJobId, bool IsPrimary);

/// <summary>
/// RoadmapItemCount/CompletedRoadmapItemCount/ProgressPercent are all computed server-side from
/// the actual rows, never trusted from a client-supplied percentage. ProgressPercent is 0 when
/// RoadmapItemCount is 0 — the frontend is expected to show "no roadmap items yet" in that case
/// rather than a misleading 0% bar.
/// </summary>
public sealed record CareerGoalResponse(
    Guid Id,
    string TargetRole,
    string? Description,
    DateOnly? TargetDate,
    string Status,
    bool IsPrimary,
    Guid? TargetJobId,
    string? TargetJobTitle,
    string? TargetJobCompanyName,
    int RoadmapItemCount,
    int CompletedRoadmapItemCount,
    int ProgressPercent,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt);

public sealed record CreateRoadmapItemRequest(string Title, string? Description, DateOnly? TargetDate, string? Priority);

public sealed record UpdateRoadmapItemRequest(string Title, string? Description, DateOnly? TargetDate, string? Priority);

public sealed record RoadmapItemResponse(
    Guid Id,
    Guid CareerGoalId,
    string Title,
    string? Description,
    DateOnly? TargetDate,
    string Priority,
    string Status,
    DateTimeOffset? CompletedAt,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt);

/// <summary>
/// Reuses JobMatchScorer against the goal's own TargetJob.Skills — the identical rule Transparent
/// Job Match uses, not a second scoring system. Returned only when the goal has a TargetJobId;
/// the endpoint returns 404 with a clear reason otherwise rather than inventing a score.
/// </summary>
public sealed record CareerGoalSkillGapResponse(
    Guid JobId,
    string JobTitle,
    string CompanyName,
    IReadOnlyList<string> MatchedSkills,
    IReadOnlyList<string> SkillsNotYetDemonstrated,
    double? SkillCoverage,
    string MatchQuality);
