using StepIn.Domain.Common;

namespace StepIn.Domain.Aidx;

/// <summary>
/// A person in the lab. Most researchers have no StepIn account, so <see cref="UserId"/>
/// is optional and no Clerk identity is required to appear on the public site.
/// Researcher email addresses are deliberately not stored here.
/// </summary>
public sealed class AidxResearcher : Entity
{
    public required string Slug { get; set; }

    public required string DisplayName { get; set; }

    public AidxResearcherCategory Category { get; set; }

    public string? Position { get; set; }

    public string? Biography { get; set; }

    public string? ProfileImageKey { get; set; }

    public string? OrcidUrl { get; set; }

    public string? GoogleScholarUrl { get; set; }

    public string? LinkedInUrl { get; set; }

    public string? WebsiteUrl { get; set; }

    public bool Published { get; set; }

    public Guid? UserId { get; set; }
}
