namespace StepIn.Domain.Aidx;

/// <summary>
/// Implemented by every AIDX content type that goes through the editorial workflow
/// (<see cref="AidxProject"/>, <see cref="AidxNews"/>, <see cref="AidxEvent"/>,
/// <see cref="AidxPublication"/> — not <see cref="AidxResearcher"/>, which stays
/// admin/LabManager-managed with its own plain <c>Published</c> bool). Lets
/// <c>AidxEditorialWorkflow</c>/<c>AidxContentEditingService</c> implement the state
/// machine, revisions, and concurrency once, generically, instead of four
/// near-duplicate copies.
/// </summary>
public interface IAidxWorkflowContent
{
    Guid Id { get; }

    AidxContentStatus Status { get; set; }

    /// <summary>
    /// Null for a canonical row. Set only on a row created to hold a post-first-publish
    /// edit — the canonical row's own <see cref="Id"/> it shadows. Never set on the very
    /// first (pre-first-publish) Draft→...→Published cycle, which happens in place on the
    /// canonical row itself.
    /// </summary>
    Guid? RevisionOfId { get; set; }

    /// <summary>Sequential per subject row (the canonical row pre-first-publish, or a
    /// revision row's own submission rounds). Assigned atomically at each Submitted transition.</summary>
    int RevisionNumber { get; set; }

    /// <summary>Set once this revision's fields have been copied onto the canonical row. A
    /// merged revision is never deleted — it remains the permanent historical snapshot.</summary>
    DateTimeOffset? MergedAt { get; set; }

    /// <summary>Set if this revision was abandoned rather than merged. Never deleted.</summary>
    DateTimeOffset? DiscardedAt { get; set; }

    Guid? CreatedByMembershipId { get; set; }
}
