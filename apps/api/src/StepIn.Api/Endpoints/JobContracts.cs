namespace StepIn.Api.Endpoints;

public sealed record JobSummaryResponse(
    Guid Id,
    string Title,
    string CompanyName,
    string Status,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt,
    DateTimeOffset? PublishedAt);

public sealed record JobResponse(
    Guid Id,
    string Title,
    string Description,
    string EmploymentType,
    string WorkplaceType,
    string Location,
    string? Compensation,
    IReadOnlyList<string> Skills,
    string Status,
    Guid CompanyId,
    string CompanyName,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt,
    DateTimeOffset? PublishedAt);

/// <summary>
/// Deliberately has no ownership field (no recruiter/company id): a job's owner
/// and company are always resolved server-side from the authenticated recruiter's
/// own profile, never accepted from the client. See <see cref="JobEndpoints"/>.
/// </summary>
public sealed record CreateJobRequest(
    string Title,
    string Description,
    string? EmploymentType,
    string? WorkplaceType,
    string Location,
    string? Compensation,
    IReadOnlyList<string>? Skills);

/// <summary>Same non-ownership contract as <see cref="CreateJobRequest"/> — status is also never set here.</summary>
public sealed record UpdateJobRequest(
    string Title,
    string Description,
    string? EmploymentType,
    string? WorkplaceType,
    string Location,
    string? Compensation,
    IReadOnlyList<string>? Skills);
