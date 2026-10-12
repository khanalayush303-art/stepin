namespace StepIn.Api.Endpoints;

/// <summary>
/// Deliberately has no CandidateProfileId, Status, or audit fields: ownership is always resolved
/// server-side from the authenticated principal, and status only ever changes through the
/// dedicated transition endpoints (submit/withdraw/resubmit for a candidate; begin-review/approve/
/// reject for an admin) — never through this input, so there is nothing here for arbitrary JSON
/// to override.
/// </summary>
public sealed record ExperienceRecordInput(
    string RecordType,
    string Title,
    string? Organization,
    string? Description,
    DateOnly? StartDate,
    DateOnly? EndDate,
    IReadOnlyList<string>? Skills,
    string? EvidenceUrl,
    string? Visibility);

/// <summary>One entry in a record's status timeline, as shown to its own owner — deliberately
/// excludes the reviewer's identity (<see cref="ExperienceAuditEntryResponse"/> is the
/// admin-facing equivalent that includes it).</summary>
public sealed record ExperienceTimelineEntryResponse(
    string Action, string PreviousStatus, string NewStatus, string? Note, DateTimeOffset CreatedAt);

public sealed record ExperienceRecordResponse(
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
    bool CanEdit,
    DateTimeOffset? SubmittedAt,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt,
    IReadOnlyList<ExperienceTimelineEntryResponse> Timeline);

public sealed record ExperienceRecordSummaryResponse(
    Guid Id,
    string RecordType,
    string Title,
    string? Organization,
    string Visibility,
    string Status,
    DateOnly? StartDate,
    DateOnly? EndDate,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt);

/// <summary>Optional reason/note on a candidate-initiated transition (withdraw, resubmit).
/// Deliberately has no status field — the target status is implied entirely by which route is
/// called.</summary>
public sealed record ExperienceTransitionRequest(string? Note);

/// <summary>What a recruiter is ever shown, through the existing application-detail ownership
/// chain only — never a direct browse. Status is always the real status: Public never implies
/// Verified.</summary>
public sealed record PublicExperienceRecordResponse(
    Guid Id,
    string RecordType,
    string Title,
    string? Organization,
    string? Description,
    DateOnly? StartDate,
    DateOnly? EndDate,
    IReadOnlyList<string> Skills,
    string Status);
