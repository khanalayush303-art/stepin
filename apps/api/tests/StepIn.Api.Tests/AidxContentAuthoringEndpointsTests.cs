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

    private async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request)
    {
        using var client = _factory.CreateClient();
        using var owned = request;
        return await client.SendAsync(owned, TestContext.Current.CancellationToken);
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

    private static byte[] BuildValidPng()
    {
        using var bitmap = new SkiaSharp.SKBitmap(4, 4);
        using var image = SkiaSharp.SKImage.FromBitmap(bitmap);
        using var data = image.Encode(SkiaSharp.SKEncodedImageFormat.Png, 100);
        return data.ToArray();
    }

    private static HttpRequestMessage BuildMultipart(
        HttpMethod method, string url, string? token, byte[] fileBytes, string fileName, string contentType,
        IReadOnlyDictionary<string, string>? fields = null)
    {
        var request = new HttpRequestMessage(method, url);
        var form = new MultipartFormDataContent();
        var fileContent = new ByteArrayContent(fileBytes);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue(contentType);
        form.Add(fileContent, "file", fileName);
        if (fields is not null)
        {
            foreach (var (name, value) in fields)
            {
                form.Add(new StringContent(value), name);
            }
        }
        request.Content = form;
        if (token is not null)
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }
        request.Headers.Add("X-Requested-With", "fetch");
        return request;
    }

    /// <summary>
    /// The full requested validation scenario, end to end against the real HTTP surface: a
    /// Contributor creates a complete draft (milestones, research area, technologies, team, and an
    /// image — not just the three bare text fields the earlier lifecycle test used), an Editor
    /// requests changes, the Contributor edits and resubmits, a different Editor approves, and the
    /// public page shows every one of those fields and the image. It also proves the two
    /// authorization boundaries the task explicitly asked not to be assumed: an unrelated
    /// Contributor cannot read someone else's Draft through the new authoring GET route, and an
    /// anonymous caller cannot read the Draft's private image before it is published.
    /// </summary>
    [Fact]
    public async Task A_complete_draft_with_milestones_areas_technologies_team_and_image_survives_the_full_review_lifecycle_onto_the_public_page()
    {
        var (contributorToken, contributorUserId) = await NewUserAsync();
        await GrantAsync(contributorUserId, AidxLabRole.Contributor);
        var (otherContributorToken, otherContributorUserId) = await NewUserAsync();
        await GrantAsync(otherContributorUserId, AidxLabRole.Contributor);
        var (editorToken, editorUserId) = await NewUserAsync();
        await GrantAsync(editorUserId, AidxLabRole.Editor);
        var (secondEditorToken, secondEditorUserId) = await NewUserAsync();
        await GrantAsync(secondEditorUserId, AidxLabRole.Editor);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var area = new AidxResearchArea { Name = Unique("Area"), Slug = Unique("area").Replace(' ', '-').ToLowerInvariant() };
        var researcher = new AidxResearcher { DisplayName = Unique("Researcher"), Slug = Unique("researcher").Replace(' ', '-').ToLowerInvariant(), Category = AidxResearcherCategory.Academic, Published = true };
        db.AddRange(area, researcher);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var title = Unique("Complete Draft Project");
        var fullRequest = new AidxProjectRequest(
            title, null, "Short.", "Long description.", null, null, null, false,
            [area.Id], ["Python"], [new AidxProjectResearcherInput(researcher.Id, "Lead")], null, "Outcome text.",
            [new AidxProjectMilestoneInput("Milestone one", "First step.", null, null)]);

        var created = (await (await SendAsync(HttpMethod.Post, "/api/v1/aidx/authoring/projects", contributorToken, fullRequest))
            .Content.ReadFromJsonAsync<AidxIdResponse>(TestContext.Current.CancellationToken))!;

        // An unrelated Contributor cannot read this Draft through the authoring GET route.
        (await SendAsync(HttpMethod.Get, $"/api/v1/aidx/authoring/projects/{created.Id}", otherContributorToken))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden);

        // The owner can read it back, and it carries exactly what was sent.
        var ownerGet = await SendAsync(HttpMethod.Get, $"/api/v1/aidx/authoring/projects/{created.Id}", contributorToken);
        ownerGet.StatusCode.Should().Be(HttpStatusCode.OK);
        var ownerDetail = await ownerGet.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        ownerDetail.GetProperty("milestones")[0].GetProperty("title").GetString().Should().Be("Milestone one");

        // Upload an image to the Draft; an anonymous caller must not be able to read it yet.
        var upload = await SendAsync(BuildMultipart(
            HttpMethod.Post, $"/api/v1/aidx/authoring/projects/{created.Id}/image", contributorToken, BuildValidPng(), "cover.png", "image/png",
            new Dictionary<string, string> { ["altText"] = "A diagram of the project." }));
        upload.StatusCode.Should().Be(HttpStatusCode.OK);

        (await SendAsync(HttpMethod.Get, $"/api/v1/aidx/media/projects/{created.Id}/image", null))
            .StatusCode.Should().Be(HttpStatusCode.Unauthorized, "an anonymous caller gets 401, not 403, and never a 200");

        // Submit, Editor A requests changes (with a comment), Contributor resubmits, Editor B approves.
        var afterUpload = await CurrentVersionAsync(created.Id);
        (await SendAsync(HttpMethod.Post, $"/api/v1/aidx/content/project/{created.Id}/submit", contributorToken,
            new AidxWorkflowActionRequest(afterUpload))).StatusCode.Should().Be(HttpStatusCode.OK);

        var afterSubmit = await CurrentVersionAsync(created.Id);
        (await SendAsync(HttpMethod.Post, $"/api/v1/aidx/content/project/{created.Id}/request-changes", editorToken,
            new AidxRequestChangesRequest(afterSubmit, "Please tighten the summary."))).StatusCode.Should().Be(HttpStatusCode.OK);

        var afterChangesRequested = await CurrentVersionAsync(created.Id);
        var editResponse = await SendAsync(HttpMethod.Put, $"/api/v1/aidx/authoring/projects/{created.Id}", contributorToken,
                fullRequest with { ExpectedVersion = afterChangesRequested });
        var editResponseBody = await editResponse.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        editResponse.StatusCode.Should().Be(HttpStatusCode.OK, editResponseBody);

        var afterEdit = await CurrentVersionAsync(created.Id);
        (await SendAsync(HttpMethod.Post, $"/api/v1/aidx/content/project/{created.Id}/submit", contributorToken,
            new AidxWorkflowActionRequest(afterEdit))).StatusCode.Should().Be(HttpStatusCode.OK);

        // The audit trail shows the feedback, correctly attributed, pinned to the right snapshot.
        var auditResponse = await SendAsync(HttpMethod.Get, $"/api/v1/aidx/content/project/{created.Id}/audit", contributorToken);
        auditResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var auditEntries = await auditResponse.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        var changesRequestedEntry = auditEntries.EnumerateArray().Single(e => e.GetProperty("action").GetString() == "ChangesRequested");
        changesRequestedEntry.GetProperty("comment").GetString().Should().Be("Please tighten the summary.");

        var afterResubmit = await CurrentVersionAsync(created.Id);
        (await SendAsync(HttpMethod.Post, $"/api/v1/aidx/content/project/{created.Id}/approve", secondEditorToken,
            new AidxWorkflowActionRequest(afterResubmit))).StatusCode.Should().Be(HttpStatusCode.OK);

        var slug = (await ReloadProjectAsync(created.Id)).Slug;
        var publicDetail = await SendAsync(HttpMethod.Get, $"/api/v1/aidx/projects/{slug}", null);
        publicDetail.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await publicDetail.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        body.GetProperty("outcomes").GetString().Should().Be("Outcome text.");
        body.GetProperty("technologies").EnumerateArray().Select(t => t.GetString()).Should().Contain("Python");
        body.GetProperty("researchAreas").EnumerateArray().Select(a => a.GetString()).Should().Contain(area.Name);
        body.GetProperty("researchers")[0].GetProperty("displayName").GetString().Should().Be(researcher.DisplayName);
        body.GetProperty("milestones")[0].GetProperty("title").GetString().Should().Be("Milestone one");
        body.GetProperty("hasImage").GetBoolean().Should().BeTrue();

        // The now-published image is publicly readable.
        var publicImage = await SendAsync(HttpMethod.Get, $"/api/v1/aidx/media/projects/{created.Id}/image", null);
        publicImage.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    /// <summary>
    /// Regression test for the production incident on 2026-10-19: creating a project from
    /// `/aidx/workspace/projects/new` with an Outcomes value longer than 512 characters produced an
    /// unhandled <c>DbUpdateException</c> (Postgres 22001, "value too long for type character
    /// varying(512)") because <see cref="AidxProject.Outcomes"/> was the one AIDX string column
    /// left unconfigured, silently falling back to <c>ApplicationDbContext</c>'s database-wide
    /// 512-character default instead of a column width sized for "freeform results/impact text".
    /// A minimal request (this test's baseline) was never the problem — only a longer Outcomes
    /// value was. Confirmed from the live Fly.io API logs for the failed request before this fix:
    /// trace <c>0HNPDK1G0EC1Q</c>, 500 response, exception at
    /// <c>AidxContentAuthoringEndpoints.CreateProjectAsync</c>.
    /// </summary>
    [Fact]
    public async Task A_minimal_project_with_no_optional_fields_can_be_created()
    {
        var (token, userId) = await NewUserAsync();
        await GrantAsync(userId, AidxLabRole.Contributor);

        var minimal = new AidxProjectRequest(
            Unique("Minimal Project"), null, "Short.", "Long description.",
            null, null, null, false, null, null, null, null, null, null);

        var response = await SendAsync(HttpMethod.Post, "/api/v1/aidx/authoring/projects", token, minimal);
        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        response.StatusCode.Should().Be(HttpStatusCode.Created, body);
    }

    [Fact]
    public async Task An_outcomes_value_longer_than_the_old_512_character_ceiling_is_accepted_and_saved()
    {
        var (token, userId) = await NewUserAsync();
        await GrantAsync(userId, AidxLabRole.Contributor);

        // 600 characters — over the old, silently-applied 512-character database default, but
        // comfortably under the new, explicit 20,000-character column width.
        var longOutcomes = new string('x', 600);
        var request = new AidxProjectRequest(
            Unique("Long Outcomes Project"), null, "Short.", "Long description.",
            null, null, null, false, null, null, null, null, longOutcomes, null);

        var response = await SendAsync(HttpMethod.Post, "/api/v1/aidx/authoring/projects", token, request);
        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        response.StatusCode.Should().Be(HttpStatusCode.Created, body);

        var created = JsonSerializer.Deserialize<AidxIdResponse>(body, JsonSerializerOptions)!;
        (await ReloadProjectAsync(created.Id)).Outcomes.Should().Be(longOutcomes);
    }

    [Fact]
    public async Task An_outcomes_value_over_the_maximum_length_is_a_clean_validation_error_not_a_server_error()
    {
        var (token, userId) = await NewUserAsync();
        await GrantAsync(userId, AidxLabRole.Contributor);

        var tooLong = new string('x', 20_001);
        var request = new AidxProjectRequest(
            Unique("Too Long Outcomes Project"), null, "Short.", "Long description.",
            null, null, null, false, null, null, null, null, tooLong, null);

        var response = await SendAsync(HttpMethod.Post, "/api/v1/aidx/authoring/projects", token, request);
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest, "an over-length Outcomes value must be rejected before it ever reaches the database");

        var problem = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        problem.GetProperty("errors").TryGetProperty("outcomes", out _).Should().BeTrue("the field-level error must name the actual offending field, not a generic failure");

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        (await db.AidxProjects.AnyAsync(p => p.Title.StartsWith("Too Long Outcomes Project"), TestContext.Current.CancellationToken))
            .Should().BeFalse("a rejected validation request must never create a partial row");
    }

    /// <summary>
    /// Task requirement: a LabManager acting lab-wide (no <c>ScopedResearchAreaId</c>) must not be
    /// mistaken for "no membership at all" — both have a null-ish shape in different fields
    /// (<c>ScopedResearchAreaId</c> vs. the membership row itself), and confusing the two would
    /// either wrongly block every lab-wide LabManager or wrongly admit an unaffiliated account.
    /// </summary>
    [Fact]
    public async Task A_lab_manager_with_no_scoped_research_area_can_create_content_but_an_account_with_no_membership_at_all_cannot()
    {
        var (labManagerToken, labManagerUserId) = await NewUserAsync();
        var labManagerMembership = await GrantAsync(labManagerUserId, AidxLabRole.LabManager);
        labManagerMembership.ScopedResearchAreaId.Should().BeNull("lab-wide, not scoped to one research area — this is the case that must not be confused with having no membership");

        var (noMembershipToken, _) = await NewUserAsync();

        var labManagerResponse = await SendAsync(HttpMethod.Post, "/api/v1/aidx/authoring/projects", labManagerToken,
            ProjectRequest(Unique("Lab Manager Project")));
        labManagerResponse.StatusCode.Should().Be(HttpStatusCode.Created, "an unscoped LabManager membership is still an active membership and must be allowed to create content");

        var noMembershipResponse = await SendAsync(HttpMethod.Post, "/api/v1/aidx/authoring/projects", noMembershipToken,
            ProjectRequest(Unique("No Membership Project")));
        noMembershipResponse.StatusCode.Should().Be(HttpStatusCode.Forbidden, "no membership row at all is the actual, distinct case that must be refused");
    }

    private static readonly JsonSerializerOptions JsonSerializerOptions = new(JsonSerializerDefaults.Web);
}
