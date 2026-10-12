namespace StepIn.Api.Endpoints;

/// <summary>One queue row — enough to triage without opening every record.</summary>
public sealed record ExperienceQueueItemResponse(
    Guid Id,
    string RecordType,
    string Title,
    string? Organization,
    string ApplicantName,
    string Status,
    DateTimeOffset? SubmittedAt,
    DateTimeOffset CreatedAt);

/// <summary>The admin-facing equivalent of ExperienceTimelineEntryResponse — includes the
/// reviewing actor's identity, which a candidate's own timeline deliberately never shows.</summary>
public sealed record ExperienceAuditEntryResponse(
    string Action,
    string PreviousStatus,
    string NewStatus,
    string ActorName,
    string ActorRole,
    string? Note,
    DateTimeOffset CreatedAt);

public sealed record ExperienceAdminRecordResponse(
    Guid Id,
    string RecordType,
    string Title,
    string? Organization,
    string? Description,
    DateOnly? StartDate,
    DateOnly? EndDate,
    IReadOnlyList<string> Skills,
    string? EvidenceUrl,
    string Visibility,
    string Status,
    string ApplicantName,
    string ApplicantEmail,
    DateTimeOffset? SubmittedAt,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt,
    IReadOnlyList<ExperienceAuditEntryResponse> AuditHistory);

/// <summary>Deliberately the only field on a reject request — the target status is implied by
/// the route, and the reason is optional but always recorded on the audit row when given.</summary>
public sealed record ExperienceReviewDecisionRequest(string? Note);
