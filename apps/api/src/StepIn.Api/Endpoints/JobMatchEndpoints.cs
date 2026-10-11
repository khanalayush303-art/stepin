using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using StepIn.Api.Infrastructure;
using StepIn.Application.Common.Interfaces;
using StepIn.Domain.Jobs;

namespace StepIn.Api.Endpoints;

/// <summary>
/// Transparent Job Match: explainable, deterministic matching between the signed-in candidate's
/// recorded skills (<see cref="StepIn.Domain.Profiles.CandidateProfile.Skills"/>) and published
/// Career jobs. Reuses the existing Job and CandidateProfile models — no new table, no AI
/// provider, no invented data. Research (AIDX) jobs are excluded, same as every other Career
/// discovery surface; a candidate applies or saves through the existing job detail, application,
/// and Saved Jobs endpoints — this route only ranks and explains, it has no write behavior at all.
/// </summary>
public static class JobMatchEndpoints
{
    private const int DefaultLimit = 20;
    private const int MaxLimit = 50;

    // Bounds the number of published jobs scored per request, so this stays one cheap,
    // predictable query regardless of how many jobs exist later. Scoring itself is in-memory
    // over this bounded set, not a per-job database round trip.
    private const int MaxJobsConsidered = 500;

    public static IEndpointRouteBuilder MapJobMatchEndpoints(this IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.MapGroup("/api/v1/jobs")
            .WithTags("Jobs")
            .RequireAuthorization("RequireApplicant")
            .MapGet("/matches", ListMatchesAsync)
            .WithName("ListJobMatches")
            .WithSummary("Published Career jobs, ranked by explainable skill coverage against the signed-in candidate's recorded skills.");

        return app;
    }

    private static async Task<IResult> ListMatchesAsync(
        int? limit,
        ClaimsPrincipal principal,
        IApplicationDbContext db,
        CancellationToken cancellationToken)
    {
        var user = await principal.GetCurrentUserAsync(db, cancellationToken);
        if (user is null)
        {
            return Results.Unauthorized();
        }

        if (limit is { } requested && requested < 1)
        {
            return Results.ValidationProblem(new Dictionary<string, string[]> { ["limit"] = ["Must be at least 1."] });
        }

        // No profile yet is not an error: every job's skills are simply all "not yet
        // demonstrated" until the candidate records some.
        var candidateProfile = await db.CandidateProfiles
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.UserId == user.Id, cancellationToken);
        var candidateSkills = candidateProfile?.Skills ?? [];

        var take = Math.Clamp(limit ?? DefaultLimit, 1, MaxLimit);

        var jobs = await db.Jobs
            .AsNoTracking()
            .Where(j => j.Status == JobStatus.Published && j.Category == JobCategory.Career)
            .OrderByDescending(j => j.PublishedAt)
            .Take(MaxJobsConsidered)
            .Select(j => new
            {
                j.Id,
                j.Title,
                CompanyName = j.Company.Name,
                j.EmploymentType,
                j.WorkplaceType,
                j.Location,
                j.Compensation,
                j.Skills,
                PublishedAt = j.PublishedAt!.Value,
            })
            .ToListAsync(cancellationToken);

        var ranked = jobs
            .Select(j => (Job: j, Result: JobMatchScorer.Score(candidateSkills, j.Skills)))
            .OrderByDescending(x => x.Result.SkillCoverage ?? -1)
            .ThenByDescending(x => x.Job.PublishedAt)
            .Take(take)
            .Select(x => new JobMatchResponse(
                x.Job.Id,
                x.Job.Title,
                x.Job.CompanyName,
                x.Job.EmploymentType.ToString(),
                x.Job.WorkplaceType.ToString(),
                x.Job.Location,
                x.Job.Compensation,
                x.Job.PublishedAt,
                x.Result.MatchedSkills,
                x.Result.SkillsNotYetDemonstrated,
                x.Result.SkillCoverage,
                x.Result.Quality.ToString()))
            .ToList();

        return Results.Ok(ranked);
    }
}
