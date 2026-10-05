using Microsoft.EntityFrameworkCore;
using StepIn.Application.Common.Interfaces;
using StepIn.Domain.Aidx;

namespace StepIn.Api.Endpoints;

/// <summary>
/// Admin CRUD and publishing for AIDX content. Every route requires <c>RequireAdmin</c>,
/// so applicants and recruiters get 403 and anonymous callers get 401. Admins can write
/// content only through these routes; the public routes are read-only.
///
/// Publishing rules: a draft or archived project, news item or event can be published,
/// and only a published one can be archived. Publishing twice is a 409 rather than a
/// silent no-op, so the audit trail stays meaningful. Researchers and publications use
/// a plain <c>Published</c> flag because they have no draft lifecycle.
/// </summary>
public static class AidxAdminEndpoints
{
    public static IEndpointRouteBuilder MapAidxAdminEndpoints(this IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        var admin = app.MapGroup("/api/v1/admin/aidx")
            .WithTags("AIDX Admin")
            .RequireAuthorization("RequireAdmin");

        admin.MapPost("/research", CreateResearchAreaAsync).WithName("CreateAidxResearchArea");
        admin.MapPut("/research/{id:guid}", UpdateResearchAreaAsync).WithName("UpdateAidxResearchArea");

        admin.MapPost("/projects", CreateProjectAsync).WithName("CreateAidxProject");
        admin.MapPut("/projects/{id:guid}", UpdateProjectAsync).WithName("UpdateAidxProject");
        admin.MapPost("/projects/{id:guid}/publish", PublishProjectAsync).WithName("PublishAidxProject");
        admin.MapPost("/projects/{id:guid}/archive", ArchiveProjectAsync).WithName("ArchiveAidxProject");

        admin.MapPost("/people", CreateResearcherAsync).WithName("CreateAidxResearcher");
        admin.MapPut("/people/{id:guid}", UpdateResearcherAsync).WithName("UpdateAidxResearcher");

        admin.MapPost("/publications", CreatePublicationAsync).WithName("CreateAidxPublication");
        admin.MapPut("/publications/{id:guid}", UpdatePublicationAsync).WithName("UpdateAidxPublication");

        admin.MapPost("/news", CreateNewsAsync).WithName("CreateAidxNews");
        admin.MapPut("/news/{id:guid}", UpdateNewsAsync).WithName("UpdateAidxNews");
        admin.MapPost("/news/{id:guid}/publish", PublishNewsAsync).WithName("PublishAidxNews");
        admin.MapPost("/news/{id:guid}/archive", ArchiveNewsAsync).WithName("ArchiveAidxNews");

        admin.MapPost("/events", CreateEventAsync).WithName("CreateAidxEvent");
        admin.MapPut("/events/{id:guid}", UpdateEventAsync).WithName("UpdateAidxEvent");
        admin.MapPost("/events/{id:guid}/publish", PublishEventAsync).WithName("PublishAidxEvent");
        admin.MapPost("/events/{id:guid}/archive", ArchiveEventAsync).WithName("ArchiveAidxEvent");

        admin.MapDelete("/research/{id:guid}", DeleteResearchAreaAsync).WithName("DeleteAidxResearchArea");
        admin.MapDelete("/projects/{id:guid}", DeleteProjectAsync).WithName("DeleteAidxProject");
        admin.MapDelete("/people/{id:guid}", DeleteResearcherAsync).WithName("DeleteAidxResearcher");
        admin.MapDelete("/publications/{id:guid}", DeletePublicationAsync).WithName("DeleteAidxPublication");
        admin.MapDelete("/news/{id:guid}", DeleteNewsAsync).WithName("DeleteAidxNews");
        admin.MapDelete("/events/{id:guid}", DeleteEventAsync).WithName("DeleteAidxEvent");

        // Admin reads include drafts and archived rows. Public reads never do.
        admin.MapGet("/research/{id:guid}", GetResearchAreaAsync).WithName("GetAidxResearchAreaAdmin");
        admin.MapGet("/projects", ListProjectsAsync).WithName("ListAidxProjectsAdmin");
        admin.MapGet("/projects/{id:guid}", GetProjectAsync).WithName("GetAidxProjectAdmin");

        return app;
    }

    private static async Task<IResult> GetResearchAreaAsync(Guid id, IApplicationDbContext db, CancellationToken cancellationToken)
    {
        var area = await db.AidxResearchAreas
            .AsNoTracking()
            .Where(a => a.Id == id)
            .Select(a => new AidxAdminResearchAreaResponse(a.Id, a.Name, a.Slug, a.Description, a.SortOrder))
            .FirstOrDefaultAsync(cancellationToken);

        return area is null ? Results.NotFound() : Results.Ok(area);
    }

    private static async Task<IResult> ListProjectsAsync(
        string? status,
        Guid? area,
        bool? featured,
        string? search,
        int? page,
        int? pageSize,
        IApplicationDbContext db,
        CancellationToken cancellationToken)
    {
        var errors = AidxEndpointHelpers.ValidatePaging(page ?? 1, pageSize ?? AidxEndpointHelpers.DefaultPageSize);
        if (!string.IsNullOrWhiteSpace(status) && !AidxEndpointHelpers.TryParseEnum<AidxContentStatus>(status, out _))
        {
            AidxEndpointHelpers.AddError(errors, "status", "Unknown status.");
        }

        if (errors.Count > 0)
        {
            return Results.ValidationProblem(errors);
        }

        var query = db.AidxProjects
            .AsNoTracking()
            .Include(p => p.ResearchAreas).ThenInclude(r => r.ResearchArea)
            .AsQueryable();

        if (AidxEndpointHelpers.TryParseEnum<AidxContentStatus>(status, out var parsedStatus))
        {
            query = query.Where(p => p.Status == parsedStatus);
        }

        if (area is { } areaId)
        {
            query = query.Where(p => p.ResearchAreas.Any(r => r.ResearchAreaId == areaId));
        }

        if (featured is { } isFeatured)
        {
            query = query.Where(p => p.Featured == isFeatured);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = $"%{search.Trim()}%";
            query = query.Where(p => EF.Functions.ILike(p.Title, term) || EF.Functions.ILike(p.Slug, term));
        }

        query = query.OrderByDescending(p => p.CreatedAt).ThenBy(p => p.Id);

        var (projects, total) = await AidxEndpointHelpers.PageAsync(query, page ?? 1, pageSize ?? AidxEndpointHelpers.DefaultPageSize, cancellationToken);

        return Results.Ok(new AidxPageResponse<AidxAdminProjectSummaryResponse>(
            projects.Select(p => new AidxAdminProjectSummaryResponse(
                p.Id,
                p.Title,
                p.Slug,
                p.ShortDescription,
                p.Status.ToString(),
                p.Featured,
                p.StartDate,
                p.EndDate,
                p.PublishedAt,
                p.UpdatedAt,
                p.ResearchAreas.Select(r => r.ResearchArea.Name).OrderBy(n => n).ToList())).ToList(),
            page ?? 1,
            pageSize ?? AidxEndpointHelpers.DefaultPageSize,
            total));
    }

    /// <summary>
    /// Everything the admin edit form needs, including the researcher links. Those links are not
    /// editable yet, but the form sends them back unchanged so a save does not drop them.
    /// </summary>
    private static async Task<IResult> GetProjectAsync(Guid id, IApplicationDbContext db, CancellationToken cancellationToken)
    {
        var project = await db.AidxProjects
            .AsNoTracking()
            .Include(p => p.ResearchAreas)
            .Include(p => p.Technologies)
            .Include(p => p.Researchers).ThenInclude(r => r.Researcher)
            .FirstOrDefaultAsync(p => p.Id == id, cancellationToken);

        if (project is null)
        {
            return Results.NotFound();
        }

        return Results.Ok(new AidxAdminProjectDetailResponse(
            project.Id,
            project.Title,
            project.Slug,
            project.ShortDescription,
            project.Description,
            project.Status.ToString(),
            project.Featured,
            project.StartDate,
            project.EndDate,
            project.ExternalUrl,
            project.PublishedAt,
            project.ResearchAreas.Select(r => r.ResearchAreaId).ToList(),
            project.Technologies.Select(t => t.Name).OrderBy(n => n).ToList(),
            project.Researchers
                .OrderBy(r => r.Researcher.DisplayName)
                .Select(r => new AidxAdminProjectResearcherResponse(r.ResearcherId, r.Researcher.DisplayName, r.Role))
                .ToList()));
    }

    // ---- Research areas ----------------------------------------------------

    private static async Task<IResult> CreateResearchAreaAsync(AidxResearchAreaRequest request, IApplicationDbContext db, CancellationToken cancellationToken)
    {
        var errors = await ValidateResearchAreaAsync(request, Guid.Empty, db, cancellationToken);
        if (errors.Count > 0)
        {
            return Results.ValidationProblem(errors);
        }

        var slug = ResolveSlug(request.Slug, request.Name);
        if (await db.AidxResearchAreas.AnyAsync(a => a.Slug == slug, cancellationToken))
        {
            return SlugInUse();
        }

        var area = new AidxResearchArea
        {
            Name = request.Name.Trim(),
            Slug = slug,
            Description = AidxEndpointHelpers.Clean(request.Description),
            SortOrder = request.SortOrder,
        };

        db.Add(area);
        await db.SaveChangesAsync(cancellationToken);

        return Results.Created((string?)null, new AidxIdResponse(area.Id, area.Slug, null));
    }

    private static async Task<IResult> UpdateResearchAreaAsync(Guid id, AidxResearchAreaRequest request, IApplicationDbContext db, CancellationToken cancellationToken)
    {
        var area = await db.AidxResearchAreas.FirstOrDefaultAsync(a => a.Id == id, cancellationToken);
        if (area is null)
        {
            return Results.NotFound();
        }

        var errors = await ValidateResearchAreaAsync(request, id, db, cancellationToken);
        if (errors.Count > 0)
        {
            return Results.ValidationProblem(errors);
        }

        var slug = ResolveSlug(request.Slug, request.Name);
        if (await db.AidxResearchAreas.AnyAsync(a => a.Slug == slug && a.Id != id, cancellationToken))
        {
            return SlugInUse();
        }

        area.Name = request.Name.Trim();
        area.Slug = slug;
        area.Description = AidxEndpointHelpers.Clean(request.Description);
        area.SortOrder = request.SortOrder;

        await db.SaveChangesAsync(cancellationToken);

        return Results.Ok(new AidxIdResponse(area.Id, area.Slug, null));
    }

    private static async Task<Dictionary<string, string[]>> ValidateResearchAreaAsync(
        AidxResearchAreaRequest request, Guid excludeId, IApplicationDbContext db, CancellationToken cancellationToken)
    {
        var errors = new Dictionary<string, string[]>();
        ValidateText(errors, "name", request.Name, 150);
        ValidateSlug(errors, request.Slug);

        if (!string.IsNullOrWhiteSpace(request.Name)
            && await db.AidxResearchAreas.AnyAsync(a => a.Name == request.Name.Trim() && a.Id != excludeId, cancellationToken))
        {
            AidxEndpointHelpers.AddError(errors, "name", "A research area with this name already exists.");
        }

        return errors;
    }

    // ---- Projects ------------------------------------------------------------

    private static async Task<IResult> CreateProjectAsync(AidxProjectRequest request, IApplicationDbContext db, CancellationToken cancellationToken)
    {
        var errors = await ValidateProjectAsync(request, db, cancellationToken);
        if (errors.Count > 0)
        {
            return Results.ValidationProblem(errors);
        }

        var slug = ResolveSlug(request.Slug, request.Title);
        if (await db.AidxProjects.AnyAsync(p => p.Slug == slug, cancellationToken))
        {
            return SlugInUse();
        }

        var project = new AidxProject
        {
            Title = request.Title.Trim(),
            Slug = slug,
            ShortDescription = request.ShortDescription.Trim(),
            Description = request.Description.Trim(),
            StartDate = request.StartDate,
            EndDate = request.EndDate,
            ExternalUrl = AidxEndpointHelpers.Clean(request.ExternalUrl),
            Featured = request.Featured,
        };

        SyncProjectRelations(project, request, db);
        db.Add(project);
        await db.SaveChangesAsync(cancellationToken);

        return Results.Created((string?)null, new AidxIdResponse(project.Id, project.Slug, project.Status.ToString()));
    }

    private static async Task<IResult> UpdateProjectAsync(Guid id, AidxProjectRequest request, IApplicationDbContext db, CancellationToken cancellationToken)
    {
        var project = await db.AidxProjects
            .Include(p => p.ResearchAreas)
            .Include(p => p.Technologies)
            .Include(p => p.Researchers)
            .FirstOrDefaultAsync(p => p.Id == id, cancellationToken);

        if (project is null)
        {
            return Results.NotFound();
        }

        var errors = await ValidateProjectAsync(request, db, cancellationToken);
        if (errors.Count > 0)
        {
            return Results.ValidationProblem(errors);
        }

        var slug = ResolveSlug(request.Slug, request.Title);
        if (await db.AidxProjects.AnyAsync(p => p.Slug == slug && p.Id != id, cancellationToken))
        {
            return SlugInUse();
        }

        project.Title = request.Title.Trim();
        project.Slug = slug;
        project.ShortDescription = request.ShortDescription.Trim();
        project.Description = request.Description.Trim();
        project.StartDate = request.StartDate;
        project.EndDate = request.EndDate;
        project.ExternalUrl = AidxEndpointHelpers.Clean(request.ExternalUrl);
        project.Featured = request.Featured;

        SyncProjectRelations(project, request, db);
        await db.SaveChangesAsync(cancellationToken);

        return Results.Ok(new AidxIdResponse(project.Id, project.Slug, project.Status.ToString()));
    }

    private static async Task<IResult> PublishProjectAsync(Guid id, IApplicationDbContext db, IDateTimeProvider clock, CancellationToken cancellationToken)
    {
        var project = await db.AidxProjects.FirstOrDefaultAsync(p => p.Id == id, cancellationToken);
        if (project is null)
        {
            return Results.NotFound();
        }

        if (CannotPublish(project.Status) is { } conflict)
        {
            return conflict;
        }

        project.Status = AidxContentStatus.Published;
        project.PublishedAt ??= clock.UtcNow;
        await db.SaveChangesAsync(cancellationToken);

        return Results.Ok(new AidxIdResponse(project.Id, project.Slug, project.Status.ToString()));
    }

    private static async Task<IResult> ArchiveProjectAsync(Guid id, IApplicationDbContext db, CancellationToken cancellationToken)
    {
        var project = await db.AidxProjects.FirstOrDefaultAsync(p => p.Id == id, cancellationToken);
        if (project is null)
        {
            return Results.NotFound();
        }

        if (CannotArchive(project.Status) is { } conflict)
        {
            return conflict;
        }

        project.Status = AidxContentStatus.Archived;
        await db.SaveChangesAsync(cancellationToken);

        return Results.Ok(new AidxIdResponse(project.Id, project.Slug, project.Status.ToString()));
    }

    private static async Task<Dictionary<string, string[]>> ValidateProjectAsync(
        AidxProjectRequest request, IApplicationDbContext db, CancellationToken cancellationToken)
    {
        var errors = new Dictionary<string, string[]>();
        ValidateText(errors, "title", request.Title, 200);
        ValidateText(errors, "shortDescription", request.ShortDescription, 500);
        ValidateText(errors, "description", request.Description, 20000);
        ValidateSlug(errors, request.Slug);
        ValidateOptionalUrl(errors, "externalUrl", request.ExternalUrl);

        if (request.StartDate is { } start && request.EndDate is { } end && end < start)
        {
            AidxEndpointHelpers.AddError(errors, "endDate", "End date must be on or after the start date.");
        }

        var technologies = NormalizeTechnologies(request.Technologies);
        if (technologies.Any(t => t.Length > 100))
        {
            AidxEndpointHelpers.AddError(errors, "technologies", "Each technology must be at most 100 characters.");
        }

        var areaIds = (request.ResearchAreaIds ?? []).Distinct().ToList();
        if (areaIds.Count > 0 && await db.AidxResearchAreas.CountAsync(a => areaIds.Contains(a.Id), cancellationToken) != areaIds.Count)
        {
            AidxEndpointHelpers.AddError(errors, "researchAreaIds", "One or more research areas do not exist.");
        }

        var researcherIds = (request.Researchers ?? []).Select(r => r.ResearcherId).Distinct().ToList();
        if (researcherIds.Count > 0 && await db.AidxResearchers.CountAsync(r => researcherIds.Contains(r.Id), cancellationToken) != researcherIds.Count)
        {
            AidxEndpointHelpers.AddError(errors, "researchers", "One or more researchers do not exist.");
        }

        return errors;
    }

    /// <summary>
    /// Brings the three project relationships in line with the request. Existing rows that
    /// are still wanted are kept untouched, stale rows are deleted explicitly, and new rows
    /// are added. Diffing by key avoids a delete and insert of the same primary key in one
    /// SaveChanges, which would fail.
    /// </summary>
    private static void SyncProjectRelations(AidxProject project, AidxProjectRequest request, IApplicationDbContext db)
    {
        var areaIds = (request.ResearchAreaIds ?? []).Distinct().ToHashSet();
        foreach (var stale in project.ResearchAreas.Where(r => !areaIds.Contains(r.ResearchAreaId)).ToList())
        {
            project.ResearchAreas.Remove(stale);
            db.RemoveRange(new[] { stale });
        }

        foreach (var areaId in areaIds.Where(id => project.ResearchAreas.All(r => r.ResearchAreaId != id)))
        {
            project.ResearchAreas.Add(new AidxProjectResearchArea { ProjectId = project.Id, ResearchAreaId = areaId });
        }

        var technologies = NormalizeTechnologies(request.Technologies).ToHashSet(StringComparer.Ordinal);
        foreach (var stale in project.Technologies.Where(t => !technologies.Contains(t.Name)).ToList())
        {
            project.Technologies.Remove(stale);
            db.RemoveRange(new[] { stale });
        }

        foreach (var name in technologies.Where(n => project.Technologies.All(t => t.Name != n)))
        {
            project.Technologies.Add(new AidxProjectTechnology { ProjectId = project.Id, Name = name });
        }

        var researcherRoles = (request.Researchers ?? [])
            .GroupBy(r => r.ResearcherId)
            .ToDictionary(g => g.Key, g => AidxEndpointHelpers.Clean(g.First().Role));

        foreach (var stale in project.Researchers.Where(r => !researcherRoles.ContainsKey(r.ResearcherId)).ToList())
        {
            project.Researchers.Remove(stale);
            db.RemoveRange(new[] { stale });
        }

        foreach (var existing in project.Researchers)
        {
            existing.Role = researcherRoles[existing.ResearcherId];
        }

        foreach (var (researcherId, role) in researcherRoles.Where(kv => project.Researchers.All(r => r.ResearcherId != kv.Key)))
        {
            project.Researchers.Add(new AidxProjectResearcher { ProjectId = project.Id, ResearcherId = researcherId, Role = role });
        }
    }

    // ---- Researchers ---------------------------------------------------------

    private static async Task<IResult> CreateResearcherAsync(AidxResearcherRequest request, IApplicationDbContext db, CancellationToken cancellationToken)
    {
        var errors = ValidateResearcher(request);
        if (errors.Count > 0)
        {
            return Results.ValidationProblem(errors);
        }

        var slug = ResolveSlug(request.Slug, request.DisplayName);
        if (await db.AidxResearchers.AnyAsync(r => r.Slug == slug, cancellationToken))
        {
            return SlugInUse();
        }

        var researcher = new AidxResearcher
        {
            DisplayName = request.DisplayName.Trim(),
            Slug = slug,
            Category = Enum.Parse<AidxResearcherCategory>(request.Category, ignoreCase: true),
            Position = AidxEndpointHelpers.Clean(request.Position),
            Biography = AidxEndpointHelpers.Clean(request.Biography),
            OrcidUrl = AidxEndpointHelpers.Clean(request.OrcidUrl),
            GoogleScholarUrl = AidxEndpointHelpers.Clean(request.GoogleScholarUrl),
            LinkedInUrl = AidxEndpointHelpers.Clean(request.LinkedInUrl),
            WebsiteUrl = AidxEndpointHelpers.Clean(request.WebsiteUrl),
            Published = request.Published,
        };

        db.Add(researcher);
        await db.SaveChangesAsync(cancellationToken);

        return Results.Created((string?)null, new AidxIdResponse(researcher.Id, researcher.Slug, null));
    }

    private static async Task<IResult> UpdateResearcherAsync(Guid id, AidxResearcherRequest request, IApplicationDbContext db, CancellationToken cancellationToken)
    {
        var researcher = await db.AidxResearchers.FirstOrDefaultAsync(r => r.Id == id, cancellationToken);
        if (researcher is null)
        {
            return Results.NotFound();
        }

        var errors = ValidateResearcher(request);
        if (errors.Count > 0)
        {
            return Results.ValidationProblem(errors);
        }

        var slug = ResolveSlug(request.Slug, request.DisplayName);
        if (await db.AidxResearchers.AnyAsync(r => r.Slug == slug && r.Id != id, cancellationToken))
        {
            return SlugInUse();
        }

        researcher.DisplayName = request.DisplayName.Trim();
        researcher.Slug = slug;
        researcher.Category = Enum.Parse<AidxResearcherCategory>(request.Category, ignoreCase: true);
        researcher.Position = AidxEndpointHelpers.Clean(request.Position);
        researcher.Biography = AidxEndpointHelpers.Clean(request.Biography);
        researcher.OrcidUrl = AidxEndpointHelpers.Clean(request.OrcidUrl);
        researcher.GoogleScholarUrl = AidxEndpointHelpers.Clean(request.GoogleScholarUrl);
        researcher.LinkedInUrl = AidxEndpointHelpers.Clean(request.LinkedInUrl);
        researcher.WebsiteUrl = AidxEndpointHelpers.Clean(request.WebsiteUrl);
        researcher.Published = request.Published;

        await db.SaveChangesAsync(cancellationToken);

        return Results.Ok(new AidxIdResponse(researcher.Id, researcher.Slug, null));
    }

    private static Dictionary<string, string[]> ValidateResearcher(AidxResearcherRequest request)
    {
        var errors = new Dictionary<string, string[]>();
        ValidateText(errors, "displayName", request.DisplayName, 150);
        ValidateSlug(errors, request.Slug);
        ValidateOptionalUrl(errors, "orcidUrl", request.OrcidUrl);
        ValidateOptionalUrl(errors, "googleScholarUrl", request.GoogleScholarUrl);
        ValidateOptionalUrl(errors, "linkedInUrl", request.LinkedInUrl);
        ValidateOptionalUrl(errors, "websiteUrl", request.WebsiteUrl);

        if (!AidxEndpointHelpers.TryParseEnum<AidxResearcherCategory>(request.Category, out _))
        {
            AidxEndpointHelpers.AddError(errors, "category", "Unknown researcher category.");
        }

        return errors;
    }

    // ---- Publications --------------------------------------------------------

    private static async Task<IResult> CreatePublicationAsync(AidxPublicationRequest request, IApplicationDbContext db, CancellationToken cancellationToken)
    {
        var errors = await ValidatePublicationAsync(request, Guid.Empty, db, cancellationToken);
        if (errors.Count > 0)
        {
            return Results.ValidationProblem(errors);
        }

        var publication = new AidxPublication
        {
            Title = request.Title.Trim(),
            Abstract = AidxEndpointHelpers.Clean(request.Abstract),
            PublicationType = Enum.Parse<AidxPublicationType>(request.PublicationType, ignoreCase: true),
            Venue = AidxEndpointHelpers.Clean(request.Venue),
            Year = request.Year,
            Doi = AidxEndpointHelpers.Clean(request.Doi),
            ExternalUrl = AidxEndpointHelpers.Clean(request.ExternalUrl),
            Published = request.Published,
        };

        SyncAuthors(publication, request.Authors ?? [], db);
        SyncPublicationLinks(publication, request, db);
        db.Add(publication);
        await db.SaveChangesAsync(cancellationToken);

        return Results.Created((string?)null, new AidxIdResponse(publication.Id, null, null));
    }

    private static async Task<IResult> UpdatePublicationAsync(Guid id, AidxPublicationRequest request, IApplicationDbContext db, CancellationToken cancellationToken)
    {
        var publication = await db.AidxPublications
            .Include(p => p.Authors)
            .Include(p => p.ResearchAreas)
            .Include(p => p.Projects)
            .FirstOrDefaultAsync(p => p.Id == id, cancellationToken);

        if (publication is null)
        {
            return Results.NotFound();
        }

        var errors = await ValidatePublicationAsync(request, id, db, cancellationToken);
        if (errors.Count > 0)
        {
            return Results.ValidationProblem(errors);
        }

        publication.Title = request.Title.Trim();
        publication.Abstract = AidxEndpointHelpers.Clean(request.Abstract);
        publication.PublicationType = Enum.Parse<AidxPublicationType>(request.PublicationType, ignoreCase: true);
        publication.Venue = AidxEndpointHelpers.Clean(request.Venue);
        publication.Year = request.Year;
        publication.Doi = AidxEndpointHelpers.Clean(request.Doi);
        publication.ExternalUrl = AidxEndpointHelpers.Clean(request.ExternalUrl);
        publication.Published = request.Published;

        SyncAuthors(publication, request.Authors ?? [], db);
        SyncPublicationLinks(publication, request, db);
        await db.SaveChangesAsync(cancellationToken);

        return Results.Ok(new AidxIdResponse(publication.Id, null, null));
    }

    private static async Task<Dictionary<string, string[]>> ValidatePublicationAsync(
        AidxPublicationRequest request, Guid excludeId, IApplicationDbContext db, CancellationToken cancellationToken)
    {
        var errors = new Dictionary<string, string[]>();
        ValidateText(errors, "title", request.Title, 400);
        ValidateOptionalUrl(errors, "externalUrl", request.ExternalUrl);

        if (!AidxEndpointHelpers.TryParseEnum<AidxPublicationType>(request.PublicationType, out _))
        {
            AidxEndpointHelpers.AddError(errors, "publicationType", "Unknown publication type.");
        }

        if (request.Year < 1900 || request.Year > 2100)
        {
            AidxEndpointHelpers.AddError(errors, "year", "Year must be between 1900 and 2100.");
        }

        var doi = AidxEndpointHelpers.Clean(request.Doi);
        if (doi is not null && await db.AidxPublications.AnyAsync(p => p.Doi == doi && p.Id != excludeId, cancellationToken))
        {
            AidxEndpointHelpers.AddError(errors, "doi", "A publication with this DOI already exists.");
        }

        var areaIds = (request.ResearchAreaIds ?? []).Distinct().ToList();
        if (areaIds.Count > 0 && await db.AidxResearchAreas.CountAsync(a => areaIds.Contains(a.Id), cancellationToken) != areaIds.Count)
        {
            AidxEndpointHelpers.AddError(errors, "researchAreaIds", "One or more research areas do not exist.");
        }

        var projectIds = (request.ProjectIds ?? []).Distinct().ToList();
        if (projectIds.Count > 0 && await db.AidxProjects.CountAsync(p => projectIds.Contains(p.Id), cancellationToken) != projectIds.Count)
        {
            AidxEndpointHelpers.AddError(errors, "projectIds", "One or more projects do not exist.");
        }

        var authors = request.Authors ?? [];
        for (var index = 0; index < authors.Count; index++)
        {
            var author = authors[index];
            var hasResearcher = author.ResearcherId is not null;
            var externalName = AidxEndpointHelpers.Clean(author.ExternalAuthorName);

            if (hasResearcher == (externalName is not null))
            {
                AidxEndpointHelpers.AddError(errors, $"authors[{index}]", "Set exactly one of researcherId or externalAuthorName.");
            }
        }

        var researcherIds = authors.Where(a => a.ResearcherId is not null).Select(a => a.ResearcherId!.Value).Distinct().ToList();
        if (researcherIds.Count > 0 && await db.AidxResearchers.CountAsync(r => researcherIds.Contains(r.Id), cancellationToken) != researcherIds.Count)
        {
            AidxEndpointHelpers.AddError(errors, "authors", "One or more authors reference a researcher that does not exist.");
        }

        return errors;
    }

    /// <summary>
    /// Replaces a publication's research-area and project links with the requested sets. Same
    /// diff-by-key approach as <see cref="SyncProjectRelations"/>, so no primary key is deleted
    /// and re-inserted in one SaveChanges.
    /// </summary>
    private static void SyncPublicationLinks(AidxPublication publication, AidxPublicationRequest request, IApplicationDbContext db)
    {
        var areaIds = (request.ResearchAreaIds ?? []).Distinct().ToHashSet();
        foreach (var stale in publication.ResearchAreas.Where(l => !areaIds.Contains(l.ResearchAreaId)).ToList())
        {
            publication.ResearchAreas.Remove(stale);
            db.RemoveRange(new[] { stale });
        }

        foreach (var areaId in areaIds.Where(id => publication.ResearchAreas.All(l => l.ResearchAreaId != id)))
        {
            publication.ResearchAreas.Add(new AidxPublicationResearchArea { PublicationId = publication.Id, ResearchAreaId = areaId });
        }

        var projectIds = (request.ProjectIds ?? []).Distinct().ToHashSet();
        foreach (var stale in publication.Projects.Where(l => !projectIds.Contains(l.ProjectId)).ToList())
        {
            publication.Projects.Remove(stale);
            db.RemoveRange(new[] { stale });
        }

        foreach (var projectId in projectIds.Where(id => publication.Projects.All(l => l.ProjectId != id)))
        {
            publication.Projects.Add(new AidxPublicationProject { PublicationId = publication.Id, ProjectId = projectId });
        }
    }

    /// <summary>
    /// Authors are ordered by position 1..n. Rows are updated in place by position rather than
    /// deleted and re-inserted, since position is part of the primary key.
    /// </summary>
    private static void SyncAuthors(AidxPublication publication, IReadOnlyList<AidxAuthorInput> authors, IApplicationDbContext db)
    {
        for (var index = 0; index < authors.Count; index++)
        {
            var position = index + 1;
            var input = authors[index];
            var externalName = AidxEndpointHelpers.Clean(input.ExternalAuthorName);
            var existing = publication.Authors.FirstOrDefault(a => a.Position == position);

            if (existing is null)
            {
                publication.Authors.Add(new AidxPublicationAuthor
                {
                    PublicationId = publication.Id,
                    Position = position,
                    ResearcherId = input.ResearcherId,
                    ExternalAuthorName = externalName,
                });
            }
            else
            {
                existing.ResearcherId = input.ResearcherId;
                existing.ExternalAuthorName = externalName;
            }
        }

        foreach (var extra in publication.Authors.Where(a => a.Position > authors.Count).ToList())
        {
            publication.Authors.Remove(extra);
            db.RemoveRange(new[] { extra });
        }
    }

    // ---- News ----------------------------------------------------------------

    private static async Task<IResult> CreateNewsAsync(AidxNewsRequest request, IApplicationDbContext db, CancellationToken cancellationToken)
    {
        var errors = await ValidateNewsAsync(request, Guid.Empty, db, cancellationToken);
        if (errors.Count > 0)
        {
            return Results.ValidationProblem(errors);
        }

        var slug = ResolveSlug(request.Slug, request.Title);
        if (await db.AidxNews.AnyAsync(n => n.Slug == slug, cancellationToken))
        {
            return SlugInUse();
        }

        var news = new AidxNews
        {
            Slug = slug,
            Title = request.Title.Trim(),
            Summary = request.Summary.Trim(),
            Body = request.Body.Trim(),
            AuthorResearcherId = request.AuthorResearcherId,
        };

        db.Add(news);
        await db.SaveChangesAsync(cancellationToken);

        return Results.Created((string?)null, new AidxIdResponse(news.Id, news.Slug, news.Status.ToString()));
    }

    private static async Task<IResult> UpdateNewsAsync(Guid id, AidxNewsRequest request, IApplicationDbContext db, CancellationToken cancellationToken)
    {
        var news = await db.AidxNews.FirstOrDefaultAsync(n => n.Id == id, cancellationToken);
        if (news is null)
        {
            return Results.NotFound();
        }

        var errors = await ValidateNewsAsync(request, id, db, cancellationToken);
        if (errors.Count > 0)
        {
            return Results.ValidationProblem(errors);
        }

        var slug = ResolveSlug(request.Slug, request.Title);
        if (await db.AidxNews.AnyAsync(n => n.Slug == slug && n.Id != id, cancellationToken))
        {
            return SlugInUse();
        }

        news.Slug = slug;
        news.Title = request.Title.Trim();
        news.Summary = request.Summary.Trim();
        news.Body = request.Body.Trim();
        news.AuthorResearcherId = request.AuthorResearcherId;

        await db.SaveChangesAsync(cancellationToken);

        return Results.Ok(new AidxIdResponse(news.Id, news.Slug, news.Status.ToString()));
    }

    private static async Task<IResult> PublishNewsAsync(Guid id, IApplicationDbContext db, IDateTimeProvider clock, CancellationToken cancellationToken)
    {
        var news = await db.AidxNews.FirstOrDefaultAsync(n => n.Id == id, cancellationToken);
        if (news is null)
        {
            return Results.NotFound();
        }

        if (CannotPublish(news.Status) is { } conflict)
        {
            return conflict;
        }

        news.Status = AidxContentStatus.Published;
        news.PublishedAt ??= clock.UtcNow;
        await db.SaveChangesAsync(cancellationToken);

        return Results.Ok(new AidxIdResponse(news.Id, news.Slug, news.Status.ToString()));
    }

    private static async Task<IResult> ArchiveNewsAsync(Guid id, IApplicationDbContext db, CancellationToken cancellationToken)
    {
        var news = await db.AidxNews.FirstOrDefaultAsync(n => n.Id == id, cancellationToken);
        if (news is null)
        {
            return Results.NotFound();
        }

        if (CannotArchive(news.Status) is { } conflict)
        {
            return conflict;
        }

        news.Status = AidxContentStatus.Archived;
        await db.SaveChangesAsync(cancellationToken);

        return Results.Ok(new AidxIdResponse(news.Id, news.Slug, news.Status.ToString()));
    }

    private static async Task<Dictionary<string, string[]>> ValidateNewsAsync(
        AidxNewsRequest request, Guid excludeId, IApplicationDbContext db, CancellationToken cancellationToken)
    {
        var errors = new Dictionary<string, string[]>();
        ValidateText(errors, "title", request.Title, 300);
        ValidateText(errors, "summary", request.Summary, 500);
        ValidateText(errors, "body", request.Body, 20000);
        ValidateSlug(errors, request.Slug);

        if (request.AuthorResearcherId is { } authorId
            && !await db.AidxResearchers.AnyAsync(r => r.Id == authorId, cancellationToken))
        {
            AidxEndpointHelpers.AddError(errors, "authorResearcherId", "The author researcher does not exist.");
        }

        return errors;
    }

    // ---- Events --------------------------------------------------------------

    private static async Task<IResult> CreateEventAsync(AidxEventRequest request, IApplicationDbContext db, CancellationToken cancellationToken)
    {
        var errors = ValidateEvent(request);
        if (errors.Count > 0)
        {
            return Results.ValidationProblem(errors);
        }

        var slug = ResolveSlug(request.Slug, request.Title);
        if (await db.AidxEvents.AnyAsync(e => e.Slug == slug, cancellationToken))
        {
            return SlugInUse();
        }

        var evt = new AidxEvent
        {
            Slug = slug,
            Title = request.Title.Trim(),
            Description = request.Description.Trim(),
            StartsAt = request.StartsAt,
            EndsAt = request.EndsAt,
            Location = AidxEndpointHelpers.Clean(request.Location),
            RegistrationUrl = AidxEndpointHelpers.Clean(request.RegistrationUrl),
            SpeakerName = AidxEndpointHelpers.Clean(request.SpeakerName),
        };

        db.Add(evt);
        await db.SaveChangesAsync(cancellationToken);

        return Results.Created((string?)null, new AidxIdResponse(evt.Id, evt.Slug, evt.Status.ToString()));
    }

    private static async Task<IResult> UpdateEventAsync(Guid id, AidxEventRequest request, IApplicationDbContext db, CancellationToken cancellationToken)
    {
        var evt = await db.AidxEvents.FirstOrDefaultAsync(e => e.Id == id, cancellationToken);
        if (evt is null)
        {
            return Results.NotFound();
        }

        var errors = ValidateEvent(request);
        if (errors.Count > 0)
        {
            return Results.ValidationProblem(errors);
        }

        var slug = ResolveSlug(request.Slug, request.Title);
        if (await db.AidxEvents.AnyAsync(e => e.Slug == slug && e.Id != id, cancellationToken))
        {
            return SlugInUse();
        }

        evt.Slug = slug;
        evt.Title = request.Title.Trim();
        evt.Description = request.Description.Trim();
        evt.StartsAt = request.StartsAt;
        evt.EndsAt = request.EndsAt;
        evt.Location = AidxEndpointHelpers.Clean(request.Location);
        evt.RegistrationUrl = AidxEndpointHelpers.Clean(request.RegistrationUrl);
        evt.SpeakerName = AidxEndpointHelpers.Clean(request.SpeakerName);

        await db.SaveChangesAsync(cancellationToken);

        return Results.Ok(new AidxIdResponse(evt.Id, evt.Slug, evt.Status.ToString()));
    }

    private static async Task<IResult> PublishEventAsync(Guid id, IApplicationDbContext db, CancellationToken cancellationToken)
    {
        var evt = await db.AidxEvents.FirstOrDefaultAsync(e => e.Id == id, cancellationToken);
        if (evt is null)
        {
            return Results.NotFound();
        }

        if (CannotPublish(evt.Status) is { } conflict)
        {
            return conflict;
        }

        evt.Status = AidxContentStatus.Published;
        await db.SaveChangesAsync(cancellationToken);

        return Results.Ok(new AidxIdResponse(evt.Id, evt.Slug, evt.Status.ToString()));
    }

    private static async Task<IResult> ArchiveEventAsync(Guid id, IApplicationDbContext db, CancellationToken cancellationToken)
    {
        var evt = await db.AidxEvents.FirstOrDefaultAsync(e => e.Id == id, cancellationToken);
        if (evt is null)
        {
            return Results.NotFound();
        }

        if (CannotArchive(evt.Status) is { } conflict)
        {
            return conflict;
        }

        evt.Status = AidxContentStatus.Archived;
        await db.SaveChangesAsync(cancellationToken);

        return Results.Ok(new AidxIdResponse(evt.Id, evt.Slug, evt.Status.ToString()));
    }

    private static Dictionary<string, string[]> ValidateEvent(AidxEventRequest request)
    {
        var errors = new Dictionary<string, string[]>();
        ValidateText(errors, "title", request.Title, 300);
        ValidateText(errors, "description", request.Description, 10000);
        ValidateSlug(errors, request.Slug);
        ValidateOptionalUrl(errors, "registrationUrl", request.RegistrationUrl);

        if (request.EndsAt is { } endsAt && endsAt < request.StartsAt)
        {
            AidxEndpointHelpers.AddError(errors, "endsAt", "End time must be on or after the start time.");
        }

        return errors;
    }

    // ---- Deletes -------------------------------------------------------------
    // Deletes remove only the row and let the database cascade to its join rows. Lab
    // content never owns a StepIn account, job or application, so none of those are touched.
    // A researcher with authorship history is refused with 409 rather than silently losing
    // the authorship link, which is the Restrict rule from the schema made explicit.

    private static Task<IResult> DeleteResearchAreaAsync(Guid id, IApplicationDbContext db, CancellationToken cancellationToken) =>
        DeleteEntityAsync(db.AidxResearchAreas, id, db, cancellationToken);

    private static Task<IResult> DeleteProjectAsync(Guid id, IApplicationDbContext db, CancellationToken cancellationToken) =>
        DeleteEntityAsync(db.AidxProjects, id, db, cancellationToken);

    private static Task<IResult> DeletePublicationAsync(Guid id, IApplicationDbContext db, CancellationToken cancellationToken) =>
        DeleteEntityAsync(db.AidxPublications, id, db, cancellationToken);

    private static Task<IResult> DeleteNewsAsync(Guid id, IApplicationDbContext db, CancellationToken cancellationToken) =>
        DeleteEntityAsync(db.AidxNews, id, db, cancellationToken);

    private static Task<IResult> DeleteEventAsync(Guid id, IApplicationDbContext db, CancellationToken cancellationToken) =>
        DeleteEntityAsync(db.AidxEvents, id, db, cancellationToken);

    private static async Task<IResult> DeleteResearcherAsync(Guid id, IApplicationDbContext db, CancellationToken cancellationToken)
    {
        if (await db.AidxPublicationAuthors.AnyAsync(a => a.ResearcherId == id, cancellationToken))
        {
            return Results.Problem(
                title: "Researcher is listed as an author on one or more publications. Remove those authorships first.",
                statusCode: StatusCodes.Status409Conflict);
        }

        return await DeleteEntityAsync(db.AidxResearchers, id, db, cancellationToken);
    }

    private static async Task<IResult> DeleteEntityAsync<T>(IQueryable<T> set, Guid id, IApplicationDbContext db, CancellationToken cancellationToken)
        where T : class
    {
        var entity = await set.FirstOrDefaultAsync(e => EF.Property<Guid>(e, "Id") == id, cancellationToken);
        if (entity is null)
        {
            return Results.NotFound();
        }

        db.RemoveRange(new[] { entity });
        await db.SaveChangesAsync(cancellationToken);

        return Results.NoContent();
    }

    // ---- Shared rules --------------------------------------------------------

    /// <summary>Only a draft or archived item can be published. Returns null when allowed.</summary>
    private static IResult? CannotPublish(AidxContentStatus status) =>
        status == AidxContentStatus.Published
            ? Results.Problem(title: "Already published.", statusCode: StatusCodes.Status409Conflict)
            : null;

    /// <summary>Only a published item can be archived. Returns null when allowed.</summary>
    private static IResult? CannotArchive(AidxContentStatus status) =>
        status != AidxContentStatus.Published
            ? Results.Problem(title: "Only published content can be archived.", statusCode: StatusCodes.Status409Conflict)
            : null;

    private static IResult SlugInUse() =>
        Results.Problem(title: "Slug is already in use.", statusCode: StatusCodes.Status409Conflict);

    private static string ResolveSlug(string? requested, string fallbackText) =>
        string.IsNullOrWhiteSpace(requested)
            ? AidxSlug.FromText(fallbackText)
            : requested.Trim().ToLowerInvariant();

    private static void ValidateSlug(Dictionary<string, string[]> errors, string? requested)
    {
        if (string.IsNullOrWhiteSpace(requested))
        {
            return;
        }

        if (!AidxSlug.IsValid(requested.Trim().ToLowerInvariant()))
        {
            AidxEndpointHelpers.AddError(errors, "slug", "Slug may contain only lowercase letters, digits and single hyphens.");
        }
    }

    private static void ValidateText(Dictionary<string, string[]> errors, string key, string? value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            AidxEndpointHelpers.AddError(errors, key, "This field is required.");
        }
        else if (value.Trim().Length > maxLength)
        {
            AidxEndpointHelpers.AddError(errors, key, $"Must be at most {maxLength} characters.");
        }
    }

    private static void ValidateOptionalUrl(Dictionary<string, string[]> errors, string key, string? value)
    {
        var cleaned = AidxEndpointHelpers.Clean(value);
        if (cleaned is not null && (cleaned.Length > 500 || !AidxEndpointHelpers.IsHttpUrl(cleaned)))
        {
            AidxEndpointHelpers.AddError(errors, key, "Must be an http or https URL of at most 500 characters.");
        }
    }

    private static List<string> NormalizeTechnologies(IReadOnlyList<string>? names) =>
        (names ?? [])
            .Select(n => n.Trim())
            .Where(n => n.Length > 0)
            .Distinct(StringComparer.Ordinal)
            .ToList();
}
