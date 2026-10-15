namespace StepIn.Domain.Aidx;

/// <summary>
/// The editorial state machine — pure, no persistence access, mirroring
/// <see cref="AidxOwnershipRules"/>'s shape. <c>AidxContentEditingService</c>
/// is the one place that actually applies a transition; this class only says
/// which transitions are structurally valid and who may invoke them.
/// </summary>
public static class AidxEditorialWorkflow
{
    private static readonly Dictionary<AidxContentStatus, AidxContentStatus[]> Transitions = new()
    {
        [AidxContentStatus.Draft] = [AidxContentStatus.Submitted],
        [AidxContentStatus.Submitted] = [AidxContentStatus.UnderReview, AidxContentStatus.ChangesRequested, AidxContentStatus.Published, AidxContentStatus.Draft],
        [AidxContentStatus.UnderReview] = [AidxContentStatus.ChangesRequested, AidxContentStatus.Published],
        [AidxContentStatus.ChangesRequested] = [AidxContentStatus.Submitted, AidxContentStatus.Draft],
        [AidxContentStatus.Published] = [AidxContentStatus.Archived],
        // An archived item can be republished directly, with no re-review: its content is exactly
        // what a reviewer already approved, and archiving/republishing is a visibility toggle, not
        // a content change. This is distinct from the Submitted/UnderReview -> Published path, which
        // always re-reviews a genuine content change.
        [AidxContentStatus.Archived] = [AidxContentStatus.Published],
    };

    public static bool CanTransition(AidxContentStatus from, AidxContentStatus to) =>
        Transitions.TryGetValue(from, out var allowed) && allowed.Contains(to);

    /// <summary>A content row is editable by its Contributor only in Draft or ChangesRequested —
    /// once Submitted/UnderReview, it is read-only to the author until it moves back to Draft
    /// (withdrawal) or ChangesRequested (reviewer feedback), removing any window where live fields
    /// could drift out from under feedback already given against an immutable snapshot.</summary>
    public static bool IsEditableByContributor(AidxContentStatus status) =>
        status is AidxContentStatus.Draft or AidxContentStatus.ChangesRequested;

    /// <summary>Only a Draft→Submitted or ChangesRequested→Submitted transition creates a new
    /// immutable snapshot — every other transition acts on the snapshot already taken.</summary>
    public static bool CreatesSnapshot(AidxContentStatus from, AidxContentStatus to) =>
        to == AidxContentStatus.Submitted && from is AidxContentStatus.Draft or AidxContentStatus.ChangesRequested;

    public static bool IsReviewerTransition(AidxContentStatus from, AidxContentStatus to) =>
        from is AidxContentStatus.Submitted or AidxContentStatus.UnderReview
        && to is AidxContentStatus.UnderReview or AidxContentStatus.ChangesRequested or AidxContentStatus.Published;

    public static bool IsContributorTransition(AidxContentStatus from, AidxContentStatus to) =>
        (from == AidxContentStatus.Draft && to == AidxContentStatus.Submitted) // submit
        || (from == AidxContentStatus.ChangesRequested && to is AidxContentStatus.Submitted or AidxContentStatus.Draft) // resubmit or withdraw
        || (from == AidxContentStatus.Submitted && to == AidxContentStatus.Draft); // withdraw before review starts
}
