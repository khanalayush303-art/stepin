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
/// Phase 4.4E.2 researchers / people CMS, against a real PostgreSQL container. Covers the admin
/// boundary, public visibility driven by the Published flag, the edit view's linked projects and
/// publications, slug stability, and deletion rules around publication authorship.
/// </summary>
public sealed class AidxPeopleCmsTests(AuthApiFactory factory) : IClassFixture<AuthApiFactory>
{
    private const string Route = "/api/v1/admin/aidx/people";

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

    private static AidxResearcherRequest PersonRequest(string name, bool published = false, string? slug = null) =>
        new(name, slug, "Academic", "Senior lecturer", "Biography.", null, null, null, null, published);

    private async Task<AidxIdResponse> CreatePersonAsync(string admin, AidxResearcherRequest request)
    {
        var response = await SendAsync(HttpMethod.Post, Route, admin, request);
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        return (await response.Content.ReadFromJsonAsync<AidxIdResponse>(TestContext.Current.CancellationToken))!;
    }

    // ----------------------------------------------------------- authorization ---

    [Fact]
    public async Task Every_people_route_follows_the_admin_boundary()
    {
        var applicant = await IssueAsync("Applicant");
        var recruiter = await IssueAsync("Recruiter");
        var admin = await AdminAsync();
        var created = await CreatePersonAsync(admin, PersonRequest(Unique("Boundary Person")));
        var body = PersonRequest(Unique("Boundary Edit"));

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
    public async Task Unpublished_people_are_in_the_admin_list_but_hidden_from_public_pages()
    {
        var admin = await AdminAsync();
        var hiddenName = Unique("Hidden Person");
        var shownName = Unique("Shown Person");
        var hidden = await CreatePersonAsync(admin, PersonRequest(hiddenName, published: false));
        var shown = await CreatePersonAsync(admin, PersonRequest(shownName, published: true));

        var adminPage = (await (await SendAsync(HttpMethod.Get, $"{Route}?pageSize=50", admin))
            .Content.ReadFromJsonAsync<AidxPageResponse<AidxAdminPersonSummaryResponse>>(TestContext.Current.CancellationToken))!;
        adminPage.Items.Select(p => p.Id).Should().Contain(new[] { hidden.Id, shown.Id });

        var publicPage = (await (await SendAsync(HttpMethod.Get, "/api/v1/aidx/people?pageSize=50", null))
            .Content.ReadFromJsonAsync<AidxPageResponse<AidxResearcherResponse>>(TestContext.Current.CancellationToken))!;
        publicPage.Items.Select(p => p.Id).Should().Contain(shown.Id).And.NotContain(hidden.Id);

        (await SendAsync(HttpMethod.Get, $"/api/v1/aidx/people/{hidden.Slug}", null))
            .StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await SendAsync(HttpMethod.Get, $"/api/v1/aidx/people/{shown.Slug}", null))
            .StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task The_published_filter_separates_public_and_private_people()
    {
        var admin = await AdminAsync();
        var privateId = (await CreatePersonAsync(admin, PersonRequest(Unique("Filter private"), published: false))).Id;

        var unpublished = (await (await SendAsync(HttpMethod.Get, $"{Route}?published=false&pageSize=50", admin))
            .Content.ReadFromJsonAsync<AidxPageResponse<AidxAdminPersonSummaryResponse>>(TestContext.Current.CancellationToken))!;

        unpublished.Items.Should().OnlyContain(p => !p.Published);
        unpublished.Items.Select(p => p.Id).Should().Contain(privateId);
    }

    [Fact]
    public async Task The_public_detail_does_not_expose_account_or_internal_fields()
    {
        var admin = await AdminAsync();
        var person = await CreatePersonAsync(admin, PersonRequest(Unique("Leak check"), published: true));

        var json = await (await SendAsync(HttpMethod.Get, $"/api/v1/aidx/people/{person.Slug}", null))
            .Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        json.Should().NotContainAny("userId", "email", "clerk", "profileImageKey", "published");
    }

    // ------------------------------------------------------------------ slugs ---

    [Fact]
    public async Task Duplicate_and_malformed_slugs_are_rejected()
    {
        var admin = await AdminAsync();
        var slug = Unique("shared-slug").Replace(' ', '-').ToLowerInvariant();
        await CreatePersonAsync(admin, PersonRequest(Unique("First"), slug: slug));

        (await SendAsync(HttpMethod.Post, Route, admin, PersonRequest(Unique("Second"), slug: slug)))
            .StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await SendAsync(HttpMethod.Post, Route, admin, PersonRequest(Unique("Third"), slug: "Not A Slug!")))
            .StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Editing_the_name_without_a_slug_keeps_the_public_url()
    {
        var admin = await AdminAsync();
        var created = await CreatePersonAsync(admin, PersonRequest(Unique("Stable URL"), published: true));

        var response = await SendAsync(HttpMethod.Put, $"{Route}/{created.Id}", admin, PersonRequest(Unique("Renamed Person"), published: true, slug: null));
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var read = (await (await SendAsync(HttpMethod.Get, $"{Route}/{created.Id}", admin))
            .Content.ReadFromJsonAsync<AidxAdminPersonDetailResponse>(TestContext.Current.CancellationToken))!;
        read.Slug.Should().Be(created.Slug, "a name change must not silently rewrite the public URL");
    }

    // ------------------------------------------------------------ validation ---

    [Fact]
    public async Task Create_rejects_an_unknown_category_and_a_missing_name()
    {
        var admin = await AdminAsync();

        (await SendAsync(HttpMethod.Post, Route, admin, new { displayName = "Bad Category", category = "Wizard", published = false }))
            .StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await SendAsync(HttpMethod.Post, Route, admin, new { displayName = "", category = "Academic", published = false }))
            .StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Unknown_people_return_404_on_read_update_and_delete()
    {
        var admin = await AdminAsync();
        var missing = Guid.NewGuid();

        (await SendAsync(HttpMethod.Get, $"{Route}/{missing}", admin)).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await SendAsync(HttpMethod.Put, $"{Route}/{missing}", admin, PersonRequest("Ghost"))).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await SendAsync(HttpMethod.Delete, $"{Route}/{missing}", admin)).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // ------------------------------------------------- relationships and deletion ---

    [Fact]
    public async Task The_edit_view_lists_linked_projects_and_authored_publications()
    {
        var admin = await AdminAsync();
        var person = await CreatePersonAsync(admin, PersonRequest(Unique("Linked person"), published: true));
        var projectTitle = Unique("Linked project");
        await SendAsync(HttpMethod.Post, "/api/v1/admin/aidx/projects", admin,
            new AidxProjectRequest(projectTitle, null, "Short.", "Long.", null, null, null, false, null, null,
                [new AidxProjectResearcherInput(person.Id, "Lead")]));
        var publicationTitle = Unique("Authored paper");
        await SendAsync(HttpMethod.Post, "/api/v1/admin/aidx/publications", admin,
            new AidxPublicationRequest(publicationTitle, null, "Report", null, 2025, null, null, false,
                [new AidxAuthorInput(person.Id, null)]));

        var detail = (await (await SendAsync(HttpMethod.Get, $"{Route}/{person.Id}", admin))
            .Content.ReadFromJsonAsync<AidxAdminPersonDetailResponse>(TestContext.Current.CancellationToken))!;

        detail.Projects.Select(p => p.Title).Should().Contain(projectTitle);
        detail.Publications.Select(p => p.Title).Should().Contain(publicationTitle);
    }

    [Fact]
    public async Task Deleting_a_person_with_no_authorship_succeeds_and_leaves_the_project_intact()
    {
        var admin = await AdminAsync();
        var person = await CreatePersonAsync(admin, PersonRequest(Unique("Deletable person")));
        var projectId = (await (await SendAsync(HttpMethod.Post, "/api/v1/admin/aidx/projects", admin,
            new AidxProjectRequest(Unique("Team project"), null, "Short.", "Long.", null, null, null, false, null, null,
                [new AidxProjectResearcherInput(person.Id, null)])))
            .Content.ReadFromJsonAsync<AidxIdResponse>(TestContext.Current.CancellationToken))!.Id;

        (await SendAsync(HttpMethod.Delete, $"{Route}/{person.Id}", admin)).StatusCode.Should().Be(HttpStatusCode.NoContent);

        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var ct = TestContext.Current.CancellationToken;
        (await db.AidxResearchers.CountAsync(r => r.Id == person.Id, ct)).Should().Be(0);
        (await db.AidxProjects.CountAsync(p => p.Id == projectId, ct)).Should().Be(1, "deleting a person must not delete the project");
        (await db.AidxProjectResearchers.CountAsync(l => l.ResearcherId == person.Id, ct)).Should().Be(0, "the team link goes with the person");
    }

    [Fact]
    public async Task Deleting_a_person_with_publication_authorship_is_refused_and_nothing_changes()
    {
        var admin = await AdminAsync();
        var person = await CreatePersonAsync(admin, PersonRequest(Unique("Authoring person")));
        var publication = (await (await SendAsync(HttpMethod.Post, "/api/v1/admin/aidx/publications", admin,
                new AidxPublicationRequest(Unique("Protected paper"), null, "Report", null, 2024, null, null, false,
                    [new AidxAuthorInput(person.Id, null)])))
            .Content.ReadFromJsonAsync<AidxIdResponse>(TestContext.Current.CancellationToken))!;

        var response = await SendAsync(HttpMethod.Delete, $"{Route}/{person.Id}", admin);
        response.StatusCode.Should().Be(HttpStatusCode.Conflict);

        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var ct = TestContext.Current.CancellationToken;
        (await db.AidxResearchers.CountAsync(r => r.Id == person.Id, ct)).Should().Be(1);
        (await db.AidxPublicationAuthors.CountAsync(a => a.PublicationId == publication.Id && a.ResearcherId == person.Id, ct))
            .Should().Be(1, "publication authorship must remain intact");
    }
}
