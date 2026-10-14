using StepIn.Domain.Common;
using StepIn.Domain.Users;

namespace StepIn.Domain.Aidx;

/// <summary>
/// The one and only gate for every AIDX lab-permission decision. Mirrors
/// <see cref="AidxOwnershipRules"/>'s shape: static, pure, no persistence
/// access — callers resolve the membership/content rows, this class decides.
///
/// Within the AIDX permission matrix only (not StepIn's platform roles,
/// which stay non-hierarchical), a Platform Admin implicitly satisfies every
/// lesser check and needs no <see cref="AidxMembership"/> row at all.
/// </summary>
public static class AidxPermissions
{
    public static bool IsPlatformAdmin(ApplicationUser user) => user.Role == UserRole.Admin;

    private static bool IsActive(AidxMembership? membership) =>
        membership is { Status: AidxMembershipStatus.Active };

    public static bool CanManageMemberships(ApplicationUser user, AidxMembership? membership) =>
        IsPlatformAdmin(user) || (IsActive(membership) && membership!.Role == AidxLabRole.LabManager);

    public static bool CanManageResearchAreas(ApplicationUser user, AidxMembership? membership) =>
        CanManageMemberships(user, membership);

    public static bool CanLinkResearcherToUser(ApplicationUser user, AidxMembership? membership) =>
        CanManageMemberships(user, membership);

    /// <summary>
    /// True if the given editor/scope reaches content tagged with
    /// <paramref name="contentAreaIds"/>. For content types with no area
    /// association at all (News/Event), pass an empty sequence — only a
    /// lab-wide editor (<see cref="AidxMembership.ScopedResearchAreaId"/> is
    /// null) ever reaches those, regardless of what's passed here.
    /// </summary>
    public static bool IsWithinEditorScope(AidxMembership membership, IEnumerable<Guid> contentAreaIds)
    {
        if (membership.ScopedResearchAreaId is null)
        {
            return true; // lab-wide
        }

        return contentAreaIds.Contains(membership.ScopedResearchAreaId.Value);
    }

    /// <summary>
    /// A Contributor may edit only content they created. A legacy row with no
    /// <see cref="AidxProject.CreatedByMembershipId"/>-style owner is never
    /// implicitly open to any Contributor — it can only become editable to
    /// them via an explicit claim-ownership action performed by an
    /// Editor/LabManager/Admin.
    /// </summary>
    public static bool CanEditAsOwner(AidxMembership? membership, Guid? createdByMembershipId) =>
        IsActive(membership) && createdByMembershipId is not null && createdByMembershipId == membership!.Id;

    /// <summary>
    /// An Editor/LabManager/Admin may claim a legacy, unowned
    /// (<paramref name="createdByMembershipId"/> is null) row. A scoped
    /// Editor may claim only content within their own scope.
    /// </summary>
    public static bool CanClaimOwnership(
        ApplicationUser user,
        AidxMembership? membership,
        Guid? createdByMembershipId,
        IEnumerable<Guid> contentAreaIds)
    {
        if (createdByMembershipId is not null)
        {
            return false; // already owned — not "legacy"
        }

        if (IsPlatformAdmin(user))
        {
            return true;
        }

        if (!IsActive(membership) || membership!.Role == AidxLabRole.Contributor)
        {
            return false;
        }

        return IsWithinEditorScope(membership, contentAreaIds);
    }
}
