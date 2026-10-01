using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using StepIn.Api.Infrastructure;
using StepIn.Application.Common.Interfaces;
using StepIn.Domain.Applications;
using StepIn.Domain.Profiles;

namespace StepIn.Api.Endpoints;

/// <summary>
/// Recruiter-side view of, and status control over, applications submitted
/// to the recruiter's own jobs. Ownership is always resolved as
/// <c>RecruiterProfile → owned Job → JobApplication</c> — deliberately NOT
/// <see cref="ApplicationEndpoints"/>'s candidate-scoped lookup, which proves
/// ownership via <see cref="JobApplication.CandidateProfileId"/>, the wrong
/// chain for a recruiter. Every single-item query (read or status update)
/// scopes by both the route id and a join back to the caller's own
/// <see cref="RecruiterProfile"/> in the same query, so an application
/// behind a job the caller doesn't own is indistinguishable from one that
/// doesn't exist (404, never 403). The candidate-facing side
/// (<see cref="ApplicationEndpoints"/>) has no write path for status at
/// all — only this recruiter-owned-job chain can ever change it.
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

        group.MapPut("/applications/{id:guid}/status", UpdateStatusAsync)
            .WithName("UpdateRecruiterApplicationStatus")
            .WithSummary("Change the status of an application on one of the signed-in recruiter's own jobs.");

        return app;
    }

    private static async Task<IResult> ListApplicationsForJobAsync(
        Guid jobId,
        string? status,
        string? search,
        string? sort,
        ClaimsPrincipal principal,
        IApplicationDbContext db,
        CancellationToken cancellationToken)
    {
        var user = await principal.GetCurrentUserAsync(db, cancellationToken);
        if (user is null)
        {
            return Results.Unauthorized();
        }

        // Pure input-format validation — touches no data, so doing it before
        // the ownership gate below leaks nothing about whether the job exists
        // or is owned by this recruiter.
        var errors = ValidateListFilters(status, sort, out var parsedStatus, out var oldestFirst);
        if (errors.Count > 0)
        {
            return Results.ValidationProblem(errors);
        }

        var recruiterProfile = await FindRecruiterProfileAsync(db, user.Id, cancellationToken);
        if (recruiterProfile is null)
        {
            return Results.NotFound();
        }

        // Ownership gate, resolved separately from the list query itself so a
        // job that doesn't exist and a job owned by another recruiter are both
        // 404 — distinct from "owned job, zero applications yet" (200, []).
        // Mirrors JobEndpoints.FindOwnedJobAsync's convention exactly. This
        // runs before any status/search/sort filter is applied below, so no
        // combination of query parameters can ever surface a job — or its
        // applications — this recruiter doesn't own.
        var jobOwned = await db.Jobs
            .AnyAsync(j => j.Id == jobId && j.RecruiterProfileId == recruiterProfile.Id, cancellationToken);
        if (!jobOwned)
        {
            return Results.NotFound();
        }

        // Single joined query — no N+1: JobApplications, CandidateProfiles and
        // Users each contribute exactly one join, regardless of row count.
        var query =
            from a in db.JobApplications.AsNoTracking()
            join c in db.CandidateProfiles on a.CandidateProfileId equals c.Id
            join u in db.Users on c.UserId equals u.Id
            where a.JobId == jobId
            select new { Application = a, u.FirstName, u.LastName, u.Email };

        if (parsedStatus is not null)
        {
            query = query.Where(x => x.Application.Status == parsedStatus);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            query = query.Where(x =>
                EF.Functions.ILike(x.FirstName, $"%{search}%") ||
                EF.Functions.ILike(x.LastName, $"%{search}%") ||
                EF.Functions.ILike(x.Email, $"%{search}%"));
        }

        // Default (sort omitted, or sort=newest) is CreatedAt descending —
        // byte-for-byte the same ordering the endpoint used before this phase.
        query = oldestFirst
            ? query.OrderBy(x => x.Application.CreatedAt)
            : query.OrderByDescending(x => x.Application.CreatedAt);

        var summaries = await query
            .Select(x => new RecruiterApplicationSummaryResponse(
                x.Application.Id,
                x.Application.JobId,
                x.Application.Job.Title,
                x.FirstName + " " + x.LastName,
                x.Email,
                x.Application.Status.ToString(),
                x.Application.ResumeOriginalFileName,
                x.Application.CreatedAt))
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

    private static async Task<IResult> UpdateStatusAsync(
        Guid id, UpdateApplicationStatusRequest request, ClaimsPrincipal principal, IApplicationDbContext db, CancellationToken cancellationToken)
    {
        var user = await principal.GetCurrentUserAsync(db, cancellationToken);
        if (user is null)
        {
            return Results.Unauthorized();
        }

        // Ownership-scoped lookup is tracked (no AsNoTracking), so mutating
        // Status below and calling SaveChangesAsync is enough — no separate
        // Update() call needed.
        var application = await FindRecruiterOwnedApplicationAsync(db, user.Id, id, cancellationToken);
        if (application is null)
        {
            return Results.NotFound();
        }

        if (!Enum.TryParse<JobApplicationStatus>(request.Status, ignoreCase: true, out var status) || !Enum.IsDefined(status))
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["status"] = ["Not a valid application status."],
            });
        }

        application.Status = status;
        await db.SaveChangesAsync(cancellationToken);

        var applicant = await FindApplicantAsync(db, application.CandidateProfileId, cancellationToken);
        if (applicant is null)
        {
            return Results.NotFound();
        }

        return Results.Ok(ToResponse(application, applicant));
    }

    // ----------------------------------------------------------------- shared ---

    /// <summary>
    /// Validates and parses the list endpoint's optional status/sort query
    /// parameters. Pure format validation — same dictionary-of-errors
    /// convention as <see cref="JobEndpoints.ValidatePublicFilters"/> and
    /// <see cref="UpdateStatusAsync"/>'s own status parsing — and touches no
    /// data, so it is safe to run before the ownership gate.
    /// </summary>
    private static Dictionary<string, string[]> ValidateListFilters(
        string? status, string? sort, out JobApplicationStatus? parsedStatus, out bool oldestFirst)
    {
        var errors = new Dictionary<string, string[]>();
        parsedStatus = null;
        oldestFirst = false;

        if (!string.IsNullOrWhiteSpace(status))
        {
            if (!Enum.TryParse<JobApplicationStatus>(status, ignoreCase: true, out var value) || !Enum.IsDefined(value))
            {
                errors["status"] = ["Not a valid application status."];
            }
            else
            {
                parsedStatus = value;
            }
        }

        if (!string.IsNullOrWhiteSpace(sort))
        {
            if (string.Equals(sort, "newest", StringComparison.OrdinalIgnoreCase))
            {
                oldestFirst = false;
            }
            else if (string.Equals(sort, "oldest", StringComparison.OrdinalIgnoreCase))
            {
                oldestFirst = true;
            }
            else
            {
                errors["sort"] = ["Must be 'newest' or 'oldest'."];
            }
        }

        return errors;
    }

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

    // See ApplicationEndpoints.ToResponse for why Entity.UpdatedAt is an
    // accurate stand-in for "status last changed": Status is the only field
    // ever mutated on a JobApplication after creation.
    private static RecruiterApplicationResponse ToResponse(JobApplication application, ApplicantInfo applicant) => new(
        application.Id,
        application.JobId,
        application.Job.Title,
        $"{applicant.FirstName} {applicant.LastName}",
        applicant.Email,
        application.Status.ToString(),
        application.CoverLetter,
        application.ResumeOriginalFileName,
        application.CreatedAt,
        application.UpdatedAt ?? application.CreatedAt);

    private sealed record ApplicantInfo(string FirstName, string LastName, string Email);
}
