using StepIn.Domain.Common;

namespace StepIn.Domain.Aidx;

/// <summary>Append-only. Never updated or deleted — the editorial audit trail.</summary>
public sealed class AidxEditorialAuditEntry : Entity
{
    public AidxContentType ContentType { get; set; }

    public required Guid SubjectId { get; set; }

    /// <summary>The snapshot this action was taken against, when the action is review-related
    /// (ChangesRequested/Published/MovedToUnderReview). Null for actions with no snapshot context
    /// (Withdrawn before any submission, OwnershipClaimed).</summary>
    public Guid? SnapshotId { get; set; }

    public AidxEditorialAction Action { get; set; }

    public required Guid PerformedByUserId { get; set; }

    /// <summary>Null when the actor is a platform Admin acting with no membership row.</summary>
    public Guid? PerformedByMembershipId { get; set; }

    /// <summary>Reviewer feedback — the "changes requested" message, in particular.</summary>
    public string? Comment { get; set; }
}
