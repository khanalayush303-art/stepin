namespace StepIn.Api.Infrastructure;

/// <summary>
/// CORS for the Next.js frontend.
///
/// Origins are configuration, never a wildcard: credentials will be sent from
/// Phase 1 onward and <c>AllowAnyOrigin</c> plus credentials is rejected by the
/// browser anyway, so the shape is correct from the start.
/// </summary>
public static class CorsSetup
{
    public const string PolicyName = "StepInFrontend";

    public static IServiceCollection AddFrontendCors(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        var origins = configuration
            .GetSection("Cors:AllowedOrigins")
            .Get<string[]>() ?? ["http://localhost:3000"];

        services.AddCors(options =>
        {
            options.AddPolicy(PolicyName, policy => policy
                .WithOrigins(origins)
                .AllowAnyHeader()
                .AllowAnyMethod()
                .AllowCredentials()
                .SetPreflightMaxAge(TimeSpan.FromHours(1)));
        });

        return services;
    }
}
