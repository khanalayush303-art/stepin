using StepIn.Domain.Common;

namespace StepIn.Domain.Aidx;

public sealed class AidxNews : Entity, IAidxWorkflowContent
{
    public required string Slug { get; set; }

    public required string Title { get; set; }

    public required string Summary { get; set; }

    public required string Body { get; set; }

    public string? ImageKey { get; set; }

    /// <summary>Required (at the application-validation layer) whenever <see cref="ImageKey"/> is set.</summary>
    public string? ImageAltText { get; set; }

    public Guid? AuthorResearcherId { get; set; }

    public AidxContentStatus Status { get; set; } = AidxContentStatus.Draft;

    public DateTimeOffset? PublishedAt { get; set; }

    /// <summary>Editing ownership — distinct from <see cref="AuthorResearcherId"/> attribution.</summary>
    public Guid? CreatedByMembershipId { get; set; }

    public Guid? RevisionOfId { get; set; }

    public int RevisionNumber { get; set; }

    public DateTimeOffset? MergedAt { get; set; }

    public DateTimeOffset? DiscardedAt { get; set; }
}
