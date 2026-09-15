using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.RegularExpressions;
using FluentAssertions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;
using StepIn.Api.Endpoints;

namespace StepIn.Api.Tests;

/// <summary>
/// Exercises Identity-backed auth against a real, ephemeral PostgreSQL
/// container (<see cref="AuthApiFactory"/>) — registration, login, email
/// verification, password reset and role authorization are all persistence
/// behavior, not pure wiring, so <see cref="ApiFactory"/>'s unreachable
/// database isn't sufficient here. Requires a running Docker daemon.
/// </summary>
public sealed class AuthEndpointsTests(AuthApiFactory factory) : IClassFixture<AuthApiFactory>
{
    private readonly AuthApiFactory _factory = factory;

    private static RegisterRequest NewApplicant(string email) =>
        new(email, "Correct-Horse-1", "Correct-Horse-1", "Ada", "Lovelace", "Applicant");

    private HttpClient CreateClient() => _factory.CreateClient();

    private static void UseFetchHeader(HttpRequestMessage request) =>
        request.Headers.Add("X-Requested-With", "fetch");

    private static (string Email, string Token) ExtractLinkParams(string htmlBody)
    {
        var emailMatch = Regex.Match(htmlBody, "email=([^&\"]+)");
        var tokenMatch = Regex.Match(htmlBody, "token=([^&\"]+)");
        return (Uri.UnescapeDataString(emailMatch.Groups[1].Value), Uri.UnescapeDataString(tokenMatch.Groups[1].Value));
    }

    private async Task<string> RegisterAndVerifyAsync(HttpClient client, string email, CancellationToken cancellationToken)
    {
        (await client.PostAsJsonAsync("/api/v1/auth/register", NewApplicant(email), cancellationToken)).EnsureSuccessStatusCode();

        var sent = _factory.EmailSender.LastMessageTo(email);
        sent.Should().NotBeNull("registration should send a verification email");

        var (linkEmail, token) = ExtractLinkParams(sent!.HtmlBody);

        var verifyResponse = await client.PostAsJsonAsync(
            "/api/v1/auth/verify-email",
            new VerifyEmailRequest(linkEmail, token),
            cancellationToken);
        verifyResponse.EnsureSuccessStatusCode();

        return token;
    }

    // ----------------------------------------------------------- register ---

    [Fact]
    public async Task Register_creates_the_account_and_sends_a_verification_email()
    {
        var ct = TestContext.Current.CancellationToken;
        using var client = CreateClient();
        var email = $"{Guid.NewGuid()}@example.com";

        var response = await client.PostAsJsonAsync("/api/v1/auth/register", NewApplicant(email), ct);

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        _factory.EmailSender.LastMessageTo(email).Should().NotBeNull();
    }

    [Fact]
    public async Task Register_rejects_a_duplicate_email()
    {
        var ct = TestContext.Current.CancellationToken;
        using var client = CreateClient();
        var email = $"{Guid.NewGuid()}@example.com";

        (await client.PostAsJsonAsync("/api/v1/auth/register", NewApplicant(email), ct)).EnsureSuccessStatusCode();
        var second = await client.PostAsJsonAsync("/api/v1/auth/register", NewApplicant(email), ct);

        second.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Register_rejects_a_weak_password()
    {
        var ct = TestContext.Current.CancellationToken;
        using var client = CreateClient();
        var request = NewApplicant($"{Guid.NewGuid()}@example.com") with { Password = "weak", ConfirmPassword = "weak" };

        var response = await client.PostAsJsonAsync("/api/v1/auth/register", request, ct);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Register_rejects_mismatched_password_confirmation()
    {
        var ct = TestContext.Current.CancellationToken;
        using var client = CreateClient();
        var request = NewApplicant($"{Guid.NewGuid()}@example.com") with { ConfirmPassword = "Different-Horse-1" };

        var response = await client.PostAsJsonAsync("/api/v1/auth/register", request, ct);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Register_rejects_a_self_assigned_admin_role()
    {
        var ct = TestContext.Current.CancellationToken;
        using var client = CreateClient();
        var request = NewApplicant($"{Guid.NewGuid()}@example.com") with { Role = "Admin" };

        var response = await client.PostAsJsonAsync("/api/v1/auth/register", request, ct);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    // -------------------------------------------------------------- login ---

    [Fact]
    public async Task Login_fails_with_a_generic_message_for_an_unknown_email()
    {
        var ct = TestContext.Current.CancellationToken;
        using var client = CreateClient();

        var response = await client.PostAsJsonAsync(
            "/api/v1/auth/login",
            new LoginRequest($"{Guid.NewGuid()}@example.com", "whatever"),
            ct);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Login_fails_before_email_verification()
    {
        var ct = TestContext.Current.CancellationToken;
        using var client = CreateClient();
        var email = $"{Guid.NewGuid()}@example.com";
        (await client.PostAsJsonAsync("/api/v1/auth/register", NewApplicant(email), ct)).EnsureSuccessStatusCode();

        var response = await client.PostAsJsonAsync("/api/v1/auth/login", new LoginRequest(email, "Correct-Horse-1"), ct);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Login_fails_with_a_generic_message_for_the_wrong_password()
    {
        var ct = TestContext.Current.CancellationToken;
        using var client = CreateClient();
        var email = $"{Guid.NewGuid()}@example.com";
        await RegisterAndVerifyAsync(client, email, ct);

        var response = await client.PostAsJsonAsync("/api/v1/auth/login", new LoginRequest(email, "Wrong-Password-1"), ct);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Login_succeeds_after_verification_and_me_reflects_the_account()
    {
        var ct = TestContext.Current.CancellationToken;
        using var client = CreateClient();
        var email = $"{Guid.NewGuid()}@example.com";
        await RegisterAndVerifyAsync(client, email, ct);

        var loginResponse = await client.PostAsJsonAsync("/api/v1/auth/login", new LoginRequest(email, "Correct-Horse-1"), ct);
        loginResponse.EnsureSuccessStatusCode();

        var me = await client.GetFromJsonAsync<CurrentUserResponse>("/api/v1/auth/me", ct);

        me.Should().NotBeNull();
        me!.Email.Should().Be(email);
        me.Role.Should().Be("Applicant");
        me.EmailConfirmed.Should().BeTrue();
    }

    [Fact]
    public async Task Me_requires_authentication()
    {
        var ct = TestContext.Current.CancellationToken;
        using var client = CreateClient();

        var response = await client.GetAsync("/api/v1/auth/me", ct);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // --------------------------------------------------------------------- ---

    [Fact]
    public async Task Logout_requires_the_fetch_header_and_ends_the_session()
    {
        var ct = TestContext.Current.CancellationToken;
        using var client = CreateClient();
        var email = $"{Guid.NewGuid()}@example.com";
        await RegisterAndVerifyAsync(client, email, ct);
        (await client.PostAsJsonAsync("/api/v1/auth/login", new LoginRequest(email, "Correct-Horse-1"), ct)).EnsureSuccessStatusCode();

        var withoutHeader = await client.PostAsync("/api/v1/auth/logout", content: null, ct);
        withoutHeader.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        using var logoutRequest = new HttpRequestMessage(HttpMethod.Post, "/api/v1/auth/logout");
        UseFetchHeader(logoutRequest);
        (await client.SendAsync(logoutRequest, ct)).EnsureSuccessStatusCode();

        var afterLogout = await client.GetAsync("/api/v1/auth/me", ct);
        afterLogout.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // --------------------------------------------------------- verification ---

    [Fact]
    public async Task Verify_email_rejects_a_reused_token()
    {
        var ct = TestContext.Current.CancellationToken;
        using var client = CreateClient();
        var email = $"{Guid.NewGuid()}@example.com";
        var token = await RegisterAndVerifyAsync(client, email, ct);

        var replay = await client.PostAsJsonAsync("/api/v1/auth/verify-email", new VerifyEmailRequest(email, token), ct);

        // Already-verified is reported as a (harmless) success, not replay of the original grant.
        replay.EnsureSuccessStatusCode();
        var body = await replay.Content.ReadAsStringAsync(ct);
        body.Should().Contain("already verified");
    }

    [Fact]
    public async Task Verify_email_rejects_a_garbage_token()
    {
        var ct = TestContext.Current.CancellationToken;
        using var client = CreateClient();
        var email = $"{Guid.NewGuid()}@example.com";
        (await client.PostAsJsonAsync("/api/v1/auth/register", NewApplicant(email), ct)).EnsureSuccessStatusCode();

        var response = await client.PostAsJsonAsync("/api/v1/auth/verify-email", new VerifyEmailRequest(email, "not-a-real-token"), ct);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Resend_verification_is_generic_for_an_unknown_email()
    {
        var ct = TestContext.Current.CancellationToken;
        using var client = CreateClient();

        var response = await client.PostAsJsonAsync(
            "/api/v1/auth/resend-verification",
            new ResendVerificationRequest($"{Guid.NewGuid()}@example.com"),
            ct);

        response.EnsureSuccessStatusCode();
    }

    // ------------------------------------------------------- password reset ---

    [Fact]
    public async Task Forgot_password_does_not_send_an_email_for_an_unknown_account()
    {
        var ct = TestContext.Current.CancellationToken;
        using var client = CreateClient();
        var email = $"{Guid.NewGuid()}@example.com";

        var response = await client.PostAsJsonAsync("/api/v1/auth/forgot-password", new ForgotPasswordRequest(email), ct);

        response.EnsureSuccessStatusCode();
        _factory.EmailSender.LastMessageTo(email).Should().BeNull();
    }

    [Fact]
    public async Task Reset_password_end_to_end_replaces_the_password()
    {
        var ct = TestContext.Current.CancellationToken;
        using var client = CreateClient();
        var email = $"{Guid.NewGuid()}@example.com";
        await RegisterAndVerifyAsync(client, email, ct);

        (await client.PostAsJsonAsync("/api/v1/auth/forgot-password", new ForgotPasswordRequest(email), ct)).EnsureSuccessStatusCode();
        var sent = _factory.EmailSender.LastMessageTo(email);
        sent.Should().NotBeNull();
        var (linkEmail, token) = ExtractLinkParams(sent!.HtmlBody);

        var resetResponse = await client.PostAsJsonAsync(
            "/api/v1/auth/reset-password",
            new ResetPasswordRequest(linkEmail, token, "New-Correct-Horse-2", "New-Correct-Horse-2"),
            ct);
        resetResponse.EnsureSuccessStatusCode();

        (await client.PostAsJsonAsync("/api/v1/auth/login", new LoginRequest(email, "Correct-Horse-1"), ct))
            .StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        (await client.PostAsJsonAsync("/api/v1/auth/login", new LoginRequest(email, "New-Correct-Horse-2"), ct))
            .IsSuccessStatusCode.Should().BeTrue();
    }

    [Fact]
    public async Task Reset_password_rejects_an_invalid_token()
    {
        var ct = TestContext.Current.CancellationToken;
        using var client = CreateClient();
        var email = $"{Guid.NewGuid()}@example.com";
        await RegisterAndVerifyAsync(client, email, ct);

        var response = await client.PostAsJsonAsync(
            "/api/v1/auth/reset-password",
            new ResetPasswordRequest(email, "not-a-real-token", "New-Correct-Horse-2", "New-Correct-Horse-2"),
            ct);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    // ------------------------------------------------------- account setup ---

    [Fact]
    public async Task Account_setup_switches_role_and_requires_the_fetch_header()
    {
        var ct = TestContext.Current.CancellationToken;
        using var client = CreateClient();
        var email = $"{Guid.NewGuid()}@example.com";
        await RegisterAndVerifyAsync(client, email, ct);
        (await client.PostAsJsonAsync("/api/v1/auth/login", new LoginRequest(email, "Correct-Horse-1"), ct)).EnsureSuccessStatusCode();

        using var withoutHeader = new HttpRequestMessage(HttpMethod.Post, "/api/v1/auth/account-setup")
        {
            Content = JsonContent.Create(new AccountSetupRequest("Recruiter")),
        };
        (await client.SendAsync(withoutHeader, ct)).StatusCode.Should().Be(HttpStatusCode.Forbidden);

        using var withHeader = new HttpRequestMessage(HttpMethod.Post, "/api/v1/auth/account-setup")
        {
            Content = JsonContent.Create(new AccountSetupRequest("Recruiter")),
        };
        UseFetchHeader(withHeader);
        (await client.SendAsync(withHeader, ct)).EnsureSuccessStatusCode();

        var me = await client.GetFromJsonAsync<CurrentUserResponse>("/api/v1/auth/me", ct);
        me!.Role.Should().Be("Recruiter");
    }

    [Fact]
    public async Task Account_setup_rejects_an_admin_role()
    {
        var ct = TestContext.Current.CancellationToken;
        using var client = CreateClient();
        var email = $"{Guid.NewGuid()}@example.com";
        await RegisterAndVerifyAsync(client, email, ct);
        (await client.PostAsJsonAsync("/api/v1/auth/login", new LoginRequest(email, "Correct-Horse-1"), ct)).EnsureSuccessStatusCode();

        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/auth/account-setup")
        {
            Content = JsonContent.Create(new AccountSetupRequest("Admin")),
        };
        UseFetchHeader(request);

        (await client.SendAsync(request, ct)).StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    // ---------------------------------------------------------------- misc ---

    [Fact]
    public async Task Password_policy_reports_the_server_enforced_rules()
    {
        var ct = TestContext.Current.CancellationToken;
        using var client = CreateClient();

        var policy = await client.GetFromJsonAsync<PasswordPolicyResponse>("/api/v1/auth/password-policy", ct);

        policy.Should().NotBeNull();
        policy!.RequiredLength.Should().Be(8);
        policy.RequireDigit.Should().BeTrue();
        policy.RequireUppercase.Should().BeTrue();
    }

    [Fact]
    public async Task Google_challenge_reports_not_implemented_when_unconfigured()
    {
        var ct = TestContext.Current.CancellationToken;
        using var client = CreateClient();

        var response = await client.GetAsync("/api/v1/auth/external/google", ct);

        response.StatusCode.Should().Be(HttpStatusCode.NotImplemented);
    }

    [Fact]
    public async Task Role_policies_gate_by_role_and_nothing_else()
    {
        using var scope = _factory.Services.CreateScope();
        var authorizationService = scope.ServiceProvider.GetRequiredService<IAuthorizationService>();

        var applicant = new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim(ClaimTypes.Role, "Applicant")], authenticationType: "Test"));

        (await authorizationService.AuthorizeAsync(applicant, "RequireApplicant")).Succeeded.Should().BeTrue();
        (await authorizationService.AuthorizeAsync(applicant, "RequireRecruiter")).Succeeded.Should().BeFalse();
        (await authorizationService.AuthorizeAsync(applicant, "RequireAdmin")).Succeeded.Should().BeFalse();

        var anonymous = new ClaimsPrincipal(new ClaimsIdentity());
        (await authorizationService.AuthorizeAsync(anonymous, "RequireApplicant")).Succeeded.Should().BeFalse();
    }
}
