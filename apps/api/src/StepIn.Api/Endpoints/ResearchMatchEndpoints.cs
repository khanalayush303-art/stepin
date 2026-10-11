using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using StepIn.Api.Infrastructure;
using StepIn.Application.Common.Interfaces;
using StepIn.Domain.Aidx;
using StepIn.Domain.Jobs;

namespace StepIn.Api.Endpoints;

/// <summary>
/// Research-to-Career Bridge: explainable, deterministic matching between the signed-in
/// candidate's recorded skills and published AIDX projects' recorded technologies. Reuses
/// <see cref="JobMatchScorer"/> as-is (the rule is identical — compare two skill-like string
/// collections) rather than reimplementing it, and reuses the existing <c>AidxProject</c> and
/// <c>Job</c> models — no new table, no AI provider.
///
/// A project is not an application target by itself. Where one or more published Research jobs
/// (<see cref="Job.Category"/> = Research) link back to the project via
/// <see cref="Job.AidxProjectId"/>, they are surfaced as <see cref="ResearchOpportunityLinkResponse"/>
/// so the candidate can tell a project worth reading about apart from one with an open role —
/// applying and saving still go entirely through the existing job detail, application, and Saved
/// Jobs endpoints. This endpoint has no write behavior at all.
/// </summary>
public static class ResearchMatchEndpoints
{
    private const int DefaultLimit = 20;
    private const int MaxLimit = 50;

    // Bounds the number of published projects scored per request, matching the same reasoning
    // JobMatchEndpoints uses for published jobs.
    private const int MaxProjectsConsidered = 500;

    public static IEndpointRouteBuilder MapResearchMatchEndpoints(this IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.MapGroup("/api/v1/aidx")
            .WithTags("AIDX")
            .RequireAuthorization("RequireApplicant")
            .MapGet("/research-matches", ListResearchMatchesAsync)
            .WithName("ListResearchMatches")
            .WithSummary("Published AIDX projects, ranked by explainable skill coverage against the signed-in candidate's recorded skills, with any linked open Research opportunities.");

        return app;
    }

    private static async Task<IResult> ListResearchMatchesAsync(
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

        // No profile yet is not an error, same as Transparent Job Match: every project's
        // technologies are simply all "not yet demonstrated" until the candidate records skills.
        var candidateProfile = await db.CandidateProfiles
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.UserId == user.Id, cancellationToken);
        var candidateSkills = candidateProfile?.Skills ?? [];

        var take = Math.Clamp(limit ?? DefaultLimit, 1, MaxLimit);

        var projects = await db.AidxProjects
            .AsNoTracking()
            .Where(p => p.Status == AidxContentStatus.Published)
            .OrderByDescending(p => p.Featured)
            .ThenByDescending(p => p.PublishedAt)
            .Take(MaxProjectsConsidered)
            .Select(p => new
            {
                p.Id,
                p.Title,
                p.Slug,
                p.ShortDescription,
                ResearchAreas = p.ResearchAreas.Select(r => r.ResearchArea.Name).ToList(),
                Technologies = p.Technologies.Select(t => t.Name).ToList(),
            })
            .ToListAsync(cancellationToken);

        var projectIds = projects.Select(p => p.Id).ToList();

        // Only an already-published Research job pointed at an already-published project ever
        // counts as "open" — the same rule AidxJobVisibility applies when a candidate opens the
        // job directly, so nothing here can surface a job that route would hide.
        var openOpportunities = await db.Jobs
            .AsNoTracking()
            .Where(j => j.Category == JobCategory.Research
                && j.Status == JobStatus.Published
                && j.AidxProjectId != null
                && projectIds.Contains(j.AidxProjectId.Value))
            .Select(j => new { j.Id, j.Title, ProjectId = j.AidxProjectId!.Value })
            .ToListAsync(cancellationToken);

        var opportunitiesByProject = openOpportunities
            .GroupBy(o => o.ProjectId)
            .ToDictionary(g => g.Key, g => (IReadOnlyList<ResearchOpportunityLinkResponse>)g
                .Select(o => new ResearchOpportunityLinkResponse(o.Id, o.Title))
                .ToList());

        var ranked = projects
            .Select(p => (Project: p, Result: JobMatchScorer.Score(candidateSkills, p.Technologies)))
            .OrderByDescending(x => x.Result.SkillCoverage ?? -1)
            .ThenByDescending(x => opportunitiesByProject.ContainsKey(x.Project.Id)) // an open opportunity nudges a tie upward
            .Take(take)
            .Select(x => new ResearchMatchResponse(
                x.Project.Id,
                x.Project.Title,
                x.Project.Slug,
                x.Project.ShortDescription,
                x.Project.ResearchAreas,
                x.Result.MatchedSkills,
                x.Result.SkillsNotYetDemonstrated,
                x.Result.SkillCoverage,
                x.Result.Quality.ToString(),
                opportunitiesByProject.TryGetValue(x.Project.Id, out var links) ? links : []))
            .ToList();

        return Results.Ok(ranked);
    }
}
