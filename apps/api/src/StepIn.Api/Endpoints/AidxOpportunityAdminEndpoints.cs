using Microsoft.EntityFrameworkCore;
using StepIn.Application.Common.Interfaces;
using StepIn.Domain.Aidx;
using StepIn.Domain.Jobs;
using StepIn.Infrastructure.Aidx;

namespace StepIn.Api.Endpoints;

/// <summary>
/// Admin management of AIDX Research Opportunities.
///
/// This is not a general Job administration API. Every operation is scoped to Research Jobs owned
/// by the canonical AIDX system recruiter at the AIDX Lab company (see <see cref="AidxOwnershipRules"/>).
/// Anything else, including Career jobs, other recruiters' Research jobs and unknown IDs, returns 404,
/// so the route never reveals whether another job exists.
///
/// Lifecycle rule violations return a validation problem, as the recruiter job endpoints do.
/// </summary>
public static class AidxOpportunityAdminEndpoints
{
    public static IEndpointRouteBuilder MapAidxOpportunityAdminEndpoints(this IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        var group = app.MapGroup("/api/v1/admin/aidx/opportunities")
            .WithTags("AIDX Admin")
            .RequireAuthorization("RequireAdmin");

        group.MapGet("/", ListAsync).WithName("ListAidxOpportunitiesAdmin").WithSummary("AIDX Research Opportunities, with filters.");
        group.MapPost("/", CreateAsync).WithName("CreateAidxOpportunity").WithSummary("Create an AIDX Research Opportunity as a draft.");
        group.MapGet("/{id:guid}", GetAsync).WithName("GetAidxOpportunityAdmin").WithSummary("One AIDX Research Opportunity.");
        group.MapPut("/{id:guid}", UpdateAsync).WithName("UpdateAidxOpportunity").WithSummary("Edit an AIDX Research Opportunity.");
        group.MapDelete("/{id:guid}", DeleteAsync).WithName("DeleteAidxOpportunity").WithSummary("Delete a draft AIDX Research Opportunity.");
        group.MapPost("/{id:guid}/publish", PublishAsync).WithName("PublishAidxOpportunity").WithSummary("Publish an AIDX Research Opportunity.");
        group.MapPost("/{id:guid}/unpublish", UnpublishAsync).WithName("UnpublishAidxOpportunity").WithSummary("Unpublish an AIDX Research Opportunity.");

        return app;
    }

    private static async Task<IResult> ListAsync(
        string? status,
        Guid? project,
        string? search,
        int? page,
        int? pageSize,
        AidxSystemOwnershipService ownershipService,
        IApplicationDbContext db,
        CancellationToken cancellationToken)
    {
        var errors = AidxEndpointHelpers.ValidatePaging(page ?? 1, pageSize ?? AidxEndpointHelpers.DefaultPageSize);
        if (!string.IsNullOrWhiteSpace(status) && !AidxEndpointHelpers.TryParseEnum<JobStatus>(status, out _))
        {
            AidxEndpointHelpers.AddError(errors, "status", "Unknown status.");
        }

        if (errors.Count > 0)
        {
            return Results.ValidationProblem(errors);
        }

        var (owner, unavailable) = await ResolveOwnerAsync(ownershipService, cancellationToken);
        if (unavailable is not null)
        {
            return unavailable;
        }

        var query = db.Jobs
            .AsNoTracking()
            .Include(j => j.AidxProject)
            .Where(AidxOwnershipRules.OwnedByAidx(owner!));

        if (AidxEndpointHelpers.TryParseEnum<JobStatus>(status, out var parsedStatus))
        {
            query = query.Where(j => j.Status == parsedStatus);
        }

        if (project is { } projectId)
        {
            query = query.Where(j => j.AidxProjectId == projectId);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            query = query.Where(j => EF.Functions.ILike(j.Title, $"%{search.Trim()}%"));
        }

        query = query.OrderByDescending(j => j.CreatedAt).ThenBy(j => j.Id);

        var (jobs, total) = await AidxEndpointHelpers.PageAsync(query, page ?? 1, pageSize ?? AidxEndpointHelpers.DefaultPageSize, cancellationToken);

        return Results.Ok(new AidxPageResponse<AidxOpportunityAdminResponse>(
            jobs.Select(j => ToResponse(j, j.AidxProject)).ToList(),
            page ?? 1,
            pageSize ?? AidxEndpointHelpers.DefaultPageSize,
            total));
    }

    private static async Task<IResult> GetAsync(
        Guid id, AidxSystemOwnershipService ownershipService, IApplicationDbContext db, CancellationToken cancellationToken)
    {
        var (owner, unavailable) = await ResolveOwnerAsync(ownershipService, cancellationToken);
        if (unavailable is not null)
        {
            return unavailable;
        }

        var job = await LoadOwnedAsync(db, owner!, id, cancellationToken);
        return job is null ? Results.NotFound() : Results.Ok(ToResponse(job, job.AidxProject));
    }

    private static async Task<IResult> CreateAsync(
        AidxOpportunityRequest request,
        AidxSystemOwnershipService ownershipService,
        IApplicationDbContext db,
        CancellationToken cancellationToken)
    {
        var (owner, unavailable) = await ResolveOwnerAsync(ownershipService, cancellationToken);
        if (unavailable is not null)
        {
            return unavailable;
        }

        var errors = await ValidateContentAsync(request, db, cancellationToken);
        if (errors.Count > 0)
        {
            return Results.ValidationProblem(errors);
        }

        var project = await FindProjectAsync(db, request.AidxProjectId, cancellationToken);

        // Ownership and category come from the server, never from the request. Every new
        // opportunity starts as a draft, so the client cannot publish on create.
        var job = new Job
        {
            RecruiterProfileId = owner!.RecruiterProfileId,
            CompanyId = owner.CompanyId,
            Category = JobCategory.Research,
            Status = JobStatus.Draft,
            Title = request.Title!.Trim(),
            Description = request.Description!.Trim(),
            Location = request.Location!.Trim(),
            Compensation = AidxEndpointHelpers.Clean(request.Compensation),
            EmploymentType = Enum.Parse<EmploymentType>(request.EmploymentType!, ignoreCase: true),
            WorkplaceType = Enum.Parse<WorkplaceType>(request.WorkplaceType!, ignoreCase: true),
            Skills = JobEndpoints.NormalizeSkills(request.Skills),
            AidxProjectId = project?.Id,
        };

        db.Add(job);
        await db.SaveChangesAsync(cancellationToken);

        return Results.Created((string?)null, ToResponse(job, project));
    }

    private static async Task<IResult> UpdateAsync(
        Guid id,
        AidxOpportunityRequest request,
        AidxSystemOwnershipService ownershipService,
        IApplicationDbContext db,
        CancellationToken cancellationToken)
    {
        var (owner, unavailable) = await ResolveOwnerAsync(ownershipService, cancellationToken);
        if (unavailable is not null)
        {
            return unavailable;
        }

        var job = await LoadOwnedAsync(db, owner!, id, cancellationToken);
        if (job is null)
        {
            return Results.NotFound();
        }

        var errors = await ValidateContentAsync(request, db, cancellationToken);

        var project = await FindProjectAsync(db, request.AidxProjectId, cancellationToken);
        var projectChanged = request.AidxProjectId != job.AidxProjectId;

        // A published opportunity must never be relinked to a project that is not public. Editing
        // other fields of an already-linked opportunity is still allowed, so an admin is not forced
        // to relink when the project changes status later.
        if (projectChanged && job.Status == JobStatus.Published && project is not null && project.Status != AidxContentStatus.Published)
        {
            AidxEndpointHelpers.AddError(errors, "aidxProjectId", "A published opportunity can only link to a published project.");
        }

        if (errors.Count > 0)
        {
            return Results.ValidationProblem(errors);
        }

        // Owner, company, category and status are not touched here. Ownership cannot be transferred.
        job.Title = request.Title!.Trim();
        job.Description = request.Description!.Trim();
        job.Location = request.Location!.Trim();
        job.Compensation = AidxEndpointHelpers.Clean(request.Compensation);
        job.EmploymentType = Enum.Parse<EmploymentType>(request.EmploymentType!, ignoreCase: true);
        job.WorkplaceType = Enum.Parse<WorkplaceType>(request.WorkplaceType!, ignoreCase: true);
        job.Skills = JobEndpoints.NormalizeSkills(request.Skills);
        job.AidxProjectId = project?.Id;

        await db.SaveChangesAsync(cancellationToken);

        return Results.Ok(ToResponse(job, project));
    }

    private static async Task<IResult> DeleteAsync(
        Guid id, AidxSystemOwnershipService ownershipService, IApplicationDbContext db, CancellationToken cancellationToken)
    {
        var (owner, unavailable) = await ResolveOwnerAsync(ownershipService, cancellationToken);
        if (unavailable is not null)
        {
            return unavailable;
        }

        var job = await LoadOwnedAsync(db, owner!, id, cancellationToken);
        if (job is null)
        {
            return Results.NotFound();
        }

        // Only drafts can be deleted. A published or unpublished opportunity may have applications
        // attached, so it is taken down by unpublishing rather than removed.
        if (job.Status != JobStatus.Draft)
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["status"] = ["Only draft opportunities can be deleted. Unpublish a published opportunity instead."],
            });
        }

        db.RemoveRange(new[] { job });
        await db.SaveChangesAsync(cancellationToken);

        return Results.NoContent();
    }

    private static async Task<IResult> PublishAsync(
        Guid id,
        AidxSystemOwnershipService ownershipService,
        IApplicationDbContext db,
        IDateTimeProvider clock,
        CancellationToken cancellationToken)
    {
        var (owner, unavailable) = await ResolveOwnerAsync(ownershipService, cancellationToken);
        if (unavailable is not null)
        {
            return unavailable;
        }

        var job = await LoadOwnedAsync(db, owner!, id, cancellationToken);
        if (job is null)
        {
            return Results.NotFound();
        }

        if (job.Status == JobStatus.Published)
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["status"] = ["Opportunity is already published."],
            });
        }

        if (job.AidxProject is not null && job.AidxProject.Status != AidxContentStatus.Published)
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["aidxProjectId"] = ["Link a published project before publishing this opportunity."],
            });
        }

        job.Status = JobStatus.Published;
        job.PublishedAt = clock.UtcNow;
        await db.SaveChangesAsync(cancellationToken);

        return Results.Ok(ToResponse(job, job.AidxProject));
    }

    private static async Task<IResult> UnpublishAsync(
        Guid id, AidxSystemOwnershipService ownershipService, IApplicationDbContext db, CancellationToken cancellationToken)
    {
        var (owner, unavailable) = await ResolveOwnerAsync(ownershipService, cancellationToken);
        if (unavailable is not null)
        {
            return unavailable;
        }

        var job = await LoadOwnedAsync(db, owner!, id, cancellationToken);
        if (job is null)
        {
            return Results.NotFound();
        }

        if (job.Status != JobStatus.Published)
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["status"] = ["Only a published opportunity can be unpublished."],
            });
        }

        // The row, its project link and any applications are kept. Public visibility follows the status.
        job.Status = JobStatus.Unpublished;
        await db.SaveChangesAsync(cancellationToken);

        return Results.Ok(ToResponse(job, job.AidxProject));
    }

    /// <summary>
    /// Resolves the AIDX system owner without creating it. A missing or inconsistent owner returns 503
    /// with a generic message, so no internal detail leaks and no record is written.
    /// </summary>
    private static async Task<(AidxSystemOwnership? Owner, IResult? Unavailable)> ResolveOwnerAsync(
        AidxSystemOwnershipService ownershipService, CancellationToken cancellationToken)
    {
        try
        {
            var owner = await ownershipService.FindAsync(cancellationToken);
            return owner is null ? (null, OwnerUnavailable()) : (owner, null);
        }
        catch (InvalidOperationException)
        {
            return (null, OwnerUnavailable());
        }
    }

    private static IResult OwnerUnavailable() => Results.Problem(
        title: "The AIDX system owner is unavailable. An administrator must check its initialisation.",
        statusCode: StatusCodes.Status503ServiceUnavailable);

    /// <summary>Loads a job only when it is an AIDX-owned Research Job. Anything else is indistinguishable from missing.</summary>
    private static async Task<Job?> LoadOwnedAsync(IApplicationDbContext db, AidxSystemOwnership owner, Guid id, CancellationToken cancellationToken)
    {
        var job = await db.Jobs
            .Include(j => j.AidxProject)
            .FirstOrDefaultAsync(j => j.Id == id, cancellationToken);

        return job is not null && AidxOwnershipRules.IsAidxResearchJob(job, owner) ? job : null;
    }

    private static async Task<Dictionary<string, string[]>> ValidateContentAsync(
        AidxOpportunityRequest request, IApplicationDbContext db, CancellationToken cancellationToken)
    {
        var errors = JobEndpoints.ValidateJob(
            request.Title, request.Description, request.EmploymentType, request.WorkplaceType, request.Location, request.Compensation);

        if (request.AidxProjectId is { } projectId && await FindProjectAsync(db, projectId, cancellationToken) is null)
        {
            errors["aidxProjectId"] = ["The AIDX project does not exist."];
        }

        return errors;
    }

    private static async Task<AidxProject?> FindProjectAsync(IApplicationDbContext db, Guid? projectId, CancellationToken cancellationToken)
    {
        if (projectId is not { } id)
        {
            return null;
        }

        return await db.AidxProjects.FirstOrDefaultAsync(p => p.Id == id, cancellationToken);
    }

    private static AidxOpportunityAdminResponse ToResponse(Job job, AidxProject? project) => new(
        job.Id,
        job.Title,
        job.Description,
        job.EmploymentType.ToString(),
        job.WorkplaceType.ToString(),
        job.Location,
        job.Compensation,
        job.Skills,
        job.Status.ToString(),
        job.AidxProjectId,
        project?.Title,
        project?.Slug,
        job.CreatedAt,
        job.UpdatedAt,
        job.PublishedAt);
}
