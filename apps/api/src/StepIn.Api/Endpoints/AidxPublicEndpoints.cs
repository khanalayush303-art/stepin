using Microsoft.EntityFrameworkCore;
using StepIn.Application.Common.Interfaces;
using StepIn.Domain.Aidx;
using StepIn.Domain.Jobs;

namespace StepIn.Api.Endpoints;

/// <summary>
/// Public, read-only AIDX Lab content. Like the public job routes, this group has no
/// authorization and no write verbs: it is read-only by construction. Every query
/// filters to published content, so a draft or archived row can never be returned.
/// Unknown or unpublished slugs return 404 and never reveal whether the row exists.
/// </summary>
public static class AidxPublicEndpoints
{
    public static IEndpointRouteBuilder MapAidxPublicEndpoints(this IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        var aidx = app.MapGroup("/api/v1/aidx").WithTags("AIDX");

        aidx.MapGet("/research", ListResearchAsync).WithName("ListAidxResearch").WithSummary("Research areas.");
        aidx.MapGet("/research/{slug}", GetResearchAsync).WithName("GetAidxResearch").WithSummary("One research area.");
        aidx.MapGet("/projects", ListProjectsAsync).WithName("ListAidxProjects").WithSummary("Published AIDX projects.");
        aidx.MapGet("/projects/{slug}", GetProjectAsync).WithName("GetAidxProject").WithSummary("One published AIDX project.");
        aidx.MapGet("/people", ListPeopleAsync).WithName("ListAidxPeople").WithSummary("Published researcher profiles.");
        aidx.MapGet("/people/{slug}", GetPersonAsync).WithName("GetAidxPerson").WithSummary("One published researcher profile.");
        aidx.MapGet("/publications", ListPublicationsAsync).WithName("ListAidxPublications").WithSummary("Published publications.");
        aidx.MapGet("/news", ListNewsAsync).WithName("ListAidxNews").WithSummary("Published news.");
        aidx.MapGet("/news/{slug}", GetNewsAsync).WithName("GetAidxNews").WithSummary("One published news item.");
        aidx.MapGet("/events", ListEventsAsync).WithName("ListAidxEvents").WithSummary("Published events.");
        aidx.MapGet("/events/{slug}", GetEventAsync).WithName("GetAidxEvent").WithSummary("One published event.");
        aidx.MapGet("/opportunities", ListOpportunitiesAsync).WithName("ListAidxOpportunities").WithSummary("Published research opportunities (StepIn jobs with Category = Research).");

        return app;
    }

    private static async Task<IResult> ListResearchAsync(string? search, IApplicationDbContext db, CancellationToken cancellationToken)
    {
        var query = db.AidxResearchAreas.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(search))
        {
            query = query.Where(a => EF.Functions.ILike(a.Name, $"%{search.Trim()}%"));
        }

        var areas = await query
            .OrderBy(a => a.SortOrder).ThenBy(a => a.Name)
            .Select(a => new AidxResearchAreaResponse(a.Id, a.Name, a.Slug, a.Description))
            .ToListAsync(cancellationToken);

        return Results.Ok(areas);
    }

    private static async Task<IResult> GetResearchAsync(string slug, IApplicationDbContext db, CancellationToken cancellationToken)
    {
        var area = await db.AidxResearchAreas
            .AsNoTracking()
            .Where(a => a.Slug == slug)
            .Select(a => new AidxResearchAreaResponse(a.Id, a.Name, a.Slug, a.Description))
            .FirstOrDefaultAsync(cancellationToken);

        return area is null ? Results.NotFound() : Results.Ok(area);
    }

    private static async Task<IResult> ListProjectsAsync(
        string? area,
        string? status,
        bool? featured,
        string? search,
        int? page,
        int? pageSize,
        IApplicationDbContext db,
        CancellationToken cancellationToken)
    {
        var errors = AidxEndpointHelpers.ValidatePaging(page ?? 1, pageSize ?? AidxEndpointHelpers.DefaultPageSize);

        // Anonymous callers may only ask for published projects. Any other status is
        // rejected outright rather than silently ignored, so a draft or archived project
        // can never be reached through this route.
        if (!string.IsNullOrWhiteSpace(status) && !string.Equals(status, nameof(AidxContentStatus.Published), StringComparison.OrdinalIgnoreCase))
        {
            AidxEndpointHelpers.AddError(errors, "status", "Only published projects are publicly available.");
        }

        if (errors.Count > 0)
        {
            return Results.ValidationProblem(errors);
        }

        var query = db.AidxProjects
            .AsNoTracking()
            .Include(p => p.ResearchAreas).ThenInclude(r => r.ResearchArea)
            .Where(p => p.Status == AidxContentStatus.Published);

        if (!string.IsNullOrWhiteSpace(area))
        {
            query = query.Where(p => p.ResearchAreas.Any(r => r.ResearchArea.Slug == area));
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = $"%{search.Trim()}%";
            query = query.Where(p => EF.Functions.ILike(p.Title, term) || EF.Functions.ILike(p.ShortDescription, term));
        }

        if (featured is { } isFeatured)
        {
            query = query.Where(p => p.Featured == isFeatured);
        }

        query = query.OrderByDescending(p => p.Featured).ThenByDescending(p => p.PublishedAt).ThenBy(p => p.Slug);

        var (projects, total) = await AidxEndpointHelpers.PageAsync(query, page ?? 1, pageSize ?? AidxEndpointHelpers.DefaultPageSize, cancellationToken);

        return Results.Ok(new AidxPageResponse<AidxProjectSummaryResponse>(
            projects.Select(ToProjectSummary).ToList(), page ?? 1, pageSize ?? AidxEndpointHelpers.DefaultPageSize, total));
    }

    private static async Task<IResult> GetProjectAsync(string slug, IApplicationDbContext db, CancellationToken cancellationToken)
    {
        var project = await db.AidxProjects
            .AsNoTracking()
            .Include(p => p.ResearchAreas).ThenInclude(r => r.ResearchArea)
            .Include(p => p.Technologies)
            .Include(p => p.Researchers).ThenInclude(r => r.Researcher)
            .FirstOrDefaultAsync(p => p.Slug == slug && p.Status == AidxContentStatus.Published, cancellationToken);

        if (project is null)
        {
            return Results.NotFound();
        }

        var response = new AidxProjectDetailResponse(
            project.Id,
            project.Title,
            project.Slug,
            project.ShortDescription,
            project.Description,
            project.Featured,
            project.StartDate,
            project.EndDate,
            project.ExternalUrl,
            project.PublishedAt,
            project.ResearchAreas.Select(r => r.ResearchArea.Name).OrderBy(n => n).ToList(),
            project.Technologies.Select(t => t.Name).OrderBy(n => n).ToList(),
            project.Researchers
                .Where(r => r.Researcher.Published)
                .OrderBy(r => r.Researcher.DisplayName)
                .Select(r => new AidxProjectResearcherResponse(r.ResearcherId, r.Researcher.Slug, r.Researcher.DisplayName, r.Role))
                .ToList());

        return Results.Ok(response);
    }

    private static async Task<IResult> ListPeopleAsync(
        string? category,
        string? search,
        int? page,
        int? pageSize,
        IApplicationDbContext db,
        CancellationToken cancellationToken)
    {
        var errors = AidxEndpointHelpers.ValidatePaging(page ?? 1, pageSize ?? AidxEndpointHelpers.DefaultPageSize);
        if (!string.IsNullOrWhiteSpace(category) && !AidxEndpointHelpers.TryParseEnum<AidxResearcherCategory>(category, out _))
        {
            AidxEndpointHelpers.AddError(errors, "category", "Unknown researcher category.");
        }

        if (errors.Count > 0)
        {
            return Results.ValidationProblem(errors);
        }

        var query = db.AidxResearchers.AsNoTracking().Where(r => r.Published);

        if (AidxEndpointHelpers.TryParseEnum<AidxResearcherCategory>(category, out var parsedCategory))
        {
            query = query.Where(r => r.Category == parsedCategory);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            query = query.Where(r => EF.Functions.ILike(r.DisplayName, $"%{search.Trim()}%"));
        }

        query = query.OrderBy(r => r.DisplayName).ThenBy(r => r.Slug);

        var (researchers, total) = await AidxEndpointHelpers.PageAsync(query, page ?? 1, pageSize ?? AidxEndpointHelpers.DefaultPageSize, cancellationToken);

        return Results.Ok(new AidxPageResponse<AidxResearcherResponse>(
            researchers.Select(ToResearcherResponse).ToList(), page ?? 1, pageSize ?? AidxEndpointHelpers.DefaultPageSize, total));
    }

    private static async Task<IResult> GetPersonAsync(string slug, IApplicationDbContext db, CancellationToken cancellationToken)
    {
        var researcher = await db.AidxResearchers
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.Slug == slug && r.Published, cancellationToken);

        return researcher is null ? Results.NotFound() : Results.Ok(ToResearcherResponse(researcher));
    }

    private static async Task<IResult> ListPublicationsAsync(
        int? year,
        string? type,
        string? area,
        string? q,
        int? page,
        int? pageSize,
        IApplicationDbContext db,
        CancellationToken cancellationToken)
    {
        var errors = AidxEndpointHelpers.ValidatePaging(page ?? 1, pageSize ?? AidxEndpointHelpers.DefaultPageSize);
        if (!string.IsNullOrWhiteSpace(type) && !AidxEndpointHelpers.TryParseEnum<AidxPublicationType>(type, out _))
        {
            AidxEndpointHelpers.AddError(errors, "type", "Unknown publication type.");
        }

        if (errors.Count > 0)
        {
            return Results.ValidationProblem(errors);
        }

        var query = db.AidxPublications
            .AsNoTracking()
            .Include(p => p.Authors).ThenInclude(a => a.Researcher)
            .Where(p => p.Published);

        if (year is { } filterYear)
        {
            query = query.Where(p => p.Year == filterYear);
        }

        if (AidxEndpointHelpers.TryParseEnum<AidxPublicationType>(type, out var parsedType))
        {
            query = query.Where(p => p.PublicationType == parsedType);
        }

        if (!string.IsNullOrWhiteSpace(area))
        {
            query = query.Where(p => p.ResearchAreas.Any(r => r.ResearchArea.Slug == area));
        }

        if (!string.IsNullOrWhiteSpace(q))
        {
            var term = $"%{q.Trim()}%";
            query = query.Where(p => EF.Functions.ILike(p.Title, term) || (p.Abstract != null && EF.Functions.ILike(p.Abstract, term)));
        }

        query = query.OrderByDescending(p => p.Year).ThenBy(p => p.Title).ThenBy(p => p.Id);

        var (publications, total) = await AidxEndpointHelpers.PageAsync(query, page ?? 1, pageSize ?? AidxEndpointHelpers.DefaultPageSize, cancellationToken);

        return Results.Ok(new AidxPageResponse<AidxPublicationResponse>(
            publications.Select(ToPublicationResponse).ToList(), page ?? 1, pageSize ?? AidxEndpointHelpers.DefaultPageSize, total));
    }

    private static async Task<IResult> ListNewsAsync(int? page, int? pageSize, IApplicationDbContext db, CancellationToken cancellationToken)
    {
        var errors = AidxEndpointHelpers.ValidatePaging(page ?? 1, pageSize ?? AidxEndpointHelpers.DefaultPageSize);
        if (errors.Count > 0)
        {
            return Results.ValidationProblem(errors);
        }

        var query = db.AidxNews
            .AsNoTracking()
            .Where(n => n.Status == AidxContentStatus.Published)
            .OrderByDescending(n => n.PublishedAt)
            .ThenBy(n => n.Slug)
            .Select(n => new AidxNewsSummaryResponse(n.Id, n.Slug, n.Title, n.Summary, n.PublishedAt));

        var (items, total) = await AidxEndpointHelpers.PageAsync(query, page ?? 1, pageSize ?? AidxEndpointHelpers.DefaultPageSize, cancellationToken);

        return Results.Ok(new AidxPageResponse<AidxNewsSummaryResponse>(items, page ?? 1, pageSize ?? AidxEndpointHelpers.DefaultPageSize, total));
    }

    private static async Task<IResult> GetNewsAsync(string slug, IApplicationDbContext db, CancellationToken cancellationToken)
    {
        var news = await db.AidxNews
            .AsNoTracking()
            .Where(n => n.Slug == slug && n.Status == AidxContentStatus.Published)
            .Select(n => new AidxNewsDetailResponse(n.Id, n.Slug, n.Title, n.Summary, n.Body, n.PublishedAt))
            .FirstOrDefaultAsync(cancellationToken);

        return news is null ? Results.NotFound() : Results.Ok(news);
    }

    private static async Task<IResult> ListEventsAsync(
        bool? upcoming,
        int? page,
        int? pageSize,
        IApplicationDbContext db,
        IDateTimeProvider clock,
        CancellationToken cancellationToken)
    {
        var errors = AidxEndpointHelpers.ValidatePaging(page ?? 1, pageSize ?? AidxEndpointHelpers.DefaultPageSize);
        if (errors.Count > 0)
        {
            return Results.ValidationProblem(errors);
        }

        var query = db.AidxEvents
            .AsNoTracking()
            .Where(e => e.Status == AidxContentStatus.Published);

        if (upcoming == true)
        {
            var now = clock.UtcNow;
            query = query.Where(e => e.StartsAt >= now);
        }

        var ordered = query.OrderBy(e => e.StartsAt).ThenBy(e => e.Slug)
            .Select(e => new AidxEventResponse(e.Id, e.Slug, e.Title, e.Description, e.StartsAt, e.EndsAt, e.Location, e.RegistrationUrl, e.SpeakerName));

        var (items, total) = await AidxEndpointHelpers.PageAsync(ordered, page ?? 1, pageSize ?? AidxEndpointHelpers.DefaultPageSize, cancellationToken);

        return Results.Ok(new AidxPageResponse<AidxEventResponse>(items, page ?? 1, pageSize ?? AidxEndpointHelpers.DefaultPageSize, total));
    }

    private static async Task<IResult> GetEventAsync(string slug, IApplicationDbContext db, CancellationToken cancellationToken)
    {
        var evt = await db.AidxEvents
            .AsNoTracking()
            .Where(e => e.Slug == slug && e.Status == AidxContentStatus.Published)
            .Select(e => new AidxEventResponse(e.Id, e.Slug, e.Title, e.Description, e.StartsAt, e.EndsAt, e.Location, e.RegistrationUrl, e.SpeakerName))
            .FirstOrDefaultAsync(cancellationToken);

        return evt is null ? Results.NotFound() : Results.Ok(evt);
    }

    /// <summary>
    /// Research opportunities are StepIn jobs with <see cref="JobCategory.Research"/>. A job is
    /// hidden unless it is published, and also hidden when it belongs to an AIDX project that is
    /// not published, so the published-only rule holds through the project as well.
    /// </summary>
    private static async Task<IResult> ListOpportunitiesAsync(
        string? type,
        int? page,
        int? pageSize,
        IApplicationDbContext db,
        CancellationToken cancellationToken)
    {
        var errors = AidxEndpointHelpers.ValidatePaging(page ?? 1, pageSize ?? AidxEndpointHelpers.DefaultPageSize);
        if (!string.IsNullOrWhiteSpace(type) && !AidxEndpointHelpers.TryParseEnum<EmploymentType>(type, out _))
        {
            AidxEndpointHelpers.AddError(errors, "type", "Unknown employment type.");
        }

        if (errors.Count > 0)
        {
            return Results.ValidationProblem(errors);
        }

        var query = db.Jobs
            .AsNoTracking()
            .Include(j => j.Company)
            .Include(j => j.AidxProject)
            .Where(j => j.Category == JobCategory.Research)
            .Where(AidxJobVisibility.IsPubliclyVisible);

        if (AidxEndpointHelpers.TryParseEnum<EmploymentType>(type, out var parsedType))
        {
            query = query.Where(j => j.EmploymentType == parsedType);
        }

        query = query.OrderByDescending(j => j.PublishedAt).ThenBy(j => j.Id);

        var (jobs, total) = await AidxEndpointHelpers.PageAsync(query, page ?? 1, pageSize ?? AidxEndpointHelpers.DefaultPageSize, cancellationToken);

        return Results.Ok(new AidxPageResponse<AidxOpportunityResponse>(
            jobs.Select(ToOpportunity).ToList(), page ?? 1, pageSize ?? AidxEndpointHelpers.DefaultPageSize, total));
    }

    private static AidxProjectSummaryResponse ToProjectSummary(AidxProject project) => new(
        project.Id,
        project.Title,
        project.Slug,
        project.ShortDescription,
        project.Featured,
        project.StartDate,
        project.EndDate,
        project.PublishedAt,
        project.ResearchAreas.Select(r => r.ResearchArea.Name).OrderBy(n => n).ToList());

    private static AidxResearcherResponse ToResearcherResponse(AidxResearcher researcher) => new(
        researcher.Id,
        researcher.Slug,
        researcher.DisplayName,
        researcher.Category.ToString(),
        researcher.Position,
        researcher.Biography,
        researcher.OrcidUrl,
        researcher.GoogleScholarUrl,
        researcher.LinkedInUrl,
        researcher.WebsiteUrl);

    /// <summary>
    /// Author names come from the linked researcher when there is one, otherwise from the
    /// external name. Authorship is factual, so it is shown even when the researcher's own
    /// profile page is unpublished.
    /// </summary>
    private static AidxPublicationResponse ToPublicationResponse(AidxPublication publication) => new(
        publication.Id,
        publication.Title,
        publication.Abstract,
        publication.PublicationType.ToString(),
        publication.Venue,
        publication.Year,
        publication.Doi,
        publication.ExternalUrl,
        publication.Authors
            .OrderBy(a => a.Position)
            .Select(a => a.Researcher is not null ? a.Researcher.DisplayName : a.ExternalAuthorName ?? string.Empty)
            .ToList());

    private static AidxOpportunityResponse ToOpportunity(StepIn.Domain.Jobs.Job job) => new(
        job.Id,
        job.Title,
        job.Company.Name,
        job.Location,
        job.EmploymentType.ToString(),
        job.WorkplaceType.ToString(),
        job.Compensation,
        job.Skills,
        job.PublishedAt,
        job.AidxProject?.Slug,
        job.AidxProject?.Title);
}
