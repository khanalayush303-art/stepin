namespace StepIn.Domain.Jobs;

/// <summary>A job posting's lifecycle state. Assigned by explicit recruiter action only.</summary>
public enum JobStatus
{
    Draft,
    Published,
    Unpublished,
}
