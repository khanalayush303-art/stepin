using Microsoft.AspNetCore.Authentication.Google;
using Microsoft.AspNetCore.Identity;
using StepIn.Infrastructure.Identity;

namespace StepIn.Api.Infrastructure;

/// <summary>
/// Cookie/OAuth scheme configuration and role-based authorization policies.
///
/// The Identity *service* wiring (AddIdentity, stores, token providers) lives in
/// <c>StepIn.Infrastructure.DependencyInjection</c> beside the DbContext; this is
/// specifically the host-level authentication surface (cookie shape, external
/// providers), which belongs with the rest of the API's pipeline configuration.
/// </summary>
public static class AuthenticationSetup
{
    public static IServiceCollection AddStepInAuthentication(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(environment);

        services.ConfigureApplicationCookie(options =>
        {
            options.Cookie.Name = "stepin.auth";
            options.Cookie.HttpOnly = true;
            options.Cookie.SameSite = SameSiteMode.Lax;
            options.Cookie.SecurePolicy = environment.IsDevelopment()
                ? CookieSecurePolicy.SameAsRequest
                : CookieSecurePolicy.Always;
            options.ExpireTimeSpan = TimeSpan.FromDays(14);
            options.SlidingExpiration = true;

            // This is a JSON API: never redirect an unauthenticated/forbidden
            // request to an HTML login page.
            options.Events.OnRedirectToLogin = context =>
            {
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                return Task.CompletedTask;
            };
            options.Events.OnRedirectToAccessDenied = context =>
            {
                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                return Task.CompletedTask;
            };
        });

        var googleClientId = configuration["Google:ClientId"];
        var googleClientSecret = configuration["Google:ClientSecret"];

        if (!string.IsNullOrWhiteSpace(googleClientId) && !string.IsNullOrWhiteSpace(googleClientSecret))
        {
            services.AddAuthentication().AddGoogle(options =>
            {
                options.SignInScheme = IdentityConstants.ExternalScheme;
                options.ClientId = googleClientId;
                options.ClientSecret = googleClientSecret;
                options.CallbackPath = "/api/v1/auth/external/google/callback";
            });
        }

        services.AddAuthorizationBuilder()
            .AddPolicy("RequireApplicant", policy => policy.RequireRole(RoleNames.Applicant))
            .AddPolicy("RequireRecruiter", policy => policy.RequireRole(RoleNames.Recruiter))
            .AddPolicy("RequireAdmin", policy => policy.RequireRole(RoleNames.Admin));

        return services;
    }

    /// <summary>Whether a Google OAuth client is actually configured (vs. placeholder-only).</summary>
    public static bool IsGoogleConfigured(IConfiguration configuration) =>
        !string.IsNullOrWhiteSpace(configuration["Google:ClientId"]) &&
        !string.IsNullOrWhiteSpace(configuration["Google:ClientSecret"]);
}
