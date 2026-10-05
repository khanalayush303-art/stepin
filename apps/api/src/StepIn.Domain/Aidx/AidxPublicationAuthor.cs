namespace StepIn.Domain.Aidx;

/// <summary>
/// An ordered author of a publication. Either linked to a lab researcher or
/// represented by <see cref="ExternalAuthorName"/> for external co-authors.
/// </summary>
public sealed class AidxPublicationAuthor
{
    public required Guid PublicationId { get; set; }

    public required int Position { get; set; }

    public Guid? ResearcherId { get; set; }

    public string? ExternalAuthorName { get; set; }

    public AidxResearcher? Researcher { get; set; }
}
