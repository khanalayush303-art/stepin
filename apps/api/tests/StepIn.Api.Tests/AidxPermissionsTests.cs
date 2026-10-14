using FluentAssertions;
using StepIn.Domain.Aidx;
using StepIn.Domain.Common;
using StepIn.Domain.Users;

namespace StepIn.Api.Tests;

/// <summary>Pure unit tests — no database, no Docker. <see cref="AidxPermissions"/> is a static,
/// side-effect-free gate, exactly like <c>AidxOwnershipRules</c>.</summary>
public sealed class AidxPermissionsTests
{
    private static ApplicationUser User(UserRole? role = null) => new()
    {
        ClerkUserId = $"user_{Guid.NewGuid():N}",
        Email = "test@example.com",
        FirstName = "Test",
        LastName = "User",
        Role = role,
    };

    private static AidxMembership Membership(AidxLabRole role, Guid? scope = null, AidxMembershipStatus status = AidxMembershipStatus.Active) => new()
    {
        UserId = Guid.NewGuid(),
        Role = role,
        Status = status,
        ScopedResearchAreaId = scope,
        GrantedByUserId = Guid.NewGuid(),
    };

    [Fact]
    public void Platform_admin_can_manage_memberships_with_no_membership_row()
    {
        AidxPermissions.CanManageMemberships(User(UserRole.Admin), null).Should().BeTrue();
    }

    [Fact]
    public void LabManager_can_manage_memberships()
    {
        AidxPermissions.CanManageMemberships(User(), Membership(AidxLabRole.LabManager)).Should().BeTrue();
    }

    [Theory]
    [InlineData(AidxLabRole.Contributor)]
    [InlineData(AidxLabRole.Editor)]
    public void Contributor_and_Editor_cannot_manage_memberships(AidxLabRole role)
    {
        AidxPermissions.CanManageMemberships(User(), Membership(role)).Should().BeFalse();
    }

    [Fact]
    public void A_revoked_LabManager_cannot_manage_memberships()
    {
        AidxPermissions.CanManageMemberships(User(), Membership(AidxLabRole.LabManager, status: AidxMembershipStatus.Revoked)).Should().BeFalse();
    }

    [Fact]
    public void Lab_wide_editor_is_within_scope_of_any_content()
    {
        var editor = Membership(AidxLabRole.Editor, scope: null);
        AidxPermissions.IsWithinEditorScope(editor, []).Should().BeTrue();
        AidxPermissions.IsWithinEditorScope(editor, [Guid.NewGuid()]).Should().BeTrue();
    }

    [Fact]
    public void Scoped_editor_is_within_scope_only_when_an_area_matches()
    {
        var areaId = Guid.NewGuid();
        var editor = Membership(AidxLabRole.Editor, scope: areaId);

        AidxPermissions.IsWithinEditorScope(editor, [areaId]).Should().BeTrue();
        AidxPermissions.IsWithinEditorScope(editor, [areaId, Guid.NewGuid()]).Should().BeTrue();
    }

    [Fact]
    public void Scoped_editor_is_out_of_scope_for_unmatched_areas_and_for_no_areas_at_all()
    {
        var editor = Membership(AidxLabRole.Editor, scope: Guid.NewGuid());

        AidxPermissions.IsWithinEditorScope(editor, [Guid.NewGuid()]).Should().BeFalse();

        // News/Event pass an empty area sequence — a scoped editor has zero rights over them.
        AidxPermissions.IsWithinEditorScope(editor, []).Should().BeFalse();
    }

    [Fact]
    public void Contributor_can_edit_only_content_they_created()
    {
        var membership = Membership(AidxLabRole.Contributor);
        AidxPermissions.CanEditAsOwner(membership, membership.Id).Should().BeTrue();
        AidxPermissions.CanEditAsOwner(membership, Guid.NewGuid()).Should().BeFalse();
    }

    [Fact]
    public void A_legacy_null_owned_row_is_never_implicitly_editable_by_a_Contributor()
    {
        var membership = Membership(AidxLabRole.Contributor);
        AidxPermissions.CanEditAsOwner(membership, null).Should().BeFalse();
    }

    [Fact]
    public void A_revoked_membership_cannot_edit_even_its_own_prior_content()
    {
        var membership = Membership(AidxLabRole.Contributor, status: AidxMembershipStatus.Revoked);
        AidxPermissions.CanEditAsOwner(membership, membership.Id).Should().BeFalse();
    }

    [Fact]
    public void Claiming_ownership_is_refused_once_a_row_already_has_an_owner()
    {
        var editor = Membership(AidxLabRole.Editor);
        AidxPermissions.CanClaimOwnership(User(), editor, createdByMembershipId: Guid.NewGuid(), contentAreaIds: []).Should().BeFalse();
    }

    [Fact]
    public void A_Contributor_cannot_claim_a_legacy_unowned_row()
    {
        var contributor = Membership(AidxLabRole.Contributor);
        AidxPermissions.CanClaimOwnership(User(), contributor, createdByMembershipId: null, contentAreaIds: []).Should().BeFalse();
    }

    [Fact]
    public void A_scoped_editor_can_claim_only_within_their_own_scope()
    {
        var areaId = Guid.NewGuid();
        var editor = Membership(AidxLabRole.Editor, scope: areaId);

        AidxPermissions.CanClaimOwnership(User(), editor, null, contentAreaIds: [areaId]).Should().BeTrue();
        AidxPermissions.CanClaimOwnership(User(), editor, null, contentAreaIds: [Guid.NewGuid()]).Should().BeFalse();
    }

    [Fact]
    public void A_platform_Admin_can_claim_any_legacy_row_with_no_membership_at_all()
    {
        AidxPermissions.CanClaimOwnership(User(UserRole.Admin), null, null, contentAreaIds: [Guid.NewGuid()]).Should().BeTrue();
    }
}
