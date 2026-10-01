using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using StepIn.Api.Infrastructure;
using StepIn.Application.Common.Interfaces;
using StepIn.Domain.Applications;
using StepIn.Domain.Profiles;

namespace StepIn.Api.Endpoints;

/// <summary>
/// Recruiter-side, read-only view of applications submitted to the
/// recruiter's own jobs. Ownership is always resolved as
/// <c>RecruiterProfile → owned Job → JobApplication</c> — deliberately NOT
/// <see cref="ApplicationEndpoints"/>'s candidate-scoped lookup, which proves
/// ownership via <see cref="JobApplication.CandidateProfileId"/>, the wrong
/// chain for a recruiter. Every single-item query scopes by both the route
/// id and a join back to the caller's own <see cref="RecruiterProfile"/> in
/// the same query, so an application behind a job the caller doesn't own is
/// indistinguishable from one that doesn't exist (404, never 403). Status
/// changes are explicitly out of scope here — see Phase 3.3.
/// </summary>
public static class RecruiterApplicationEndpoints
{
    public static IEndpointRouteBuilder MapRecruiterApplicationEndpoints(this IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        var group = app.MapGroup("/api/v1/recruiter")
            .WithTags("Applications")
            .RequireAuthorization("RequireRecruiter");

        group.MapGet("/jobs/{jobId:guid}/applications", ListApplicationsForJobAsync)
            .WithName("ListRecruiterApplicationsForJob")
            .WithSummary("Applications submitted to one of the signed-in recruiter's own jobs.");

        group.MapGet("/applications/{id:guid}", GetApplicationAsync)
            .WithName("GetRecruiterApplication")
            .WithSummary("One application belonging to one of the signed-in recruiter's own jobs.");

        group.MapGet("/applications/{id:guid}/resume", DownloadResumeAsync)
            .WithName("DownloadRecruiterApplicationResume")
            .WithSummary("The resume attached to an application on one of the signed-in recruiter's own jobs.");

        return app;
    }

    private static async Task<IResult> ListApplicationsForJobAsync(
        Guid jobId, ClaimsPrincipal principal, IApplicationDbContext db, CancellationToken cancellationToken)
    {
        var user = await principal.GetCurrentUserAsync(db, cancellationToken);
        if (user is null)
        {
            return Results.Unauthorized();
        }

        var recruiterProfile = await FindRecruiterProfileAsync(db, user.Id, cancellationToken);
        if (recruiterProfile is null)
        {
            return Results.NotFound();
        }

        // Ownership gate, resolved separately from the list query itself so a
        // job that doesn't exist and a job owned by another recruiter are both
        // 404 — distinct from "owned job, zero applications yet" (200, []).
        // Mirrors JobEndpoints.FindOwnedJobAsync's convention exactly.
        var jobOwned = await db.Jobs
            .AnyAsync(j => j.Id == jobId && j.RecruiterProfileId == recruiterProfile.Id, cancellationToken);
        if (!jobOwned)
        {
            return Results.NotFound();
        }

        // Single joined query — no N+1: JobApplications, CandidateProfiles and
        // Users each contribute exactly one join, regardless of row count.
        var summaries = await (
            from a in db.JobApplications.AsNoTracking()
            join c in db.CandidateProfiles on a.CandidateProfileId equals c.Id
            join u in db.Users on c.UserId equals u.Id
            where a.JobId == jobId
            orderby a.CreatedAt descending
            select new RecruiterApplicationSummaryResponse(
                a.Id,
                a.JobId,
                a.Job.Title,
                u.FirstName + " " + u.LastName,
                u.Email,
                a.Status.ToString(),
                a.ResumeOriginalFileName,
                a.CreatedAt))
            .ToListAsync(cancellationToken);

        return Results.Ok(summaries);
    }

    private static async Task<IResult> GetApplicationAsync(
        Guid id, ClaimsPrincipal principal, IApplicationDbContext db, CancellationToken cancellationToken)
    {
        var user = await principal.GetCurrentUserAsync(db, cancellationToken);
        if (user is null)
        {
            return Results.Unauthorized();
        }

        var application = await FindRecruiterOwnedApplicationAsync(db, user.Id, id, cancellationToken);
        if (application is null)
        {
            return Results.NotFound();
        }

        var applicant = await FindApplicantAsync(db, application.CandidateProfileId, cancellationToken);
        if (applicant is null)
        {
            // The candidate profile or user row is gone — a data-integrity
            // concern, not an authorization one; nothing coherent to show.
            return Results.NotFound();
        }

        return Results.Ok(ToResponse(application, applicant));
    }

    private static async Task<IResult> DownloadResumeAsync(
        Guid id, ClaimsPrincipal principal, IApplicationDbContext db, IResumeStorage storage, CancellationToken cancellationToken)
    {
        var user = await principal.GetCurrentUserAsync(db, cancellationToken);
        if (user is null)
        {
            return Results.Unauthorized();
        }

        var application = await FindRecruiterOwnedApplicationAsync(db, user.Id, id, cancellationToken);
        if (application is null)
        {
            return Results.NotFound();
        }

        var stream = await storage.OpenReadAsync(application.ResumeStorageKey, cancellationToken);
        if (stream is null)
        {
            return Results.NotFound();
        }

        return Results.File(stream, application.ResumeContentType, application.ResumeOriginalFileName);
    }

    // ----------------------------------------------------------------- shared ---

    private static Task<RecruiterProfile?> FindRecruiterProfileAsync(IApplicationDbContext db, Guid userId, CancellationToken cancellationToken) =>
        db.RecruiterProfiles.FirstOrDefaultAsync(r => r.UserId == userId, cancellationToken);

    /// <summary>
    /// The single place an application is ever looked up for a recruiter-side
    /// single-item read: ownership is proven by joining back to the caller's
    /// own <see cref="RecruiterProfile"/> through the application's
    /// <see cref="JobApplication.Job"/>, in the same query — never by
    /// application id alone, never by role alone, and deliberately never
    /// <see cref="ApplicationEndpoints"/>'s candidate-scoped lookup. An
    /// application behind a job the caller doesn't own is indistinguishable
    /// from one that doesn't exist — both 404, never 403.
    /// </summary>
    private static async Task<JobApplication?> FindRecruiterOwnedApplicationAsync(
        IApplicationDbContext db, Guid userId, Guid applicationId, CancellationToken cancellationToken)
    {
        var recruiterProfile = await FindRecruiterProfileAsync(db, userId, cancellationToken);
        if (recruiterProfile is null)
        {
            return null;
        }

        return await db.JobApplications
            .Include(a => a.Job)
            .FirstOrDefaultAsync(a => a.Id == applicationId && a.Job.RecruiterProfileId == recruiterProfile.Id, cancellationToken);
    }

    private static Task<ApplicantInfo?> FindApplicantAsync(IApplicationDbContext db, Guid candidateProfileId, CancellationToken cancellationToken) =>
        db.CandidateProfiles
            .Where(c => c.Id == candidateProfileId)
            .Join(db.Users, c => c.UserId, u => u.Id, (c, u) => new ApplicantInfo(u.FirstName, u.LastName, u.Email))
            .FirstOrDefaultAsync(cancellationToken);

    private static RecruiterApplicationResponse ToResponse(JobApplication application, ApplicantInfo applicant) => new(
        application.Id,
        application.JobId,
        application.Job.Title,
        $"{applicant.FirstName} {applicant.LastName}",
        applicant.Email,
        application.Status.ToString(),
        application.CoverLetter,
        application.ResumeOriginalFileName,
        application.CreatedAt);

    private sealed record ApplicantInfo(string FirstName, string LastName, string Email);
}
