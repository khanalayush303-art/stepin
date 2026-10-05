using StepIn.Domain.Common;

namespace StepIn.Domain.Aidx;

/// <summary>A configurable research area. Admin-editable, so a table rather than an enum.</summary>
public sealed class AidxResearchArea : Entity
{
    public required string Name { get; set; }

    public required string Slug { get; set; }

    public string? Description { get; set; }

    public int SortOrder { get; set; }
}
