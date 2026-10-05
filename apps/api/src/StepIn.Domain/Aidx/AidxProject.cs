using StepIn.Domain.Common;

namespace StepIn.Domain.Aidx;

public sealed class AidxProject : Entity
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

    public List<AidxProjectResearchArea> ResearchAreas { get; set; } = [];

    public List<AidxProjectTechnology> Technologies { get; set; } = [];

    public List<AidxProjectResearcher> Researchers { get; set; } = [];
}
