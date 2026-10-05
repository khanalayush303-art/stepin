namespace StepIn.Domain.Aidx;

public sealed class AidxPublicationProject
{
    public required Guid PublicationId { get; set; }

    public required Guid ProjectId { get; set; }

    public AidxProject Project { get; set; } = null!;
}
