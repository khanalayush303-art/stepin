namespace StepIn.Domain.Aidx;

public sealed class AidxProjectResearcher
{
    public required Guid ProjectId { get; set; }

    public required Guid ResearcherId { get; set; }

    public string? Role { get; set; }

    public AidxResearcher Researcher { get; set; } = null!;
}
