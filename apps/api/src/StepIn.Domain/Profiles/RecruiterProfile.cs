using StepIn.Domain.Common;
using StepIn.Domain.Companies;

namespace StepIn.Domain.Profiles;

/// <summary>
/// A recruiter's application-specific profile data, optionally linked to a
/// <see cref="Company"/>. Like <see cref="CandidateProfile"/>, the relationship
/// to <see cref="Users.ApplicationUser"/> is foreign-key-only.
/// </summary>
public sealed class RecruiterProfile : Entity
{
    public required Guid UserId { get; set; }

    public string? JobTitle { get; set; }

    public string? PhoneNumber { get; set; }

    public string? PhotoUrl { get; set; }

    public Guid? CompanyId { get; set; }

    public Company? Company { get; set; }
}
