using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using StepIn.Api.Endpoints;
using StepIn.Domain.Aidx;
using StepIn.Domain.Common;
using StepIn.Domain.Users;
using StepIn.Infrastructure.Persistence;

namespace StepIn.Api.Tests;

/// <summary>
/// Milestone 1 — AIDX lab membership, against a real PostgreSQL container.
/// Covers: CRUD, revoke-preserves-history, audit trail, no-self-assignment,
/// immediate revocation (no claim caching), and suspended-account enforcement.
/// Scope-matching (<see cref="AidxPermissions.IsWithinEditorScope"/>) is a
/// pure function covered separately by <see cref="AidxPermissionsTests"/> —
/// self-review and claim-ownership against real content are Milestone 2's
/// concern, once the editorial workflow and submission snapshots exist.
/// </summary>
public sealed class AidxMembershipEndpointsTests(AuthApiFactory factory) : IClassFixture<AuthApiFactory>
{
    private readonly AuthApiFactory _factory = factory;

    private static HttpRequestMessage Build(HttpMethod method, string url, string? token, object? body = null)
    {
        var request = new HttpRequestMessage(method, url);
        if (body is not null)
        {
            request.Content = JsonContent.Create(body);
        }

        if (token is not null)
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }

        if (method != HttpMethod.Get)
        {
            request.Headers.Add("X-Requested-With", "fetch");
        }

        return request;
    }

    private async Task<HttpResponseMessage> SendAsync(HttpMethod method, string url, string? token, object? body = null)
    {
        using var client = _factory.CreateClient();
        using var request = Build(method, url, token, body);
        return await client.SendAsync(request, TestContext.Current.CancellationToken);
    }

    /// <summary>A real signed-in StepIn user (Applicant role — the AIDX system doesn't care which
    /// platform role someone holds), returning both the Clerk-test token and their ApplicationUser.Id.</summary>
    private async Task<(string Token, Guid UserId)> NewUserAsync()
    {
        var clerkUserId = $"user_{Guid.NewGuid():N}";
        var token = AuthApiFactory.IssueToken(clerkUserId, $"{Guid.NewGuid()}@example.com");
        using var setup = Build(HttpMethod.Post, "/api/v1/auth/account-setup", token, new AccountSetupRequest("Applicant"));
        using var client = _factory.CreateClient();
        (await client.SendAsync(setup, TestContext.Current.CancellationToken)).EnsureSuccessStatusCode();

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var userId = await db.Users.Where(u => u.ClerkUserId == clerkUserId).Select(u => u.Id)
            .SingleAsync(TestContext.Current.CancellationToken);

        return (token, userId);
    }

    private async Task<AidxMembership> GrantDirectAsync(Guid userId, AidxLabRole role, Guid? scope = null)
    {
        using var scope2 = _factory.Services.CreateScope();
        var db = scope2.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var membership = new AidxMembership
        {
            UserId = userId,
            Role = role,
            Status = AidxMembershipStatus.Active,
            ScopedResearchAreaId = scope,
            GrantedByUserId = userId,
        };
        db.AidxMemberships.Add(membership);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        return membership;
    }

    [Fact]
    public async Task Granting_membership_creates_a_row_and_an_audit_entry()
    {
        var (managerToken, managerId) = await NewUserAsync();
        await GrantDirectAsync(managerId, AidxLabRole.LabManager);
        var (_, targetUserId) = await NewUserAsync();

        var response = await SendAsync(HttpMethod.Post, "/api/v1/admin/aidx/memberships", managerToken,
            new GrantAidxMembershipRequest(targetUserId, "Contributor", null));

        response.StatusCode.Should().Be(HttpStatusCode.Created);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var membership = await db.AidxMemberships.SingleAsync(m => m.UserId == targetUserId, TestContext.Current.CancellationToken);
        membership.Role.Should().Be(AidxLabRole.Contributor);
        membership.Status.Should().Be(AidxMembershipStatus.Active);

        var audit = await db.AidxMembershipAuditEntries.SingleAsync(a => a.MembershipId == membership.Id, TestContext.Current.CancellationToken);
        audit.Action.Should().Be(AidxMembershipAuditAction.Granted);
        audit.NewRole.Should().Be(AidxLabRole.Contributor);
    }

    [Fact]
    public async Task A_non_manager_cannot_grant_membership()
    {
        var (contributorToken, contributorId) = await NewUserAsync();
        await GrantDirectAsync(contributorId, AidxLabRole.Contributor);
        var (_, targetUserId) = await NewUserAsync();

        var response = await SendAsync(HttpMethod.Post, "/api/v1/admin/aidx/memberships", contributorToken,
            new GrantAidxMembershipRequest(targetUserId, "Editor", null));

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task A_platform_Admin_can_manage_memberships_with_no_membership_row_at_all()
    {
        var (adminToken, adminId) = await NewUserAsync();
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            await db.Users.Where(u => u.Id == adminId)
                .ExecuteUpdateAsync(s => s.SetProperty(u => u.Role, UserRole.Admin), TestContext.Current.CancellationToken);
        }

        var (_, targetUserId) = await NewUserAsync();

        var response = await SendAsync(HttpMethod.Post, "/api/v1/admin/aidx/memberships", adminToken,
            new GrantAidxMembershipRequest(targetUserId, "Editor", null));

        response.StatusCode.Should().Be(HttpStatusCode.Created);

        using var verifyScope = _factory.Services.CreateScope();
        var verifyDb = verifyScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        (await verifyDb.AidxMemberships.AnyAsync(m => m.UserId == adminId, TestContext.Current.CancellationToken)).Should().BeFalse();
    }

    [Fact]
    public async Task A_manager_cannot_grant_membership_to_themselves()
    {
        var (managerToken, managerId) = await NewUserAsync();
        await GrantDirectAsync(managerId, AidxLabRole.LabManager);

        var response = await SendAsync(HttpMethod.Post, "/api/v1/admin/aidx/memberships", managerToken,
            new GrantAidxMembershipRequest(managerId, "LabManager", null));

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task A_manager_cannot_revoke_or_update_their_own_membership()
    {
        var (managerToken, managerId) = await NewUserAsync();
        var membership = await GrantDirectAsync(managerId, AidxLabRole.LabManager);

        var revoke = await SendAsync(HttpMethod.Post, $"/api/v1/admin/aidx/memberships/{membership.Id}/revoke", managerToken);
        revoke.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        var update = await SendAsync(HttpMethod.Put, $"/api/v1/admin/aidx/memberships/{membership.Id}", managerToken,
            new UpdateAidxMembershipRequest("Contributor", null));
        update.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Revoking_keeps_the_row_and_its_audit_history_and_takes_effect_on_the_next_request()
    {
        var (managerToken, managerId) = await NewUserAsync();
        await GrantDirectAsync(managerId, AidxLabRole.LabManager);
        var (targetToken, targetUserId) = await NewUserAsync();
        var targetMembership = await GrantDirectAsync(targetUserId, AidxLabRole.Contributor);

        var revoke = await SendAsync(HttpMethod.Post, $"/api/v1/admin/aidx/memberships/{targetMembership.Id}/revoke", managerToken);
        revoke.StatusCode.Should().Be(HttpStatusCode.OK);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var reloaded = await db.AidxMemberships.SingleAsync(m => m.Id == targetMembership.Id, TestContext.Current.CancellationToken);
        reloaded.Status.Should().Be(AidxMembershipStatus.Revoked);
        reloaded.RevokedAt.Should().NotBeNull();

        var auditEntries = await db.AidxMembershipAuditEntries
            .Where(a => a.MembershipId == targetMembership.Id)
            .ToListAsync(TestContext.Current.CancellationToken);
        auditEntries.Should().Contain(a => a.Action == AidxMembershipAuditAction.Revoked);

        // Revocation is immediate, not dependent on a fresh Clerk token: the same
        // still-valid token used before is rejected on the very next request.
        var meAfterRevoke = await SendAsync(HttpMethod.Get, "/api/v1/aidx/membership/me", targetToken);
        meAfterRevoke.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await meAfterRevoke.Content.ReadFromJsonAsync<AidxMembershipMeResponse>(TestContext.Current.CancellationToken);
        body!.HasMembership.Should().BeFalse();
    }

    [Fact]
    public async Task A_suspended_account_gets_no_membership_access_even_with_an_active_row()
    {
        var (token, userId) = await NewUserAsync();
        await GrantDirectAsync(userId, AidxLabRole.LabManager);

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            await db.Users.Where(u => u.Id == userId)
                .ExecuteUpdateAsync(s => s.SetProperty(u => u.AccountStatus, AccountStatus.Suspended), TestContext.Current.CancellationToken);
        }

        var me = await SendAsync(HttpMethod.Get, "/api/v1/aidx/membership/me", token);
        me.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await me.Content.ReadFromJsonAsync<AidxMembershipMeResponse>(TestContext.Current.CancellationToken);
        body!.HasMembership.Should().BeFalse();

        var (_, otherUserId) = await NewUserAsync();
        var grant = await SendAsync(HttpMethod.Post, "/api/v1/admin/aidx/memberships", token,
            new GrantAidxMembershipRequest(otherUserId, "Contributor", null));
        grant.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Me_reports_no_membership_for_a_user_who_has_never_been_granted_one()
    {
        var (token, _) = await NewUserAsync();

        var response = await SendAsync(HttpMethod.Get, "/api/v1/aidx/membership/me", token);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<AidxMembershipMeResponse>(TestContext.Current.CancellationToken);
        body!.HasMembership.Should().BeFalse();
        body.Role.Should().BeNull();
    }

    [Fact]
    public async Task Linking_a_researcher_to_a_user_is_rejected_for_a_user_already_linked_elsewhere()
    {
        var (managerToken, managerId) = await NewUserAsync();
        await GrantDirectAsync(managerId, AidxLabRole.LabManager);
        var (_, candidateUserId) = await NewUserAsync();

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var researcherA = new AidxResearcher { Slug = $"a-{Guid.NewGuid():N}", DisplayName = "A", UserId = candidateUserId };
        var researcherB = new AidxResearcher { Slug = $"b-{Guid.NewGuid():N}", DisplayName = "B" };
        db.AidxResearchers.AddRange(researcherA, researcherB);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var response = await SendAsync(
            HttpMethod.Post, $"/api/v1/admin/aidx/researchers/{researcherB.Id}/link-user", managerToken,
            new LinkResearcherToUserRequest(candidateUserId));

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }
}
