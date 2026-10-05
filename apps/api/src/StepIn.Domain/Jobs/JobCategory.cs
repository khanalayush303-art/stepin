namespace StepIn.Domain.Jobs;

/// <summary>
/// Distinguishes a StepIn career posting from an AIDX Lab research opportunity.
/// Both share the same Job lifecycle, ownership and application workflow.
/// Existing rows default to <see cref="Career"/>; only AIDX-owned jobs are <see cref="Research"/>.
/// </summary>
public enum JobCategory
{
    Career,
    Research,
}
