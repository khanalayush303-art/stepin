namespace StepIn.Api.Endpoints;

public sealed record AidxWorkflowActionRequest(string ExpectedVersion);

public sealed record AidxRequestChangesRequest(string ExpectedVersion, string Comment);

public sealed record AidxWorkflowStatusResponse(Guid Id, string Status, string Version, Guid? RevisionOfId, int RevisionNumber);

public sealed record AidxRevisionSummaryResponse(
    Guid Id, int RevisionNumber, string Status, DateTimeOffset? MergedAt, DateTimeOffset? DiscardedAt);

public sealed record AidxContentSnapshotResponse(
    Guid Id, int SnapshotNumber, Guid? SubmittedByMembershipId, DateTimeOffset CreatedAt, string ContentJson);

public sealed record AidxEditorialAuditEntryResponse(
    Guid Id, string Action, Guid? SnapshotId, string? Comment, DateTimeOffset CreatedAt, string PerformedByName);
