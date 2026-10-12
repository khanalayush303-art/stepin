using StepIn.Domain.Common;

namespace StepIn.Domain.Experience;

/// <summary>
/// One immutable row per state transition on an <see cref="ExperienceRecord"/>. There is no
/// update or delete path for this entity anywhere in the API — every endpoint that touches
/// verification state writes exactly one new row here, in the same database transaction as the
/// status change, and nothing ever modifies or removes an existing row. This is an
/// application-level audit trail, protected by authorization and the absence of any write path
/// other than the transition endpoints themselves — not a cryptographically tamper-proof ledger.
///
/// <see cref="ActorUserId"/> and <see cref="ActorRole"/> are resolved server-side from the
/// authenticated principal performing the transition — never from client input.
/// <see cref="ActorRole"/> is a snapshot of the actor's role at the moment of the action, because
/// a user's role can change later and the audit trail should describe what actually happened, not
/// be silently reinterpreted by a later role change.
/// </summary>
public sealed class ExperienceVerificationAudit : Entity
{
    public required Guid ExperienceRecordId { get; set; }

    public required Guid ActorUserId { get; set; }

    public required string ActorRole { get; set; }

    public required ExperienceAuditAction Action { get; set; }

    public required ExperienceVerificationStatus PreviousStatus { get; set; }

    public required ExperienceVerificationStatus NewStatus { get; set; }

    public string? Note { get; set; }
}
