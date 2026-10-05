namespace StepIn.Api.Endpoints;

/// <summary>
/// Content fields for an AIDX Research Opportunity. Deliberately has no owner, company, category
/// or status field: ownership is server-controlled, Research is implied by the route, and the
/// lifecycle moves only through the publish and unpublish endpoints. Unknown properties in the
/// request body are ignored, so a client cannot smuggle these in.
/// </summary>
public sealed record AidxOpportunityRequest(
    string? Title,
    string? Description,
    string? EmploymentType,
    string? WorkplaceType,
    string? Location,
    string? Compensation,
    IReadOnlyList<string>? Skills,
    Guid? AidxProjectId);

/// <summary>Admin view of an AIDX Research Opportunity. Owner and company internals are not exposed.</summary>
public sealed record AidxOpportunityAdminResponse(
    Guid Id,
    string Title,
    string Description,
    string EmploymentType,
    string WorkplaceType,
    string Location,
    string? Compensation,
    IReadOnlyList<string> Skills,
    string Status,
    Guid? AidxProjectId,
    string? AidxProjectTitle,
    string? AidxProjectSlug,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt,
    DateTimeOffset? PublishedAt);
