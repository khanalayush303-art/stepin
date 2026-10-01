namespace StepIn.Domain.Applications;

/// <summary>
/// A submitted application's lifecycle state. <see cref="Submitted"/> is set
/// once, server-side, at creation (Phase 2.4) and never chosen by a client.
/// The remaining values are recruiter-set via Phase 3.3's status-update
/// endpoint, scoped to jobs the recruiter owns — never candidate-settable.
/// Stored as a plain <c>varchar(20)</c> with no database CHECK constraint
/// (see <c>JobApplicationConfiguration</c>), so adding a value here is a
/// schema-compatible enum addition, not a migration.
/// </summary>
public enum JobApplicationStatus
{
    Submitted,
    Reviewed,
    Shortlisted,
    Rejected,
}
