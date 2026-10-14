using System.Text.Json.Serialization;
using StepIn.Domain.Common;

namespace StepIn.Domain.Aidx;

public sealed class AidxProject : Entity, IAidxWorkflowContent
{
    public required string Title { get; set; }

    public required string Slug { get; set; }

    public required string ShortDescription { get; set; }

    public required string Description { get; set; }

    public AidxContentStatus Status { get; set; } = AidxContentStatus.Draft;

    public DateOnly? StartDate { get; set; }

    public DateOnly? EndDate { get; set; }

    public string? ExternalUrl { get; set; }

    public bool Featured { get; set; }

    public DateTimeOffset? PublishedAt { get; set; }

    /// <summary>
    /// Editing ownership — distinct from authorship. Null for pre-existing
    /// admin-created rows; never implicitly open to any Contributor edit
    /// (see AidxPermissions.CanEdit and the "claim-ownership" action).
    /// </summary>
    public Guid? CreatedByMembershipId { get; set; }

    /// <summary>Null for the canonical row; set on a row holding a post-first-publish edit.</summary>
    public Guid? RevisionOfId { get; set; }

    public int RevisionNumber { get; set; }

    public DateTimeOffset? MergedAt { get; set; }

    public DateTimeOffset? DiscardedAt { get; set; }

    // Excluded from the JSON clone/snapshot (AidxContentEditingService.BeginEditAsync /
    // SubmitAsync): a naive JSON round-trip would carry each child's FK still pointing at the
    // canonical row, colliding with its own existing child rows on save. Reparenting these onto
    // a new revision is instead done explicitly per content type (see the Reparent delegates in
    // AidxWorkflowEndpoints) — a known, accepted limitation is that raw revision-comparison JSON
    // does not surface tag/author changes, only scalar content fields.
    [JsonIgnore]
    public List<AidxProjectResearchArea> ResearchAreas { get; set; } = [];

    [JsonIgnore]
    public List<AidxProjectTechnology> Technologies { get; set; } = [];

    [JsonIgnore]
    public List<AidxProjectResearcher> Researchers { get; set; } = [];
}
