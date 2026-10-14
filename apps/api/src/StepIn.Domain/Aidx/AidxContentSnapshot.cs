using StepIn.Domain.Common;

namespace StepIn.Domain.Aidx;

/// <summary>
/// An immutable, append-only full-field copy taken at every transition into
/// <see cref="AidxContentStatus.Submitted"/> — the first submission, or a
/// resubmission after <see cref="AidxContentStatus.ChangesRequested"/>.
/// Nothing on this row is ever updated after insert: review outcomes
/// (approved / changes requested / published / archived) are recorded as
/// separate <see cref="AidxEditorialAuditEntry"/> rows referencing this
/// snapshot by id, never written back here. This is what makes "feedback
/// stays pinned to the exact content it was given on" true even after a
/// later edit changes the live row.
///
/// <see cref="SubjectId"/> is the canonical row's own id for a
/// pre-first-publish round, or a revision row's id for every round after
/// first publish — in both cases, "the row this snapshot was taken of."
/// </summary>
public sealed class AidxContentSnapshot : Entity
{
    public AidxContentType ContentType { get; set; }

    public required Guid SubjectId { get; set; }

    /// <summary>Sequential per subject row, assigned inside the same locked transaction as the transition.</summary>
    public int SnapshotNumber { get; set; }

    /// <summary>Null only for the synthetic snapshot #1 backfilled for pre-existing published rows
    /// that had no membership system when they were created.</summary>
    public Guid? SubmittedByMembershipId { get; set; }

    /// <summary>Full field copy of the content at the moment of submission, serialized as JSON.
    /// Deliberately a snapshot blob rather than a second strongly-typed table per content type —
    /// the four content types' fields differ enough that four near-duplicate tables would be pure
    /// repetition for a row that is never queried by individual field, only ever rendered whole.</summary>
    public required string ContentJson { get; set; }
}
