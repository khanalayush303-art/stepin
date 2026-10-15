using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using StepIn.Api.Endpoints;
using StepIn.Domain.Aidx;
using StepIn.Infrastructure.Persistence;

namespace StepIn.Api.Tests;

/// <summary>
/// The Contributor-authoring gap this correction closes: before this endpoint existed, nothing
/// but direct DB manipulation in tests ever set <see cref="AidxProject.CreatedByMembershipId"/> —
/// a Contributor had no way to create brand-new content at all. These tests prove the real,
/// end-to-end path: an empty workspace to a Draft, through submit/changes-requested/resubmit, to
/// a different authorized Editor publishing it — against the real HTTP surface and a real
/// PostgreSQL container, not a mocked shortcut.
/// </summary>
public sealed class AidxContentAuthoringEndpointsTests(AuthApiFactory factory) : IClassFixture<AuthApiFactory>
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

    private static string Unique(string prefix) => $"{prefix} {Guid.NewGuid():N}";

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

    private async Task<AidxMembership> GrantAsync(Guid userId, AidxLabRole role)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var membership = new AidxMembership { UserId = userId, Role = role, Status = AidxMembershipStatus.Active, GrantedByUserId = userId };
        db.AidxMemberships.Add(membership);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        return membership;
    }

    private static AidxProjectRequest ProjectRequest(string title, string? expectedVersion = null) =>
        new(title, null, "Short.", "Long description.", null, null, null, false, null, null, null, expectedVersion);

    private async Task<AidxProject> ReloadProjectAsync(Guid id)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        return await db.AidxProjects.AsNoTracking().SingleAsync(p => p.Id == id, TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task A_contributor_creates_their_own_project_and_ownership_is_never_client_supplied()
    {
        var (token, userId) = await NewUserAsync();
        var membership = await GrantAsync(userId, AidxLabRole.Contributor);

        var response = await SendAsync(HttpMethod.Post, "/api/v1/aidx/authoring/projects", token, ProjectRequest(Unique("My First Project")));
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var created = (await response.Content.ReadFromJsonAsync<AidxIdResponse>(TestContext.Current.CancellationToken))!;

        var project = await ReloadProjectAsync(created.Id);
        project.CreatedByMembershipId.Should().Be(membership.Id, "ownership is always resolved from the caller's own membership, never accepted from the request body");
        project.Status.Should().Be(AidxContentStatus.Draft);
    }

    [Fact]
    public async Task Two_contributors_creating_projects_never_cross_assign_ownership()
    {
        var (tokenA, userIdA) = await NewUserAsync();
        var membershipA = await GrantAsync(userIdA, AidxLabRole.Contributor);
        var (tokenB, userIdB) = await NewUserAsync();
        var membershipB = await GrantAsync(userIdB, AidxLabRole.Contributor);

        var responseA = await SendAsync(HttpMethod.Post, "/api/v1/aidx/authoring/projects", tokenA, ProjectRequest(Unique("Project A")));
        var createdA = (await responseA.Content.ReadFromJsonAsync<AidxIdResponse>(TestContext.Current.CancellationToken))!;
        var responseB = await SendAsync(HttpMethod.Post, "/api/v1/aidx/authoring/projects", tokenB, ProjectRequest(Unique("Project B")));
        var createdB = (await responseB.Content.ReadFromJsonAsync<AidxIdResponse>(TestContext.Current.CancellationToken))!;

        (await ReloadProjectAsync(createdA.Id)).CreatedByMembershipId.Should().Be(membershipA.Id);
        (await ReloadProjectAsync(createdB.Id)).CreatedByMembershipId.Should().Be(membershipB.Id);
    }

    [Fact]
    public async Task Someone_without_any_membership_cannot_create_content()
    {
        var (token, _) = await NewUserAsync();

        (await SendAsync(HttpMethod.Post, "/api/v1/aidx/authoring/projects", token, ProjectRequest(Unique("No Membership"))))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task A_contributor_cannot_edit_another_contributors_project()
    {
        var (ownerToken, ownerUserId) = await NewUserAsync();
        await GrantAsync(ownerUserId, AidxLabRole.Contributor);
        var (otherToken, otherUserId) = await NewUserAsync();
        await GrantAsync(otherUserId, AidxLabRole.Contributor);

        var created = (await (await SendAsync(HttpMethod.Post, "/api/v1/aidx/authoring/projects", ownerToken, ProjectRequest(Unique("Owned Project"))))
            .Content.ReadFromJsonAsync<AidxIdResponse>(TestContext.Current.CancellationToken))!;

        (await SendAsync(HttpMethod.Put, $"/api/v1/aidx/authoring/projects/{created.Id}", otherToken, ProjectRequest(Unique("Hijacked"), created.Version)))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task A_stale_version_on_the_authoring_route_is_rejected_before_any_write()
    {
        var (token, userId) = await NewUserAsync();
        await GrantAsync(userId, AidxLabRole.Contributor);
        var originalTitle = Unique("Guarded Project");
        var created = (await (await SendAsync(HttpMethod.Post, "/api/v1/aidx/authoring/projects", token, ProjectRequest(originalTitle)))
            .Content.ReadFromJsonAsync<AidxIdResponse>(TestContext.Current.CancellationToken))!;

        (await SendAsync(HttpMethod.Put, $"/api/v1/aidx/authoring/projects/{created.Id}", token, ProjectRequest(Unique("Should Not Apply"), "0")))
            .StatusCode.Should().Be(HttpStatusCode.Conflict);

        (await ReloadProjectAsync(created.Id)).Title.Should().Be(originalTitle);
    }

    [Fact]
    public async Task Full_lifecycle_empty_workspace_to_submit_changes_requested_resubmit_and_a_different_editor_publishes()
    {
        var (contributorToken, contributorUserId) = await NewUserAsync();
        await GrantAsync(contributorUserId, AidxLabRole.Contributor);
        var (editorToken, editorUserId) = await NewUserAsync();
        await GrantAsync(editorUserId, AidxLabRole.Editor);

        // 1. Create, from nothing — an empty workspace.
        var title = Unique("Lifecycle Project");
        var created = (await (await SendAsync(HttpMethod.Post, "/api/v1/aidx/authoring/projects", contributorToken, ProjectRequest(title)))
            .Content.ReadFromJsonAsync<AidxIdResponse>(TestContext.Current.CancellationToken))!;
        created.Status.Should().Be("Draft");

        // 2. Submit.
        var submit = await SendAsync(HttpMethod.Post, $"/api/v1/aidx/content/project/{created.Id}/submit", contributorToken,
            new AidxWorkflowActionRequest(created.Version!));
        submit.StatusCode.Should().Be(HttpStatusCode.OK);

        // 3. A different Editor requests changes.
        var afterSubmitVersion = await CurrentVersionAsync(created.Id);
        var requestChanges = await SendAsync(HttpMethod.Post, $"/api/v1/aidx/content/project/{created.Id}/request-changes", editorToken,
            new AidxRequestChangesRequest(afterSubmitVersion, "Please add more detail to the short description."));
        requestChanges.StatusCode.Should().Be(HttpStatusCode.OK);

        // 4. The Contributor edits (now back in ChangesRequested — editable in place) and resubmits.
        var afterChangesRequestedVersion = await CurrentVersionAsync(created.Id);
        var revisedTitle = Unique("Lifecycle Project Revised");
        var edited = await SendAsync(HttpMethod.Put, $"/api/v1/aidx/authoring/projects/{created.Id}", contributorToken,
            ProjectRequest(revisedTitle, afterChangesRequestedVersion));
        edited.StatusCode.Should().Be(HttpStatusCode.OK);
        var editedBody = (await edited.Content.ReadFromJsonAsync<AidxIdResponse>(TestContext.Current.CancellationToken))!;
        editedBody.RevisionOfId.Should().BeNull("ChangesRequested, pre-first-publish, is edited in place — not through a revision");

        var resubmit = await SendAsync(HttpMethod.Post, $"/api/v1/aidx/content/project/{created.Id}/submit", contributorToken,
            new AidxWorkflowActionRequest(editedBody.Version!));
        resubmit.StatusCode.Should().Be(HttpStatusCode.OK);

        // 5. The Editor approves — a different authorized actor than the Contributor who authored it.
        var beforeApproveVersion = await CurrentVersionAsync(created.Id);
        var approve = await SendAsync(HttpMethod.Post, $"/api/v1/aidx/content/project/{created.Id}/approve", editorToken,
            new AidxWorkflowActionRequest(beforeApproveVersion));
        approve.StatusCode.Should().Be(HttpStatusCode.OK);

        var published = await ReloadProjectAsync(created.Id);
        published.Status.Should().Be(AidxContentStatus.Published);
        published.Title.Should().Be(revisedTitle, "the resubmitted, Editor-approved content is what actually went live");
    }

    [Fact]
    public async Task Editing_while_Submitted_or_UnderReview_is_refused_until_the_contributor_withdraws()
    {
        var (contributorToken, contributorUserId) = await NewUserAsync();
        await GrantAsync(contributorUserId, AidxLabRole.Contributor);

        var created = (await (await SendAsync(HttpMethod.Post, "/api/v1/aidx/authoring/projects", contributorToken, ProjectRequest(Unique("Read Only During Review"))))
            .Content.ReadFromJsonAsync<AidxIdResponse>(TestContext.Current.CancellationToken))!;

        (await SendAsync(HttpMethod.Post, $"/api/v1/aidx/content/project/{created.Id}/submit", contributorToken,
            new AidxWorkflowActionRequest(created.Version!))).StatusCode.Should().Be(HttpStatusCode.OK);

        var submittedVersion = await CurrentVersionAsync(created.Id);
        (await SendAsync(HttpMethod.Put, $"/api/v1/aidx/authoring/projects/{created.Id}", contributorToken,
                ProjectRequest(Unique("Should Be Refused"), submittedVersion)))
            .StatusCode.Should().Be(HttpStatusCode.Conflict);

        (await ReloadProjectAsync(created.Id)).Status.Should().Be(AidxContentStatus.Submitted, "the refused write must not have changed anything");
    }

    [Fact]
    public async Task Editing_an_already_published_project_the_contributor_owns_opens_a_revision_not_a_direct_write()
    {
        var (contributorToken, contributorUserId) = await NewUserAsync();
        var contributorMembership = await GrantAsync(contributorUserId, AidxLabRole.Contributor);
        var (editorToken, editorUserId) = await NewUserAsync();
        await GrantAsync(editorUserId, AidxLabRole.Editor);

        var originalTitle = Unique("Published By Contributor");
        var created = (await (await SendAsync(HttpMethod.Post, "/api/v1/aidx/authoring/projects", contributorToken, ProjectRequest(originalTitle)))
            .Content.ReadFromJsonAsync<AidxIdResponse>(TestContext.Current.CancellationToken))!;
        (await SendAsync(HttpMethod.Post, $"/api/v1/aidx/content/project/{created.Id}/submit", contributorToken,
            new AidxWorkflowActionRequest(created.Version!))).StatusCode.Should().Be(HttpStatusCode.OK);
        var afterSubmit = await CurrentVersionAsync(created.Id);
        (await SendAsync(HttpMethod.Post, $"/api/v1/aidx/content/project/{created.Id}/approve", editorToken,
            new AidxWorkflowActionRequest(afterSubmit))).StatusCode.Should().Be(HttpStatusCode.OK);

        var publishedVersion = await CurrentVersionAsync(created.Id);
        var newTitle = Unique("Edited After Publish");
        var editResponse = await SendAsync(HttpMethod.Put, $"/api/v1/aidx/authoring/projects/{created.Id}", contributorToken,
            ProjectRequest(newTitle, publishedVersion));
        editResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var editBody = (await editResponse.Content.ReadFromJsonAsync<AidxIdResponse>(TestContext.Current.CancellationToken))!;

        editBody.RevisionOfId.Should().Be(created.Id, "editing published content opens a revision instead of mutating the canonical row");
        (await ReloadProjectAsync(created.Id)).Title.Should().Be(originalTitle, "the live, public canonical row is untouched until the revision is approved");

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var revision = await db.AidxProjects.AsNoTracking().SingleAsync(p => p.Id == editBody.Id, TestContext.Current.CancellationToken);
        revision.Title.Should().Be(newTitle);
        revision.CreatedByMembershipId.Should().Be(contributorMembership.Id);
    }

    [Fact]
    public async Task Outcomes_set_through_authoring_survive_approval_and_appear_on_the_public_page()
    {
        var (contributorToken, contributorUserId) = await NewUserAsync();
        await GrantAsync(contributorUserId, AidxLabRole.Contributor);
        var (editorToken, editorUserId) = await NewUserAsync();
        await GrantAsync(editorUserId, AidxLabRole.Editor);

        var title = Unique("Outcomes Project");
        var created = (await (await SendAsync(HttpMethod.Post, "/api/v1/aidx/authoring/projects", contributorToken, ProjectRequest(title)))
            .Content.ReadFromJsonAsync<AidxIdResponse>(TestContext.Current.CancellationToken))!;

        var withOutcomes = new AidxProjectRequest(
            title, null, "Short.", "Long description.", null, null, null, false, null, null, null, created.Version, "Delivered a working prototype.");
        (await SendAsync(HttpMethod.Put, $"/api/v1/aidx/authoring/projects/{created.Id}", contributorToken, withOutcomes))
            .StatusCode.Should().Be(HttpStatusCode.OK);

        var afterEditVersion = await CurrentVersionAsync(created.Id);
        (await SendAsync(HttpMethod.Post, $"/api/v1/aidx/content/project/{created.Id}/submit", contributorToken,
            new AidxWorkflowActionRequest(afterEditVersion))).StatusCode.Should().Be(HttpStatusCode.OK);
        var afterSubmitVersion = await CurrentVersionAsync(created.Id);
        (await SendAsync(HttpMethod.Post, $"/api/v1/aidx/content/project/{created.Id}/approve", editorToken,
            new AidxWorkflowActionRequest(afterSubmitVersion))).StatusCode.Should().Be(HttpStatusCode.OK);

        var slug = (await ReloadProjectAsync(created.Id)).Slug;
        var publicDetail = await SendAsync(HttpMethod.Get, $"/api/v1/aidx/projects/{slug}", null);
        var body = await publicDetail.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        body.GetProperty("outcomes").GetString().Should().Be("Delivered a working prototype.");
    }

    private async Task<string> CurrentVersionAsync(Guid id)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var tracked = await db.AidxProjects.FirstAsync(p => p.Id == id, TestContext.Current.CancellationToken);
        return db.Entry(tracked).Property<uint>("xmin").CurrentValue.ToString();
    }
}
