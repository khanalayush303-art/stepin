namespace StepIn.Api.Endpoints;

/// <summary>
/// Deliberately excludes CandidateProfileId/JobId/ClerkUserId and the raw
/// ResumeStorageKey — only JobTitle/CompanyName (projected) and
/// ResumeFileName (the original display name) are exposed. See
/// <see cref="ApplicationEndpoints"/>.
/// </summary>
public sealed record ApplicationSummaryResponse(
    Guid Id,
    Guid JobId,
    string JobTitle,
    string CompanyName,
    string Status,
    string ResumeFileName,
    DateTimeOffset CreatedAt);

public sealed record ApplicationResponse(
    Guid Id,
    Guid JobId,
    string JobTitle,
    string CompanyName,
    string Status,
    string? CoverLetter,
    string ResumeFileName,
    DateTimeOffset CreatedAt);

/// <summary>
/// Whether the signed-in candidate has already applied to a given job — the
/// one minimal shape the "already applied" UX needs, nothing more.
/// </summary>
public sealed record ApplicationEligibilityResponse(bool HasApplied, Guid? ApplicationId);
