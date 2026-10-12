namespace StepIn.Api.Endpoints;

/// <summary>
/// Deliberately has no CandidateProfileId: ownership is always resolved server-side from the
/// authenticated principal, never accepted from the client — same shape discipline as
/// CreateCareerGoalRequest. ResearchProjectId, when provided, must reference a Published AIDX
/// project; the endpoint validates that, not this record. Visibility is a plain string
/// ("Private"/"Public") so an unrecognised value is a clear 400, not a silent default.
/// </summary>
public sealed record PortfolioProjectInput(
    string Title,
    string Description,
    IReadOnlyList<string>? Skills,
    DateOnly? StartDate,
    DateOnly? EndDate,
    string? RepositoryUrl,
    string? DemoUrl,
    string? Visibility,
    Guid? ResearchProjectId);

/// <summary>The candidate's own full view of a project, including its current visibility.</summary>
public sealed record PortfolioProjectResponse(
    Guid Id,
    string Title,
    string Description,
    IReadOnlyList<string> Skills,
    DateOnly? StartDate,
    DateOnly? EndDate,
    string? RepositoryUrl,
    string? DemoUrl,
    string Visibility,
    Guid? ResearchProjectId,
    string? ResearchProjectTitle,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt);

/// <summary>
/// What a recruiter is ever shown, through the existing application-detail ownership chain only:
/// no visibility flag (it is always Public by construction of the query that produces this), and
/// no edit-oriented fields. There is no endpoint anywhere that returns this type for a project the
/// candidate has not explicitly made Public.
/// </summary>
public sealed record PublicPortfolioProjectResponse(
    Guid Id,
    string Title,
    string Description,
    IReadOnlyList<string> Skills,
    DateOnly? StartDate,
    DateOnly? EndDate,
    string? RepositoryUrl,
    string? DemoUrl,
    string? ResearchProjectTitle);
