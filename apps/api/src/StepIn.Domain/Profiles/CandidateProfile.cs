using StepIn.Domain.Common;

namespace StepIn.Domain.Profiles;

/// <summary>
/// A candidate's application-specific profile data. Kept separate from
/// <see cref="Users.ApplicationUser"/> (which stays Clerk-derived identity only)
/// via a foreign-key-only relationship — <see cref="Users.ApplicationUser"/> has
/// no navigation property back here, so <c>StepIn.Domain.Users</c> stays
/// decoupled from this namespace.
/// </summary>
public sealed class CandidateProfile : Entity
{
    public required Guid UserId { get; set; }

    public string? PhoneNumber { get; set; }

    public string? Location { get; set; }

    public string? Headline { get; set; }

    public string? Bio { get; set; }

    public string? PhotoUrl { get; set; }

    public string? LinkedInUrl { get; set; }

    public string? PortfolioUrl { get; set; }

    public string? GitHubUrl { get; set; }

    public string[] Skills { get; set; } = [];

    public List<CandidateEducation> Education { get; set; } = [];

    public List<CandidateExperience> Experience { get; set; } = [];

    public List<CandidateCertification> Certifications { get; set; } = [];
}
