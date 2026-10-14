using StepIn.Domain.Common;

namespace StepIn.Domain.Aidx;

/// <summary>
/// One row per user who has ever been granted lab access. Role changes and
/// revocation mutate this same row (never a new one), so
/// <see cref="AidxProject.CreatedByMembershipId"/>-style ownership links and
/// <see cref="AidxMembershipAuditEntry"/> history always point at a stable id —
/// revoking access never deletes this row, only flips <see cref="Status"/>.
/// </summary>
public sealed class AidxMembership : Entity
{
    public required Guid UserId { get; set; }

    public AidxLabRole Role { get; set; }

    public AidxMembershipStatus Status { get; set; } = AidxMembershipStatus.Active;

    /// <summary>
    /// Null = lab-wide Editor. Set = this Editor may only review/publish/archive
    /// content tagged with this research area (Project/Publication only —
    /// News/Event have no area association, so a scoped Editor has zero rights
    /// over those two content types; only a lab-wide Editor may review them).
    /// Ignored for Contributor and LabManager roles.
    /// </summary>
    public Guid? ScopedResearchAreaId { get; set; }

    public required Guid GrantedByUserId { get; set; }

    public DateTimeOffset? RevokedAt { get; set; }
}
