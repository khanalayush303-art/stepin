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
}
