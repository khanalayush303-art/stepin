using StepIn.Domain.Common;

namespace StepIn.Domain.Profiles;

/// <summary>One education entry belonging to a <see cref="CandidateProfile"/>.</summary>
public sealed class CandidateEducation : Entity
{
    public required Guid CandidateProfileId { get; set; }

    public required string Institution { get; set; }

    public string? Degree { get; set; }

    public string? FieldOfStudy { get; set; }

    public DateOnly? StartDate { get; set; }

    public DateOnly? EndDate { get; set; }

    public string? Description { get; set; }
}
