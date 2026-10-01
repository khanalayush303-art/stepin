namespace StepIn.Api.Endpoints;

/// <summary>
/// Deliberately not a nested <see cref="PublicJobSummaryResponse"/>: that
/// record assumes a Published-only job (it has no status field, since
/// <see cref="JobEndpoints.ListPublicJobsAsync"/> only ever returns Published
/// jobs), but a saved job's underlying <c>Job</c> can later become
/// Unpublished — so this mirrors the same summary field set, swaps
/// <c>PublishedAt</c> for <c>SavedAt</c> (the relevant ordering field here),
/// and adds <c>JobStatus</c> so a later frontend phase can show "no longer
/// available" instead of silently showing stale data as if it were current.
/// </summary>
public sealed record SavedJobResponse(
    Guid Id,
    DateTimeOffset SavedAt,
    Guid JobId,
    string JobTitle,
    string CompanyName,
    string EmploymentType,
    string WorkplaceType,
    string Location,
    string? Compensation,
    string JobStatus);
