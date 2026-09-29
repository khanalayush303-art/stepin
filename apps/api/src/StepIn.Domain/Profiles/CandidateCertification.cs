using StepIn.Domain.Common;

namespace StepIn.Domain.Profiles;

/// <summary>One certification entry belonging to a <see cref="CandidateProfile"/>.</summary>
public sealed class CandidateCertification : Entity
{
    public required Guid CandidateProfileId { get; set; }

    public required string Name { get; set; }

    public string? IssuingOrganization { get; set; }

    public DateOnly? IssueDate { get; set; }

    public string? CredentialUrl { get; set; }
}
