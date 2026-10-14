using System.Text.Json.Serialization;
using StepIn.Domain.Common;

namespace StepIn.Domain.Aidx;

public sealed class AidxPublication : Entity, IAidxWorkflowContent
{
    public required string Title { get; set; }

    public string? Abstract { get; set; }

    public AidxPublicationType PublicationType { get; set; }

    public string? Venue { get; set; }

    public int Year { get; set; }

    public string? Doi { get; set; }

    public string? ExternalUrl { get; set; }

    /// <summary>Future storage reference only. No PDF upload exists yet.</summary>
    public string? PdfKey { get; set; }

    /// <summary>
    /// Expand-migration compatibility field (docs/deployment/aidx-migration-plan.md):
    /// kept and dual-written alongside <see cref="Status"/> for the rollout window so an
    /// old, not-yet-redeployed API instance still reads a correct value. Dropping this is a
    /// separate, later, explicitly out-of-scope contract migration — never bundled with the
    /// expand step that introduced <see cref="Status"/>.
    /// </summary>
    public bool Published { get; set; }

    public AidxContentStatus Status { get; set; } = AidxContentStatus.Draft;

    public DateTimeOffset? PublishedAt { get; set; }

    /// <summary>Editing ownership — distinct from <see cref="Authors"/> attribution.</summary>
    public Guid? CreatedByMembershipId { get; set; }

    public Guid? RevisionOfId { get; set; }

    public int RevisionNumber { get; set; }

    public DateTimeOffset? MergedAt { get; set; }

    public DateTimeOffset? DiscardedAt { get; set; }

    // Excluded from the JSON clone/snapshot — see AidxProject's identical note.
    [JsonIgnore]
    public List<AidxPublicationAuthor> Authors { get; set; } = [];

    [JsonIgnore]
    public List<AidxPublicationResearchArea> ResearchAreas { get; set; } = [];

    [JsonIgnore]
    public List<AidxPublicationProject> Projects { get; set; } = [];
}
