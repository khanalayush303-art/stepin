using StepIn.Domain.Common;

namespace StepIn.Domain.Companies;

/// <summary>
/// An employer a recruiter profile is associated with. In Phase 2.1 a company is
/// owned by the single recruiter profile that created it; multiple recruiters
/// sharing one company record is a later-phase concern.
/// </summary>
public sealed class Company : Entity
{
    public required string Name { get; set; }

    public string? Description { get; set; }

    public string? Website { get; set; }

    public string? LogoUrl { get; set; }

    public string? Industry { get; set; }

    public string? Location { get; set; }
}
