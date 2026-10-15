using Microsoft.EntityFrameworkCore;
using Serilog;
using StepIn.Api.Endpoints;
using StepIn.Api.Infrastructure;
using StepIn.Api.Operations;
using StepIn.Application;
using StepIn.Infrastructure;
using StepIn.Infrastructure.Persistence;

var builder = WebApplication.CreateBuilder(args);

// ---------------------------------------------------------------- logging ---
// Structured from the first line: the bootstrap logger catches failures that
// happen before the host is built, which is exactly when configuration is wrong.
Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .Enrich.FromLogContext()
    .WriteTo.Console()
    .CreateBootstrapLogger();

builder.Host.UseSerilog((context, services, configuration) => configuration
    .ReadFrom.Configuration(context.Configuration)
    .ReadFrom.Services(services)
    .Enrich.FromLogContext()
    .WriteTo.Console());

// --------------------------------------------------------------- services ---
builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddFrontendCors(builder.Configuration);
builder.Services.AddClerkAuthentication(builder.Configuration);

builder.Services.AddProblemDetails(options =>
{
    options.CustomizeProblemDetails = context =>
        context.ProblemDetails.Extensions["traceId"] = context.HttpContext.TraceIdentifier;
});
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();

builder.Services.AddOpenApi("v1", options => options.AddDocumentTransformer<BearerSecuritySchemeTransformer>());

builder.Services.AddRequestTimeouts();
builder.Services.AddResponseCompression();

// Narrowly scoped to the new public AIDX collaboration submission endpoints (enquiries, event
// registration) — never applied globally. Partitioned per remote IP (not AddFixedWindowLimiter's
// single global bucket, which would let one caller's traffic starve every other caller's quota)
// — these are the only two anonymous POST surfaces this feature adds. Testcontainers-backed
// integration tests share one process/IP across many independent test cases hitting these same
// routes, so the real 5/minute production limit is relaxed in the Testing environment only —
// same environment-gating convention as the test-only role-probe endpoints further down.
var aidxSubmissionPermitLimit = builder.Environment.IsEnvironment("Testing") ? 1000 : 5;
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddPolicy("aidx-public-submission", httpContext => System.Threading.RateLimiting.RateLimitPartition.GetFixedWindowLimiter(
        partitionKey: httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        factory: _ => new System.Threading.RateLimiting.FixedWindowRateLimiterOptions
        {
            PermitLimit = aidxSubmissionPermitLimit,
            Window = TimeSpan.FromMinutes(1),
            QueueLimit = 0,
        }));
});

var app = builder.Build();

// Logs only the normalized provider name actually selected by AddResumeStorage's own
// branching logic — never the raw config value, and never a URL, key, or bucket name. This is
// the one place that answers "which storage path is actually active?" from the logs alone,
// without printing anything secret.
Log.Information(
    "Resume storage provider: {Provider}",
    string.Equals(app.Configuration["Storage:Provider"], "Supabase", StringComparison.OrdinalIgnoreCase) ? "Supabase" : "Local");

// -------------------------------------------------------------- pipeline ---
app.UseExceptionHandler();
app.UseStatusCodePages();

app.UseSerilogRequestLogging(options =>
{
    // Health probes would otherwise dominate the log at one line per second.
    options.GetLevel = (httpContext, _, exception) =>
        exception is not null
            ? Serilog.Events.LogEventLevel.Error
            : httpContext.Request.Path.StartsWithSegments("/health")
                ? Serilog.Events.LogEventLevel.Verbose
                : Serilog.Events.LogEventLevel.Information;
});

if (!app.Environment.IsDevelopment())
{
    app.UseHsts();
}

app.UseResponseCompression();
app.UseCors(CorsSetup.PolicyName);
app.UseRequestTimeouts();

app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();

// OpenAPI is exposed in every environment: the frontend generates its client
// from it, and a document that only exists in Development drifts immediately.
app.MapOpenApi("/openapi/{documentName}.json");
app.UseSwaggerUI(options =>
{
    options.SwaggerEndpoint("/openapi/v1.json", "StepIn API v1");
    options.RoutePrefix = "swagger";
    options.DocumentTitle = "StepIn API";
});

app.MapHealthEndpoints();
app.MapMetaEndpoints();
app.MapAuthEndpoints();
app.MapProfileEndpoints();
app.MapJobEndpoints();
app.MapJobMatchEndpoints();
app.MapResearchMatchEndpoints();
app.MapCareerGoalEndpoints();
app.MapPortfolioEndpoints();
app.MapInterviewEndpoints();
app.MapExperienceEndpoints();
app.MapExperienceAdminEndpoints();
app.MapApplicationEndpoints();
app.MapRecruiterApplicationEndpoints();
app.MapSavedJobEndpoints();
app.MapAidxPublicEndpoints();
app.MapAidxAdminEndpoints();
app.MapAidxOpportunityAdminEndpoints();
app.MapAidxMembershipEndpoints();
app.MapAidxWorkflowEndpoints();
app.MapAidxWorkspaceEndpoints();
app.MapAidxSavedItemEndpoints();
app.MapAidxCollaborationEndpoints();
app.MapAidxEventRegistrationEndpoints();

app.MapGet("/", () => Results.Redirect("/swagger")).ExcludeFromDescription();

// Testing-only: proves the role policies actually gate access. Never present
// in Development/Production — there's nothing else in Phase 1 that uses them yet.
if (app.Environment.IsEnvironment("Testing"))
{
    app.MapGet("/api/v1/test/applicant-only", () => Results.Ok()).RequireAuthorization("RequireApplicant").ExcludeFromDescription();
    app.MapGet("/api/v1/test/recruiter-only", () => Results.Ok()).RequireAuthorization("RequireRecruiter").ExcludeFromDescription();
    app.MapGet("/api/v1/test/admin-only", () => Results.Ok()).RequireAuthorization("RequireAdmin").ExcludeFromDescription();
}

// ------------------------------------------------------- operator commands ---
// One-off commands run instead of the web host. They never start Kestrel, and starting the web
// host never runs one of these, so a command cannot happen as a side effect of booting the API.
if (args.Contains(AidxOwnerInitCommand.Name, StringComparer.OrdinalIgnoreCase))
{
    var exitCode = await AidxOwnerInitCommand.RunAsync(app.Services, Console.Out, CancellationToken.None);
    await Log.CloseAndFlushAsync();
    return exitCode;
}

if (args.Contains(MigrateDatabaseCommand.Name, StringComparer.OrdinalIgnoreCase))
{
    // The one argument after the command name is the operator-reviewed target migration id — see
    // MigrateDatabaseCommand's own doc comment for why this is mandatory rather than "just migrate
    // to latest".
    var commandIndex = Array.FindIndex(args, a => string.Equals(a, MigrateDatabaseCommand.Name, StringComparison.OrdinalIgnoreCase));
    var expectedFinalMigrationId = commandIndex + 1 < args.Length ? args[commandIndex + 1] : null;

    var exitCode = await MigrateDatabaseCommand.RunAsync(app.Services, Console.Out, expectedFinalMigrationId, CancellationToken.None);
    await Log.CloseAndFlushAsync();
    return exitCode;
}

if (args.Contains(AidxBootstrapManagerCommand.Name, StringComparer.OrdinalIgnoreCase))
{
    string? ReadFlag(string flag)
    {
        var index = Array.FindIndex(args, a => string.Equals(a, flag, StringComparison.OrdinalIgnoreCase));
        return index >= 0 && index + 1 < args.Length ? args[index + 1] : null;
    }

    var exitCode = await AidxBootstrapManagerCommand.RunAsync(
        app.Services, Console.Out, ReadFlag("--user-id"), ReadFlag("--clerk-user-id"), CancellationToken.None);
    await Log.CloseAndFlushAsync();
    return exitCode;
}

// --------------------------------------------------------------- startup ---
try
{
    await ApplyMigrationsAsync(app);
    Log.Information("StepIn API starting in {Environment}", app.Environment.EnvironmentName);
    await app.RunAsync();
}
catch (Exception ex) when (ex is not HostAbortedException)
{
    Log.Fatal(ex, "StepIn API terminated unexpectedly");
    throw;
}
finally
{
    await Log.CloseAndFlushAsync();
}

return 0;

// Applies pending migrations on boot in Development only.
//
// In any other environment migrations are a deploy step, not a startup side
// effect: two instances racing to migrate the same database is how you lose an
// afternoon. The method logs and continues if the database is not reachable, so
// the container still starts and /health/ready reports the real problem.
static async Task ApplyMigrationsAsync(WebApplication app)
{
    if (!app.Environment.IsDevelopment())
    {
        return;
    }

    using var scope = app.Services.CreateScope();
    var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

    try
    {
        var pending = (await context.Database.GetPendingMigrationsAsync()).ToArray();

        if (pending.Length == 0)
        {
            Log.Information("Database schema is up to date");
            return;
        }

        Log.Information("Applying {Count} pending migration(s): {Migrations}", pending.Length, pending);
        await context.Database.MigrateAsync();
    }
    catch (Exception ex)
    {
        Log.Warning(ex, "Could not reach PostgreSQL at startup; /health/ready will report unhealthy");
    }
}

/// <summary>Exposed so the xUnit integration tests can boot the real pipeline.</summary>
public partial class Program;
