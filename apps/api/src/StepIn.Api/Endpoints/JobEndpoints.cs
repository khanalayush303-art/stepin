using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using StepIn.Api.Infrastructure;
using StepIn.Application.Common.Interfaces;
using StepIn.Domain.Jobs;
using StepIn.Domain.Profiles;

namespace StepIn.Api.Endpoints;

/// <summary>
/// Recruiter-owned job postings. Ownership (<see cref="Job.RecruiterProfileId"/>)
/// and company (<see cref="Job.CompanyId"/>) are always resolved server-side from
/// the authenticated principal's own <see cref="RecruiterProfile"/> — never from a
/// client-supplied id. <see cref="CreateJobRequest"/>/<see cref="UpdateJobRequest"/>
/// structurally have no such field, so there is nothing for a client to override.
/// </summary>
public static class JobEndpoints
{
    public static IEndpointRouteBuilder MapJobEndpoints(this IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        var jobs = app.MapGroup("/api/v1/recruiter/jobs")
            .WithTags("Jobs")
            .RequireAuthorization("RequireRecruiter");

        jobs.MapGet("/", ListJobsAsync).WithName("ListJobs").WithSummary("The signed-in recruiter's own jobs.");
        jobs.MapGet("/{id:guid}", GetJobAsync).WithName("GetJob").WithSummary("One of the signed-in recruiter's own jobs.");
        jobs.MapPost("/", CreateJobAsync).WithName("CreateJob").WithSummary("Create a job as a draft.");
        jobs.MapPut("/{id:guid}", UpdateJobAsync).WithName("UpdateJob").WithSummary("Update one of the signed-in recruiter's own jobs.");
        jobs.MapPost("/{id:guid}/publish", PublishJobAsync).WithName("PublishJob").WithSummary("Publish a draft or unpublished job.");
        jobs.MapPost("/{id:guid}/unpublish", UnpublishJobAsync).WithName("UnpublishJob").WithSummary("Take a published job down.");

        // Candidate/public job discovery — deliberately no RequireAuthorization
        // (see the type-level doc comment) and, just as deliberately, no
        // MapPost/MapPut/MapPatch/MapDelete anywhere in this group: it is
        // read-only by construction, not by convention.
        var publicJobs = app.MapGroup("/api/v1/jobs").WithTags("Jobs");

        publicJobs.MapGet("/", ListPublicJobsAsync).WithName("ListPublicJobs").WithSummary("Published jobs, optionally filtered.");
        publicJobs.MapGet("/{id:guid}", GetPublicJobAsync).WithName("GetPublicJob").WithSummary("A single published job.");

        return app;
    }

    private static async Task<IResult> ListJobsAsync(ClaimsPrincipal principal, IApplicationDbContext db, CancellationToken cancellationToken)
    {
        var user = await principal.GetCurrentUserAsync(db, cancellationToken);
        if (user is null)
        {
            return Results.Unauthorized();
        }

        var recruiterProfile = await FindRecruiterProfileAsync(db, user.Id, cancellationToken);
        if (recruiterProfile is null)
        {
            return Results.Ok(Array.Empty<JobSummaryResponse>());
        }

        var summaries = await db.Jobs
            .Include(j => j.Company)
            .Where(j => j.RecruiterProfileId == recruiterProfile.Id)
            .OrderByDescending(j => j.CreatedAt)
            .Select(j => ToSummary(j))
            .ToListAsync(cancellationToken);

        return Results.Ok(summaries);
    }

    private static async Task<IResult> GetJobAsync(
        Guid id, ClaimsPrincipal principal, IApplicationDbContext db, CancellationToken cancellationToken)
    {
        var user = await principal.GetCurrentUserAsync(db, cancellationToken);
        if (user is null)
        {
            return Results.Unauthorized();
        }

        var job = await FindOwnedJobAsync(db, user.Id, id, cancellationToken);

        return job is null ? Results.NotFound() : Results.Ok(ToResponse(job));
    }

    private static async Task<IResult> CreateJobAsync(
        CreateJobRequest request, ClaimsPrincipal principal, IApplicationDbContext db, CancellationToken cancellationToken)
    {
        var user = await principal.GetCurrentUserAsync(db, cancellationToken);
        if (user is null)
        {
            return Results.Unauthorized();
        }

        var recruiterProfile = await FindRecruiterProfileAsync(db, user.Id, cancellationToken);

        if (recruiterProfile is null)
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["profile"] = ["Complete your recruiter profile before creating a job."],
            });
        }

        if (recruiterProfile.CompanyId is null)
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["company"] = ["Set up your company profile before creating a job."],
            });
        }

        var errors = ValidateJob(request.Title, request.Description, request.EmploymentType, request.WorkplaceType, request.Location, request.Compensation);
        if (errors.Count > 0)
        {
            return Results.ValidationProblem(errors);
        }

        Enum.TryParse<EmploymentType>(request.EmploymentType, ignoreCase: true, out var employmentType);
        Enum.TryParse<WorkplaceType>(request.WorkplaceType, ignoreCase: true, out var workplaceType);

        var job = new Job
        {
            // Ownership and company are resolved from the server-side recruiter
            // profile, not from the request — CreateJobRequest has no such field.
            RecruiterProfileId = recruiterProfile.Id,
            CompanyId = recruiterProfile.CompanyId.Value,
            Title = request.Title.Trim(),
            Description = request.Description.Trim(),
            EmploymentType = employmentType,
            WorkplaceType = workplaceType,
            Location = request.Location.Trim(),
            Compensation = Trim(request.Compensation),
            Skills = NormalizeSkills(request.Skills),
        };

        db.Add(job);
        await db.SaveChangesAsync(cancellationToken);

        // Reload with the Company navigation populated for the response.
        job = await db.Jobs.Include(j => j.Company).FirstAsync(j => j.Id == job.Id, cancellationToken);

        return Results.Created($"/api/v1/recruiter/jobs/{job.Id}", ToResponse(job));
    }

    private static async Task<IResult> UpdateJobAsync(
        Guid id, UpdateJobRequest request, ClaimsPrincipal principal, IApplicationDbContext db, CancellationToken cancellationToken)
    {
        var user = await principal.GetCurrentUserAsync(db, cancellationToken);
        if (user is null)
        {
            return Results.Unauthorized();
        }

        var job = await FindOwnedJobAsync(db, user.Id, id, cancellationToken);
        if (job is null)
        {
            return Results.NotFound();
        }

        var errors = ValidateJob(request.Title, request.Description, request.EmploymentType, request.WorkplaceType, request.Location, request.Compensation);
        if (errors.Count > 0)
        {
            return Results.ValidationProblem(errors);
        }

        Enum.TryParse<EmploymentType>(request.EmploymentType, ignoreCase: true, out var employmentType);
        Enum.TryParse<WorkplaceType>(request.WorkplaceType, ignoreCase: true, out var workplaceType);

        // Only editable fields are touched here — RecruiterProfileId, CompanyId and
        // Status are never written from the request, whatever the client sends.
        job.Title = request.Title.Trim();
        job.Description = request.Description.Trim();
        job.EmploymentType = employmentType;
        job.WorkplaceType = workplaceType;
        job.Location = request.Location.Trim();
        job.Compensation = Trim(request.Compensation);
        job.Skills = NormalizeSkills(request.Skills);

        await db.SaveChangesAsync(cancellationToken);

        return Results.Ok(ToResponse(job));
    }

    private static async Task<IResult> PublishJobAsync(
        Guid id, ClaimsPrincipal principal, IApplicationDbContext db, IDateTimeProvider clock, CancellationToken cancellationToken)
    {
        var user = await principal.GetCurrentUserAsync(db, cancellationToken);
        if (user is null)
        {
            return Results.Unauthorized();
        }

        var job = await FindOwnedJobAsync(db, user.Id, id, cancellationToken);
        if (job is null)
        {
            return Results.NotFound();
        }

        if (job.Status == JobStatus.Published)
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["status"] = ["Job is already published."],
            });
        }

        job.Status = JobStatus.Published;
        job.PublishedAt = clock.UtcNow;
        await db.SaveChangesAsync(cancellationToken);

        return Results.Ok(ToResponse(job));
    }

    private static async Task<IResult> UnpublishJobAsync(
        Guid id, ClaimsPrincipal principal, IApplicationDbContext db, CancellationToken cancellationToken)
    {
        var user = await principal.GetCurrentUserAsync(db, cancellationToken);
        if (user is null)
        {
            return Results.Unauthorized();
        }

        var job = await FindOwnedJobAsync(db, user.Id, id, cancellationToken);
        if (job is null)
        {
            return Results.NotFound();
        }

        if (job.Status != JobStatus.Published)
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["status"] = ["Job is not currently published."],
            });
        }

        job.Status = JobStatus.Unpublished;
        await db.SaveChangesAsync(cancellationToken);

        return Results.Ok(ToResponse(job));
    }

    // ------------------------------------------------------- candidate/public ---

    private static async Task<IResult> ListPublicJobsAsync(
        string? search,
        string? employmentType,
        string? workplaceType,
        string? location,
        IApplicationDbContext db,
        CancellationToken cancellationToken)
    {
        var errors = ValidatePublicFilters(employmentType, workplaceType);
        if (errors.Count > 0)
        {
            return Results.ValidationProblem(errors);
        }

        var query = db.Jobs.AsNoTracking().Where(j => j.Status == JobStatus.Published);

        if (!string.IsNullOrWhiteSpace(search))
        {
            query = query.Where(j => EF.Functions.ILike(j.Title, $"%{search}%") || EF.Functions.ILike(j.Description, $"%{search}%"));
        }

        if (!string.IsNullOrWhiteSpace(employmentType) && Enum.TryParse<EmploymentType>(employmentType, ignoreCase: true, out var parsedEmploymentType))
        {
            query = query.Where(j => j.EmploymentType == parsedEmploymentType);
        }

        if (!string.IsNullOrWhiteSpace(workplaceType) && Enum.TryParse<WorkplaceType>(workplaceType, ignoreCase: true, out var parsedWorkplaceType))
        {
            query = query.Where(j => j.WorkplaceType == parsedWorkplaceType);
        }

        if (!string.IsNullOrWhiteSpace(location))
        {
            query = query.Where(j => EF.Functions.ILike(j.Location, $"%{location}%"));
        }

        var summaries = await query
            .OrderByDescending(j => j.PublishedAt)
            .Select(j => new PublicJobSummaryResponse(
                j.Id,
                j.Title,
                j.Company.Name,
                j.EmploymentType.ToString(),
                j.WorkplaceType.ToString(),
                j.Location,
                j.Compensation,
                j.PublishedAt!.Value))
            .ToListAsync(cancellationToken);

        return Results.Ok(summaries);
    }

    private static async Task<IResult> GetPublicJobAsync(Guid id, IApplicationDbContext db, CancellationToken cancellationToken)
    {
        var job = await db.Jobs
            .AsNoTracking()
            .Where(j => j.Id == id && j.Status == JobStatus.Published)
            .Select(j => new PublicJobResponse(
                j.Id,
                j.Title,
                j.Description,
                j.EmploymentType.ToString(),
                j.WorkplaceType.ToString(),
                j.Location,
                j.Compensation,
                j.Skills,
                j.Company.Name,
                j.Company.Description,
                j.Company.Website,
                j.Company.LogoUrl,
                j.Company.Industry,
                j.Company.Location,
                j.PublishedAt!.Value))
            .FirstOrDefaultAsync(cancellationToken);

        return job is null ? Results.NotFound() : Results.Ok(job);
    }

    private static Dictionary<string, string[]> ValidatePublicFilters(string? employmentType, string? workplaceType)
    {
        var errors = new Dictionary<string, string[]>();

        if (!string.IsNullOrWhiteSpace(employmentType) && !Enum.TryParse<EmploymentType>(employmentType, ignoreCase: true, out _))
        {
            errors["employmentType"] = ["Not a valid employment type."];
        }

        if (!string.IsNullOrWhiteSpace(workplaceType) && !Enum.TryParse<WorkplaceType>(workplaceType, ignoreCase: true, out _))
        {
            errors["workplaceType"] = ["Not a valid workplace type."];
        }

        return errors;
    }

    // ----------------------------------------------------------------- shared ---

    private static Task<RecruiterProfile?> FindRecruiterProfileAsync(IApplicationDbContext db, Guid userId, CancellationToken cancellationToken) =>
        db.RecruiterProfiles.FirstOrDefaultAsync(r => r.UserId == userId, cancellationToken);

    /// <summary>
    /// The single place a job is ever looked up for a mutating or single-item read
    /// operation: scoped by both the route id and the caller's own recruiter
    /// profile id in the same query, so a job belonging to another recruiter is
    /// indistinguishable from one that doesn't exist — both 404, never 403 (which
    /// would confirm the job exists).
    /// </summary>
    private static async Task<Job?> FindOwnedJobAsync(IApplicationDbContext db, Guid userId, Guid jobId, CancellationToken cancellationToken)
    {
        var recruiterProfile = await FindRecruiterProfileAsync(db, userId, cancellationToken);
        if (recruiterProfile is null)
        {
            return null;
        }

        return await db.Jobs
            .Include(j => j.Company)
            .FirstOrDefaultAsync(j => j.Id == jobId && j.RecruiterProfileId == recruiterProfile.Id, cancellationToken);
    }

    private static JobResponse ToResponse(Job job) => new(
        job.Id,
        job.Title,
        job.Description,
        job.EmploymentType.ToString(),
        job.WorkplaceType.ToString(),
        job.Location,
        job.Compensation,
        job.Skills,
        job.Status.ToString(),
        job.CompanyId,
        job.Company.Name,
        job.CreatedAt,
        job.UpdatedAt,
        job.PublishedAt);

    private static JobSummaryResponse ToSummary(Job job) => new(
        job.Id,
        job.Title,
        job.Company.Name,
        job.Status.ToString(),
        job.CreatedAt,
        job.UpdatedAt,
        job.PublishedAt);

    private static string[] NormalizeSkills(IReadOnlyList<string>? skills) =>
        (skills ?? [])
            .Select(s => s.Trim())
            .Where(s => s.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

    private static string? Trim(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static Dictionary<string, string[]> ValidateJob(
        string? title, string? description, string? employmentType, string? workplaceType, string? location, string? compensation)
    {
        var errors = new Dictionary<string, string[]>();

        if (string.IsNullOrWhiteSpace(title))
        {
            errors["title"] = ["Title is required."];
        }
        else if (title.Length > 200)
        {
            errors["title"] = ["Must be 200 characters or fewer."];
        }

        if (string.IsNullOrWhiteSpace(description))
        {
            errors["description"] = ["Description is required."];
        }
        else if (description.Length > 5000)
        {
            errors["description"] = ["Must be 5000 characters or fewer."];
        }

        if (string.IsNullOrWhiteSpace(location))
        {
            errors["location"] = ["Location is required."];
        }
        else if (location.Length > 200)
        {
            errors["location"] = ["Must be 200 characters or fewer."];
        }

        if (compensation is { Length: > 100 })
        {
            errors["compensation"] = ["Must be 100 characters or fewer."];
        }

        if (!Enum.TryParse<EmploymentType>(employmentType, ignoreCase: true, out var parsedEmploymentType) || !Enum.IsDefined(parsedEmploymentType))
        {
            errors["employmentType"] = ["Select a valid employment type."];
        }

        if (!Enum.TryParse<WorkplaceType>(workplaceType, ignoreCase: true, out var parsedWorkplaceType) || !Enum.IsDefined(parsedWorkplaceType))
        {
            errors["workplaceType"] = ["Select a valid workplace type."];
        }

        return errors;
    }
}
