using StepIn.Domain.Common;

namespace StepIn.Domain.Career;

/// <summary>
/// One actionable step toward a <see cref="CareerGoal"/>. <see cref="CareerGoalId"/> is set once
/// at creation from the route's goal id, only after that goal's ownership has already been
/// verified server-side — never trusted from the request body.
/// </summary>
public sealed class RoadmapItem : Entity
{
    public required Guid CareerGoalId { get; set; }

    public required string Title { get; set; }

    public string? Description { get; set; }

    public DateOnly? TargetDate { get; set; }

    public RoadmapItemPriority Priority { get; set; } = RoadmapItemPriority.Medium;

    public RoadmapItemStatus Status { get; set; } = RoadmapItemStatus.NotStarted;

    /// <summary>Set server-side exactly when <see cref="Status"/> transitions to Completed, and
    /// cleared on reopen. Never trusted from client input.</summary>
    public DateTimeOffset? CompletedAt { get; set; }
}
