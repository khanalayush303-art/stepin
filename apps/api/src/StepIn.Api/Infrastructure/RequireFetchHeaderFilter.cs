namespace StepIn.Api.Infrastructure;

/// <summary>
/// Lightweight CSRF defense-in-depth for cookie-authenticated, state-changing
/// endpoints: a cross-site HTML form cannot attach a custom header, so its
/// absence is a strong signal the request did not come from the app's own
/// fetch-based client. This sits alongside SameSite=Lax and single-origin CORS,
/// not in place of them.
/// </summary>
public sealed class RequireFetchHeaderFilter : IEndpointFilter
{
    public ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(next);

        if (context.HttpContext.Request.Headers["X-Requested-With"] != "fetch")
        {
            return ValueTask.FromResult<object?>(Results.Problem(
                statusCode: StatusCodes.Status403Forbidden,
                title: "Missing required request header."));
        }

        return next(context);
    }
}
