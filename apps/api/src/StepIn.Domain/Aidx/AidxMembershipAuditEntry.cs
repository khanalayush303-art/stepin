using StepIn.Domain.Common;

namespace StepIn.Domain.Aidx;

/// <summary>
/// Append-only. Never updated or deleted after insert — this is the audit
/// trail the membership system requires, not a mutable log line.
/// </summary>
public sealed class AidxMembershipAuditEntry : Entity
{
    public required Guid MembershipId { get; set; }

    public AidxMembershipAuditAction Action { get; set; }

    public required Guid PerformedByUserId { get; set; }

    public AidxLabRole? PreviousRole { get; set; }

    public AidxLabRole? NewRole { get; set; }

    public Guid? PreviousScopeId { get; set; }

    public Guid? NewScopeId { get; set; }
}
