using StepIn.Domain.Common;

namespace StepIn.Domain.Jobs;

/// <summary>
/// A candidate's bookmark of a job, independent of whether they've applied.
/// <see cref="CandidateProfileId"/> and <see cref="JobId"/> are set once,
/// server-side, at creation from the authenticated candidate and the route's
/// job id — never from client input — and never change afterward; a saved
/// job is either present or removed, there is nothing else to update.
/// <see cref="Entity.CreatedAt"/> doubles as "saved at".
/// </summary>
public sealed class SavedJob : Entity
{
    public required Guid CandidateProfileId { get; set; }

    public required Guid JobId { get; set; }

    public Job Job { get; set; } = null!;
}
