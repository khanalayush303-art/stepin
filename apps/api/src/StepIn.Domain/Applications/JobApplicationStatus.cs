namespace StepIn.Domain.Applications;

/// <summary>
/// A submitted application's lifecycle state. Only one value exists in Phase
/// 2.4 — this is candidate submission only, with no recruiter-controlled
/// transitions yet. Adding states like Reviewed/Shortlisted later is a
/// schema-compatible enum addition, not a rename.
/// </summary>
public enum JobApplicationStatus
{
    Submitted,
}
