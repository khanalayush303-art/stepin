using StepIn.Domain.Common;

namespace StepIn.Domain.Aidx;

public sealed class AidxProjectMilestone : Entity
{
    public required Guid ProjectId { get; set; }

    public required string Title { get; set; }

    public string? Description { get; set; }

    public DateOnly? TargetDate { get; set; }

    public DateOnly? CompletedDate { get; set; }

    public int SortOrder { get; set; }
}
