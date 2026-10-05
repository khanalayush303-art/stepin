using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using StepIn.Api.Endpoints;
using StepIn.Domain.Common;
using StepIn.Infrastructure.Persistence;

namespace StepIn.Api.Tests;

/// <summary>
/// Phase 4.4E.3 publications CMS, against a real PostgreSQL container. Covers the admin boundary, public
/// visibility, ordered authors, research-area and project links, title-only edits that must not drop
/// relationships, and deletion that removes only the publication's own rows.
/// </summary>
public sealed class AidxPublicationsCmsTests(AuthApiFactory factory) : IClassFixture<AuthApiFactory>
{
    private const string Route = "/api/v1/admin/aidx/publications";

    private readonly AuthApiFactory _factory = factory;

    private static string Unique(string prefix) => $"{prefix} {Guid.NewGuid():N}";

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

    private async Task<string> IssueAsync(string role)
    {
        var token = AuthApiFactory.IssueToken($"user_{Guid.NewGuid():N}", $"{Guid.NewGuid()}@example.com");
        using var setup = Build(HttpMethod.Post, "/api/v1/auth/account-setup", token, new AccountSetupRequest(role));
        using var client = _factory.CreateClient();
        (await client.SendAsync(setup, TestContext.Current.CancellationToken)).EnsureSuccessStatusCode();
        return token;
    }

    private async Task<string> AdminAsync()
    {
        var clerkUserId = $"admin_{Guid.NewGuid():N}";
        var token = AuthApiFactory.IssueToken(clerkUserId, $"{Guid.NewGuid()}@example.com");
        using (var client = _factory.CreateClient())
        {
            using var setup = Build(HttpMethod.Post, "/api/v1/auth/account-setup", token, new AccountSetupRequest("Applicant"));
            (await client.SendAsync(setup, TestContext.Current.CancellationToken)).EnsureSuccessStatusCode();
        }

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        await db.Users.Where(u => u.ClerkUserId == clerkUserId)
            .ExecuteUpdateAsync(s => s.SetProperty(u => u.Role, UserRole.Admin), TestContext.Current.CancellationToken);
        return token;
    }

    private async Task<Guid> CreateResearcherAsync(string admin, bool published = true)
    {
        var response = await SendAsync(HttpMethod.Post, "/api/v1/admin/aidx/people", admin,
            new AidxResearcherRequest(Unique("Pub author"), null, "Academic", null, null, null, null, null, null, published));
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        return (await response.Content.ReadFromJsonAsync<AidxIdResponse>(TestContext.Current.CancellationToken))!.Id;
    }

    private async Task<Guid> CreateAreaAsync(string admin)
    {
        var response = await SendAsync(HttpMethod.Post, "/api/v1/admin/aidx/research", admin,
            new AidxResearchAreaRequest(Unique("Pub area"), null, null, 0));
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        return (await response.Content.ReadFromJsonAsync<AidxIdResponse>(TestContext.Current.CancellationToken))!.Id;
    }

    private async Task<Guid> CreateProjectAsync(string admin)
    {
        var response = await SendAsync(HttpMethod.Post, "/api/v1/admin/aidx/projects", admin,
            new AidxProjectRequest(Unique("Pub project"), null, "Short.", "Long.", null, null, null, false, null, null, null));
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        return (await response.Content.ReadFromJsonAsync<AidxIdResponse>(TestContext.Current.CancellationToken))!.Id;
    }

    private static AidxPublicationRequest Pub(string title, bool published, IReadOnlyList<AidxAuthorInput>? authors = null,
        IReadOnlyList<Guid>? areas = null, IReadOnlyList<Guid>? projects = null, int year = 2024, string type = "JournalArticle") =>
        new(title, "Abstract text.", type, "Venue", year, null, null, published, authors, areas, projects);

    private async Task<AidxIdResponse> CreateAsync(string admin, AidxPublicationRequest request)
    {
        var response = await SendAsync(HttpMethod.Post, Route, admin, request);
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        return (await response.Content.ReadFromJsonAsync<AidxIdResponse>(TestContext.Current.CancellationToken))!;
    }

    private async Task<AidxAdminPublicationDetailResponse> DetailAsync(string admin, Guid id)
    {
        var response = await SendAsync(HttpMethod.Get, $"{Route}/{id}", admin);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        return (await response.Content.ReadFromJsonAsync<AidxAdminPublicationDetailResponse>(TestContext.Current.CancellationToken))!;
    }

    // ----------------------------------------------------------- authorization ---

    [Fact]
    public async Task Every_publication_route_follows_the_admin_boundary()
    {
        var applicant = await IssueAsync("Applicant");
        var recruiter = await IssueAsync("Recruiter");
        var admin = await AdminAsync();
        var created = await CreateAsync(admin, Pub(Unique("Boundary paper"), published: false));
        var body = Pub(Unique("Boundary edit"), published: false);

        foreach (var (method, url, payload) in new (HttpMethod, string, object?)[]
                 {
                     (HttpMethod.Get, Route, null),
                     (HttpMethod.Get, $"{Route}/{created.Id}", null),
                     (HttpMethod.Post, Route, body),
                     (HttpMethod.Put, $"{Route}/{created.Id}", body),
                     (HttpMethod.Delete, $"{Route}/{created.Id}", null),
                 })
        {
            (await SendAsync(method, url, null, payload)).StatusCode.Should().Be(HttpStatusCode.Unauthorized, $"{method} {url}");
            (await SendAsync(method, url, applicant, payload)).StatusCode.Should().Be(HttpStatusCode.Forbidden, $"{method} {url}");
            (await SendAsync(method, url, recruiter, payload)).StatusCode.Should().Be(HttpStatusCode.Forbidden, $"{method} {url}");
        }

        (await SendAsync(HttpMethod.Get, Route, admin)).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    // ------------------------------------------------------------- visibility ---

    [Fact]
    public async Task Unpublished_publications_are_listed_for_admins_and_hidden_from_public_listing()
    {
        var admin = await AdminAsync();
        var year = Random.Shared.Next(1950, 2090);
        var hidden = await CreateAsync(admin, Pub(Unique("Hidden paper"), published: false, year: year));
        var shown = await CreateAsync(admin, Pub(Unique("Shown paper"), published: true, year: year));

        var adminPage = (await (await SendAsync(HttpMethod.Get, $"{Route}?year={year}&pageSize=50", admin))
            .Content.ReadFromJsonAsync<AidxPageResponse<AidxAdminPublicationSummaryResponse>>(TestContext.Current.CancellationToken))!;
        adminPage.Items.Select(p => p.Id).Should().Contain(new[] { hidden.Id, shown.Id });

        var publicPage = (await (await SendAsync(HttpMethod.Get, $"/api/v1/aidx/publications?year={year}&pageSize=50", null))
            .Content.ReadFromJsonAsync<AidxPageResponse<AidxPublicationResponse>>(TestContext.Current.CancellationToken))!;
        publicPage.Items.Select(p => p.Id).Should().Contain(shown.Id).And.NotContain(hidden.Id);
    }

    [Fact]
    public async Task The_public_listing_never_exposes_the_pdf_storage_key_or_account_fields()
    {
        var admin = await AdminAsync();
        var year = Random.Shared.Next(1950, 2090);
        await CreateAsync(admin, Pub(Unique("Leak check paper"), published: true, year: year));

        var json = await (await SendAsync(HttpMethod.Get, $"/api/v1/aidx/publications?year={year}", null))
            .Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        json.Should().NotContainAny("pdfKey", "userId", "email", "clerk");
    }

    // ---------------------------------------------------------- authors and order ---

    [Fact]
    public async Task Authors_are_returned_in_order_with_researcher_and_external_names()
    {
        var admin = await AdminAsync();
        var researcher = await CreateResearcherAsync(admin);
        var created = await CreateAsync(admin, Pub(Unique("Ordered paper"), published: false,
            authors: [new AidxAuthorInput(researcher, null), new AidxAuthorInput(null, "External Co-author")]));

        var detail = await DetailAsync(admin, created.Id);

        detail.Authors.Select(a => a.Position).Should().Equal(1, 2);
        detail.Authors[0].ResearcherId.Should().Be(researcher);
        detail.Authors[0].ResearcherPublished.Should().BeTrue();
        detail.Authors[1].ExternalAuthorName.Should().Be("External Co-author");
        detail.Authors[1].ResearcherPublished.Should().BeNull();
    }

    [Fact]
    public async Task Reordering_authors_saves_the_new_order()
    {
        var admin = await AdminAsync();
        var first = await CreateResearcherAsync(admin);
        var second = await CreateResearcherAsync(admin);
        var created = await CreateAsync(admin, Pub(Unique("Reorder paper"), published: false,
            authors: [new AidxAuthorInput(first, null), new AidxAuthorInput(second, null)]));

        var response = await SendAsync(HttpMethod.Put, $"{Route}/{created.Id}", admin,
            Pub(Unique("Reorder paper"), published: false, authors: [new AidxAuthorInput(second, null), new AidxAuthorInput(first, null)]));
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var detail = await DetailAsync(admin, created.Id);
        detail.Authors.Select(a => a.ResearcherId).Should().Equal(second, first);
    }

    [Fact]
    public async Task The_same_researcher_cannot_be_listed_twice()
    {
        var admin = await AdminAsync();
        var researcher = await CreateResearcherAsync(admin);

        (await SendAsync(HttpMethod.Post, Route, admin,
                Pub(Unique("Duplicate author"), published: false, authors: [new AidxAuthorInput(researcher, null), new AidxAuthorInput(researcher, null)])))
            .StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task An_author_with_both_or_neither_identity_is_rejected()
    {
        var admin = await AdminAsync();
        var researcher = await CreateResearcherAsync(admin);

        (await SendAsync(HttpMethod.Post, Route, admin,
                Pub(Unique("Both identities"), published: false, authors: [new AidxAuthorInput(researcher, "Also a name")])))
            .StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await SendAsync(HttpMethod.Post, Route, admin,
                Pub(Unique("No identity"), published: false, authors: [new AidxAuthorInput(null, null)])))
            .StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Unknown_researcher_ids_are_rejected()
    {
        var admin = await AdminAsync();

        (await SendAsync(HttpMethod.Post, Route, admin,
                Pub(Unique("Ghost author"), published: false, authors: [new AidxAuthorInput(Guid.NewGuid(), null)])))
            .StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Hidden_researchers_stay_linked_as_authors()
    {
        var admin = await AdminAsync();
        var hiddenResearcher = await CreateResearcherAsync(admin, published: false);
        var created = await CreateAsync(admin, Pub(Unique("Hidden author paper"), published: false,
            authors: [new AidxAuthorInput(hiddenResearcher, null)]));

        var detail = await DetailAsync(admin, created.Id);

        detail.Authors.Should().ContainSingle().Which.ResearcherId.Should().Be(hiddenResearcher);
        detail.Authors.Single().ResearcherPublished.Should().BeFalse("the form can show that this author is hidden");
    }

    // ----------------------------------------------------- title-only edits ---

    [Fact]
    public async Task A_title_only_edit_keeps_authors_areas_and_projects()
    {
        var admin = await AdminAsync();
        var researcher = await CreateResearcherAsync(admin);
        var area = await CreateAreaAsync(admin);
        var project = await CreateProjectAsync(admin);
        var created = await CreateAsync(admin, Pub(Unique("Keep links"), published: false,
            authors: [new AidxAuthorInput(researcher, null)], areas: [area], projects: [project]));

        // The request sends only the title; the collections are omitted.
        var response = await SendAsync(HttpMethod.Put, $"{Route}/{created.Id}", admin,
            new { title = "Renamed paper", abstractText = "x", publicationType = "JournalArticle", venue = "V", year = 2024, published = false });
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var detail = await DetailAsync(admin, created.Id);
        detail.Title.Should().Be("Renamed paper");
        detail.Authors.Select(a => a.ResearcherId).Should().Equal(researcher);
        detail.ResearchAreaIds.Should().Equal(area);
        detail.Projects.Select(p => p.Id).Should().Equal(project);
    }

    [Fact]
    public async Task Changing_only_the_areas_leaves_authors_and_projects_alone()
    {
        var admin = await AdminAsync();
        var researcher = await CreateResearcherAsync(admin);
        var areaA = await CreateAreaAsync(admin);
        var areaB = await CreateAreaAsync(admin);
        var project = await CreateProjectAsync(admin);
        var created = await CreateAsync(admin, Pub(Unique("Area swap"), published: false,
            authors: [new AidxAuthorInput(researcher, null)], areas: [areaA], projects: [project]));

        (await SendAsync(HttpMethod.Put, $"{Route}/{created.Id}", admin,
                new { title = "Area swap", publicationType = "JournalArticle", venue = "V", year = 2024, published = false, researchAreaIds = new[] { areaB } }))
            .StatusCode.Should().Be(HttpStatusCode.OK);

        var detail = await DetailAsync(admin, created.Id);
        detail.ResearchAreaIds.Should().Equal(areaB);
        detail.Authors.Select(a => a.ResearcherId).Should().Equal(researcher);
        detail.Projects.Select(p => p.Id).Should().Equal(project);
    }

    // ------------------------------------------------------- relationship safety ---

    [Fact]
    public async Task Unknown_area_and_project_ids_are_rejected()
    {
        var admin = await AdminAsync();

        (await SendAsync(HttpMethod.Post, Route, admin, Pub(Unique("Bad area"), published: false, areas: [Guid.NewGuid()])))
            .StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await SendAsync(HttpMethod.Post, Route, admin, Pub(Unique("Bad project"), published: false, projects: [Guid.NewGuid()])))
            .StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Deleting_a_publication_removes_only_its_own_rows()
    {
        var admin = await AdminAsync();
        var researcher = await CreateResearcherAsync(admin);
        var area = await CreateAreaAsync(admin);
        var project = await CreateProjectAsync(admin);
        var created = await CreateAsync(admin, Pub(Unique("Delete me"), published: false,
            authors: [new AidxAuthorInput(researcher, null)], areas: [area], projects: [project]));

        (await SendAsync(HttpMethod.Delete, $"{Route}/{created.Id}", admin)).StatusCode.Should().Be(HttpStatusCode.NoContent);

        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var ct = TestContext.Current.CancellationToken;
        (await db.AidxPublications.CountAsync(p => p.Id == created.Id, ct)).Should().Be(0);
        (await db.AidxPublicationAuthors.CountAsync(a => a.PublicationId == created.Id, ct)).Should().Be(0);
        (await db.AidxPublicationResearchAreas.CountAsync(l => l.PublicationId == created.Id, ct)).Should().Be(0);
        (await db.AidxPublicationProjects.CountAsync(l => l.PublicationId == created.Id, ct)).Should().Be(0);
        (await db.AidxResearchers.CountAsync(r => r.Id == researcher, ct)).Should().Be(1, "a publication must not delete its researchers");
        (await db.AidxResearchAreas.CountAsync(a => a.Id == area, ct)).Should().Be(1, "a publication must not delete its areas");
        (await db.AidxProjects.CountAsync(p => p.Id == project, ct)).Should().Be(1, "a publication must not delete its projects");
    }

    [Fact]
    public async Task Admin_list_filters_by_type_and_rejects_unknown_types()
    {
        var admin = await AdminAsync();
        var year = Random.Shared.Next(1950, 2090);
        await CreateAsync(admin, Pub(Unique("Dataset paper"), published: false, year: year, type: "Dataset"));

        var datasets = (await (await SendAsync(HttpMethod.Get, $"{Route}?type=Dataset&year={year}&pageSize=50", admin))
            .Content.ReadFromJsonAsync<AidxPageResponse<AidxAdminPublicationSummaryResponse>>(TestContext.Current.CancellationToken))!;
        datasets.Items.Should().OnlyContain(p => p.PublicationType == "Dataset");

        (await SendAsync(HttpMethod.Get, $"{Route}?type=Pamphlet", admin)).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await SendAsync(HttpMethod.Get, $"{Route}?year=1000", admin)).StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Unknown_publication_returns_404_on_read_update_and_delete()
    {
        var admin = await AdminAsync();
        var missing = Guid.NewGuid();

        (await SendAsync(HttpMethod.Get, $"{Route}/{missing}", admin)).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await SendAsync(HttpMethod.Put, $"{Route}/{missing}", admin, Pub("Ghost", published: false))).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await SendAsync(HttpMethod.Delete, $"{Route}/{missing}", admin)).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
