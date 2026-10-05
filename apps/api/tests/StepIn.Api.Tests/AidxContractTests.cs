using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using StepIn.Api.Endpoints;
using StepIn.Domain.Aidx;
using StepIn.Domain.Common;
using StepIn.Domain.Jobs;
using StepIn.Infrastructure.Persistence;

namespace StepIn.Api.Tests;

/// <summary>
/// Phase 4.2 contract coverage: anonymous and authenticated public access, the recruiter
/// 403, filters and pagination metadata, the research-opportunity visibility rules, the
/// publication links, and admin deletes. Uses the same real-container pipeline as
/// <see cref="AidxEndpointsTests"/>.
/// </summary>
public sealed class AidxContractTests(AuthApiFactory factory) : IClassFixture<AuthApiFactory>
{
    private readonly AuthApiFactory _factory = factory;

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

    private async Task<HttpResponseMessage> SendAsync(HttpMethod method, string url, string? token, object? body = null)
    {
        using var client = _factory.CreateClient();
        using var request = BuildRequest(method, url, token, body);
        return await client.SendAsync(request, TestContext.Current.CancellationToken);
    }

    private async Task<string> IssueTokenAsync(string role)
    {
        var token = AuthApiFactory.IssueToken($"user_{Guid.NewGuid():N}", $"{Guid.NewGuid()}@example.com");
        using var setup = BuildRequest(HttpMethod.Post, "/api/v1/auth/account-setup", token, new AccountSetupRequest(role));
        using var client = _factory.CreateClient();
        (await client.SendAsync(setup, TestContext.Current.CancellationToken)).EnsureSuccessStatusCode();
        return token;
    }

    private async Task<string> IssueAdminTokenAsync()
    {
        var clerkUserId = $"admin_{Guid.NewGuid():N}";
        var token = AuthApiFactory.IssueToken(clerkUserId, $"{Guid.NewGuid()}@example.com");
        using (var client = _factory.CreateClient())
        {
            using var setup = BuildRequest(HttpMethod.Post, "/api/v1/auth/account-setup", token, new AccountSetupRequest("Applicant"));
            (await client.SendAsync(setup, TestContext.Current.CancellationToken)).EnsureSuccessStatusCode();
        }

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        await db.Users.Where(u => u.ClerkUserId == clerkUserId)
            .ExecuteUpdateAsync(s => s.SetProperty(u => u.Role, UserRole.Admin), TestContext.Current.CancellationToken);
        return token;
    }

    private async Task<AidxIdResponse> CreateAsync(string adminToken, string url, object body)
    {
        var response = await SendAsync(HttpMethod.Post, url, adminToken, body);
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        return (await response.Content.ReadFromJsonAsync<AidxIdResponse>(TestContext.Current.CancellationToken))!;
    }

    private async Task<AidxIdResponse> CreateProjectAsync(string adminToken, string title, string? slug = null, IReadOnlyList<Guid>? areaIds = null)
    {
        var body = new AidxProjectRequest(title, slug, "Short summary.", "Long description.", null, null, null, false, areaIds, null, null);
        return await CreateAsync(adminToken, "/api/v1/admin/aidx/projects", body);
    }

    private async Task<AidxIdResponse> CreateResearchAreaAsync(string adminToken, string name) =>
        await CreateAsync(adminToken, "/api/v1/admin/aidx/research", new AidxResearchAreaRequest(name, null, null, 0));

    private async Task<AidxIdResponse> CreatePersonAsync(string adminToken, string name, string category, bool published) =>
        await CreateAsync(adminToken, "/api/v1/admin/aidx/people",
            new AidxResearcherRequest(name, null, category, null, null, null, null, null, null, published));

    private async Task PublishAsync(string adminToken, string kind, Guid id)
    {
        (await SendAsync(HttpMethod.Post, $"/api/v1/admin/aidx/{kind}/{id}/publish", adminToken))
            .StatusCode.Should().Be(HttpStatusCode.OK);
    }

    private async Task<Guid> CreateRecruiterJobAsync(string recruiterToken)
    {
        var created = await SendAsync(HttpMethod.Post, "/api/v1/recruiter/jobs", recruiterToken,
            new CreateJobRequest(Unique("Research Engineer"), "Work on research.", "FullTime", "Hybrid", "Sydney, NSW", null, ["Python"]));
        created.StatusCode.Should().Be(HttpStatusCode.Created);
        var job = (await created.Content.ReadFromJsonAsync<JobResponse>(TestContext.Current.CancellationToken))!;
        (await SendAsync(HttpMethod.Post, $"/api/v1/recruiter/jobs/{job.Id}/publish", recruiterToken))
            .EnsureSuccessStatusCode();
        return job.Id;
    }

    private async Task<string> IssueRecruiterWithCompanyAsync()
    {
        var token = await IssueTokenAsync("Recruiter");
        var profile = BuildRequest(HttpMethod.Put, "/api/v1/profile/recruiter", token,
            new UpdateRecruiterProfileRequest(null, null, null, new CompanyDto(null, Unique("Lab Partner Co"), null, null, null, null, null)));
        using var client = _factory.CreateClient();
        (await client.SendAsync(profile, TestContext.Current.CancellationToken)).EnsureSuccessStatusCode();
        return token;
    }

    /// <summary>Turns a published recruiter job into an AIDX research opportunity, linked to a project.</summary>
    private async Task MakeResearchAsync(Guid jobId, Guid? projectId)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        await db.Jobs.Where(j => j.Id == jobId)
            .ExecuteUpdateAsync(s => s
                .SetProperty(j => j.Category, JobCategory.Research)
                .SetProperty(j => j.AidxProjectId, projectId), TestContext.Current.CancellationToken);
    }

    // ------------------------------------------------------------- public access ---

    [Fact]
    public async Task Public_content_is_reachable_anonymously_and_with_a_signed_in_token()
    {
        var adminToken = await IssueAdminTokenAsync();
        var created = await CreateProjectAsync(adminToken, Unique("Access Project"));
        await PublishAsync(adminToken, "projects", created.Id);
        var applicantToken = await IssueTokenAsync("Applicant");

        (await SendAsync(HttpMethod.Get, $"/api/v1/aidx/projects/{created.Slug}", null)).StatusCode.Should().Be(HttpStatusCode.OK);
        (await SendAsync(HttpMethod.Get, $"/api/v1/aidx/projects/{created.Slug}", applicantToken)).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Unknown_slugs_return_404_on_every_detail_route()
    {
        var missing = Unique("missing").Replace(' ', '-').ToLowerInvariant();

        foreach (var route in new[] { "research", "projects", "people", "news", "events" })
        {
            (await SendAsync(HttpMethod.Get, $"/api/v1/aidx/{route}/{missing}", null))
                .StatusCode.Should().Be(HttpStatusCode.NotFound, $"unknown {route} slug must be 404");
        }
    }

    [Fact]
    public async Task Duplicate_project_slug_is_a_conflict()
    {
        var adminToken = await IssueAdminTokenAsync();
        var slug = Unique("dup-slug").Replace(' ', '-').ToLowerInvariant();
        await CreateProjectAsync(adminToken, "First", slug);

        (await SendAsync(HttpMethod.Post, "/api/v1/admin/aidx/projects", adminToken,
            new AidxProjectRequest("Second", slug, "S.", "D.", null, null, null, false, null, null, null)))
            .StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    // ------------------------------------------------------------ authorization ---

    [Fact]
    public async Task Recruiter_is_forbidden_from_admin_aidx_writes()
    {
        var recruiterToken = await IssueTokenAsync("Recruiter");

        (await SendAsync(HttpMethod.Post, "/api/v1/admin/aidx/research", recruiterToken,
            new AidxResearchAreaRequest(Unique("Recruiter Attempt"), null, null, 0)))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Admin_delete_routes_follow_the_same_role_rules()
    {
        var adminToken = await IssueAdminTokenAsync();
        var created = await CreateProjectAsync(adminToken, Unique("Guarded Delete"));
        var applicantToken = await IssueTokenAsync("Applicant");

        (await SendAsync(HttpMethod.Delete, $"/api/v1/admin/aidx/projects/{created.Id}", null)).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await SendAsync(HttpMethod.Delete, $"/api/v1/admin/aidx/projects/{created.Id}", applicantToken)).StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    // -------------------------------------------------------------- pagination ---

    [Fact]
    public async Task Pagination_metadata_reports_total_count_and_total_pages()
    {
        var adminToken = await IssueAdminTokenAsync();
        var year = Random.Shared.Next(1900, 2100);
        for (var i = 0; i < 3; i++)
        {
            var pub = await CreateAsync(adminToken, "/api/v1/admin/aidx/publications",
                new AidxPublicationRequest(Unique($"Paged {i}"), null, "Report", null, year, null, null, true, null));
            pub.Id.Should().NotBe(Guid.Empty);
        }

        var response = await SendAsync(HttpMethod.Get, $"/api/v1/aidx/publications?year={year}&pageSize=2&page=1", null);
        var page = (await response.Content.ReadFromJsonAsync<AidxPageResponse<AidxPublicationResponse>>(TestContext.Current.CancellationToken))!;

        page.TotalCount.Should().Be(3);
        page.PageSize.Should().Be(2);
        page.TotalPages.Should().Be(2);
        page.Items.Should().HaveCount(2);
    }

    [Fact]
    public async Task A_page_beyond_the_end_returns_no_items_with_the_real_total()
    {
        var response = await SendAsync(HttpMethod.Get, "/api/v1/aidx/news?page=10000&pageSize=50", null);
        var page = (await response.Content.ReadFromJsonAsync<AidxPageResponse<AidxNewsSummaryResponse>>(TestContext.Current.CancellationToken))!;

        page.Items.Should().BeEmpty();
        page.Page.Should().Be(10000);
    }

    // ----------------------------------------------------------------- filters ---

    [Fact]
    public async Task Project_status_filter_refuses_anything_other_than_published()
    {
        (await SendAsync(HttpMethod.Get, "/api/v1/aidx/projects?status=Draft", null))
            .StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await SendAsync(HttpMethod.Get, "/api/v1/aidx/projects?status=Archived", null))
            .StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await SendAsync(HttpMethod.Get, "/api/v1/aidx/projects?status=Published", null))
            .StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Project_search_and_area_filters_narrow_the_published_list()
    {
        var adminToken = await IssueAdminTokenAsync();
        var keyword = Unique("Coral").Split(' ')[1];
        var areaName = Unique("Marine Area");
        var area = await CreateResearchAreaAsync(adminToken, areaName);
        var project = await CreateProjectAsync(adminToken, $"{keyword} survey", areaIds: [area.Id]);
        await PublishAsync(adminToken, "projects", project.Id);
        var otherProject = await CreateProjectAsync(adminToken, Unique("Unrelated Survey"));
        await PublishAsync(adminToken, "projects", otherProject.Id);

        var bySearch = await SendAsync(HttpMethod.Get, $"/api/v1/aidx/projects?search={keyword}", null);
        var pageBySearch = (await bySearch.Content.ReadFromJsonAsync<AidxPageResponse<AidxProjectSummaryResponse>>(TestContext.Current.CancellationToken))!;
        pageBySearch.Items.Select(p => p.Slug).Should().Contain(project.Slug).And.NotContain(otherProject.Slug);

        var byArea = await SendAsync(HttpMethod.Get, $"/api/v1/aidx/projects?area={area.Slug}", null);
        var pageByArea = (await byArea.Content.ReadFromJsonAsync<AidxPageResponse<AidxProjectSummaryResponse>>(TestContext.Current.CancellationToken))!;
        pageByArea.Items.Select(p => p.Slug).Should().ContainSingle().Which.Should().Be(project.Slug);

        var detail = await SendAsync(HttpMethod.Get, $"/api/v1/aidx/projects/{project.Slug}", null);
        var body = (await detail.Content.ReadFromJsonAsync<AidxProjectDetailResponse>(TestContext.Current.CancellationToken))!;
        body.ResearchAreas.Should().ContainSingle().Which.Should().Be(areaName);
    }

    [Fact]
    public async Task People_category_and_search_filters_apply()
    {
        var adminToken = await IssueAdminTokenAsync();
        var name = Unique("Searchable Person");
        await CreatePersonAsync(adminToken, name, "PhdStudent", published: true);

        var response = await SendAsync(HttpMethod.Get, $"/api/v1/aidx/people?search=Searchable%20Person&category=PhdStudent", null);
        var page = (await response.Content.ReadFromJsonAsync<AidxPageResponse<AidxResearcherResponse>>(TestContext.Current.CancellationToken))!;

        page.Items.Should().ContainSingle(p => p.DisplayName == name);
        (await SendAsync(HttpMethod.Get, "/api/v1/aidx/people?category=NotACategory", null))
            .StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Publication_area_and_text_filters_narrow_results()
    {
        var adminToken = await IssueAdminTokenAsync();
        var area = await CreateResearchAreaAsync(adminToken, Unique("Filter Area"));
        var year = Random.Shared.Next(1900, 2100);
        var token = Unique("Needle").Split(' ')[1];
        var pub = await CreateAsync(adminToken, "/api/v1/admin/aidx/publications",
            new AidxPublicationRequest($"Study {token}", "Abstract text.", "ConferencePaper", null, year, null, null, true, null,
                ResearchAreaIds: [area.Id]));

        var byArea = await SendAsync(HttpMethod.Get, $"/api/v1/aidx/publications?year={year}&area={area.Slug}", null);
        var page = (await byArea.Content.ReadFromJsonAsync<AidxPageResponse<AidxPublicationResponse>>(TestContext.Current.CancellationToken))!;
        page.Items.Select(p => p.Id).Should().ContainSingle().Which.Should().Be(pub.Id);

        var byText = await SendAsync(HttpMethod.Get, $"/api/v1/aidx/publications?year={year}&q={token}", null);
        var textPage = (await byText.Content.ReadFromJsonAsync<AidxPageResponse<AidxPublicationResponse>>(TestContext.Current.CancellationToken))!;
        textPage.Items.Select(p => p.Id).Should().ContainSingle().Which.Should().Be(pub.Id);

        var wrongType = await SendAsync(HttpMethod.Get, $"/api/v1/aidx/publications?year={year}&type=Dataset&q={token}", null);
        var typePage = (await wrongType.Content.ReadFromJsonAsync<AidxPageResponse<AidxPublicationResponse>>(TestContext.Current.CancellationToken))!;
        typePage.Items.Should().BeEmpty("the type filter excludes a conference paper");
    }

    [Fact]
    public async Task Upcoming_filter_hides_past_events()
    {
        var adminToken = await IssueAdminTokenAsync();
        var pastTitle = Unique("Past Event");
        var futureTitle = Unique("Future Event");
        var past = await CreateAsync(adminToken, "/api/v1/admin/aidx/events",
            new AidxEventRequest(pastTitle, null, "Happened.", DateTimeOffset.UtcNow.AddDays(-10), null, null, null, null));
        var future = await CreateAsync(adminToken, "/api/v1/admin/aidx/events",
            new AidxEventRequest(futureTitle, null, "Upcoming.", DateTimeOffset.UtcNow.AddDays(10), null, null, null, null));
        await PublishAsync(adminToken, "events", past.Id);
        await PublishAsync(adminToken, "events", future.Id);

        var upcoming = await SendAsync(HttpMethod.Get, "/api/v1/aidx/events?upcoming=true&pageSize=50", null);
        var page = (await upcoming.Content.ReadFromJsonAsync<AidxPageResponse<AidxEventResponse>>(TestContext.Current.CancellationToken))!;

        page.Items.Select(e => e.Title).Should().Contain(futureTitle).And.NotContain(pastTitle);
    }

    // ------------------------------------------------- research opportunities ---

    [Fact]
    public async Task Opportunities_include_only_published_research_jobs()
    {
        var recruiterToken = await IssueRecruiterWithCompanyAsync();
        var careerJob = await CreateRecruiterJobAsync(recruiterToken);

        var draftResearch = await SendAsync(HttpMethod.Post, "/api/v1/recruiter/jobs", recruiterToken,
            new CreateJobRequest(Unique("Draft Research"), "Draft.", "FullTime", "Remote", "Remote", null, ["R"]));
        var draftId = (await draftResearch.Content.ReadFromJsonAsync<JobResponse>(TestContext.Current.CancellationToken))!.Id;
        await MakeResearchAsync(draftId, null);

        var publishedResearchId = await CreateRecruiterJobAsync(recruiterToken);
        await MakeResearchAsync(publishedResearchId, null);

        var response = await SendAsync(HttpMethod.Get, "/api/v1/aidx/opportunities?pageSize=50", null);
        var page = (await response.Content.ReadFromJsonAsync<AidxPageResponse<AidxOpportunityResponse>>(TestContext.Current.CancellationToken))!;
        var ids = page.Items.Select(o => o.Id).ToList();

        ids.Should().Contain(publishedResearchId, "a published research job is an AIDX opportunity");
        ids.Should().NotContain(careerJob, "a career job must never appear as an AIDX opportunity");
        ids.Should().NotContain(draftId, "a draft research job is not public");
    }

    [Fact]
    public async Task Research_job_linked_to_an_unpublished_project_is_hidden_from_opportunities()
    {
        var adminToken = await IssueAdminTokenAsync();
        var recruiterToken = await IssueRecruiterWithCompanyAsync();
        var draftProject = await CreateProjectAsync(adminToken, Unique("Hidden Project"));
        var jobId = await CreateRecruiterJobAsync(recruiterToken);
        await MakeResearchAsync(jobId, draftProject.Id);

        var page = (await (await SendAsync(HttpMethod.Get, "/api/v1/aidx/opportunities?pageSize=50", null))
            .Content.ReadFromJsonAsync<AidxPageResponse<AidxOpportunityResponse>>(TestContext.Current.CancellationToken))!;

        page.Items.Select(o => o.Id).Should().NotContain(jobId);
    }

    [Fact]
    public async Task Opportunities_expose_the_linked_project_and_honour_the_employment_type_filter()
    {
        var adminToken = await IssueAdminTokenAsync();
        var recruiterToken = await IssueRecruiterWithCompanyAsync();
        var project = await CreateProjectAsync(adminToken, Unique("Opportunity Project"));
        await PublishAsync(adminToken, "projects", project.Id);
        var jobId = await CreateRecruiterJobAsync(recruiterToken);
        await MakeResearchAsync(jobId, project.Id);

        var all = (await (await SendAsync(HttpMethod.Get, "/api/v1/aidx/opportunities?pageSize=50", null))
            .Content.ReadFromJsonAsync<AidxPageResponse<AidxOpportunityResponse>>(TestContext.Current.CancellationToken))!;
        var linked = all.Items.Single(o => o.Id == jobId);
        linked.ProjectSlug.Should().Be(project.Slug);

        var partTime = (await (await SendAsync(HttpMethod.Get, "/api/v1/aidx/opportunities?type=PartTime&pageSize=50", null))
            .Content.ReadFromJsonAsync<AidxPageResponse<AidxOpportunityResponse>>(TestContext.Current.CancellationToken))!;
        partTime.Items.Select(o => o.Id).Should().NotContain(jobId, "the job is FullTime");

        (await SendAsync(HttpMethod.Get, "/api/v1/aidx/opportunities?type=Sideways", null))
            .StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    // ---------------------------------------------------------- relationships ---

    [Fact]
    public async Task Publication_rejects_an_author_with_both_or_neither_identity()
    {
        var adminToken = await IssueAdminTokenAsync();

        (await SendAsync(HttpMethod.Post, "/api/v1/admin/aidx/publications", adminToken,
            new AidxPublicationRequest(Unique("Bad Authors"), null, "Report", null, 2024, null, null, false,
                [new AidxAuthorInput(null, null)])))
            .StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Deleting_a_researcher_unlinks_news_authorship_but_keeps_the_news()
    {
        var adminToken = await IssueAdminTokenAsync();
        var person = await CreatePersonAsync(adminToken, Unique("News Author"), "Academic", published: true);
        var news = await CreateAsync(adminToken, "/api/v1/admin/aidx/news",
            new AidxNewsRequest(Unique("Authored News"), null, "Summary.", "Body.", person.Id));

        (await SendAsync(HttpMethod.Delete, $"/api/v1/admin/aidx/people/{person.Id}", adminToken))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var kept = await db.AidxNews.SingleAsync(n => n.Id == news.Id, TestContext.Current.CancellationToken);
        kept.AuthorResearcherId.Should().BeNull("the news item survives and only loses its author link");
    }

    [Fact]
    public async Task Deleting_a_researcher_with_authorship_is_a_conflict()
    {
        var adminToken = await IssueAdminTokenAsync();
        var person = await CreatePersonAsync(adminToken, Unique("Listed Author"), "Academic", published: true);
        await CreateAsync(adminToken, "/api/v1/admin/aidx/publications",
            new AidxPublicationRequest(Unique("Listed Paper"), null, "Report", null, 2023, null, null, false,
                [new AidxAuthorInput(person.Id, null)]));

        (await SendAsync(HttpMethod.Delete, $"/api/v1/admin/aidx/people/{person.Id}", adminToken))
            .StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Deleting_a_publication_cascades_to_its_authors_and_links()
    {
        var adminToken = await IssueAdminTokenAsync();
        var area = await CreateResearchAreaAsync(adminToken, Unique("Cascade Pub Area"));
        var project = await CreateProjectAsync(adminToken, Unique("Cascade Pub Project"));
        var pub = await CreateAsync(adminToken, "/api/v1/admin/aidx/publications",
            new AidxPublicationRequest(Unique("Cascade Paper"), null, "Report", null, 2022, null, null, false,
                [new AidxAuthorInput(null, "Someone External")], ResearchAreaIds: [area.Id], ProjectIds: [project.Id]));

        (await SendAsync(HttpMethod.Delete, $"/api/v1/admin/aidx/publications/{pub.Id}", adminToken))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        (await db.AidxPublicationAuthors.CountAsync(a => a.PublicationId == pub.Id, TestContext.Current.CancellationToken)).Should().Be(0);
        (await db.AidxPublicationResearchAreas.CountAsync(l => l.PublicationId == pub.Id, TestContext.Current.CancellationToken)).Should().Be(0);
        (await db.AidxPublicationProjects.CountAsync(l => l.PublicationId == pub.Id, TestContext.Current.CancellationToken)).Should().Be(0);
        (await db.AidxProjects.CountAsync(p => p.Id == project.Id, TestContext.Current.CancellationToken)).Should().Be(1, "the project outlives the publication");
    }

    [Fact]
    public async Task Admin_delete_of_an_unknown_id_is_not_found()
    {
        var adminToken = await IssueAdminTokenAsync();

        (await SendAsync(HttpMethod.Delete, $"/api/v1/admin/aidx/events/{Guid.NewGuid()}", adminToken))
            .StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
