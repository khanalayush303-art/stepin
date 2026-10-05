using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using StepIn.Api.Endpoints;
using StepIn.Domain.Aidx;
using StepIn.Domain.Jobs;
using StepIn.Domain.Common;
using StepIn.Infrastructure.Persistence;

namespace StepIn.Api.Tests;

/// <summary>
/// AIDX Lab backend behaviour against the real auth → user-sync → role-policy pipeline
/// and a real PostgreSQL container (see <see cref="AuthApiFactory"/>). Covers public
/// visibility, the publishing lifecycle, admin authorization, relationships and cascades,
/// and the career-listing regression that keeps research opportunities out of StepIn jobs.
/// </summary>
public sealed class AidxEndpointsTests(AuthApiFactory factory) : IClassFixture<AuthApiFactory>
{
    private readonly AuthApiFactory _factory = factory;

    private HttpClient CreateClient() => _factory.CreateClient();

    private static string Unique(string prefix) => $"{prefix} {Guid.NewGuid():N}";

    private static HttpRequestMessage BuildRequest(HttpMethod method, string url, string? token, object? body = null)
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

    /// <summary>Signs a user up through the real account-setup endpoint, then optionally promotes them.</summary>
    private async Task<string> IssueTokenAsync(string role, CancellationToken ct)
    {
        using var client = CreateClient();
        var clerkUserId = $"user_{Guid.NewGuid():N}";
        var token = AuthApiFactory.IssueToken(clerkUserId, $"{Guid.NewGuid()}@example.com");

        using var setup = BuildRequest(HttpMethod.Post, "/api/v1/auth/account-setup", token, new AccountSetupRequest(role));
        (await client.SendAsync(setup, ct)).EnsureSuccessStatusCode();

        return token;
    }

    /// <summary>
    /// Admin is never client-settable, so the test promotes an account directly in the
    /// database. This mirrors the documented <c>psql</c> promotion step for real admins.
    /// </summary>
    private async Task<string> IssueAdminTokenAsync(CancellationToken ct)
    {
        var clerkUserId = $"admin_{Guid.NewGuid():N}";
        var token = AuthApiFactory.IssueToken(clerkUserId, $"{Guid.NewGuid()}@example.com");

        using (var client = CreateClient())
        {
            using var setup = BuildRequest(HttpMethod.Post, "/api/v1/auth/account-setup", token, new AccountSetupRequest("Applicant"));
            (await client.SendAsync(setup, ct)).EnsureSuccessStatusCode();
        }

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        await db.Users
            .Where(u => u.ClerkUserId == clerkUserId)
            .ExecuteUpdateAsync(s => s.SetProperty(u => u.Role, UserRole.Admin), ct);

        return token;
    }

    private async Task<HttpResponseMessage> SendAsync(HttpMethod method, string url, string? token, object? body, CancellationToken ct)
    {
        using var client = CreateClient();
        using var request = BuildRequest(method, url, token, body);
        return await client.SendAsync(request, ct);
    }

    private async Task<Guid> CreateResearchAreaAsync(string adminToken, CancellationToken ct)
    {
        var name = Unique("Research Area");
        var response = await SendAsync(HttpMethod.Post, "/api/v1/admin/aidx/research", adminToken,
            new AidxResearchAreaRequest(name, null, "Test area", 0), ct);
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        return (await response.Content.ReadFromJsonAsync<AidxIdResponse>(ct))!.Id;
    }

    private async Task<AidxIdResponse> CreateResearcherAsync(string adminToken, string displayName, bool published, CancellationToken ct)
    {
        var response = await SendAsync(HttpMethod.Post, "/api/v1/admin/aidx/people", adminToken,
            new AidxResearcherRequest(displayName, null, "PhdStudent", "PhD candidate", null, null, null, null, null, published), ct);
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        return (await response.Content.ReadFromJsonAsync<AidxIdResponse>(ct))!;
    }

    private static AidxProjectRequest ProjectRequest(string title, IReadOnlyList<Guid>? areaIds = null, IReadOnlyList<AidxProjectResearcherInput>? researchers = null) => new(
        title,
        null,
        "A short summary of the project.",
        "A longer description of the project.",
        new DateOnly(2026, 1, 1),
        new DateOnly(2026, 12, 31),
        "https://example.org/project",
        false,
        areaIds,
        ["Python", "Pandas"],
        researchers);

    // ---------------------------------------------------------------- authorization ---

    [Fact]
    public async Task Admin_write_routes_require_authentication_and_the_admin_role()
    {
        var applicantToken = await IssueTokenAsync("Applicant", TestContext.Current.CancellationToken);
        var body = ProjectRequest(Unique("Auth Check"));

        (await SendAsync(HttpMethod.Post, "/api/v1/admin/aidx/projects", null, body, TestContext.Current.CancellationToken))
            .StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        (await SendAsync(HttpMethod.Post, "/api/v1/admin/aidx/projects", applicantToken, body, TestContext.Current.CancellationToken))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden);

        var adminToken = await IssueAdminTokenAsync(TestContext.Current.CancellationToken);
        (await SendAsync(HttpMethod.Post, "/api/v1/admin/aidx/projects", adminToken, body, TestContext.Current.CancellationToken))
            .StatusCode.Should().Be(HttpStatusCode.Created);
    }

    [Fact]
    public async Task Admin_publish_and_archive_routes_are_protected_too()
    {
        var adminToken = await IssueAdminTokenAsync(TestContext.Current.CancellationToken);
        var created = await SendAsync(HttpMethod.Post, "/api/v1/admin/aidx/projects", adminToken, ProjectRequest(Unique("Protected")), TestContext.Current.CancellationToken);
        var id = (await created.Content.ReadFromJsonAsync<AidxIdResponse>(TestContext.Current.CancellationToken))!.Id;

        var applicantToken = await IssueTokenAsync("Applicant", TestContext.Current.CancellationToken);
        (await SendAsync(HttpMethod.Post, $"/api/v1/admin/aidx/projects/{id}/publish", applicantToken, null, TestContext.Current.CancellationToken))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await SendAsync(HttpMethod.Post, $"/api/v1/admin/aidx/projects/{id}/publish", null, null, TestContext.Current.CancellationToken))
            .StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // ---------------------------------------------------------- publishing lifecycle ---

    [Fact]
    public async Task Draft_projects_are_invisible_to_the_public_and_publish_then_archive_follows_the_rules()
    {
        var ct = TestContext.Current.CancellationToken;
        var adminToken = await IssueAdminTokenAsync(ct);
        var title = Unique("Lifecycle Project");

        var created = await SendAsync(HttpMethod.Post, "/api/v1/admin/aidx/projects", adminToken, ProjectRequest(title), ct);
        created.StatusCode.Should().Be(HttpStatusCode.Created);
        var created0 = (await created.Content.ReadFromJsonAsync<AidxIdResponse>(ct))!;
        created0.Slug.Should().Be(AidxSlug.FromText(title));
        created0.Status.Should().Be("Draft");

        (await SendAsync(HttpMethod.Get, $"/api/v1/aidx/projects/{created0.Slug}", null, null, ct))
            .StatusCode.Should().Be(HttpStatusCode.NotFound);

        var publish = await SendAsync(HttpMethod.Post, $"/api/v1/admin/aidx/projects/{created0.Id}/publish", adminToken, null, ct);
        publish.StatusCode.Should().Be(HttpStatusCode.OK);

        (await SendAsync(HttpMethod.Get, $"/api/v1/aidx/projects/{created0.Slug}", null, null, ct))
            .StatusCode.Should().Be(HttpStatusCode.OK);

        (await SendAsync(HttpMethod.Post, $"/api/v1/admin/aidx/projects/{created0.Id}/publish", adminToken, null, ct))
            .StatusCode.Should().Be(HttpStatusCode.Conflict, "publishing an already published project is a conflict");

        var archive = await SendAsync(HttpMethod.Post, $"/api/v1/admin/aidx/projects/{created0.Id}/archive", adminToken, null, ct);
        archive.StatusCode.Should().Be(HttpStatusCode.OK);

        (await SendAsync(HttpMethod.Get, $"/api/v1/aidx/projects/{created0.Slug}", null, null, ct))
            .StatusCode.Should().Be(HttpStatusCode.NotFound, "archived content is hidden from the public");

        (await SendAsync(HttpMethod.Post, $"/api/v1/admin/aidx/projects/{created0.Id}/archive", adminToken, null, ct))
            .StatusCode.Should().Be(HttpStatusCode.Conflict, "only a published project can be archived");
    }

    [Fact]
    public async Task Archiving_a_draft_is_a_conflict()
    {
        var ct = TestContext.Current.CancellationToken;
        var adminToken = await IssueAdminTokenAsync(ct);
        var created = await SendAsync(HttpMethod.Post, "/api/v1/admin/aidx/news", adminToken,
            new AidxNewsRequest(Unique("Draft News"), null, "Summary.", "Body.", null), ct);
        var id = (await created.Content.ReadFromJsonAsync<AidxIdResponse>(ct))!.Id;

        (await SendAsync(HttpMethod.Post, $"/api/v1/admin/aidx/news/{id}/archive", adminToken, null, ct))
            .StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Duplicate_slugs_are_rejected_with_conflict_and_malformed_slugs_with_validation_error()
    {
        var ct = TestContext.Current.CancellationToken;
        var adminToken = await IssueAdminTokenAsync(ct);
        var title = Unique("Shared Title");

        (await SendAsync(HttpMethod.Post, "/api/v1/admin/aidx/events", adminToken,
            new AidxEventRequest(title, null, "First event.", DateTimeOffset.UtcNow.AddDays(10), null, null, null, null), ct))
            .StatusCode.Should().Be(HttpStatusCode.Created);

        (await SendAsync(HttpMethod.Post, "/api/v1/admin/aidx/events", adminToken,
            new AidxEventRequest(title, null, "Second event.", DateTimeOffset.UtcNow.AddDays(11), null, null, null, null), ct))
            .StatusCode.Should().Be(HttpStatusCode.Conflict);

        var malformed = await SendAsync(HttpMethod.Post, "/api/v1/admin/aidx/events", adminToken,
            new AidxEventRequest(Unique("Bad Slug"), "Not A Slug!", "Desc.", DateTimeOffset.UtcNow.AddDays(12), null, null, null, null), ct);
        malformed.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    // -------------------------------------------------------------- public listings ---

    [Fact]
    public async Task Public_listings_reject_invalid_paging()
    {
        var ct = TestContext.Current.CancellationToken;

        (await SendAsync(HttpMethod.Get, "/api/v1/aidx/projects", null, null, ct))
            .StatusCode.Should().Be(HttpStatusCode.OK, "paging parameters are optional and default to page 1");
        (await SendAsync(HttpMethod.Get, "/api/v1/aidx/projects?pageSize=0", null, null, ct))
            .StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await SendAsync(HttpMethod.Get, "/api/v1/aidx/news?page=0", null, null, ct))
            .StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await SendAsync(HttpMethod.Get, "/api/v1/aidx/people?pageSize=51", null, null, ct))
            .StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Unpublished_researchers_are_hidden_and_published_ones_are_visible()
    {
        var ct = TestContext.Current.CancellationToken;
        var adminToken = await IssueAdminTokenAsync(ct);

        var hidden = await CreateResearcherAsync(adminToken, Unique("Hidden Researcher"), published: false, ct);
        var shown = await CreateResearcherAsync(adminToken, Unique("Visible Researcher"), published: true, ct);

        (await SendAsync(HttpMethod.Get, $"/api/v1/aidx/people/{hidden.Slug}", null, null, ct))
            .StatusCode.Should().Be(HttpStatusCode.NotFound);

        (await SendAsync(HttpMethod.Get, $"/api/v1/aidx/people/{shown.Slug}", null, null, ct))
            .StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Project_detail_shows_research_areas_technologies_and_only_published_researchers()
    {
        var ct = TestContext.Current.CancellationToken;
        var adminToken = await IssueAdminTokenAsync(ct);

        var areaId = await CreateResearchAreaAsync(adminToken, ct);
        var publishedResearcher = await CreateResearcherAsync(adminToken, Unique("Published Member"), published: true, ct);
        var unpublishedResearcher = await CreateResearcherAsync(adminToken, Unique("Unpublished Member"), published: false, ct);

        var created = await SendAsync(HttpMethod.Post, "/api/v1/admin/aidx/projects", adminToken,
            ProjectRequest(
                Unique("Relationship Project"),
                [areaId],
                [new AidxProjectResearcherInput(publishedResearcher.Id, "Lead"), new AidxProjectResearcherInput(unpublishedResearcher.Id, null)]),
            ct);
        created.StatusCode.Should().Be(HttpStatusCode.Created);
        var project = (await created.Content.ReadFromJsonAsync<AidxIdResponse>(ct))!;

        (await SendAsync(HttpMethod.Post, $"/api/v1/admin/aidx/projects/{project.Id}/publish", adminToken, null, ct))
            .StatusCode.Should().Be(HttpStatusCode.OK);

        var detail = await SendAsync(HttpMethod.Get, $"/api/v1/aidx/projects/{project.Slug}", null, null, ct);
        detail.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = (await detail.Content.ReadFromJsonAsync<AidxProjectDetailResponse>(ct))!;

        body.Technologies.Should().BeEquivalentTo(["Pandas", "Python"]);
        body.Researchers.Should().ContainSingle(r => r.Id == publishedResearcher.Id && r.Role == "Lead");
        body.Researchers.Should().NotContain(r => r.Id == unpublishedResearcher.Id);
    }

    [Fact]
    public async Task Updating_a_project_replaces_its_relationships_without_key_conflicts()
    {
        var ct = TestContext.Current.CancellationToken;
        var adminToken = await IssueAdminTokenAsync(ct);

        var firstArea = await CreateResearchAreaAsync(adminToken, ct);
        var secondArea = await CreateResearchAreaAsync(adminToken, ct);
        var researcher = await CreateResearcherAsync(adminToken, Unique("Swap Member"), published: true, ct);

        var created = await SendAsync(HttpMethod.Post, "/api/v1/admin/aidx/projects", adminToken,
            ProjectRequest(Unique("Swap Project"), [firstArea], [new AidxProjectResearcherInput(researcher.Id, "Old role")]), ct);
        var project = (await created.Content.ReadFromJsonAsync<AidxIdResponse>(ct))!;

        var update = await SendAsync(HttpMethod.Put, $"/api/v1/admin/aidx/projects/{project.Id}", adminToken,
            ProjectRequest(Unique("Swap Project Renamed"), [secondArea], [new AidxProjectResearcherInput(researcher.Id, "New role")]), ct);
        update.StatusCode.Should().Be(HttpStatusCode.OK);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var links = await db.AidxProjectResearchAreas.Where(l => l.ProjectId == project.Id).ToListAsync(ct);
        links.Should().ContainSingle().Which.ResearchAreaId.Should().Be(secondArea);
        (await db.AidxProjectResearchers.SingleAsync(l => l.ProjectId == project.Id, ct)).Role.Should().Be("New role");
    }

    [Fact]
    public async Task Publication_authors_are_listed_in_position_order_with_researcher_or_external_names()
    {
        var ct = TestContext.Current.CancellationToken;
        var adminToken = await IssueAdminTokenAsync(ct);
        var researcher = await CreateResearcherAsync(adminToken, Unique("Author Member"), published: true, ct);

        var year = Random.Shared.Next(1950, 2090);
        var title = Unique("Authored Paper");
        var response = await SendAsync(HttpMethod.Post, "/api/v1/admin/aidx/publications", adminToken,
            new AidxPublicationRequest(
                title,
                "An abstract.",
                "JournalArticle",
                "Test Journal",
                year,
                null,
                null,
                true,
                [new AidxAuthorInput(researcher.Id, null), new AidxAuthorInput(null, "External Co-author")]),
            ct);
        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var list = await SendAsync(HttpMethod.Get, $"/api/v1/aidx/publications?year={year}&pageSize=50", null, null, ct);
        var page = (await list.Content.ReadFromJsonAsync<AidxPageResponse<AidxPublicationResponse>>(ct))!;

        var researcherName = await GetResearcherNameAsync(researcher.Id, ct);
        page.Items.Should().ContainSingle(p => p.Title == title)
            .Which.Authors.Should().Equal(new[] { researcherName, "External Co-author" });
    }

    private async Task<string> GetResearcherNameAsync(Guid researcherId, CancellationToken ct)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        return await db.AidxResearchers.Where(r => r.Id == researcherId).Select(r => r.DisplayName).SingleAsync(ct);
    }

    // -------------------------------------------------------------- cascades / FKs ---

    [Fact]
    public async Task Deleting_a_project_cascades_to_its_join_rows_but_not_to_the_researcher()
    {
        var ct = TestContext.Current.CancellationToken;
        var adminToken = await IssueAdminTokenAsync(ct);
        var areaId = await CreateResearchAreaAsync(adminToken, ct);
        var researcher = await CreateResearcherAsync(adminToken, Unique("Cascade Member"), published: true, ct);

        var created = await SendAsync(HttpMethod.Post, "/api/v1/admin/aidx/projects", adminToken,
            ProjectRequest(Unique("Cascade Project"), [areaId], [new AidxProjectResearcherInput(researcher.Id, null)]), ct);
        var project = (await created.Content.ReadFromJsonAsync<AidxIdResponse>(ct))!;

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        await db.AidxProjects.Where(p => p.Id == project.Id).ExecuteDeleteAsync(ct);

        (await db.AidxProjectResearchAreas.CountAsync(l => l.ProjectId == project.Id, ct)).Should().Be(0);
        (await db.AidxProjectResearchers.CountAsync(l => l.ProjectId == project.Id, ct)).Should().Be(0);
        (await db.AidxProjectTechnologies.CountAsync(t => t.ProjectId == project.Id, ct)).Should().Be(0);
        (await db.AidxResearchers.CountAsync(r => r.Id == researcher.Id, ct)).Should().Be(1, "a project never owns its researchers");
        (await db.AidxResearchAreas.CountAsync(a => a.Id == areaId, ct)).Should().Be(1, "a project never owns the research area catalogue");
    }

    [Fact]
    public async Task A_researcher_with_authorship_cannot_be_deleted_silently()
    {
        var ct = TestContext.Current.CancellationToken;
        var adminToken = await IssueAdminTokenAsync(ct);
        var researcher = await CreateResearcherAsync(adminToken, Unique("Restricted Author"), published: true, ct);

        (await SendAsync(HttpMethod.Post, "/api/v1/admin/aidx/publications", adminToken,
            new AidxPublicationRequest(Unique("Restricted Paper"), null, "Report", null, 2024, null, null, false,
                [new AidxAuthorInput(researcher.Id, null)]), ct)).StatusCode.Should().Be(HttpStatusCode.Created);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var act = () => db.AidxResearchers.Where(r => r.Id == researcher.Id).ExecuteDeleteAsync(ct);
        await act.Should().ThrowAsync<Exception>("authorship history must block deletion of the researcher");
    }

    // ------------------------------------------------ career regression (Research) ---

    [Fact]
    public async Task Research_jobs_are_excluded_from_the_career_listing_but_served_by_job_detail()
    {
        var ct = TestContext.Current.CancellationToken;
        var recruiterToken = await IssueRecruiterWithCompanyTokenAsync(ct);
        using var client = CreateClient();

        var careerJob = await CreateAndPublishJobAsync(client, recruiterToken, ct);
        var researchJob = await CreateAndPublishJobAsync(client, recruiterToken, ct);

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            await db.Jobs
                .Where(j => j.Id == researchJob)
                .ExecuteUpdateAsync(s => s.SetProperty(j => j.Category, JobCategory.Research), ct);
        }

        var list = (await client.GetFromJsonAsync<List<PublicJobSummaryResponse>>("/api/v1/jobs", ct))!;
        list.Select(j => j.Id).Should().Contain(careerJob, "a published career job remains listed");
        list.Select(j => j.Id).Should().NotContain(researchJob, "research opportunities are AIDX content, not career listings");

        (await client.GetAsync($"/api/v1/jobs/{researchJob}", ct)).StatusCode.Should().Be(HttpStatusCode.OK, "a published research job is served by the shared job detail endpoint");
        (await client.GetAsync($"/api/v1/jobs/{careerJob}", ct)).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    private async Task<string> IssueRecruiterWithCompanyTokenAsync(CancellationToken ct)
    {
        using var client = CreateClient();
        var token = AuthApiFactory.IssueToken($"user_{Guid.NewGuid():N}", $"{Guid.NewGuid()}@example.com");

        using var setup = BuildRequest(HttpMethod.Post, "/api/v1/auth/account-setup", token, new AccountSetupRequest("Recruiter"));
        (await client.SendAsync(setup, ct)).EnsureSuccessStatusCode();

        using var profile = BuildRequest(
            HttpMethod.Put,
            "/api/v1/profile/recruiter",
            token,
            new UpdateRecruiterProfileRequest(null, null, null, new CompanyDto(null, Unique("AIDX Test Co"), null, null, null, null, null)));
        (await client.SendAsync(profile, ct)).EnsureSuccessStatusCode();

        return token;
    }

    private static async Task<Guid> CreateAndPublishJobAsync(HttpClient client, string recruiterToken, CancellationToken ct)
    {
        using var create = BuildRequest(
            HttpMethod.Post,
            "/api/v1/recruiter/jobs",
            recruiterToken,
            new CreateJobRequest(Unique("Engineer"), "Build things.", "FullTime", "Hybrid", "Sydney, NSW", null, ["C#"]));
        var created = await client.SendAsync(create, ct);
        created.StatusCode.Should().Be(HttpStatusCode.Created);
        var job = (await created.Content.ReadFromJsonAsync<JobResponse>(ct))!;

        using var publish = BuildRequest(HttpMethod.Post, $"/api/v1/recruiter/jobs/{job.Id}/publish", recruiterToken);
        (await client.SendAsync(publish, ct)).EnsureSuccessStatusCode();

        return job.Id;
    }
}
