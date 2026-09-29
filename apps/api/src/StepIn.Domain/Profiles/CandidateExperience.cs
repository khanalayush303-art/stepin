using StepIn.Domain.Common;

namespace StepIn.Domain.Profiles;

/// <summary>One work-experience entry belonging to a <see cref="CandidateProfile"/>.</summary>
public sealed class CandidateExperience : Entity
{
    public required Guid CandidateProfileId { get; set; }

    public required string CompanyName { get; set; }

    public required string Title { get; set; }

    public DateOnly? StartDate { get; set; }

    public DateOnly? EndDate { get; set; }

    public string? Description { get; set; }
}
