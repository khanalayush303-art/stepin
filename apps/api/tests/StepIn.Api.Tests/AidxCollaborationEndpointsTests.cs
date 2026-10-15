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
/// Milestone 4 — collaboration enquiries, event registration (capacity locking, hashed
/// cancellation tokens), and saved items.
/// </summary>
public sealed class AidxCollaborationEndpointsTests(AuthApiFactory factory) : IClassFixture<AuthApiFactory>
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

    private async Task<AidxProject> CreatePublishedProjectAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var project = new AidxProject
        {
            Title = "P", Slug = $"p-{Guid.NewGuid():N}", ShortDescription = "s", Description = "d",
            Status = AidxContentStatus.Published,
        };
        db.AidxProjects.Add(project);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        return project;
    }

    private async Task<AidxEvent> CreatePublishedEventAsync(int? capacity = null)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var evt = new AidxEvent
        {
            Title = "E", Slug = $"e-{Guid.NewGuid():N}", Description = "d",
            StartsAt = DateTimeOffset.UtcNow.AddDays(5), Status = AidxContentStatus.Published, Capacity = capacity,
        };
        db.AidxEvents.Add(evt);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        return evt;
    }

    [Fact]
    public async Task Submitting_a_collaboration_enquiry_against_a_published_project_succeeds()
    {
        var project = await CreatePublishedProjectAsync();

        var response = await SendAsync(HttpMethod.Post, $"/api/v1/aidx/projects/{project.Id}/enquiries", null,
            new SubmitCollaborationEnquiryRequest("Jane Doe", "jane@example.com", "I'd love to collaborate."));

        response.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    [Fact]
    public async Task An_enquiry_against_an_unpublished_or_unknown_project_is_404()
    {
        var response = await SendAsync(HttpMethod.Post, $"/api/v1/aidx/projects/{Guid.NewGuid()}/enquiries", null,
            new SubmitCollaborationEnquiryRequest("Jane", "jane@example.com", "Hi"));

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Only_the_projects_owner_or_a_reviewer_can_read_its_enquiry_inbox()
    {
        var (ownerToken, ownerUserId) = await NewUserAsync();
        var project = await CreatePublishedProjectAsync();
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var membership = new AidxMembership { UserId = ownerUserId, Role = AidxLabRole.Contributor, Status = AidxMembershipStatus.Active, GrantedByUserId = ownerUserId };
            db.AidxMemberships.Add(membership);
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
            await db.AidxProjects.Where(p => p.Id == project.Id)
                .ExecuteUpdateAsync(s => s.SetProperty(p => p.CreatedByMembershipId, membership.Id), TestContext.Current.CancellationToken);
        }
        await SendAsync(HttpMethod.Post, $"/api/v1/aidx/projects/{project.Id}/enquiries", null,
            new SubmitCollaborationEnquiryRequest("Jane", "jane@example.com", "Hi"));

        var ownerRead = await SendAsync(HttpMethod.Get, $"/api/v1/aidx/projects/{project.Id}/enquiries", ownerToken);
        ownerRead.StatusCode.Should().Be(HttpStatusCode.OK);

        var (strangerToken, _) = await NewUserAsync();
        var strangerRead = await SendAsync(HttpMethod.Get, $"/api/v1/aidx/projects/{project.Id}/enquiries", strangerToken);
        strangerRead.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Anonymous_registration_returns_a_cancellation_token_and_cancellation_requires_it()
    {
        var evt = await CreatePublishedEventAsync();

        var register = await SendAsync(HttpMethod.Post, $"/api/v1/aidx/events/{evt.Id}/registrations", null,
            new RegisterForAidxEventRequest("Jane Doe", "jane@example.com"));
        register.StatusCode.Should().Be(HttpStatusCode.Created);
        var registered = await register.Content.ReadFromJsonAsync<AidxEventRegistrationResponse>(TestContext.Current.CancellationToken);
        registered!.CancellationToken.Should().NotBeNullOrEmpty();

        var cancelWithoutToken = await SendAsync(HttpMethod.Post, $"/api/v1/aidx/events/{evt.Id}/registrations/cancel", null,
            new CancelAidxEventRegistrationRequest(null));
        cancelWithoutToken.StatusCode.Should().Be(HttpStatusCode.NotFound, "email alone is never sufficient to cancel");

        var cancelWithWrongToken = await SendAsync(HttpMethod.Post, $"/api/v1/aidx/events/{evt.Id}/registrations/cancel", null,
            new CancelAidxEventRegistrationRequest("not-the-real-token"));
        cancelWithWrongToken.StatusCode.Should().Be(HttpStatusCode.NotFound);

        var cancelWithRealToken = await SendAsync(HttpMethod.Post, $"/api/v1/aidx/events/{evt.Id}/registrations/cancel", null,
            new CancelAidxEventRegistrationRequest(registered.CancellationToken));
        cancelWithRealToken.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Registering_twice_with_the_same_email_does_not_re_issue_a_token()
    {
        var evt = await CreatePublishedEventAsync();
        await SendAsync(HttpMethod.Post, $"/api/v1/aidx/events/{evt.Id}/registrations", null,
            new RegisterForAidxEventRequest("Jane", "dup@example.com"));

        var second = await SendAsync(HttpMethod.Post, $"/api/v1/aidx/events/{evt.Id}/registrations", null,
            new RegisterForAidxEventRequest("Jane", "dup@example.com"));

        second.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task The_cancellation_token_is_never_persisted_in_plaintext()
    {
        var evt = await CreatePublishedEventAsync();
        var register = await SendAsync(HttpMethod.Post, $"/api/v1/aidx/events/{evt.Id}/registrations", null,
            new RegisterForAidxEventRequest("Jane", "plaintext-check@example.com"));
        var registered = await register.Content.ReadFromJsonAsync<AidxEventRegistrationResponse>(TestContext.Current.CancellationToken);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var row = await db.AidxEventRegistrations.SingleAsync(r => r.Id == registered!.Id, TestContext.Current.CancellationToken);

        row.CancellationTokenHash.Should().NotBe(registered!.CancellationToken);
        row.CancellationTokenHash.Should().NotContain(registered.CancellationToken!);
    }

    [Fact]
    public async Task Concurrent_registrations_against_a_capacity_limited_event_never_overbook_it()
    {
        // The "aidx-public-submission" rate limit this endpoint is also protected by is relaxed
        // in the Testing environment (see Program.cs) specifically so a real production safety
        // control doesn't produce a false failure signal for the capacity race actually under
        // test here.
        var evt = await CreatePublishedEventAsync(capacity: 3);

        var attempts = Enumerable.Range(0, 6)
            .Select(i => SendAsync(HttpMethod.Post, $"/api/v1/aidx/events/{evt.Id}/registrations", null,
                new RegisterForAidxEventRequest($"Guest {i}", $"guest{i}-{Guid.NewGuid():N}@example.com")));
        var results = await Task.WhenAll(attempts);

        results.Count(r => r.StatusCode == HttpStatusCode.Created).Should().Be(3, "capacity is exactly 3");
        results.Count(r => r.StatusCode == HttpStatusCode.Conflict).Should().Be(3, "the rest must cleanly report the event is full, not overbook it");

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var actualCount = await db.AidxEventRegistrations
            .CountAsync(r => r.EventId == evt.Id && r.Status == AidxEventRegistrationStatus.Registered, TestContext.Current.CancellationToken);
        actualCount.Should().Be(3);
    }

    [Fact]
    public async Task Saving_and_unsaving_a_published_project_works_and_duplicates_conflict()
    {
        var (token, _) = await NewUserAsync();
        var project = await CreatePublishedProjectAsync();

        var save = await SendAsync(HttpMethod.Post, $"/api/v1/aidx/project/{project.Id}/saved", token);
        save.StatusCode.Should().Be(HttpStatusCode.Created);

        var duplicate = await SendAsync(HttpMethod.Post, $"/api/v1/aidx/project/{project.Id}/saved", token);
        duplicate.StatusCode.Should().Be(HttpStatusCode.Conflict);

        var unsave = await SendAsync(HttpMethod.Delete, $"/api/v1/aidx/project/{project.Id}/saved", token);
        unsave.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task Saving_an_unpublished_or_unknown_project_is_404_not_403()
    {
        var (token, _) = await NewUserAsync();

        var response = await SendAsync(HttpMethod.Post, $"/api/v1/aidx/project/{Guid.NewGuid()}/saved", token);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
