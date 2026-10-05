using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;
using StepIn.Api.Endpoints;
using StepIn.Domain.Common;
using StepIn.Infrastructure.Persistence;

namespace StepIn.Api.Tests;

/// <summary>
/// Phase 4.4E.5 events CMS, against a real PostgreSQL container. Covers the admin boundary, slug stability
/// (the main risk, because each event has a public URL), the Draft / Published / Archived lifecycle,
/// public visibility, date and URL validation, and draft-only deletion.
/// </summary>
public sealed class AidxEventsCmsTests(AuthApiFactory factory) : IClassFixture<AuthApiFactory>
{
    private const string Route = "/api/v1/admin/aidx/events";

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

    private static AidxEventRequest Event(string title, string? slug = null, DateTimeOffset? starts = null, DateTimeOffset? ends = null,
        string? registrationUrl = null) =>
        new(title, slug, "Event description.", starts ?? DateTimeOffset.UtcNow.AddDays(10), ends, "Sydney", registrationUrl, "A speaker");

    private async Task<AidxIdResponse> CreateAsync(string admin, AidxEventRequest request)
    {
        var response = await SendAsync(HttpMethod.Post, Route, admin, request);
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        return (await response.Content.ReadFromJsonAsync<AidxIdResponse>(TestContext.Current.CancellationToken))!;
    }

    private async Task<AidxAdminEventDetailResponse> DetailAsync(string admin, Guid id)
    {
        var response = await SendAsync(HttpMethod.Get, $"{Route}/{id}", admin);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        return (await response.Content.ReadFromJsonAsync<AidxAdminEventDetailResponse>(TestContext.Current.CancellationToken))!;
    }

    private async Task PublishAsync(string admin, Guid id) =>
        (await SendAsync(HttpMethod.Post, $"{Route}/{id}/publish", admin)).StatusCode.Should().Be(HttpStatusCode.OK);

    // ----------------------------------------------------------- authorization ---

    [Fact]
    public async Task Every_event_route_follows_the_admin_boundary()
    {
        var applicant = await IssueAsync("Applicant");
        var recruiter = await IssueAsync("Recruiter");
        var admin = await AdminAsync();
        var created = await CreateAsync(admin, Event(Unique("Boundary event")));
        var body = Event(Unique("Boundary edit"));

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
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var created = await CreateAsync(admin, Event($"AIDX Research Showcase 2026 {suffix}"));
        var originalSlug = (await DetailAsync(admin, created.Id)).Slug;
        originalSlug.Should().Be($"aidx-research-showcase-2026-{suffix}", "the slug is derived from the title on create");
        await PublishAsync(admin, created.Id);

        var response = await SendAsync(HttpMethod.Put, $"{Route}/{created.Id}", admin,
            Event($"AIDX Research and Innovation Showcase 2026 {suffix}"));
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        (await DetailAsync(admin, created.Id)).Slug.Should().Be(originalSlug, "a title change must not rewrite the public URL");
        (await SendAsync(HttpMethod.Get, $"/api/v1/aidx/events/{originalSlug}", null)).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task An_explicit_slug_change_moves_the_public_url_and_the_old_one_stops_resolving()
    {
        var admin = await AdminAsync();
        var created = await CreateAsync(admin, Event(Unique("Moving event")));
        await PublishAsync(admin, created.Id);
        var oldSlug = (await DetailAsync(admin, created.Id)).Slug;
        var newSlug = Unique("moved-event").Replace(' ', '-').ToLowerInvariant();

        (await SendAsync(HttpMethod.Put, $"{Route}/{created.Id}", admin, Event(Unique("Moving event renamed"), newSlug)))
            .StatusCode.Should().Be(HttpStatusCode.OK);

        (await SendAsync(HttpMethod.Get, $"/api/v1/aidx/events/{newSlug}", null)).StatusCode.Should().Be(HttpStatusCode.OK);
        (await SendAsync(HttpMethod.Get, $"/api/v1/aidx/events/{oldSlug}", null)).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Duplicate_and_malformed_slugs_are_rejected()
    {
        var admin = await AdminAsync();
        var slug = Unique("event-dup").Replace(' ', '-').ToLowerInvariant();
        await CreateAsync(admin, Event(Unique("First event"), slug));

        (await SendAsync(HttpMethod.Post, Route, admin, Event(Unique("Second event"), slug))).StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await SendAsync(HttpMethod.Post, Route, admin, Event(Unique("Third event"), "Not A Slug!"))).StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    // --------------------------------------------------------------- lifecycle ---

    [Fact]
    public async Task Publish_then_archive_then_republish_follows_the_lifecycle()
    {
        var admin = await AdminAsync();
        var created = await CreateAsync(admin, Event(Unique("Lifecycle event")));
        var slug = (await DetailAsync(admin, created.Id)).Slug;

        (await SendAsync(HttpMethod.Get, $"/api/v1/aidx/events/{slug}", null)).StatusCode.Should().Be(HttpStatusCode.NotFound, "a draft is not public");
        await PublishAsync(admin, created.Id);
        (await SendAsync(HttpMethod.Get, $"/api/v1/aidx/events/{slug}", null)).StatusCode.Should().Be(HttpStatusCode.OK);

        (await SendAsync(HttpMethod.Post, $"{Route}/{created.Id}/archive", admin)).StatusCode.Should().Be(HttpStatusCode.OK);
        (await SendAsync(HttpMethod.Get, $"/api/v1/aidx/events/{slug}", null)).StatusCode.Should().Be(HttpStatusCode.NotFound, "an archived event is not public");

        await PublishAsync(admin, created.Id);
        (await SendAsync(HttpMethod.Get, $"/api/v1/aidx/events/{slug}", null)).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Invalid_transitions_are_rejected()
    {
        var admin = await AdminAsync();
        var draft = await CreateAsync(admin, Event(Unique("Draft transition")));
        var published = await CreateAsync(admin, Event(Unique("Published transition")));
        await PublishAsync(admin, published.Id);

        (await SendAsync(HttpMethod.Post, $"{Route}/{draft.Id}/archive", admin)).StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await SendAsync(HttpMethod.Post, $"{Route}/{published.Id}/publish", admin)).StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Only_drafts_can_be_deleted()
    {
        var admin = await AdminAsync();
        var draft = await CreateAsync(admin, Event(Unique("Deletable event")));
        var published = await CreateAsync(admin, Event(Unique("Protected event")));
        await PublishAsync(admin, published.Id);

        (await SendAsync(HttpMethod.Delete, $"{Route}/{draft.Id}", admin)).StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await SendAsync(HttpMethod.Delete, $"{Route}/{published.Id}", admin)).StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await DetailAsync(admin, published.Id)).Status.Should().Be("Published");
    }

    // -------------------------------------------------------------- validation ---

    [Fact]
    public async Task An_end_before_the_start_is_rejected()
    {
        var admin = await AdminAsync();
        var start = DateTimeOffset.UtcNow.AddDays(10);

        (await SendAsync(HttpMethod.Post, Route, admin, Event(Unique("Backwards event"), starts: start, ends: start.AddHours(-1))))
            .StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Unsafe_or_non_http_registration_links_are_rejected()
    {
        var admin = await AdminAsync();

        (await SendAsync(HttpMethod.Post, Route, admin, Event(Unique("Script link"), registrationUrl: "javascript:alert(1)")))
            .StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await SendAsync(HttpMethod.Post, Route, admin, Event(Unique("Data link"), registrationUrl: "data:text/html,hi")))
            .StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Unknown_events_return_404_on_read_update_and_delete()
    {
        var admin = await AdminAsync();
        var missing = Guid.NewGuid();

        (await SendAsync(HttpMethod.Get, $"{Route}/{missing}", admin)).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await SendAsync(HttpMethod.Put, $"{Route}/{missing}", admin, Event("Ghost"))).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await SendAsync(HttpMethod.Delete, $"{Route}/{missing}", admin)).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // ------------------------------------------------------------ list and public ---

    [Fact]
    public async Task Admin_list_filters_by_status_and_upcoming_and_rejects_unknown_status()
    {
        var admin = await AdminAsync();
        var keyword = Unique("Orbit").Split(' ')[1];
        var past = await CreateAsync(admin, Event($"{keyword} past", starts: DateTimeOffset.UtcNow.AddDays(-10)));
        var future = await CreateAsync(admin, Event($"{keyword} future", starts: DateTimeOffset.UtcNow.AddDays(10)));

        var upcoming = (await (await SendAsync(HttpMethod.Get, $"{Route}?q={keyword}&upcoming=true&pageSize=50", admin))
            .Content.ReadFromJsonAsync<AidxPageResponse<AidxAdminEventSummaryResponse>>(TestContext.Current.CancellationToken))!;
        upcoming.Items.Select(e => e.Id).Should().ContainSingle().Which.Should().Be(future.Id);

        var drafts = (await (await SendAsync(HttpMethod.Get, $"{Route}?q={keyword}&status=Draft&pageSize=50", admin))
            .Content.ReadFromJsonAsync<AidxPageResponse<AidxAdminEventSummaryResponse>>(TestContext.Current.CancellationToken))!;
        drafts.Items.Select(e => e.Id).Should().Contain(new[] { past.Id, future.Id });

        (await SendAsync(HttpMethod.Get, $"{Route}?status=Sideways", admin)).StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Public_listing_shows_published_events_and_hides_drafts_and_archived_ones()
    {
        var admin = await AdminAsync();
        var published = await CreateAsync(admin, Event(Unique("Public event")));
        await PublishAsync(admin, published.Id);
        var draft = await CreateAsync(admin, Event(Unique("Draft event")));
        var archived = await CreateAsync(admin, Event(Unique("Archived event")));
        await PublishAsync(admin, archived.Id);
        (await SendAsync(HttpMethod.Post, $"{Route}/{archived.Id}/archive", admin)).StatusCode.Should().Be(HttpStatusCode.OK);

        var listing = (await (await SendAsync(HttpMethod.Get, "/api/v1/aidx/events?pageSize=50", null))
            .Content.ReadFromJsonAsync<AidxPageResponse<AidxEventResponse>>(TestContext.Current.CancellationToken))!;
        var ids = listing.Items.Select(e => e.Id).ToList();

        ids.Should().Contain(published.Id);
        ids.Should().NotContain(draft.Id);
        ids.Should().NotContain(archived.Id);
    }

    [Fact]
    public async Task Public_event_responses_expose_no_image_key_or_account_fields()
    {
        var admin = await AdminAsync();
        var created = await CreateAsync(admin, Event(Unique("Leak check event")));
        await PublishAsync(admin, created.Id);
        var slug = (await DetailAsync(admin, created.Id)).Slug;

        var json = await (await SendAsync(HttpMethod.Get, $"/api/v1/aidx/events/{slug}", null))
            .Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        json.Should().NotContainAny("imageKey", "userId", "email", "status");
    }
}
