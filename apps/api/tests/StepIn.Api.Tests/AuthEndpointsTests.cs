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
/// Exercises the real Clerk-token-validation → user-sync → authorization
/// pipeline against a real, ephemeral PostgreSQL container
/// (<see cref="AuthApiFactory"/>) — <see cref="ApiFactory"/>'s unreachable
/// database only tests wiring, which isn't enough for persistence-backed
/// behavior like sync idempotency. Tokens are locally signed
/// (<see cref="AuthApiFactory.IssueToken"/>) rather than issued by a real
/// Clerk tenant, since there is no Clerk application configured in CI — the
/// real validation/claims-transformation code still runs, only the signing
/// key is substituted. Requires a running Docker daemon.
/// </summary>
public sealed class AuthEndpointsTests(AuthApiFactory factory) : IClassFixture<AuthApiFactory>
{
    private readonly AuthApiFactory _factory = factory;

    private HttpClient CreateClient() => _factory.CreateClient();

    private static void UseFetchHeader(HttpRequestMessage request) =>
        request.Headers.Add("X-Requested-With", "fetch");

    private static void Authorize(HttpRequestMessage request, string token) =>
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

    // ---------------------------------------------------------------- /me ---

    [Fact]
    public async Task Me_without_a_token_is_unauthorized()
    {
        var ct = TestContext.Current.CancellationToken;
        using var client = CreateClient();

        var response = await client.GetAsync("/api/v1/auth/me", ct);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Me_with_a_garbage_token_is_unauthorized()
    {
        var ct = TestContext.Current.CancellationToken;
        using var client = CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/auth/me");
        Authorize(request, "not-a-real-token");

        var response = await client.SendAsync(request, ct);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Me_with_an_expired_token_is_unauthorized()
    {
        var ct = TestContext.Current.CancellationToken;
        using var client = CreateClient();
        var token = AuthApiFactory.IssueToken(
            $"user_{Guid.NewGuid():N}", $"{Guid.NewGuid()}@example.com", expires: DateTime.UtcNow.AddMinutes(-5));
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/auth/me");
        Authorize(request, token);

        var response = await client.SendAsync(request, ct);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Me_with_a_token_from_an_unauthorized_party_is_rejected()
    {
        var ct = TestContext.Current.CancellationToken;
        using var client = CreateClient();
        var token = AuthApiFactory.IssueToken(
            $"user_{Guid.NewGuid():N}", $"{Guid.NewGuid()}@example.com", azp: "https://evil.example.com");
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/auth/me");
        Authorize(request, token);

        var response = await client.SendAsync(request, ct);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Me_with_a_valid_token_syncs_and_returns_the_application_user()
    {
        var ct = TestContext.Current.CancellationToken;
        using var client = CreateClient();
        var email = $"{Guid.NewGuid()}@example.com";
        var token = AuthApiFactory.IssueToken($"user_{Guid.NewGuid():N}", email, "Ada", "Lovelace");
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/auth/me");
        Authorize(request, token);

        var response = await client.SendAsync(request, ct);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<CurrentUserResponse>(ct);
        body!.Email.Should().Be(email);
        body.FirstName.Should().Be("Ada");
        body.LastName.Should().Be("Lovelace");
        body.Role.Should().BeNull();
        body.EmailVerified.Should().BeTrue();
    }

    [Fact]
    public async Task Repeated_authentication_for_the_same_clerk_user_does_not_create_duplicate_rows()
    {
        var ct = TestContext.Current.CancellationToken;
        using var client = CreateClient();
        var clerkUserId = $"user_{Guid.NewGuid():N}";
        var email = $"{Guid.NewGuid()}@example.com";

        async Task<Guid> FetchIdAsync()
        {
            var token = AuthApiFactory.IssueToken(clerkUserId, email);
            using var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/auth/me");
            Authorize(request, token);
            var response = await client.SendAsync(request, ct);
            var body = await response.Content.ReadFromJsonAsync<CurrentUserResponse>(ct);
            return body!.Id;
        }

        var firstId = await FetchIdAsync();
        var secondId = await FetchIdAsync();
        var thirdId = await FetchIdAsync();

        firstId.Should().Be(secondId).And.Be(thirdId);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var matchCount = await db.Users.CountAsync(u => u.ClerkUserId == clerkUserId, ct);
        matchCount.Should().Be(1);
    }

    [Fact]
    public async Task Two_different_clerk_users_never_share_an_application_user()
    {
        var ct = TestContext.Current.CancellationToken;
        using var client = CreateClient();

        async Task<Guid> RegisterAsync()
        {
            var token = AuthApiFactory.IssueToken($"user_{Guid.NewGuid():N}", $"{Guid.NewGuid()}@example.com");
            using var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/auth/me");
            Authorize(request, token);
            var response = await client.SendAsync(request, ct);
            var body = await response.Content.ReadFromJsonAsync<CurrentUserResponse>(ct);
            return body!.Id;
        }

        (await RegisterAsync()).Should().NotBe(await RegisterAsync());
    }

    // --------------------------------------------------------- account setup ---

    [Fact]
    public async Task Account_setup_rejects_an_invalid_role()
    {
        var ct = TestContext.Current.CancellationToken;
        using var client = CreateClient();
        var token = AuthApiFactory.IssueToken($"user_{Guid.NewGuid():N}", $"{Guid.NewGuid()}@example.com");
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/auth/account-setup")
        {
            Content = JsonContent.Create(new AccountSetupRequest("Admin")),
        };
        Authorize(request, token);
        UseFetchHeader(request);

        var response = await client.SendAsync(request, ct);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Theory]
    [InlineData("Applicant", "/dashboard")]
    [InlineData("Recruiter", "/recruiter")]
    public async Task Account_setup_sets_the_role_and_returns_the_right_dashboard(string role, string expectedRedirect)
    {
        var ct = TestContext.Current.CancellationToken;
        using var client = CreateClient();
        var clerkUserId = $"user_{Guid.NewGuid():N}";
        var email = $"{Guid.NewGuid()}@example.com";
        var token = AuthApiFactory.IssueToken(clerkUserId, email);

        using var setupRequest = new HttpRequestMessage(HttpMethod.Post, "/api/v1/auth/account-setup")
        {
            Content = JsonContent.Create(new AccountSetupRequest(role)),
        };
        Authorize(setupRequest, token);
        UseFetchHeader(setupRequest);
        var setupResponse = await client.SendAsync(setupRequest, ct);

        setupResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var setupBody = await setupResponse.Content.ReadFromJsonAsync<Dictionary<string, string>>(ct);
        setupBody!["redirectTo"].Should().Be(expectedRedirect);

        using var meRequest = new HttpRequestMessage(HttpMethod.Get, "/api/v1/auth/me");
        Authorize(meRequest, AuthApiFactory.IssueToken(clerkUserId, email));
        var meResponse = await client.SendAsync(meRequest, ct);
        var meBody = await meResponse.Content.ReadFromJsonAsync<CurrentUserResponse>(ct);
        meBody!.Role.Should().Be(role);
    }

    [Fact]
    public async Task Account_setup_without_a_token_is_unauthorized()
    {
        var ct = TestContext.Current.CancellationToken;
        using var client = CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/auth/account-setup")
        {
            Content = JsonContent.Create(new AccountSetupRequest("Applicant")),
        };
        UseFetchHeader(request);

        var response = await client.SendAsync(request, ct);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // ------------------------------------------------------- role policies ---

    [Fact]
    public async Task Role_policy_rejects_a_user_without_the_required_role()
    {
        var ct = TestContext.Current.CancellationToken;
        using var client = CreateClient();
        var token = await IssueTokenWithRoleAsync(UserRole.Applicant, ct);
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/test/recruiter-only");
        Authorize(request, token);

        var response = await client.SendAsync(request, ct);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Role_policy_allows_a_user_with_the_required_role()
    {
        var ct = TestContext.Current.CancellationToken;
        using var client = CreateClient();
        var token = await IssueTokenWithRoleAsync(UserRole.Recruiter, ct);
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/test/recruiter-only");
        Authorize(request, token);

        var response = await client.SendAsync(request, ct);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Role_policy_rejects_a_user_with_no_role_set_yet()
    {
        var ct = TestContext.Current.CancellationToken;
        using var client = CreateClient();
        var token = AuthApiFactory.IssueToken($"user_{Guid.NewGuid():N}", $"{Guid.NewGuid()}@example.com");
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/test/applicant-only");
        Authorize(request, token);

        var response = await client.SendAsync(request, ct);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    /// <summary>Completes account-setup for a fresh Clerk identity, then re-issues a token for the same identity.</summary>
    private async Task<string> IssueTokenWithRoleAsync(UserRole role, CancellationToken ct)
    {
        using var client = CreateClient();
        var clerkUserId = $"user_{Guid.NewGuid():N}";
        var email = $"{Guid.NewGuid()}@example.com";

        using var setupRequest = new HttpRequestMessage(HttpMethod.Post, "/api/v1/auth/account-setup")
        {
            Content = JsonContent.Create(new AccountSetupRequest(role.ToString())),
        };
        Authorize(setupRequest, AuthApiFactory.IssueToken(clerkUserId, email));
        UseFetchHeader(setupRequest);
        (await client.SendAsync(setupRequest, ct)).EnsureSuccessStatusCode();

        return AuthApiFactory.IssueToken(clerkUserId, email);
    }
}
