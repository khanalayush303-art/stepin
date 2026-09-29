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

// -------------------------------------------------------- candidate/public ---

/// <summary>
/// The public shape of a job: deliberately excludes every internal identifier
/// except <see cref="Id"/> (needed for navigation to the details page) —
/// no <c>RecruiterProfileId</c>, no raw <c>CompanyId</c>, no <c>Company.Id</c>,
/// no audit timestamps beyond <see cref="PublishedAt"/>. See <see cref="JobEndpoints"/>.
/// </summary>
public sealed record PublicJobSummaryResponse(
    Guid Id,
    string Title,
    string CompanyName,
    string EmploymentType,
    string WorkplaceType,
    string Location,
    string? Compensation,
    DateTimeOffset PublishedAt);

/// <summary>Same exclusions as <see cref="PublicJobSummaryResponse"/>, plus the full description/skills/company detail.</summary>
public sealed record PublicJobResponse(
    Guid Id,
    string Title,
    string Description,
    string EmploymentType,
    string WorkplaceType,
    string Location,
    string? Compensation,
    IReadOnlyList<string> Skills,
    string CompanyName,
    string? CompanyDescription,
    string? CompanyWebsite,
    string? CompanyLogoUrl,
    string? CompanyIndustry,
    string? CompanyLocation,
    DateTimeOffset PublishedAt);
