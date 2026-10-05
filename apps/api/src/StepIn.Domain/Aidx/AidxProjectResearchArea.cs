namespace StepIn.Domain.Aidx;

public sealed class AidxProjectResearchArea
{
    public required Guid ProjectId { get; set; }

    public required Guid ResearchAreaId { get; set; }

    public AidxResearchArea ResearchArea { get; set; } = null!;
}
