using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace StepIn.Api.Infrastructure;

/// <summary>
/// Turns any unhandled exception into an RFC 9457 problem document.
///
/// The response never carries a stack trace or an exception message outside
/// Development — a client learns that the request failed and gets a trace id it
/// can quote, and the detail goes to the log instead.
/// </summary>
public sealed class GlobalExceptionHandler(
    IProblemDetailsService problemDetailsService,
    IHostEnvironment environment,
    ILogger<GlobalExceptionHandler> logger) : IExceptionHandler
{
    /// <summary>499 "client closed request" has no constant in ASP.NET Core.</summary>
    private const int StatusClientClosedRequest = 499;

    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(httpContext);
        ArgumentNullException.ThrowIfNull(exception);

        var traceId = httpContext.TraceIdentifier;

        logger.LogError(
            exception,
            "Unhandled exception for {Method} {Path} (trace {TraceId})",
            httpContext.Request.Method,
            httpContext.Request.Path,
            traceId);

        var (status, title) = exception switch
        {
            BadHttpRequestException => (StatusCodes.Status400BadRequest, "Malformed request"),
            OperationCanceledException => (StatusClientClosedRequest, "Request cancelled"),
            _ => (StatusCodes.Status500InternalServerError, "An unexpected error occurred"),
        };

        httpContext.Response.StatusCode = status;

        var problem = new ProblemDetails
        {
            Status = status,
            Title = title,
            Instance = $"{httpContext.Request.Method} {httpContext.Request.Path}",
            Detail = environment.IsDevelopment()
                ? exception.ToString()
                : "The request could not be completed. Quote the trace id if you contact support.",
        };
        problem.Extensions["traceId"] = traceId;

        return await problemDetailsService.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            Exception = exception,
            ProblemDetails = problem,
        });
    }
}
