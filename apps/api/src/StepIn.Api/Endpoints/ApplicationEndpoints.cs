using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using StepIn.Api.Infrastructure;
using StepIn.Application.Common.Interfaces;
using StepIn.Domain.Aidx;
using StepIn.Domain.Applications;
using StepIn.Domain.Jobs;
using StepIn.Domain.Profiles;

namespace StepIn.Api.Endpoints;

/// <summary>
/// Candidate-side job applications. Ownership (<see cref="JobApplication.CandidateProfileId"/>)
/// is always resolved server-side from the authenticated principal's own
/// <see cref="CandidateProfile"/> — never from a client-supplied id — and every
/// single-item query scopes by both the route id and that profile id in the
/// same WHERE clause, so another candidate's application is indistinguishable
/// from one that doesn't exist (404, never 403). Applications are immutable
/// once submitted: there is no update or delete endpoint here, and none should
/// be added in this phase.
/// </summary>
public static class ApplicationEndpoints
{
    private const long MaxResumeSizeBytes = 5 * 1024 * 1024;

    private static readonly HashSet<string> AllowedResumeExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".pdf", ".doc", ".docx",
    };

    private static readonly HashSet<string> AllowedResumeContentTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "application/pdf",
        "application/msword",
        "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
    };

    public static IEndpointRouteBuilder MapApplicationEndpoints(this IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        var applications = app.MapGroup("/api/v1")
            .WithTags("Applications")
            .RequireAuthorization("RequireApplicant");

        applications.MapPost("/jobs/{jobId:guid}/applications", SubmitApplicationAsync)
            .WithName("SubmitApplication")
            .WithSummary("Apply to a published job with a resume.")
            .DisableAntiforgery();

        applications.MapGet("/jobs/{jobId:guid}/applications/mine", GetApplicationEligibilityAsync)
            .WithName("GetApplicationEligibility")
            .WithSummary("Whether the signed-in candidate has already applied to this job.");

        applications.MapGet("/applications", ListApplicationsAsync)
            .WithName("ListApplications")
            .WithSummary("The signed-in candidate's own applications.");

        applications.MapGet("/applications/{id:guid}", GetApplicationAsync)
            .WithName("GetApplication")
            .WithSummary("One of the signed-in candidate's own applications.");

        applications.MapGet("/applications/{id:guid}/resume", DownloadResumeAsync)
            .WithName("DownloadApplicationResume")
            .WithSummary("The resume attached to one of the signed-in candidate's own applications.");

        return app;
    }

    private static async Task<IResult> SubmitApplicationAsync(
        Guid jobId,
        IFormFile resume,
        [FromForm] string? coverLetter,
        ClaimsPrincipal principal,
        IApplicationDbContext db,
        IResumeStorage storage,
        CancellationToken cancellationToken)
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
                ["profile"] = ["Complete your candidate profile before applying."],
            });
        }

        // Public visibility is enforced in the query itself: a Draft or Unpublished job, or a
        // Research job whose AIDX project is not published, is 404, indistinguishable from a
        // job that doesn't exist.
        var job = await db.Jobs
            .Where(j => j.Id == jobId)
            .Where(AidxJobVisibility.IsPubliclyVisible)
            .FirstOrDefaultAsync(cancellationToken);
        if (job is null)
        {
            return Results.NotFound();
        }

        // Fast, friendly-error-message path only — the unique index below is
        // the actual guarantee against a race between two simultaneous requests.
        var alreadyApplied = await db.JobApplications
            .AnyAsync(a => a.CandidateProfileId == candidateProfile.Id && a.JobId == jobId, cancellationToken);
        if (alreadyApplied)
        {
            return Results.Conflict();
        }

        var errors = ValidateResume(resume);
        if (!string.IsNullOrWhiteSpace(coverLetter) && coverLetter.Length > 5000)
        {
            errors["coverLetter"] = ["Must be 5000 characters or fewer."];
        }

        if (errors.Count > 0)
        {
            return Results.ValidationProblem(errors);
        }

        var extension = Path.GetExtension(resume.FileName);
        string storageKey;

        await using (var stream = resume.OpenReadStream())
        {
            storageKey = await storage.SaveAsync(stream, extension, cancellationToken);
        }

        var application = new JobApplication
        {
            CandidateProfileId = candidateProfile.Id,
            JobId = jobId,
            CoverLetter = Trim(coverLetter),
            ResumeStorageKey = storageKey,
            ResumeOriginalFileName = Path.GetFileName(resume.FileName),
            ResumeContentType = resume.ContentType,
            ResumeSizeBytes = resume.Length,
        };

        db.Add(application);

        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            // Lost the race against the unique (CandidateProfileId, JobId) index —
            // clean up the file this request just saved, since nothing else will.
            await storage.DeleteAsync(storageKey, cancellationToken);
            return Results.Conflict();
        }

        application = await db.JobApplications
            .Include(a => a.Job).ThenInclude(j => j.Company)
            .FirstAsync(a => a.Id == application.Id, cancellationToken);

        return Results.Created($"/api/v1/applications/{application.Id}", ToResponse(application));
    }

    private static async Task<IResult> GetApplicationEligibilityAsync(
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
            return Results.Ok(new ApplicationEligibilityResponse(false, null));
        }

        var applicationId = await db.JobApplications
            .Where(a => a.CandidateProfileId == candidateProfile.Id && a.JobId == jobId)
            .Select(a => (Guid?)a.Id)
            .FirstOrDefaultAsync(cancellationToken);

        return Results.Ok(new ApplicationEligibilityResponse(applicationId is not null, applicationId));
    }

    private static async Task<IResult> ListApplicationsAsync(
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
            return Results.Ok(Array.Empty<ApplicationSummaryResponse>());
        }

        var summaries = await db.JobApplications
            .AsNoTracking()
            .Where(a => a.CandidateProfileId == candidateProfile.Id)
            .OrderByDescending(a => a.CreatedAt)
            .Select(a => new ApplicationSummaryResponse(
                a.Id,
                a.JobId,
                a.Job.Title,
                a.Job.Company.Name,
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

        var application = await FindOwnedApplicationAsync(db, user.Id, id, cancellationToken);

        return application is null ? Results.NotFound() : Results.Ok(ToResponse(application));
    }

    private static async Task<IResult> DownloadResumeAsync(
        Guid id, ClaimsPrincipal principal, IApplicationDbContext db, IResumeStorage storage, CancellationToken cancellationToken)
    {
        var user = await principal.GetCurrentUserAsync(db, cancellationToken);
        if (user is null)
        {
            return Results.Unauthorized();
        }

        var application = await FindOwnedApplicationAsync(db, user.Id, id, cancellationToken);
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

    private static Task<CandidateProfile?> FindCandidateProfileAsync(IApplicationDbContext db, Guid userId, CancellationToken cancellationToken) =>
        db.CandidateProfiles.FirstOrDefaultAsync(c => c.UserId == userId, cancellationToken);

    /// <summary>
    /// The single place an application is ever looked up for a single-item read:
    /// scoped by both the route id and the caller's own candidate profile id in
    /// the same query, so another candidate's application is indistinguishable
    /// from one that doesn't exist — both 404, never 403.
    /// </summary>
    private static async Task<JobApplication?> FindOwnedApplicationAsync(
        IApplicationDbContext db, Guid userId, Guid applicationId, CancellationToken cancellationToken)
    {
        var candidateProfile = await FindCandidateProfileAsync(db, userId, cancellationToken);
        if (candidateProfile is null)
        {
            return null;
        }

        return await db.JobApplications
            .Include(a => a.Job).ThenInclude(j => j.Company)
            .FirstOrDefaultAsync(a => a.Id == applicationId && a.CandidateProfileId == candidateProfile.Id, cancellationToken);
    }

    // StatusUpdatedAt reuses Entity.UpdatedAt rather than a dedicated column:
    // Status is the only field ever mutated after creation (see this class's
    // own type-level doc comment — applications are otherwise immutable), so
    // UpdatedAt is, in practice, exactly "when the status last changed". Falls
    // back to CreatedAt for an application whose status has never changed.
    private static ApplicationResponse ToResponse(JobApplication application) => new(
        application.Id,
        application.JobId,
        application.Job.Title,
        application.Job.Company.Name,
        application.Status.ToString(),
        application.CoverLetter,
        application.ResumeOriginalFileName,
        application.CreatedAt,
        application.UpdatedAt ?? application.CreatedAt);

    private static Dictionary<string, string[]> ValidateResume(IFormFile? resume)
    {
        var errors = new Dictionary<string, string[]>();

        if (resume is null || resume.Length == 0)
        {
            errors["resume"] = ["A resume file is required."];
            return errors;
        }

        if (resume.Length > MaxResumeSizeBytes)
        {
            errors["resume"] = ["Must be 5 MB or smaller."];
            return errors;
        }

        var extension = Path.GetExtension(resume.FileName);
        if (string.IsNullOrEmpty(extension) || !AllowedResumeExtensions.Contains(extension))
        {
            errors["resume"] = ["Must be a .pdf, .doc, or .docx file."];
            return errors;
        }

        if (string.IsNullOrEmpty(resume.ContentType) || !AllowedResumeContentTypes.Contains(resume.ContentType))
        {
            errors["resume"] = ["Must be a .pdf, .doc, or .docx file."];
        }

        return errors;
    }

    private static string? Trim(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
