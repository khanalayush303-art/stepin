namespace StepIn.Api.Endpoints;

/// <summary>
/// Deliberately excludes CandidateProfileId/RecruiterProfileId/CompanyId/
/// ClerkUserId and the raw ResumeStorageKey — same exclusion shape as
/// <see cref="ApplicationSummaryResponse"/>/<see cref="ApplicationResponse"/>,
/// just from the recruiter's side. Applicant identity is the only new
/// personal information exposed here, and only to the recruiter who owns the
/// job the application was submitted against. See
/// <see cref="RecruiterApplicationEndpoints"/>.
/// </summary>
public sealed record RecruiterApplicationSummaryResponse(
    Guid Id,
    Guid JobId,
    string JobTitle,
    string ApplicantName,
    string ApplicantEmail,
    string Status,
    string ResumeFileName,
    DateTimeOffset CreatedAt);

public sealed record RecruiterApplicationResponse(
    Guid Id,
    Guid JobId,
    string JobTitle,
    string ApplicantName,
    string ApplicantEmail,
    string Status,
    string? CoverLetter,
    string ResumeFileName,
    DateTimeOffset CreatedAt,
    DateTimeOffset StatusUpdatedAt);

/// <summary>
/// Deliberately the only field — no JobId/CandidateProfileId/RecruiterProfileId
/// can be supplied; the application being changed is identified entirely by
/// the route's {id}, resolved and ownership-checked server-side. See
/// <see cref="RecruiterApplicationEndpoints"/>.
/// </summary>
public sealed record UpdateApplicationStatusRequest(string Status);
