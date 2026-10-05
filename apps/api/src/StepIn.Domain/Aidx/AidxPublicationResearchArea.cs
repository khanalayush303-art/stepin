namespace StepIn.Domain.Aidx;

public sealed class AidxPublicationResearchArea
{
    public required Guid PublicationId { get; set; }

    public required Guid ResearchAreaId { get; set; }

    public AidxResearchArea ResearchArea { get; set; } = null!;
}
