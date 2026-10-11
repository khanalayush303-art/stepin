using StepIn.Domain.Common;
using StepIn.Domain.Jobs;

namespace StepIn.Domain.Career;

/// <summary>
/// A candidate's own career-planning goal. <see cref="CandidateProfileId"/> is set once,
/// server-side, from the authenticated candidate's own profile — never from client input — and
/// never changes after creation, the same FK-only ownership pattern as
/// <see cref="StepIn.Domain.Applications.JobApplication.CandidateProfileId"/>.
///
/// <see cref="TargetJobId"/> is optional and deliberately so: Career GPS stays fully usable for a
/// goal with no linked job at all (manual planning). When set, it must be a published Career job
/// — enforced server-side at creation and update time, not by this FK alone.
/// </summary>
public sealed class CareerGoal : Entity
{
    public required Guid CandidateProfileId { get; set; }

    public required string TargetRole { get; set; }

    public string? Description { get; set; }

    public DateOnly? TargetDate { get; set; }

    public CareerGoalStatus Status { get; set; } = CareerGoalStatus.Active;

    /// <summary>At most one of a candidate's goals should carry this — enforced at the endpoint,
    /// not by a database constraint, by clearing it on the candidate's other goals when set.</summary>
    public bool IsPrimary { get; set; }

    public Guid? TargetJobId { get; set; }

    public Job? TargetJob { get; set; }

    public List<RoadmapItem> RoadmapItems { get; set; } = [];
}
