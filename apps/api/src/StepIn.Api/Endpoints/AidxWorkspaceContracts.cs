namespace StepIn.Api.Endpoints;

/// <summary>Real counts of the caller's own content, by status, across all four workflow
/// content types. Never fabricated — every field is a live query result.</summary>
public sealed record AidxWorkspaceSummaryResponse(
    int MyDrafts,
    int MySubmitted,
    int MyUnderReview,
    int MyChangesRequested,
    int MyPublished,
    int MyArchived);

/// <summary>
/// Review-queue counts for Editor/LabManager/Admin. A null field means "not visible to your
/// scope" (e.g. News/Event for a scoped Editor) — distinct from a real zero, which means
/// "in your scope, currently nothing waiting."
/// </summary>
public sealed record AidxManageSummaryResponse(
    int? ProjectsAwaitingReview,
    int? NewsAwaitingReview,
    int? EventsAwaitingReview,
    int? PublicationsAwaitingReview);

public sealed record AidxWorkspaceItemResponse(
    string ContentType, Guid Id, string Title, string Status, Guid? RevisionOfId, DateTimeOffset? UpdatedAt);
