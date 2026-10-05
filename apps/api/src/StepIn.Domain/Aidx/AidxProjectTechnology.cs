using StepIn.Domain.Common;

namespace StepIn.Domain.Aidx;

public sealed class AidxProjectTechnology : Entity
{
    public required Guid ProjectId { get; set; }

    public required string Name { get; set; }
}
