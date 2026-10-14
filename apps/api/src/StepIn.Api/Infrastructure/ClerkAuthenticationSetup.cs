using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using StepIn.Domain.Common;

namespace StepIn.Api.Infrastructure;

/// <summary>
/// Trusts Clerk-issued access tokens via standard JWT Bearer + OIDC/JWKS
/// discovery against Clerk's Frontend API — no Clerk SDK or secret key needed
/// on this side, since JWKS is public. See
/// <see cref="ClerkUserSyncClaimsTransformation"/> for how a validated token
/// becomes an <c>ApplicationUser</c> and a role claim.
/// </summary>
public static class ClerkAuthenticationSetup
{
    public static IServiceCollection AddClerkAuthentication(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        var authority = configuration["Clerk:Authority"];

        if (string.IsNullOrWhiteSpace(authority))
        {
            throw new InvalidOperationException(
                "Clerk:Authority is not configured. Set it to your Clerk Frontend API domain, " +
                "e.g. https://your-app.clerk.accounts.dev (Clerk Dashboard → Configure → API Keys).");
        }

        var authorizedParties = (configuration.GetSection("Clerk:AuthorizedParties").Get<string[]>() ?? [])
            .Where(p => !string.IsNullOrWhiteSpace(p))
            .ToArray();

        if (authorizedParties.Length == 0)
        {
            // Fail startup, the same way a missing Clerk:Authority already does — not fail
            // *open* at request time. Without this, an unset/emptied AuthorizedParties list used
            // to make every token pass the azp check below regardless of its value, because
            // `authorizedParties.Length > 0 && ...` is false whenever the list is empty. A token
            // is never trusted on the strength of signature/issuer/lifetime alone; azp must
            // always be checked against a real, non-empty allow-list.
            throw new InvalidOperationException(
                "Clerk:AuthorizedParties is not configured. Set at least one trusted frontend origin " +
                "(e.g. https://your-app.vercel.app) via Clerk__AuthorizedParties__0 in the environment, " +
                "or the equivalent appsettings key. Tokens are never trusted without this.");
        }

        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.Authority = authority;

                // Clerk's default session token carries no standard `aud` claim at all — this is
                // the actual shape of a real Clerk token, not a shortcut taken to get auth
                // working. Enabling audience validation against a token format that never sets
                // `aud` would reject every request, not add security; `azp` (authorized party) is
                // Clerk's own documented substitute for exactly this purpose, enforced below
                // against a now-guaranteed-non-empty allow-list.
                options.TokenValidationParameters.ValidateAudience = false;

                options.Events = new JwtBearerEvents
                {
                    OnTokenValidated = context =>
                    {
                        var azp = context.Principal?.FindFirstValue("azp");

                        if (azp is null || !authorizedParties.Contains(azp))
                        {
                            context.Fail("Token was not issued for an authorized party.");
                        }

                        return Task.CompletedTask;
                    },
                };
            });

        services.AddAuthorizationBuilder()
            .AddPolicy("RequireApplicant", policy => policy.RequireRole(nameof(UserRole.Applicant)))
            .AddPolicy("RequireRecruiter", policy => policy.RequireRole(nameof(UserRole.Recruiter)))
            .AddPolicy("RequireAdmin", policy => policy.RequireRole(nameof(UserRole.Admin)));

        services.AddScoped<IClaimsTransformation, ClerkUserSyncClaimsTransformation>();

        return services;
    }
}
