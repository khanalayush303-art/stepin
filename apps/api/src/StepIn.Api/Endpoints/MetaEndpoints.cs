using System.Reflection;

namespace StepIn.Api.Endpoints;

/// <summary>
/// Non-business endpoints that exist from Phase 0 so deployment, monitoring and
/// the frontend's connectivity check all have something real to talk to.
/// </summary>
public static class MetaEndpoints
{
    public static IEndpointRouteBuilder MapMetaEndpoints(this IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        var group = app.MapGroup("/api/v1/meta").WithTags("Meta");

        group.MapGet("/", (IHostEnvironment env) => Results.Ok(new
        {
            name = "StepIn API",
            phase = 0,
            environment = env.EnvironmentName,
            version = Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "0.1.0",
            utcNow = DateTimeOffset.UtcNow,
        }))
        .WithName("GetApiMeta")
        .WithSummary("Service identity and build metadata.");

        return app;
    }
}
