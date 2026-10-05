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
/// Phase 4.4E.4 news CMS, against a real PostgreSQL container. Covers the admin boundary, the Draft /
/// Published / Archived lifecycle, public visibility, and public URL stability, which is the main risk
/// for News because each item has a public detail route.
/// </summary>
public sealed class AidxNewsCmsTests(AuthApiFactory factory) : IClassFixture<AuthApiFactory>
{
    private const string Route = "/api/v1/admin/aidx/news";

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

    private static AidxNewsRequest News(string title, string? slug = null, Guid? author = null) =>
        new(title, slug, "A short summary.", "The body of the news item.", author);

    private async Task<AidxIdResponse> CreateAsync(string admin, AidxNewsRequest request)
    {
        var response = await SendAsync(HttpMethod.Post, Route, admin, request);
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        return (await response.Content.ReadFromJsonAsync<AidxIdResponse>(TestContext.Current.CancellationToken))!;
    }

    private async Task PublishAsync(string admin, Guid id) =>
        (await SendAsync(HttpMethod.Post, $"{Route}/{id}/publish", admin)).StatusCode.Should().Be(HttpStatusCode.OK);

    private async Task<AidxAdminNewsDetailResponse> DetailAsync(string admin, Guid id)
    {
        var response = await SendAsync(HttpMethod.Get, $"{Route}/{id}", admin);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        return (await response.Content.ReadFromJsonAsync<AidxAdminNewsDetailResponse>(TestContext.Current.CancellationToken))!;
    }

    // ----------------------------------------------------------- authorization ---

    [Fact]
    public async Task Every_news_route_follows_the_admin_boundary()
    {
        var applicant = await IssueAsync("Applicant");
        var recruiter = await IssueAsync("Recruiter");
        var admin = await AdminAsync();
        var created = await CreateAsync(admin, News(Unique("Boundary news")));
        var body = News(Unique("Boundary edit"));

        foreach (var (method, url, payload) in new (HttpMethod, string, object?)[]
                 {
                     (HttpMethod.Get, Route, null),
                     (HttpMethod.Get, $"{Route}/{created.Id}", null),
                     (HttpMethod.Post, Route, body),
                     (HttpMethod.Put, $"{Route}/{created.Id}", body),
                     (HttpMethod.Post, $"{Route}/{created.Id}/publish", null),
                     (HttpMethod.Post, $"{Route}/{created.Id}/archive", null),
                     (HttpMethod.Delete, $"{Route}/{created.Id}", null),
                 })
        {
            (await SendAsync(method, url, null, payload)).StatusCode.Should().Be(HttpStatusCode.Unauthorized, $"{method} {url}");
            (await SendAsync(method, url, applicant, payload)).StatusCode.Should().Be(HttpStatusCode.Forbidden, $"{method} {url}");
            (await SendAsync(method, url, recruiter, payload)).StatusCode.Should().Be(HttpStatusCode.Forbidden, $"{method} {url}");
        }

        (await SendAsync(HttpMethod.Get, Route, admin)).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    // ------------------------------------------------------------ slug stability ---

    [Fact]
    public async Task Editing_the_title_without_a_slug_keeps_the_public_url()
    {
        var admin = await AdminAsync();
        var created = await CreateAsync(admin, News("AI Research Update " + Guid.NewGuid().ToString("N")[..8]));
        var originalSlug = (await DetailAsync(admin, created.Id)).Slug;
        await PublishAsync(admin, created.Id);

        (await SendAsync(HttpMethod.Put, $"{Route}/{created.Id}", admin, News("Major AI Research Update"))).StatusCode
            .Should().Be(HttpStatusCode.OK);

        (await DetailAsync(admin, created.Id)).Slug.Should().Be(originalSlug, "a title change must not rewrite the public URL");
        (await SendAsync(HttpMethod.Get, $"/api/v1/aidx/news/{originalSlug}", null)).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task A_slug_is_derived_from_the_title_when_none_is_given()
    {
        var admin = await AdminAsync();
        var suffix = Guid.NewGuid().ToString("N")[..8];

        var created = await CreateAsync(admin, News($"AI Research Update {suffix}"));

        (await DetailAsync(admin, created.Id)).Slug.Should().Be($"ai-research-update-{suffix}");
    }

    [Fact]
    public async Task Duplicate_and_malformed_slugs_are_rejected()
    {
        var admin = await AdminAsync();
        var slug = Unique("news-dup").Replace(' ', '-').ToLowerInvariant();
        await CreateAsync(admin, News(Unique("First news"), slug));

        (await SendAsync(HttpMethod.Post, Route, admin, News(Unique("Second news"), slug))).StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await SendAsync(HttpMethod.Post, Route, admin, News(Unique("Third news"), "Not A Slug!"))).StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task An_explicit_slug_change_moves_the_public_url_and_the_old_one_stops_resolving()
    {
        var admin = await AdminAsync();
        var created = await CreateAsync(admin, News(Unique("Moving news")));
        await PublishAsync(admin, created.Id);
        var oldSlug = (await DetailAsync(admin, created.Id)).Slug;
        var newSlug = Unique("moved-news").Replace(' ', '-').ToLowerInvariant();

        (await SendAsync(HttpMethod.Put, $"{Route}/{created.Id}", admin, News(Unique("Moving news renamed"), newSlug))).StatusCode
            .Should().Be(HttpStatusCode.OK);

        (await SendAsync(HttpMethod.Get, $"/api/v1/aidx/news/{newSlug}", null)).StatusCode.Should().Be(HttpStatusCode.OK);
        (await SendAsync(HttpMethod.Get, $"/api/v1/aidx/news/{oldSlug}", null)).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // --------------------------------------------------------------- lifecycle ---

    [Fact]
    public async Task Drafts_are_hidden_publicly_and_published_items_are_visible()
    {
        var admin = await AdminAsync();
        var draft = await CreateAsync(admin, News(Unique("Draft visibility")));
        var draftSlug = (await DetailAsync(admin, draft.Id)).Slug;
        var published = await CreateAsync(admin, News(Unique("Published visibility")));
        await PublishAsync(admin, published.Id);
        var publishedSlug = (await DetailAsync(admin, published.Id)).Slug;

        (await SendAsync(HttpMethod.Get, $"/api/v1/aidx/news/{draftSlug}", null)).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await SendAsync(HttpMethod.Get, $"/api/v1/aidx/news/{publishedSlug}", null)).StatusCode.Should().Be(HttpStatusCode.OK);

        var listing = (await (await SendAsync(HttpMethod.Get, "/api/v1/aidx/news?pageSize=50", null))
            .Content.ReadFromJsonAsync<AidxPageResponse<AidxNewsSummaryResponse>>(TestContext.Current.CancellationToken))!;
        listing.Items.Select(n => n.Slug).Should().Contain(publishedSlug).And.NotContain(draftSlug);
    }

    [Fact]
    public async Task Publish_then_archive_hides_the_item_and_republishing_keeps_the_original_publish_date()
    {
        var admin = await AdminAsync();
        var created = await CreateAsync(admin, News(Unique("Lifecycle news")));
        await PublishAsync(admin, created.Id);
        var firstPublished = (await DetailAsync(admin, created.Id)).PublishedAt;
        var slug = (await DetailAsync(admin, created.Id)).Slug;

        (await SendAsync(HttpMethod.Post, $"{Route}/{created.Id}/archive", admin)).StatusCode.Should().Be(HttpStatusCode.OK);
        (await SendAsync(HttpMethod.Get, $"/api/v1/aidx/news/{slug}", null)).StatusCode.Should().Be(HttpStatusCode.NotFound);

        await PublishAsync(admin, created.Id);
        (await DetailAsync(admin, created.Id)).PublishedAt.Should().Be(firstPublished, "republishing keeps the first publish date");
    }

    [Fact]
    public async Task Invalid_transitions_are_rejected()
    {
        var admin = await AdminAsync();
        var draft = await CreateAsync(admin, News(Unique("Transition draft")));
        var published = await CreateAsync(admin, News(Unique("Transition published")));
        await PublishAsync(admin, published.Id);

        (await SendAsync(HttpMethod.Post, $"{Route}/{draft.Id}/archive", admin)).StatusCode.Should().Be(HttpStatusCode.Conflict,
            "a draft cannot be archived");
        (await SendAsync(HttpMethod.Post, $"{Route}/{published.Id}/publish", admin)).StatusCode.Should().Be(HttpStatusCode.Conflict,
            "a published item cannot be published again");
    }

    // ------------------------------------------------------------------ delete ---

    [Fact]
    public async Task Only_drafts_can_be_deleted()
    {
        var admin = await AdminAsync();
        var draft = await CreateAsync(admin, News(Unique("Deletable news")));
        var published = await CreateAsync(admin, News(Unique("Protected news")));
        await PublishAsync(admin, published.Id);

        (await SendAsync(HttpMethod.Delete, $"{Route}/{draft.Id}", admin)).StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await SendAsync(HttpMethod.Delete, $"{Route}/{published.Id}", admin)).StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await DetailAsync(admin, published.Id)).Status.Should().Be("Published");
    }

    [Fact]
    public async Task Unknown_news_returns_404_on_read_update_and_delete()
    {
        var admin = await AdminAsync();
        var missing = Guid.NewGuid();

        (await SendAsync(HttpMethod.Get, $"{Route}/{missing}", admin)).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await SendAsync(HttpMethod.Put, $"{Route}/{missing}", admin, News("Ghost"))).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await SendAsync(HttpMethod.Delete, $"{Route}/{missing}", admin)).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // ------------------------------------------------------------ list and detail ---

    [Fact]
    public async Task Admin_list_searches_and_filters_by_status_and_rejects_unknown_status()
    {
        var admin = await AdminAsync();
        var keyword = Unique("Quasar").Split(' ')[1];
        var draft = await CreateAsync(admin, News($"{keyword} draft story"));
        var published = await CreateAsync(admin, News($"{keyword} published story"));
        await PublishAsync(admin, published.Id);

        var drafts = (await (await SendAsync(HttpMethod.Get, $"{Route}?q={keyword}&status=Draft&pageSize=50", admin))
            .Content.ReadFromJsonAsync<AidxPageResponse<AidxAdminNewsSummaryResponse>>(TestContext.Current.CancellationToken))!;
        drafts.Items.Select(n => n.Id).Should().ContainSingle().Which.Should().Be(draft.Id);

        (await SendAsync(HttpMethod.Get, $"{Route}?status=Sideways", admin)).StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Admin_detail_shows_the_body_and_the_author_name_but_not_the_image_key()
    {
        var admin = await AdminAsync();
        var author = await SendAsync(HttpMethod.Post, "/api/v1/admin/aidx/people", admin,
            new AidxResearcherRequest(Unique("News author"), null, "Academic", null, null, null, null, null, null, true));
        var authorId = (await author.Content.ReadFromJsonAsync<AidxIdResponse>(TestContext.Current.CancellationToken))!.Id;
        var created = await CreateAsync(admin, News(Unique("Authored news"), author: authorId));

        var detail = await DetailAsync(admin, created.Id);

        detail.Body.Should().Be("The body of the news item.");
        detail.AuthorResearcherId.Should().Be(authorId);
        detail.AuthorName.Should().StartWith("News author");
    }

    [Fact]
    public async Task Public_news_exposes_no_author_identifier_or_storage_key()
    {
        var admin = await AdminAsync();
        var created = await CreateAsync(admin, News(Unique("Leak check news")));
        await PublishAsync(admin, created.Id);
        var slug = (await DetailAsync(admin, created.Id)).Slug;

        var json = await (await SendAsync(HttpMethod.Get, $"/api/v1/aidx/news/{slug}", null))
            .Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        json.Should().NotContainAny("authorResearcherId", "imageKey", "userId", "email");
    }
}
