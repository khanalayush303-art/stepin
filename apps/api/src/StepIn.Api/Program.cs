using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Serilog;
using StepIn.Api.Endpoints;
using StepIn.Api.Infrastructure;
using StepIn.Application;
using StepIn.Infrastructure;
using StepIn.Infrastructure.Identity;
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
builder.Services.AddStepInAuthentication(builder.Configuration, builder.Environment);

builder.Services.AddProblemDetails(options =>
{
    options.CustomizeProblemDetails = context =>
        context.ProblemDetails.Extensions["traceId"] = context.HttpContext.TraceIdentifier;
});
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();

builder.Services.AddOpenApi("v1");

builder.Services.AddRequestTimeouts();
builder.Services.AddResponseCompression();

builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddPolicy(AuthEndpoints.RateLimitPolicyName, httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 10,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0,
            }));
});

var app = builder.Build();

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
app.UseRateLimiter();

app.UseAuthentication();
app.UseAuthorization();

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

app.MapGet("/", () => Results.Redirect("/swagger")).ExcludeFromDescription();

// --------------------------------------------------------------- startup ---
try
{
    await ApplyMigrationsAsync(app);
    await SeedIdentityAsync(app);
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

// Seeds the three platform roles (and, opt-in only, one local dev admin) on
// boot. Development-only, for the same reason migrations are: in any other
// environment this is a deploy step, not a startup side effect.
static async Task SeedIdentityAsync(WebApplication app)
{
    if (!app.Environment.IsDevelopment())
    {
        return;
    }

    using var scope = app.Services.CreateScope();

    try
    {
        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole<Guid>>>();
        await RoleSeeder.SeedAsync(roleManager);

        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
        await DevAdminSeeder.SeedAsync(app.Environment, app.Configuration, userManager, logger);
    }
    catch (Exception ex)
    {
        Log.Warning(ex, "Could not seed Identity roles/dev admin at startup");
    }
}

/// <summary>Exposed so the xUnit integration tests can boot the real pipeline.</summary>
public partial class Program;
