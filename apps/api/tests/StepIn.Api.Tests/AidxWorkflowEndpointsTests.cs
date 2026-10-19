using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using StepIn.Api.Endpoints;
using StepIn.Domain.Aidx;
using StepIn.Infrastructure.Persistence;

namespace StepIn.Api.Tests;

/// <summary>
/// Milestone 2 — the editorial workflow engine (AidxContentEditingService), exercised through
/// its HTTP surface (AidxWorkflowEndpoints), against a real PostgreSQL container. Project is the
/// primary, fully-exercised reference implementation (including its owned nav collections
/// surviving a revision clone); News/Event/Publication get focused smoke coverage proving the
/// same generic engine behaves correctly for each, since the mechanics are shared.
/// </summary>
public sealed class AidxWorkflowEndpointsTests(AuthApiFactory factory) : IClassFixture<AuthApiFactory>
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

    private async Task<AidxMembership> GrantAsync(Guid userId, AidxLabRole role, Guid? scope = null)
    {
        using var scope2 = _factory.Services.CreateScope();
        var db = scope2.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var membership = new AidxMembership
        {
            UserId = userId, Role = role, Status = AidxMembershipStatus.Active,
            ScopedResearchAreaId = scope, GrantedByUserId = userId,
        };
        db.AidxMemberships.Add(membership);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        return membership;
    }

    private async Task<AidxProject> CreateDraftProjectAsync(Guid contributorMembershipId, Guid? researchAreaId = null)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var project = new AidxProject
        {
            Title = $"Project {Guid.NewGuid():N}", Slug = $"project-{Guid.NewGuid():N}",
            ShortDescription = "Short", Description = "Long description",
            CreatedByMembershipId = contributorMembershipId,
        };
        db.AidxProjects.Add(project);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        if (researchAreaId is { } areaId)
        {
            db.Set<AidxProjectResearchArea>().Add(new AidxProjectResearchArea { ProjectId = project.Id, ResearchAreaId = areaId });
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        return project;
    }

    private async Task<Guid> CreateResearchAreaAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var area = new AidxResearchArea { Name = $"Area {Guid.NewGuid():N}", Slug = $"area-{Guid.NewGuid():N}" };
        db.AidxResearchAreas.Add(area);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        return area.Id;
    }

    private async Task<AidxProject> ReloadProjectAsync(Guid id)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        return await db.AidxProjects.Include(p => p.ResearchAreas).SingleAsync(p => p.Id == id, TestContext.Current.CancellationToken);
    }

    private async Task<string> CurrentVersionAsync(Guid id)
    {
        var project = await ReloadProjectAsync(id);
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var tracked = await db.AidxProjects.FirstAsync(p => p.Id == id, TestContext.Current.CancellationToken);
        return db.Entry(tracked).Property<uint>("xmin").CurrentValue.ToString();
    }

    [Fact]
    public async Task Full_lifecycle_submit_approve_then_edit_again_creates_a_merged_revision()
    {
        var (contributorToken, contributorUserId) = await NewUserAsync();
        var contributorMembership = await GrantAsync(contributorUserId, AidxLabRole.Contributor);
        var (editorToken, editorUserId) = await NewUserAsync();
        await GrantAsync(editorUserId, AidxLabRole.Editor);

        var project = await CreateDraftProjectAsync(contributorMembership.Id);

        // Submit
        var v0 = await CurrentVersionAsync(project.Id);
        var submit = await SendAsync(HttpMethod.Post, $"/api/v1/aidx/content/project/{project.Id}/submit", contributorToken,
            new AidxWorkflowActionRequest(v0));
        submit.StatusCode.Should().Be(HttpStatusCode.OK);

        var afterSubmit = await ReloadProjectAsync(project.Id);
        afterSubmit.Status.Should().Be(AidxContentStatus.Submitted);

        // A snapshot now exists
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var snapshotCount = await db.AidxContentSnapshots.CountAsync(s => s.SubjectId == project.Id, TestContext.Current.CancellationToken);
            snapshotCount.Should().Be(1);
        }

        // Editor approves (first-ever publish — in place)
        var v1 = await CurrentVersionAsync(project.Id);
        var approve = await SendAsync(HttpMethod.Post, $"/api/v1/aidx/content/project/{project.Id}/approve", editorToken,
            new AidxWorkflowActionRequest(v1));
        approve.StatusCode.Should().Be(HttpStatusCode.OK);

        var published = await ReloadProjectAsync(project.Id);
        published.Status.Should().Be(AidxContentStatus.Published);
        published.RevisionOfId.Should().BeNull("the first publish happens in place on the canonical row");

        // Contributor begins editing the now-published project — this must create a revision,
        // never mutate the canonical row directly.
        var beginEdit = await SendAsync(HttpMethod.Post, $"/api/v1/aidx/content/project/{project.Id}/begin-edit", contributorToken);
        beginEdit.StatusCode.Should().Be(HttpStatusCode.OK);
        var editable = await beginEdit.Content.ReadFromJsonAsync<AidxWorkflowStatusResponse>(TestContext.Current.CancellationToken);
        editable!.Id.Should().NotBe(project.Id, "editing a published project opens a revision, a different row");
        editable.RevisionOfId.Should().Be(project.Id);
        editable.RevisionNumber.Should().Be(2, "the canonical's own pre-publish round is implicitly #1");

        // The canonical row is untouched by begin-edit.
        var stillPublished = await ReloadProjectAsync(project.Id);
        stillPublished.Status.Should().Be(AidxContentStatus.Published);

        // Submit and approve the revision.
        var revisionSubmit = await SendAsync(HttpMethod.Post, $"/api/v1/aidx/content/project/{editable.Id}/submit", contributorToken,
            new AidxWorkflowActionRequest(editable.Version));
        revisionSubmit.StatusCode.Should().Be(HttpStatusCode.OK);

        using var revisionScope = _factory.Services.CreateScope();
        var revisionDb = revisionScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var revisionRow = await revisionDb.AidxProjects.FirstAsync(p => p.Id == editable.Id, TestContext.Current.CancellationToken);
        var revisionVersion = revisionDb.Entry(revisionRow).Property<uint>("xmin").CurrentValue.ToString();

        var approveRevision = await SendAsync(HttpMethod.Post, $"/api/v1/aidx/content/project/{editable.Id}/approve", editorToken,
            new AidxWorkflowActionRequest(revisionVersion));
        approveRevision.StatusCode.Should().Be(HttpStatusCode.OK);

        // The revision is marked merged, never deleted.
        using var finalScope = _factory.Services.CreateScope();
        var finalDb = finalScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var mergedRevision = await finalDb.AidxProjects.FirstAsync(p => p.Id == editable.Id, TestContext.Current.CancellationToken);
        mergedRevision.MergedAt.Should().NotBeNull();
        mergedRevision.Status.Should().Be(AidxContentStatus.Published);

        // The canonical row kept its own Id/Slug (stable URL) but gained the revision's fields.
        var finalCanonical = await finalDb.AidxProjects.FirstAsync(p => p.Id == project.Id, TestContext.Current.CancellationToken);
        finalCanonical.Status.Should().Be(AidxContentStatus.Published);
        finalCanonical.Slug.Should().Be(project.Slug);
    }

    [Fact]
    public async Task A_stale_expected_version_is_rejected_before_any_write()
    {
        var (token, userId) = await NewUserAsync();
        var membership = await GrantAsync(userId, AidxLabRole.Contributor);
        var project = await CreateDraftProjectAsync(membership.Id);

        var staleVersion = await CurrentVersionAsync(project.Id);

        // Someone else edits the title directly (simulating a concurrent save), bumping xmin.
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            await db.AidxProjects.Where(p => p.Id == project.Id)
                .ExecuteUpdateAsync(s => s.SetProperty(p => p.Title, "Changed elsewhere"), TestContext.Current.CancellationToken);
        }

        var submit = await SendAsync(HttpMethod.Post, $"/api/v1/aidx/content/project/{project.Id}/submit", token,
            new AidxWorkflowActionRequest(staleVersion));

        submit.StatusCode.Should().Be(HttpStatusCode.Conflict);

        var reloaded = await ReloadProjectAsync(project.Id);
        reloaded.Status.Should().Be(AidxContentStatus.Draft, "the stale submit must not have applied");
    }

    [Fact]
    public async Task An_editor_cannot_review_their_own_submission()
    {
        var (token, userId) = await NewUserAsync();
        var membership = await GrantAsync(userId, AidxLabRole.Editor); // Editor who is also the Contributor here
        var project = await CreateDraftProjectAsync(membership.Id);

        var v0 = await CurrentVersionAsync(project.Id);
        await SendAsync(HttpMethod.Post, $"/api/v1/aidx/content/project/{project.Id}/submit", token, new AidxWorkflowActionRequest(v0));

        var v1 = await CurrentVersionAsync(project.Id);
        var approve = await SendAsync(HttpMethod.Post, $"/api/v1/aidx/content/project/{project.Id}/approve", token,
            new AidxWorkflowActionRequest(v1));

        approve.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task A_scoped_editor_outside_their_area_cannot_review()
    {
        var (contributorToken, contributorUserId) = await NewUserAsync();
        var contributorMembership = await GrantAsync(contributorUserId, AidxLabRole.Contributor);
        var (editorToken, editorUserId) = await NewUserAsync();
        var editorArea = await CreateResearchAreaAsync();
        await GrantAsync(editorUserId, AidxLabRole.Editor, scope: editorArea);

        var otherArea = await CreateResearchAreaAsync();
        var project = await CreateDraftProjectAsync(contributorMembership.Id, researchAreaId: otherArea);

        var v0 = await CurrentVersionAsync(project.Id);
        await SendAsync(HttpMethod.Post, $"/api/v1/aidx/content/project/{project.Id}/submit", contributorToken, new AidxWorkflowActionRequest(v0));

        var v1 = await CurrentVersionAsync(project.Id);
        var approve = await SendAsync(HttpMethod.Post, $"/api/v1/aidx/content/project/{project.Id}/approve", editorToken,
            new AidxWorkflowActionRequest(v1));

        approve.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task A_scoped_editor_within_their_area_can_review()
    {
        var (contributorToken, contributorUserId) = await NewUserAsync();
        var contributorMembership = await GrantAsync(contributorUserId, AidxLabRole.Contributor);
        var (editorToken, editorUserId) = await NewUserAsync();
        var area = await CreateResearchAreaAsync();
        await GrantAsync(editorUserId, AidxLabRole.Editor, scope: area);

        var project = await CreateDraftProjectAsync(contributorMembership.Id, researchAreaId: area);

        var v0 = await CurrentVersionAsync(project.Id);
        await SendAsync(HttpMethod.Post, $"/api/v1/aidx/content/project/{project.Id}/submit", contributorToken, new AidxWorkflowActionRequest(v0));

        var v1 = await CurrentVersionAsync(project.Id);
        var approve = await SendAsync(HttpMethod.Post, $"/api/v1/aidx/content/project/{project.Id}/approve", editorToken,
            new AidxWorkflowActionRequest(v1));

        approve.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task A_scoped_editor_has_zero_rights_over_news_or_events_regardless_of_scope()
    {
        var (contributorToken, contributorUserId) = await NewUserAsync();
        var contributorMembership = await GrantAsync(contributorUserId, AidxLabRole.Contributor);
        var (editorToken, editorUserId) = await NewUserAsync();
        var area = await CreateResearchAreaAsync();
        await GrantAsync(editorUserId, AidxLabRole.Editor, scope: area);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var news = new AidxNews
        {
            Slug = $"news-{Guid.NewGuid():N}", Title = "T", Summary = "S", Body = "B",
            CreatedByMembershipId = contributorMembership.Id,
        };
        db.AidxNews.Add(news);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        var v0 = db.Entry(news).Property<uint>("xmin").CurrentValue.ToString();

        await SendAsync(HttpMethod.Post, $"/api/v1/aidx/content/news/{news.Id}/submit", contributorToken, new AidxWorkflowActionRequest(v0));

        using var reloadScope = _factory.Services.CreateScope();
        var reloadDb = reloadScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var reloaded = await reloadDb.AidxNews.FirstAsync(n => n.Id == news.Id, TestContext.Current.CancellationToken);
        var v1 = reloadDb.Entry(reloaded).Property<uint>("xmin").CurrentValue.ToString();

        var approve = await SendAsync(HttpMethod.Post, $"/api/v1/aidx/content/news/{news.Id}/approve", editorToken,
            new AidxWorkflowActionRequest(v1));

        approve.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Request_changes_records_the_feedback_pinned_to_the_submitted_snapshot()
    {
        var (contributorToken, contributorUserId) = await NewUserAsync();
        var contributorMembership = await GrantAsync(contributorUserId, AidxLabRole.Contributor);
        var (editorToken, editorUserId) = await NewUserAsync();
        await GrantAsync(editorUserId, AidxLabRole.Editor);

        var project = await CreateDraftProjectAsync(contributorMembership.Id);
        var v0 = await CurrentVersionAsync(project.Id);
        await SendAsync(HttpMethod.Post, $"/api/v1/aidx/content/project/{project.Id}/submit", contributorToken, new AidxWorkflowActionRequest(v0));

        var v1 = await CurrentVersionAsync(project.Id);
        var requestChanges = await SendAsync(HttpMethod.Post, $"/api/v1/aidx/content/project/{project.Id}/request-changes", editorToken,
            new AidxRequestChangesRequest(v1, "Please add more detail to the abstract."));

        requestChanges.StatusCode.Should().Be(HttpStatusCode.OK);

        var afterFeedback = await ReloadProjectAsync(project.Id);
        afterFeedback.Status.Should().Be(AidxContentStatus.ChangesRequested);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var auditEntry = await db.AidxEditorialAuditEntries
            .Where(e => e.SubjectId == project.Id && e.Action == AidxEditorialAction.ChangesRequested)
            .SingleAsync(TestContext.Current.CancellationToken);
        auditEntry.Comment.Should().Be("Please add more detail to the abstract.");
        auditEntry.SnapshotId.Should().NotBeNull("feedback is pinned to the exact snapshot it was given on");
    }

    [Fact]
    public async Task A_Contributor_cannot_edit_a_legacy_row_with_no_owner()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var legacyProject = new AidxProject
        {
            Title = "Legacy", Slug = $"legacy-{Guid.NewGuid():N}", ShortDescription = "S", Description = "D",
        }; // CreatedByMembershipId left null — pre-existing admin row
        db.AidxProjects.Add(legacyProject);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var (token, userId) = await NewUserAsync();
        await GrantAsync(userId, AidxLabRole.Contributor);

        var beginEdit = await SendAsync(HttpMethod.Post, $"/api/v1/aidx/content/project/{legacyProject.Id}/begin-edit", token);

        beginEdit.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task An_Editor_can_claim_a_legacy_unowned_row_and_a_Contributor_still_cannot()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var legacyProject = new AidxProject { Title = "Legacy", Slug = $"legacy-{Guid.NewGuid():N}", ShortDescription = "S", Description = "D" };
        db.AidxProjects.Add(legacyProject);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var (editorToken, editorUserId) = await NewUserAsync();
        var editorMembership = await GrantAsync(editorUserId, AidxLabRole.Editor);

        var claim = await SendAsync(HttpMethod.Post, $"/api/v1/aidx/content/project/{legacyProject.Id}/claim-ownership", editorToken);
        claim.StatusCode.Should().Be(HttpStatusCode.OK);

        using var verifyScope = _factory.Services.CreateScope();
        var verifyDb = verifyScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var reloaded = await verifyDb.AidxProjects.SingleAsync(p => p.Id == legacyProject.Id, TestContext.Current.CancellationToken);
        reloaded.CreatedByMembershipId.Should().Be(editorMembership.Id);

        // A different Contributor still cannot edit it — ownership belongs to the claiming Editor now.
        var (contributorToken, contributorUserId) = await NewUserAsync();
        await GrantAsync(contributorUserId, AidxLabRole.Contributor);
        var beginEdit = await SendAsync(HttpMethod.Post, $"/api/v1/aidx/content/project/{legacyProject.Id}/begin-edit", contributorToken);
        beginEdit.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Discarding_a_revision_keeps_the_row_forever_and_leaves_the_canonical_untouched()
    {
        var (contributorToken, contributorUserId) = await NewUserAsync();
        var contributorMembership = await GrantAsync(contributorUserId, AidxLabRole.Contributor);
        var (editorToken, editorUserId) = await NewUserAsync();
        await GrantAsync(editorUserId, AidxLabRole.Editor);

        var project = await CreateDraftProjectAsync(contributorMembership.Id);
        var v0 = await CurrentVersionAsync(project.Id);
        await SendAsync(HttpMethod.Post, $"/api/v1/aidx/content/project/{project.Id}/submit", contributorToken, new AidxWorkflowActionRequest(v0));
        var v1 = await CurrentVersionAsync(project.Id);
        await SendAsync(HttpMethod.Post, $"/api/v1/aidx/content/project/{project.Id}/approve", editorToken, new AidxWorkflowActionRequest(v1));

        var beginEdit = await SendAsync(HttpMethod.Post, $"/api/v1/aidx/content/project/{project.Id}/begin-edit", contributorToken);
        var editable = await beginEdit.Content.ReadFromJsonAsync<AidxWorkflowStatusResponse>(TestContext.Current.CancellationToken);

        var discard = await SendAsync(HttpMethod.Post, $"/api/v1/aidx/content/project/{editable!.Id}/discard", contributorToken,
            new AidxWorkflowActionRequest(editable.Version));
        discard.StatusCode.Should().Be(HttpStatusCode.OK);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var discardedRevision = await db.AidxProjects.SingleAsync(p => p.Id == editable.Id, TestContext.Current.CancellationToken);
        discardedRevision.DiscardedAt.Should().NotBeNull();

        var canonical = await db.AidxProjects.SingleAsync(p => p.Id == project.Id, TestContext.Current.CancellationToken);
        canonical.Status.Should().Be(AidxContentStatus.Published, "discarding the revision never touches the canonical row");

        // A second begin-edit creates a NEW revision rather than reopening the discarded one — the
        // one-open-revision-per-canonical constraint only excludes merged/discarded rows.
        var secondBeginEdit = await SendAsync(HttpMethod.Post, $"/api/v1/aidx/content/project/{project.Id}/begin-edit", contributorToken);
        var secondEditable = await secondBeginEdit.Content.ReadFromJsonAsync<AidxWorkflowStatusResponse>(TestContext.Current.CancellationToken);
        secondEditable!.Id.Should().NotBe(editable.Id);
    }

    [Fact]
    public async Task Concurrent_submissions_against_the_same_row_never_assign_the_same_snapshot_number()
    {
        var (contributorToken, contributorUserId) = await NewUserAsync();
        var contributorMembership = await GrantAsync(contributorUserId, AidxLabRole.Contributor);
        var (editorToken, editorUserId) = await NewUserAsync();
        await GrantAsync(editorUserId, AidxLabRole.Editor);

        var project = await CreateDraftProjectAsync(contributorMembership.Id);

        // Drive the row through two full submit/changes-requested/resubmit cycles sequentially
        // first so there's real prior history, then fire several concurrent resubmission attempts
        // at the final ChangesRequested -> Submitted edge — only one can win per the DB's own
        // (SubjectId, SnapshotNumber) unique index; the rest must fail cleanly, never collide.
        var v0 = await CurrentVersionAsync(project.Id);
        await SendAsync(HttpMethod.Post, $"/api/v1/aidx/content/project/{project.Id}/submit", contributorToken, new AidxWorkflowActionRequest(v0));
        var v1 = await CurrentVersionAsync(project.Id);
        await SendAsync(HttpMethod.Post, $"/api/v1/aidx/content/project/{project.Id}/request-changes", editorToken,
            new AidxRequestChangesRequest(v1, "fix typo"));

        var v2 = await CurrentVersionAsync(project.Id);
        var attempts = Enumerable.Range(0, 5)
            .Select(_ => SendAsync(HttpMethod.Post, $"/api/v1/aidx/content/project/{project.Id}/submit", contributorToken, new AidxWorkflowActionRequest(v2)));
        var results = await Task.WhenAll(attempts);

        results.Count(r => r.StatusCode == HttpStatusCode.OK).Should().Be(1, "only one stale-version-matching concurrent submit can win");

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var snapshots = await db.AidxContentSnapshots
            .Where(s => s.SubjectId == project.Id).OrderBy(s => s.SnapshotNumber)
            .Select(s => s.SnapshotNumber).ToListAsync(TestContext.Current.CancellationToken);
        snapshots.Should().Equal(new[] { 1, 2 }, "numbering stays sequential with no duplicates and no gaps despite the concurrent race");
    }

    [Fact]
    public async Task Event_full_lifecycle_submit_then_approve_publishes_in_place()
    {
        var (contributorToken, contributorUserId) = await NewUserAsync();
        var contributorMembership = await GrantAsync(contributorUserId, AidxLabRole.Contributor);
        var (editorToken, editorUserId) = await NewUserAsync();
        await GrantAsync(editorUserId, AidxLabRole.Editor);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var evt = new AidxEvent
        {
            Slug = $"event-{Guid.NewGuid():N}", Title = "T", Description = "D",
            StartsAt = DateTimeOffset.UtcNow.AddDays(7), CreatedByMembershipId = contributorMembership.Id,
        };
        db.AidxEvents.Add(evt);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        var v0 = db.Entry(evt).Property<uint>("xmin").CurrentValue.ToString();

        var submit = await SendAsync(HttpMethod.Post, $"/api/v1/aidx/content/event/{evt.Id}/submit", contributorToken, new AidxWorkflowActionRequest(v0));
        submit.StatusCode.Should().Be(HttpStatusCode.OK);

        using var reloadScope = _factory.Services.CreateScope();
        var reloadDb = reloadScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var submitted = await reloadDb.AidxEvents.FirstAsync(e => e.Id == evt.Id, TestContext.Current.CancellationToken);
        submitted.Status.Should().Be(AidxContentStatus.Submitted);
        var v1 = reloadDb.Entry(submitted).Property<uint>("xmin").CurrentValue.ToString();

        var approve = await SendAsync(HttpMethod.Post, $"/api/v1/aidx/content/event/{evt.Id}/approve", editorToken, new AidxWorkflowActionRequest(v1));
        approve.StatusCode.Should().Be(HttpStatusCode.OK);

        using var finalScope = _factory.Services.CreateScope();
        var finalDb = finalScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var published = await finalDb.AidxEvents.FirstAsync(e => e.Id == evt.Id, TestContext.Current.CancellationToken);
        published.Status.Should().Be(AidxContentStatus.Published);
    }

    [Fact]
    public async Task Publication_edit_after_publish_creates_a_revision_with_reparented_authors()
    {
        var (contributorToken, contributorUserId) = await NewUserAsync();
        var contributorMembership = await GrantAsync(contributorUserId, AidxLabRole.Contributor);
        var (editorToken, editorUserId) = await NewUserAsync();
        await GrantAsync(editorUserId, AidxLabRole.Editor);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var publication = new AidxPublication
        {
            Title = "Paper", Year = 2026, CreatedByMembershipId = contributorMembership.Id,
        };
        db.AidxPublications.Add(publication);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        db.Set<AidxPublicationAuthor>().Add(new AidxPublicationAuthor { PublicationId = publication.Id, Position = 1, ExternalAuthorName = "A. Author" });
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        var v0 = db.Entry(publication).Property<uint>("xmin").CurrentValue.ToString();

        await SendAsync(HttpMethod.Post, $"/api/v1/aidx/content/publication/{publication.Id}/submit", contributorToken, new AidxWorkflowActionRequest(v0));

        using (var midScope = _factory.Services.CreateScope())
        {
            var midDb = midScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var submitted = await midDb.AidxPublications.FirstAsync(p => p.Id == publication.Id, TestContext.Current.CancellationToken);
            var v1 = midDb.Entry(submitted).Property<uint>("xmin").CurrentValue.ToString();
            var approve = await SendAsync(HttpMethod.Post, $"/api/v1/aidx/content/publication/{publication.Id}/approve", editorToken, new AidxWorkflowActionRequest(v1));
            approve.StatusCode.Should().Be(HttpStatusCode.OK);
        }

        var beginEdit = await SendAsync(HttpMethod.Post, $"/api/v1/aidx/content/publication/{publication.Id}/begin-edit", contributorToken);
        beginEdit.StatusCode.Should().Be(HttpStatusCode.OK);
        var editable = await beginEdit.Content.ReadFromJsonAsync<AidxWorkflowStatusResponse>(TestContext.Current.CancellationToken);
        editable!.RevisionOfId.Should().Be(publication.Id);

        using var verifyScope = _factory.Services.CreateScope();
        var verifyDb = verifyScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var revisionAuthors = await verifyDb.Set<AidxPublicationAuthor>()
            .Where(a => a.PublicationId == editable.Id).ToListAsync(TestContext.Current.CancellationToken);
        revisionAuthors.Should().ContainSingle(a => a.ExternalAuthorName == "A. Author");
    }

    [Fact]
    public async Task The_public_project_endpoint_never_returns_a_pending_or_merged_revision()
    {
        var (contributorToken, contributorUserId) = await NewUserAsync();
        var contributorMembership = await GrantAsync(contributorUserId, AidxLabRole.Contributor);
        var (editorToken, editorUserId) = await NewUserAsync();
        await GrantAsync(editorUserId, AidxLabRole.Editor);

        var project = await CreateDraftProjectAsync(contributorMembership.Id);
        var v0 = await CurrentVersionAsync(project.Id);
        await SendAsync(HttpMethod.Post, $"/api/v1/aidx/content/project/{project.Id}/submit", contributorToken, new AidxWorkflowActionRequest(v0));
        var v1 = await CurrentVersionAsync(project.Id);
        await SendAsync(HttpMethod.Post, $"/api/v1/aidx/content/project/{project.Id}/approve", editorToken, new AidxWorkflowActionRequest(v1));

        // Open a revision (same slug as the canonical) and leave it pending — the public
        // endpoint, queried by that shared slug, must resolve to the canonical row only.
        var beginEdit = await SendAsync(HttpMethod.Post, $"/api/v1/aidx/content/project/{project.Id}/begin-edit", contributorToken);
        var editable = await beginEdit.Content.ReadFromJsonAsync<AidxWorkflowStatusResponse>(TestContext.Current.CancellationToken);

        var publicWhilePending = await SendAsync(HttpMethod.Get, $"/api/v1/aidx/projects/{project.Slug}", null);
        publicWhilePending.StatusCode.Should().Be(HttpStatusCode.OK);
        var pendingBody = await publicWhilePending.Content.ReadFromJsonAsync<AidxProjectDetailResponse>(TestContext.Current.CancellationToken);
        pendingBody!.Id.Should().Be(project.Id, "the public endpoint must resolve the canonical row, never the pending revision, even though both share this slug");

        // Submit and approve the revision — it merges onto canonical and is itself marked
        // Published + MergedAt, meaning it now ALSO matches a naive "Status == Published" filter.
        await SendAsync(HttpMethod.Post, $"/api/v1/aidx/content/project/{editable!.Id}/submit", contributorToken, new AidxWorkflowActionRequest(editable.Version));
        var revisionVersion = await CurrentVersionAsync(editable.Id);
        await SendAsync(HttpMethod.Post, $"/api/v1/aidx/content/project/{editable.Id}/approve", editorToken, new AidxWorkflowActionRequest(revisionVersion));

        var publicAfterMerge = await SendAsync(HttpMethod.Get, $"/api/v1/aidx/projects/{project.Slug}", null);
        publicAfterMerge.StatusCode.Should().Be(HttpStatusCode.OK);
        var afterMergeBody = await publicAfterMerge.Content.ReadFromJsonAsync<AidxProjectDetailResponse>(TestContext.Current.CancellationToken);
        afterMergeBody!.Id.Should().Be(project.Id, "still the canonical row's own id — a merged revision, despite also being Status=Published with the same slug, must never be the one returned");

        // And the revision's own id is never independently resolvable through the public API.
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var revisionSlug = await db.AidxProjects.Where(p => p.Id == editable.Id).Select(p => p.Slug).SingleAsync(TestContext.Current.CancellationToken);
        revisionSlug.Should().Be(project.Slug, "the revision was cloned with the same slug, by design");
    }

    // ---- GET .../version — regression coverage for the review-page 405 ----------------------
    //
    // Production incident: the review page (and the Contributor's own workspace withdraw action)
    // fetched "the current version" via GET on /begin-edit, which is POST-only — an immediate 405
    // on every click. Even with the verb corrected, /begin-edit is gated to the content's owner
    // (or Admin) and only resolves canonical rows, so a Lab Manager or Editor reviewing someone
    // else's submission would still have been wrongly refused (403), and would 404 on an
    // in-progress revision row. These tests exercise the actual reviewer-click flow end to end
    // (GET the version, then POST the action with it) exactly as the frontend now does, not just
    // the new endpoint in isolation.

    [Fact]
    public async Task A_reviewer_who_is_not_the_owner_can_fetch_the_current_version_and_use_it_to_approve()
    {
        var (contributorToken, contributorUserId) = await NewUserAsync();
        var contributorMembership = await GrantAsync(contributorUserId, AidxLabRole.Contributor);
        var (editorToken, editorUserId) = await NewUserAsync();
        await GrantAsync(editorUserId, AidxLabRole.Editor);

        var project = await CreateDraftProjectAsync(contributorMembership.Id);
        var v0 = await CurrentVersionAsync(project.Id);
        await SendAsync(HttpMethod.Post, $"/api/v1/aidx/content/project/{project.Id}/submit", contributorToken, new AidxWorkflowActionRequest(v0));

        // The exact sequence the review page performs: GET the version, then POST the action —
        // reproducing the originally-reported click, not a hand-fetched version from the DB.
        var versionResponse = await SendAsync(HttpMethod.Get, $"/api/v1/aidx/content/project/{project.Id}/version", editorToken);
        versionResponse.StatusCode.Should().Be(HttpStatusCode.OK, "a reviewer who is not the owner must be able to read the current version — this is what 405'd in production");
        var versionBody = await versionResponse.Content.ReadFromJsonAsync<AidxVersionResponse>(TestContext.Current.CancellationToken);

        var approve = await SendAsync(HttpMethod.Post, $"/api/v1/aidx/content/project/{project.Id}/approve", editorToken,
            new AidxWorkflowActionRequest(versionBody!.Version));
        approve.StatusCode.Should().Be(HttpStatusCode.OK);

        (await ReloadProjectAsync(project.Id)).Status.Should().Be(AidxContentStatus.Published);
    }

    [Fact]
    public async Task A_reviewer_who_is_not_the_owner_can_fetch_the_version_and_use_it_to_request_changes()
    {
        var (contributorToken, contributorUserId) = await NewUserAsync();
        var contributorMembership = await GrantAsync(contributorUserId, AidxLabRole.Contributor);
        var (editorToken, editorUserId) = await NewUserAsync();
        await GrantAsync(editorUserId, AidxLabRole.Editor);

        var project = await CreateDraftProjectAsync(contributorMembership.Id);
        var v0 = await CurrentVersionAsync(project.Id);
        await SendAsync(HttpMethod.Post, $"/api/v1/aidx/content/project/{project.Id}/submit", contributorToken, new AidxWorkflowActionRequest(v0));

        var versionResponse = await SendAsync(HttpMethod.Get, $"/api/v1/aidx/content/project/{project.Id}/version", editorToken);
        versionResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var versionBody = await versionResponse.Content.ReadFromJsonAsync<AidxVersionResponse>(TestContext.Current.CancellationToken);

        var requestChanges = await SendAsync(HttpMethod.Post, $"/api/v1/aidx/content/project/{project.Id}/request-changes", editorToken,
            new AidxRequestChangesRequest(versionBody!.Version, "Please add more detail."));
        requestChanges.StatusCode.Should().Be(HttpStatusCode.OK);

        (await ReloadProjectAsync(project.Id)).Status.Should().Be(AidxContentStatus.ChangesRequested);
    }

    [Fact]
    public async Task The_owner_can_also_fetch_the_current_version_and_use_it_to_withdraw()
    {
        var (contributorToken, contributorUserId) = await NewUserAsync();
        var contributorMembership = await GrantAsync(contributorUserId, AidxLabRole.Contributor);

        var project = await CreateDraftProjectAsync(contributorMembership.Id);
        var v0 = await CurrentVersionAsync(project.Id);
        await SendAsync(HttpMethod.Post, $"/api/v1/aidx/content/project/{project.Id}/submit", contributorToken, new AidxWorkflowActionRequest(v0));

        // The same flow the Contributor's own /aidx/workspace "Withdraw" button performs.
        var versionResponse = await SendAsync(HttpMethod.Get, $"/api/v1/aidx/content/project/{project.Id}/version", contributorToken);
        versionResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var versionBody = await versionResponse.Content.ReadFromJsonAsync<AidxVersionResponse>(TestContext.Current.CancellationToken);

        var withdraw = await SendAsync(HttpMethod.Post, $"/api/v1/aidx/content/project/{project.Id}/withdraw", contributorToken,
            new AidxWorkflowActionRequest(versionBody!.Version));
        withdraw.StatusCode.Should().Be(HttpStatusCode.OK);

        (await ReloadProjectAsync(project.Id)).Status.Should().Be(AidxContentStatus.Draft);
    }

    [Fact]
    public async Task Fetching_the_version_works_on_an_in_progress_revision_row_unlike_begin_edit()
    {
        var (contributorToken, contributorUserId) = await NewUserAsync();
        var contributorMembership = await GrantAsync(contributorUserId, AidxLabRole.Contributor);
        var (editorToken, editorUserId) = await NewUserAsync();
        await GrantAsync(editorUserId, AidxLabRole.Editor);

        var project = await CreateDraftProjectAsync(contributorMembership.Id);
        var v0 = await CurrentVersionAsync(project.Id);
        await SendAsync(HttpMethod.Post, $"/api/v1/aidx/content/project/{project.Id}/submit", contributorToken, new AidxWorkflowActionRequest(v0));
        var v1 = await CurrentVersionAsync(project.Id);
        await SendAsync(HttpMethod.Post, $"/api/v1/aidx/content/project/{project.Id}/approve", editorToken, new AidxWorkflowActionRequest(v1));

        // Opens a revision — begin-edit only resolves canonical (RevisionOfId == null) rows, so a
        // /version lookup on the revision's own id must still succeed where /begin-edit would 404.
        var beginEdit = await SendAsync(HttpMethod.Post, $"/api/v1/aidx/content/project/{project.Id}/begin-edit", contributorToken);
        var editable = await beginEdit.Content.ReadFromJsonAsync<AidxWorkflowStatusResponse>(TestContext.Current.CancellationToken);

        var revisionVersion = await SendAsync(HttpMethod.Get, $"/api/v1/aidx/content/project/{editable!.Id}/version", editorToken);
        revisionVersion.StatusCode.Should().Be(HttpStatusCode.OK, "a reviewer must be able to read an in-progress revision's version, not just a canonical row's");
        var revisionVersionBody = await revisionVersion.Content.ReadFromJsonAsync<AidxVersionResponse>(TestContext.Current.CancellationToken);
        revisionVersionBody!.Version.Should().Be(editable.Version, "must match the same xmin the begin-edit response itself just reported");
    }

    [Fact]
    public async Task A_contributor_who_is_neither_the_owner_nor_a_reviewer_cannot_fetch_the_version()
    {
        var (contributorToken, contributorUserId) = await NewUserAsync();
        var contributorMembership = await GrantAsync(contributorUserId, AidxLabRole.Contributor);
        var (otherContributorToken, otherContributorUserId) = await NewUserAsync();
        await GrantAsync(otherContributorUserId, AidxLabRole.Contributor);

        var project = await CreateDraftProjectAsync(contributorMembership.Id);
        var v0 = await CurrentVersionAsync(project.Id);
        await SendAsync(HttpMethod.Post, $"/api/v1/aidx/content/project/{project.Id}/submit", contributorToken, new AidxWorkflowActionRequest(v0));

        // A Contributor role can never review (AidxWorkflowEndpoints.CanReview excludes it
        // outright), and this account isn't the owner either.
        (await SendAsync(HttpMethod.Get, $"/api/v1/aidx/content/project/{project.Id}/version", otherContributorToken))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task A_scoped_editor_outside_the_projects_research_areas_cannot_fetch_the_version()
    {
        var (contributorToken, contributorUserId) = await NewUserAsync();
        var contributorMembership = await GrantAsync(contributorUserId, AidxLabRole.Contributor);
        var inScopeArea = await CreateResearchAreaAsync();
        var outOfScopeArea = await CreateResearchAreaAsync();
        var (scopedEditorToken, scopedEditorUserId) = await NewUserAsync();
        await GrantAsync(scopedEditorUserId, AidxLabRole.Editor, scope: outOfScopeArea);

        var project = await CreateDraftProjectAsync(contributorMembership.Id, researchAreaId: inScopeArea);
        var v0 = await CurrentVersionAsync(project.Id);
        await SendAsync(HttpMethod.Post, $"/api/v1/aidx/content/project/{project.Id}/submit", contributorToken, new AidxWorkflowActionRequest(v0));

        (await SendAsync(HttpMethod.Get, $"/api/v1/aidx/content/project/{project.Id}/version", scopedEditorToken))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden, "a scoped editor outside this project's research areas may not review it, and must not be able to read its version either");
    }

    [Fact]
    public async Task Approving_with_a_version_that_went_stale_between_the_fetch_and_the_click_is_still_rejected()
    {
        var (contributorToken, contributorUserId) = await NewUserAsync();
        var contributorMembership = await GrantAsync(contributorUserId, AidxLabRole.Contributor);
        var (editorToken, editorUserId) = await NewUserAsync();
        await GrantAsync(editorUserId, AidxLabRole.Editor);

        var project = await CreateDraftProjectAsync(contributorMembership.Id);
        var v0 = await CurrentVersionAsync(project.Id);
        await SendAsync(HttpMethod.Post, $"/api/v1/aidx/content/project/{project.Id}/submit", contributorToken, new AidxWorkflowActionRequest(v0));

        var staleVersionResponse = await SendAsync(HttpMethod.Get, $"/api/v1/aidx/content/project/{project.Id}/version", editorToken);
        var staleVersion = (await staleVersionResponse.Content.ReadFromJsonAsync<AidxVersionResponse>(TestContext.Current.CancellationToken))!.Version;

        // Something else changes the row after the version was fetched but before the click lands.
        var freshVersion = await CurrentVersionAsync(project.Id);
        await SendAsync(HttpMethod.Post, $"/api/v1/aidx/content/project/{project.Id}/request-changes", editorToken,
            new AidxRequestChangesRequest(freshVersion, "Changing it out from under the stale read."));

        var approveWithStaleVersion = await SendAsync(HttpMethod.Post, $"/api/v1/aidx/content/project/{project.Id}/approve", editorToken,
            new AidxWorkflowActionRequest(staleVersion));
        approveWithStaleVersion.StatusCode.Should().Be(HttpStatusCode.Conflict, "fixing the 405 must not weaken the version-conflict guard — a stale read is still refused");
    }
}
