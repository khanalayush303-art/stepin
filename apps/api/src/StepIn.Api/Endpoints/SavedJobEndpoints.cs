using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using StepIn.Api.Infrastructure;
using StepIn.Application.Common.Interfaces;
using StepIn.Domain.Applications;
using StepIn.Domain.Jobs;
using StepIn.Domain.Profiles;

namespace StepIn.Api.Endpoints;

/// <summary>
/// Candidate-side job bookmarks, independent of <see cref="JobApplication"/>.
/// Ownership (<see cref="SavedJob.CandidateProfileId"/>) is always resolved
/// server-side from the authenticated principal's own <see cref="CandidateProfile"/>
/// — never from a client-supplied id — and the unsave/list operations scope
/// by that profile id, so another candidate's saved jobs are indistinguishable
/// from ones that don't exist (404, never 403). Mirrors
/// <see cref="ApplicationEndpoints"/>'s ownership pattern exactly.
/// </summary>
public static class SavedJobEndpoints
{
    public static IEndpointRouteBuilder MapSavedJobEndpoints(this IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        var group = app.MapGroup("/api/v1").WithTags("SavedJobs").RequireAuthorization("RequireApplicant");

        group.MapPost("/jobs/{jobId:guid}/saved", SaveJobAsync)
            .WithName("SaveJob")
            .WithSummary("Bookmark a published job for the signed-in candidate.");

        group.MapDelete("/jobs/{jobId:guid}/saved", UnsaveJobAsync)
            .WithName("UnsaveJob")
            .WithSummary("Remove a bookmark for the signed-in candidate.");

        group.MapGet("/saved-jobs", ListSavedJobsAsync)
            .WithName("ListSavedJobs")
            .WithSummary("The signed-in candidate's own bookmarked jobs, newest saved first.");

        return app;
    }

    private static async Task<IResult> SaveJobAsync(
        Guid jobId, ClaimsPrincipal principal, IApplicationDbContext db, CancellationToken cancellationToken)
    {
        var user = await principal.GetCurrentUserAsync(db, cancellationToken);
        if (user is null)
        {
            return Results.Unauthorized();
        }

        var candidateProfile = await FindCandidateProfileAsync(db, user.Id, cancellationToken);
        if (candidateProfile is null)
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["profile"] = ["Complete your candidate profile before saving jobs."],
            });
        }

        // Published-only eligibility, same convention as SubmitApplicationAsync:
        // a Draft or Unpublished job is 404, indistinguishable from one that
        // doesn't exist at all.
        var job = await db.Jobs
            .FirstOrDefaultAsync(j => j.Id == jobId && j.Status == JobStatus.Published, cancellationToken);
        if (job is null)
        {
            return Results.NotFound();
        }

        // Fast, friendly-error-message path only — the unique index below is
        // the actual guarantee against a race between two simultaneous requests.
        var alreadySaved = await db.SavedJobs
            .AnyAsync(s => s.CandidateProfileId == candidateProfile.Id && s.JobId == jobId, cancellationToken);
        if (alreadySaved)
        {
            return Results.Conflict();
        }

        var savedJob = new SavedJob
        {
            CandidateProfileId = candidateProfile.Id,
            JobId = jobId,
        };

        db.Add(savedJob);

        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            // Lost the race against the unique (CandidateProfileId, JobId) index.
            return Results.Conflict();
        }

        savedJob = await db.SavedJobs
            .Include(s => s.Job).ThenInclude(j => j.Company)
            .FirstAsync(s => s.Id == savedJob.Id, cancellationToken);

        // No single-saved-job GET endpoint exists in this phase's scope, so
        // there is no real location to point a Location header at.
        return Results.Created((string?)null, ToResponse(savedJob));
    }

    private static async Task<IResult> UnsaveJobAsync(
        Guid jobId, ClaimsPrincipal principal, IApplicationDbContext db, CancellationToken cancellationToken)
    {
        var user = await principal.GetCurrentUserAsync(db, cancellationToken);
        if (user is null)
        {
            return Results.Unauthorized();
        }

        var candidateProfile = await FindCandidateProfileAsync(db, user.Id, cancellationToken);
        if (candidateProfile is null)
        {
            return Results.NotFound();
        }

        var savedJob = await db.SavedJobs
            .FirstOrDefaultAsync(s => s.JobId == jobId && s.CandidateProfileId == candidateProfile.Id, cancellationToken);
        if (savedJob is null)
        {
            return Results.NotFound();
        }

        db.RemoveRange([savedJob]);
        await db.SaveChangesAsync(cancellationToken);

        return Results.NoContent();
    }

    private static async Task<IResult> ListSavedJobsAsync(
        ClaimsPrincipal principal, IApplicationDbContext db, CancellationToken cancellationToken)
    {
        var user = await principal.GetCurrentUserAsync(db, cancellationToken);
        if (user is null)
        {
            return Results.Unauthorized();
        }

        var candidateProfile = await FindCandidateProfileAsync(db, user.Id, cancellationToken);
        if (candidateProfile is null)
        {
            return Results.Ok(Array.Empty<SavedJobResponse>());
        }

        // Jobs are never hard-deleted anywhere in this codebase (no MapDelete
        // on JobEndpoints), so the only lifecycle change to account for here
        // is a saved job's underlying Job later becoming Unpublished — it is
        // still returned, with its current JobStatus, rather than hidden or
        // silently shown as if still current.
        var summaries = await db.SavedJobs
            .AsNoTracking()
            .Where(s => s.CandidateProfileId == candidateProfile.Id)
            .OrderByDescending(s => s.CreatedAt)
            .Select(s => new SavedJobResponse(
                s.Id,
                s.CreatedAt,
                s.JobId,
                s.Job.Title,
                s.Job.Company.Name,
                s.Job.EmploymentType.ToString(),
                s.Job.WorkplaceType.ToString(),
                s.Job.Location,
                s.Job.Compensation,
                s.Job.Status.ToString()))
            .ToListAsync(cancellationToken);

        return Results.Ok(summaries);
    }

    // ----------------------------------------------------------------- shared ---

    private static Task<CandidateProfile?> FindCandidateProfileAsync(IApplicationDbContext db, Guid userId, CancellationToken cancellationToken) =>
        db.CandidateProfiles.FirstOrDefaultAsync(c => c.UserId == userId, cancellationToken);

    private static SavedJobResponse ToResponse(SavedJob savedJob) => new(
        savedJob.Id,
        savedJob.CreatedAt,
        savedJob.JobId,
        savedJob.Job.Title,
        savedJob.Job.Company.Name,
        savedJob.Job.EmploymentType.ToString(),
        savedJob.Job.WorkplaceType.ToString(),
        savedJob.Job.Location,
        savedJob.Job.Compensation,
        savedJob.Job.Status.ToString());
}
