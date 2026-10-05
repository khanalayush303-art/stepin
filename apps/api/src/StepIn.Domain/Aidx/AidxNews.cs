using StepIn.Domain.Common;

namespace StepIn.Domain.Aidx;

public sealed class AidxNews : Entity
{
    public required string Slug { get; set; }

    public required string Title { get; set; }

    public required string Summary { get; set; }

    public required string Body { get; set; }

    public string? ImageKey { get; set; }

    public Guid? AuthorResearcherId { get; set; }

    public AidxContentStatus Status { get; set; } = AidxContentStatus.Draft;

    public DateTimeOffset? PublishedAt { get; set; }
}
