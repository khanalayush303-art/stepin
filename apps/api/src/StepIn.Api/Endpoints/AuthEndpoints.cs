using System.Net;
using System.Net.Mail;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Google;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Options;
using StepIn.Api.Infrastructure;
using StepIn.Application.Common.Interfaces;
using StepIn.Infrastructure.Identity;

namespace StepIn.Api.Endpoints;

/// <summary>
/// Registration, sign-in, email verification, password reset and Google OAuth.
///
/// Built directly on UserManager/SignInManager/RoleManager rather than the
/// built-in MapIdentityApi — that endpoint set doesn't support the custom
/// fields (first/last name, role) or ProblemDetails response shape this app
/// needs, and it defaults to bearer tokens rather than the cookie session this
/// architecture is built around.
/// </summary>
public static class AuthEndpoints
{
    public const string RateLimitPolicyName = "AuthEndpoints";

    public static IEndpointRouteBuilder MapAuthEndpoints(this IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        var group = app.MapGroup("/api/v1/auth").WithTags("Auth");

        group.MapPost("/register", RegisterAsync)
            .AllowAnonymous()
            .RequireRateLimiting(RateLimitPolicyName)
            .WithName("Register")
            .WithSummary("Create an applicant or recruiter account and send a verification email.");

        group.MapPost("/login", LoginAsync)
            .AllowAnonymous()
            .RequireRateLimiting(RateLimitPolicyName)
            .WithName("Login")
            .WithSummary("Sign in with email and password, establishing the auth cookie.");

        group.MapPost("/logout", LogoutAsync)
            .RequireAuthorization()
            .AddEndpointFilter<RequireFetchHeaderFilter>()
            .WithName("Logout")
            .WithSummary("End the current session.");

        group.MapGet("/me", GetCurrentUserAsync)
            .RequireAuthorization()
            .WithName("GetCurrentUser")
            .WithSummary("The signed-in user's profile and role.");

        group.MapGet("/password-policy", GetPasswordPolicy)
            .AllowAnonymous()
            .WithName("GetPasswordPolicy")
            .WithSummary("The server-enforced password rules, so the frontend never hardcodes rules that could drift.");

        group.MapPost("/verify-email", VerifyEmailAsync)
            .AllowAnonymous()
            .WithName("VerifyEmail")
            .WithSummary("Confirm an email address using the token from the verification link.");

        group.MapPost("/resend-verification", ResendVerificationAsync)
            .AllowAnonymous()
            .RequireRateLimiting(RateLimitPolicyName)
            .WithName("ResendVerification")
            .WithSummary("Send a new verification link if the account exists and isn't verified yet.");

        group.MapPost("/forgot-password", ForgotPasswordAsync)
            .AllowAnonymous()
            .RequireRateLimiting(RateLimitPolicyName)
            .WithName("ForgotPassword")
            .WithSummary("Send a password reset link. Always responds the same way, regardless of whether the account exists.");

        group.MapPost("/reset-password", ResetPasswordAsync)
            .AllowAnonymous()
            .RequireRateLimiting(RateLimitPolicyName)
            .WithName("ResetPassword")
            .WithSummary("Set a new password using the token from the reset link.");

        group.MapPost("/account-setup", CompleteAccountSetupAsync)
            .RequireAuthorization()
            .AddEndpointFilter<RequireFetchHeaderFilter>()
            .WithName("CompleteAccountSetup")
            .WithSummary("Confirm the account's role after email verification (Applicant or Recruiter only).");

        group.MapGet("/external/google", ChallengeGoogleAsync)
            .AllowAnonymous()
            .WithName("ChallengeGoogle")
            .WithSummary("Start the Google OAuth flow. Navigate the browser here directly; do not fetch() it.");

        group.MapGet("/external/google/complete", CompleteGoogleSignInAsync)
            .AllowAnonymous()
            .WithName("CompleteGoogleSignIn")
            .WithSummary("Finishes Google sign-in after the OAuth callback and redirects into the app.");

        return app;
    }

    // ----------------------------------------------------------- register ---

    private static async Task<IResult> RegisterAsync(
        RegisterRequest request,
        UserManager<ApplicationUser> userManager,
        IEmailSender emailSender,
        IConfiguration configuration,
        CancellationToken cancellationToken)
    {
        var errors = new Dictionary<string, string[]>();

        if (!IsValidEmail(request.Email))
        {
            errors["email"] = ["Enter a valid email address."];
        }

        if (!string.Equals(request.Password, request.ConfirmPassword, StringComparison.Ordinal))
        {
            errors["confirmPassword"] = ["Passwords do not match."];
        }

        if (!TryNormalizeRole(request.Role, out var role))
        {
            errors["role"] = ["Select Applicant or Recruiter."];
        }

        if (string.IsNullOrWhiteSpace(request.FirstName))
        {
            errors["firstName"] = ["First name is required."];
        }

        if (string.IsNullOrWhiteSpace(request.LastName))
        {
            errors["lastName"] = ["Last name is required."];
        }

        if (errors.Count > 0)
        {
            return Results.ValidationProblem(errors);
        }

        if (await userManager.FindByEmailAsync(request.Email) is not null)
        {
            return Results.Problem(statusCode: StatusCodes.Status409Conflict, title: "An account with this email already exists.");
        }

        var user = new ApplicationUser
        {
            Id = Guid.CreateVersion7(),
            UserName = request.Email,
            Email = request.Email,
            FirstName = request.FirstName.Trim(),
            LastName = request.LastName.Trim(),
        };

        var createResult = await userManager.CreateAsync(user, request.Password);

        if (!createResult.Succeeded)
        {
            return Results.ValidationProblem(ToErrorDictionary(createResult.Errors));
        }

        await userManager.AddToRoleAsync(user, role!);
        await SendVerificationEmailAsync(user, userManager, emailSender, configuration, cancellationToken);

        return Results.Created((string?)null, new { message = "Account created. Check your email to verify your address." });
    }

    // --------------------------------------------------------------- login ---

    private static async Task<IResult> LoginAsync(
        LoginRequest request,
        SignInManager<ApplicationUser> signInManager,
        UserManager<ApplicationUser> userManager)
    {
        var user = await userManager.FindByEmailAsync(request.Email);

        if (user is null)
        {
            return InvalidCredentials();
        }

        var result = await signInManager.PasswordSignInAsync(user, request.Password, request.RememberMe, lockoutOnFailure: true);

        if (result.IsLockedOut)
        {
            return Results.Problem(
                statusCode: StatusCodes.Status423Locked,
                title: "This account is temporarily locked due to repeated failed sign-in attempts. Try again later.");
        }

        if (result.IsNotAllowed)
        {
            return Results.Problem(
                statusCode: StatusCodes.Status403Forbidden,
                title: "Please verify your email address before signing in.",
                extensions: new Dictionary<string, object?> { ["reason"] = "email_not_confirmed" });
        }

        if (!result.Succeeded)
        {
            return InvalidCredentials();
        }

        return Results.Ok(await BuildCurrentUserResponseAsync(user, userManager));

        static IResult InvalidCredentials() =>
            Results.Problem(statusCode: StatusCodes.Status401Unauthorized, title: "Invalid email or password.");
    }

    // -------------------------------------------------------------- logout ---

    private static async Task<IResult> LogoutAsync(SignInManager<ApplicationUser> signInManager)
    {
        await signInManager.SignOutAsync();
        return Results.Ok(new { message = "Signed out." });
    }

    // ------------------------------------------------------------------ me ---

    private static async Task<IResult> GetCurrentUserAsync(ClaimsPrincipal principal, UserManager<ApplicationUser> userManager)
    {
        var user = await userManager.GetUserAsync(principal);

        if (user is null)
        {
            return Results.Unauthorized();
        }

        return Results.Ok(await BuildCurrentUserResponseAsync(user, userManager));
    }

    private static IResult GetPasswordPolicy(IOptions<IdentityOptions> identityOptions)
    {
        var policy = identityOptions.Value.Password;

        return Results.Ok(new PasswordPolicyResponse(
            policy.RequiredLength,
            policy.RequireDigit,
            policy.RequireLowercase,
            policy.RequireUppercase,
            policy.RequireNonAlphanumeric));
    }

    // --------------------------------------------------------- verify email ---

    private static async Task<IResult> VerifyEmailAsync(VerifyEmailRequest request, UserManager<ApplicationUser> userManager)
    {
        var user = await userManager.FindByEmailAsync(request.Email);

        if (user is null)
        {
            return InvalidOrExpiredLink();
        }

        if (user.EmailConfirmed)
        {
            return Results.Ok(new { message = "Your email is already verified." });
        }

        if (!TryDecodeToken(request.Token, out var decodedToken))
        {
            return InvalidOrExpiredLink();
        }

        var result = await userManager.ConfirmEmailAsync(user, decodedToken);

        if (!result.Succeeded)
        {
            return InvalidOrExpiredLink();
        }

        // Rotating the security stamp invalidates this token (and any other
        // outstanding token for this user) so it cannot be replayed.
        await userManager.UpdateSecurityStampAsync(user);

        return Results.Ok(new { message = "Email verified." });

        static IResult InvalidOrExpiredLink() =>
            Results.Problem(statusCode: StatusCodes.Status400BadRequest, title: "This verification link is invalid or has expired.");
    }

    private static async Task<IResult> ResendVerificationAsync(
        ResendVerificationRequest request,
        UserManager<ApplicationUser> userManager,
        IEmailSender emailSender,
        IConfiguration configuration,
        CancellationToken cancellationToken)
    {
        var user = await userManager.FindByEmailAsync(request.Email);

        if (user is not null && !user.EmailConfirmed)
        {
            await SendVerificationEmailAsync(user, userManager, emailSender, configuration, cancellationToken);
        }

        return Results.Ok(new { message = "If an account exists for that email and needs verifying, a new link has been sent." });
    }

    // ------------------------------------------------------- password reset ---

    private static async Task<IResult> ForgotPasswordAsync(
        ForgotPasswordRequest request,
        UserManager<ApplicationUser> userManager,
        IEmailSender emailSender,
        IConfiguration configuration,
        CancellationToken cancellationToken)
    {
        var user = await userManager.FindByEmailAsync(request.Email);

        if (user is not null && user.EmailConfirmed)
        {
            var token = await userManager.GeneratePasswordResetTokenAsync(user);
            var link = BuildFrontendUrl(configuration, "/reset-password", ("email", user.Email!), ("token", EncodeToken(token)));

            await emailSender.SendAsync(
                user.Email!,
                "Reset your StepIn password",
                $"""<p>Hi {WebUtility.HtmlEncode(user.FirstName)},</p><p>Use the link below to choose a new password. It expires in 1 hour.</p><p><a href="{link}">Reset password</a></p><p>If you didn't request this, you can ignore this email.</p>""",
                cancellationToken);
        }

        return Results.Ok(new { message = "If an account exists for that email, you will receive password reset instructions." });
    }

    private static async Task<IResult> ResetPasswordAsync(ResetPasswordRequest request, UserManager<ApplicationUser> userManager)
    {
        if (!string.Equals(request.NewPassword, request.ConfirmPassword, StringComparison.Ordinal))
        {
            return Results.ValidationProblem(new Dictionary<string, string[]> { ["confirmPassword"] = ["Passwords do not match."] });
        }

        var user = await userManager.FindByEmailAsync(request.Email);

        if (user is null)
        {
            return InvalidOrExpiredLink();
        }

        if (!TryDecodeToken(request.Token, out var decodedToken))
        {
            return InvalidOrExpiredLink();
        }

        var result = await userManager.ResetPasswordAsync(user, decodedToken, request.NewPassword);

        if (!result.Succeeded)
        {
            if (result.Errors.Any(e => e.Code is "InvalidToken"))
            {
                return InvalidOrExpiredLink();
            }

            return Results.ValidationProblem(ToErrorDictionary(result.Errors));
        }

        // Belt-and-braces: guarantees this (and any other outstanding) reset
        // token for the user cannot be replayed even if ResetPasswordAsync's
        // own stamp rotation ever changes.
        await userManager.UpdateSecurityStampAsync(user);

        return Results.Ok(new { message = "Password updated. You can now sign in." });

        static IResult InvalidOrExpiredLink() =>
            Results.Problem(statusCode: StatusCodes.Status400BadRequest, title: "This reset link is invalid or has expired.");
    }

    // ------------------------------------------------------- account setup ---

    private static async Task<IResult> CompleteAccountSetupAsync(
        AccountSetupRequest request,
        ClaimsPrincipal principal,
        UserManager<ApplicationUser> userManager)
    {
        if (!TryNormalizeRole(request.Role, out var role))
        {
            return Results.ValidationProblem(new Dictionary<string, string[]> { ["role"] = ["Select Applicant or Recruiter."] });
        }

        var user = await userManager.GetUserAsync(principal);

        if (user is null)
        {
            return Results.Unauthorized();
        }

        var currentRoles = await userManager.GetRolesAsync(user);

        if (!currentRoles.Contains(role!))
        {
            await userManager.RemoveFromRolesAsync(user, currentRoles);
            await userManager.AddToRoleAsync(user, role!);
        }

        return Results.Ok(new { redirectTo = DashboardPathForRole(role!) });
    }

    // -------------------------------------------------------- google oauth ---

    private static IResult ChallengeGoogleAsync(string? returnUrl, IConfiguration configuration)
    {
        if (!AuthenticationSetup.IsGoogleConfigured(configuration))
        {
            return Results.Problem(statusCode: StatusCodes.Status501NotImplemented, title: "Google sign-in is not configured on this server.");
        }

        var redirectUri = "/api/v1/auth/external/google/complete" +
            (string.IsNullOrEmpty(returnUrl) ? "" : $"?returnUrl={Uri.EscapeDataString(returnUrl)}");

        var properties = new AuthenticationProperties { RedirectUri = redirectUri };

        return Results.Challenge(properties, [GoogleDefaults.AuthenticationScheme]);
    }

    private static async Task<IResult> CompleteGoogleSignInAsync(
        SignInManager<ApplicationUser> signInManager,
        UserManager<ApplicationUser> userManager,
        IConfiguration configuration)
    {
        var info = await signInManager.GetExternalLoginInfoAsync();

        if (info is null)
        {
            return Results.Redirect(BuildFrontendUrl(configuration, "/sign-in", ("error", "google_failed")));
        }

        var signInResult = await signInManager.ExternalLoginSignInAsync(
            info.LoginProvider, info.ProviderKey, isPersistent: true, bypassTwoFactor: true);

        if (signInResult.Succeeded)
        {
            var existingUser = await userManager.FindByLoginAsync(info.LoginProvider, info.ProviderKey);
            return Results.Redirect(BuildFrontendUrl(configuration, await LandingPathAsync(existingUser, userManager)));
        }

        // First time signing in with Google for this identity: find-or-create the local account.
        var email = info.Principal.FindFirstValue(ClaimTypes.Email);

        if (string.IsNullOrEmpty(email))
        {
            return Results.Redirect(BuildFrontendUrl(configuration, "/sign-in", ("error", "google_failed")));
        }

        var user = await userManager.FindByEmailAsync(email);

        if (user is null)
        {
            user = new ApplicationUser
            {
                Id = Guid.CreateVersion7(),
                UserName = email,
                Email = email,
                EmailConfirmed = true, // Google has already verified this address.
                FirstName = info.Principal.FindFirstValue(ClaimTypes.GivenName) ?? "New",
                LastName = info.Principal.FindFirstValue(ClaimTypes.Surname) ?? "User",
            };

            if (!(await userManager.CreateAsync(user)).Succeeded)
            {
                return Results.Redirect(BuildFrontendUrl(configuration, "/sign-in", ("error", "google_failed")));
            }
        }

        if (!(await userManager.AddLoginAsync(user, info)).Succeeded)
        {
            return Results.Redirect(BuildFrontendUrl(configuration, "/sign-in", ("error", "google_failed")));
        }

        await signInManager.SignInAsync(user, isPersistent: true);

        return Results.Redirect(BuildFrontendUrl(configuration, await LandingPathAsync(user, userManager)));

        static async Task<string> LandingPathAsync(ApplicationUser? user, UserManager<ApplicationUser> userManager)
        {
            if (user is null)
            {
                return "/sign-in";
            }

            var roles = await userManager.GetRolesAsync(user);
            return roles.Count > 0 ? DashboardPathForRole(roles[0]) : "/account-setup";
        }
    }

    // ------------------------------------------------------------- helpers ---

    private static async Task SendVerificationEmailAsync(
        ApplicationUser user,
        UserManager<ApplicationUser> userManager,
        IEmailSender emailSender,
        IConfiguration configuration,
        CancellationToken cancellationToken)
    {
        var token = await userManager.GenerateEmailConfirmationTokenAsync(user);
        var link = BuildFrontendUrl(configuration, "/verify-email", ("email", user.Email!), ("token", EncodeToken(token)));

        await emailSender.SendAsync(
            user.Email!,
            "Verify your StepIn email address",
            $"""<p>Hi {WebUtility.HtmlEncode(user.FirstName)},</p><p>Confirm your email address to finish setting up your StepIn account.</p><p><a href="{link}">Verify email address</a></p><p>This link expires in 24 hours.</p>""",
            cancellationToken);
    }

    private static string DashboardPathForRole(string role) => role switch
    {
        RoleNames.Recruiter => "/recruiter",
        RoleNames.Admin => "/admin",
        _ => "/dashboard",
    };

    private static string BuildFrontendUrl(IConfiguration configuration, string path, params (string Key, string Value)[] query)
    {
        var origin = configuration.GetSection("Cors:AllowedOrigins").Get<string[]>()?.FirstOrDefault() ?? "http://localhost:3000";
        var url = $"{origin.TrimEnd('/')}{path}";

        if (query.Length == 0)
        {
            return url;
        }

        var queryString = string.Join('&', query.Select(q => $"{Uri.EscapeDataString(q.Key)}={Uri.EscapeDataString(q.Value)}"));
        return $"{url}?{queryString}";
    }

    private static string EncodeToken(string token) => WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(token));

    private static bool TryDecodeToken(string encodedToken, out string decodedToken)
    {
        try
        {
            decodedToken = Encoding.UTF8.GetString(WebEncoders.Base64UrlDecode(encodedToken));
            return true;
        }
        catch (FormatException)
        {
            decodedToken = "";
            return false;
        }
    }

    private static bool IsValidEmail(string email)
    {
        if (string.IsNullOrWhiteSpace(email))
        {
            return false;
        }

        try
        {
            _ = new MailAddress(email);
            return true;
        }
        catch (FormatException)
        {
            return false;
        }
    }

    private static bool TryNormalizeRole(string? role, out string? normalized)
    {
        normalized = role?.Trim() switch
        {
            var r when string.Equals(r, RoleNames.Applicant, StringComparison.OrdinalIgnoreCase) => RoleNames.Applicant,
            var r when string.Equals(r, RoleNames.Recruiter, StringComparison.OrdinalIgnoreCase) => RoleNames.Recruiter,
            _ => null,
        };

        return normalized is not null;
    }

    private static Dictionary<string, string[]> ToErrorDictionary(IEnumerable<IdentityError> errors) =>
        new() { ["password"] = errors.Select(e => e.Description).ToArray() };

    private static async Task<CurrentUserResponse> BuildCurrentUserResponseAsync(ApplicationUser user, UserManager<ApplicationUser> userManager)
    {
        var roles = await userManager.GetRolesAsync(user);

        return new CurrentUserResponse(
            user.Id,
            user.Email!,
            user.FirstName,
            user.LastName,
            roles.FirstOrDefault() ?? string.Empty,
            user.EmailConfirmed,
            user.AccountStatus.ToString());
    }
}
