using StepIn.Domain.Common;

namespace StepIn.Domain.Aidx;

public sealed class AidxPublication : Entity
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

    public bool Published { get; set; }

    public List<AidxPublicationAuthor> Authors { get; set; } = [];

    public List<AidxPublicationResearchArea> ResearchAreas { get; set; } = [];

    public List<AidxPublicationProject> Projects { get; set; } = [];
}
