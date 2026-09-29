using StepIn.Domain.Common;
using StepIn.Domain.Companies;

namespace StepIn.Domain.Jobs;

/// <summary>
/// A recruiter-owned job posting. <see cref="RecruiterProfileId"/> and
/// <see cref="CompanyId"/> are set once, server-side, at creation time from the
/// authenticated recruiter's own profile — never from client input — and never
/// change after that. Like <see cref="Profiles.CandidateProfile"/>, the relationship
/// to <see cref="Profiles.RecruiterProfile"/> is foreign-key-only; the relationship
/// to <see cref="Company"/> is a real navigation, matching
/// <see cref="Profiles.RecruiterProfile.Company"/>.
/// </summary>
public sealed class Job : Entity
{
    public required Guid RecruiterProfileId { get; set; }

    public required Guid CompanyId { get; set; }

    public Company Company { get; set; } = null!;

    public required string Title { get; set; }

    public required string Description { get; set; }

    public EmploymentType EmploymentType { get; set; }

    public WorkplaceType WorkplaceType { get; set; }

    public required string Location { get; set; }

    public string? Compensation { get; set; }

    public string[] Skills { get; set; } = [];

    public JobStatus Status { get; set; } = JobStatus.Draft;

    public DateTimeOffset? PublishedAt { get; set; }
}
