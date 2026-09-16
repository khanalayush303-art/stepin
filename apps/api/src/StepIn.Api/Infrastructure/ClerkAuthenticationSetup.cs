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

        var authorizedParties = configuration.GetSection("Clerk:AuthorizedParties").Get<string[]>() ?? [];

        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.Authority = authority;
                options.TokenValidationParameters.ValidateAudience = false;

                options.Events = new JwtBearerEvents
                {
                    OnTokenValidated = context =>
                    {
                        // Clerk tokens carry no standard `aud`; `azp` (authorized party) is
                        // Clerk's documented substitute — reject tokens issued for a frontend
                        // origin this API wasn't configured to trust.
                        var azp = context.Principal?.FindFirstValue("azp");

                        if (authorizedParties.Length > 0 && (azp is null || !authorizedParties.Contains(azp)))
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
