using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using StepIn.Application.Common.Interfaces;
using StepIn.Domain.Aidx;
using StepIn.Infrastructure.Aidx;
using StepIn.Infrastructure.Persistence;

namespace StepIn.Api.Endpoints;

/// <summary>
/// Admin CRUD and publishing for AIDX content. Every route requires <c>RequireAdmin</c>,
/// so applicants and recruiters get 403 and anonymous callers get 401. Admins can write
/// content only through these routes; the public routes are read-only.
///
/// Mutations of workflow content (Project/News/Event/Publication) route through the same
/// <see cref="AidxContentEditingService"/> the membership-gated endpoints use — there is
/// structurally one place that writes these rows, not two that could drift apart. A Draft
/// or ChangesRequested row is still edited in place (nothing public is at stake yet); editing
/// an already-Published row never touches its live fields — it opens (or updates) a pending
/// revision instead, which must then go through the normal submit/approve cycle (the same
/// generic <c>/api/v1/aidx/content/{contentType}/{id}/...</c> routes Admin already has access
/// to, since Admin satisfies every AIDX review check with no membership row needed) before it
/// takes effect. "Publish" here therefore specifically means "first publish of a never-yet-
/// published Draft" — it chains Submit→Approve through the engine so even this one-click admin
/// action produces a real, audited snapshot, rather than a bare field flip with no history.
///
/// Publication has no independent <c>Published</c> flag a client can set directly — that was
/// the mechanism by which Status and Published could previously diverge. Both are now set only
/// by the engine's <c>onFirstPublish</c>/<c>onArchive</c> hooks, in lockstep, every time.
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
        admin.MapPost("/publications/{id:guid}/publish", PublishPublicationAsync).WithName("PublishAidxPublication");
        admin.MapPost("/publications/{id:guid}/archive", ArchivePublicationAsync).WithName("ArchiveAidxPublication");

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
        admin.MapGet("/people", ListPeopleAsync).WithName("ListAidxPeopleAdmin");
        admin.MapGet("/people/{id:guid}", GetPersonAsync).WithName("GetAidxPersonAdmin");
        admin.MapGet("/publications", ListPublicationsAdminAsync).WithName("ListAidxPublicationsAdmin");
        admin.MapGet("/publications/{id:guid}", GetPublicationAdminAsync).WithName("GetAidxPublicationAdmin");
        admin.MapGet("/news", ListNewsAdminAsync).WithName("ListAidxNewsAdmin");
        admin.MapGet("/news/{id:guid}", GetNewsAdminAsync).WithName("GetAidxNewsAdmin");
        admin.MapGet("/events", ListEventsAdminAsync).WithName("ListAidxEventsAdmin");
        admin.MapGet("/events/{id:guid}", GetEventAdminAsync).WithName("GetAidxEventAdmin");

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
            // A revision is a separate row sharing the canonical's content shape — it is never
            // an independent project in the admin list. See it via the revision-history route.
            .Where(p => p.RevisionOfId == null)
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
    private static async Task<IResult> GetProjectAsync(
        Guid id, IApplicationDbContext db, AidxContentEditingService editing, CancellationToken cancellationToken)
    {
        var project = await db.AidxProjects
            .Include(p => p.ResearchAreas)
            .Include(p => p.Technologies)
            .Include(p => p.Researchers).ThenInclude(r => r.Researcher)
            .FirstOrDefaultAsync(p => p.Id == id && p.RevisionOfId == null, cancellationToken);

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
                .ToList(),
            editing.GetVersion(project)));
    }

    private static async Task<IResult> ListPeopleAsync(
        string? search,
        string? category,
        bool? published,
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

        var query = db.AidxResearchers.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
        {
            query = query.Where(r => EF.Functions.ILike(r.DisplayName, $"%{search.Trim()}%"));
        }

        if (AidxEndpointHelpers.TryParseEnum<AidxResearcherCategory>(category, out var parsedCategory))
        {
            query = query.Where(r => r.Category == parsedCategory);
        }

        if (published is { } isPublished)
        {
            query = query.Where(r => r.Published == isPublished);
        }

        query = query.OrderBy(r => r.DisplayName).ThenBy(r => r.Id);

        var (people, total) = await AidxEndpointHelpers.PageAsync(query, page ?? 1, pageSize ?? AidxEndpointHelpers.DefaultPageSize, cancellationToken);

        return Results.Ok(new AidxPageResponse<AidxAdminPersonSummaryResponse>(
            people.Select(r => new AidxAdminPersonSummaryResponse(
                r.Id,
                r.Slug,
                r.DisplayName,
                r.Category.ToString(),
                r.Position,
                r.Published)).ToList(),
            page ?? 1,
            pageSize ?? AidxEndpointHelpers.DefaultPageSize,
            total));
    }

    /// <summary>
    /// The admin edit view. Linked projects and authored publications are read-only here. They show an
    /// admin what would be affected by a deletion, and publication authorship is never edited through the
    /// people CMS.
    /// </summary>
    private static async Task<IResult> GetPersonAsync(Guid id, IApplicationDbContext db, CancellationToken cancellationToken)
    {
        var person = await db.AidxResearchers
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.Id == id, cancellationToken);

        if (person is null)
        {
            return Results.NotFound();
        }

        var projects = await db.AidxProjects
            .AsNoTracking()
            .Where(p => p.Researchers.Any(link => link.ResearcherId == id))
            .OrderBy(p => p.Title)
            .Select(p => new AidxLinkedProjectResponse(p.Id, p.Title, p.Slug, p.Status.ToString()))
            .ToListAsync(cancellationToken);

        var publications = await db.AidxPublications
            .AsNoTracking()
            .Where(p => p.Authors.Any(author => author.ResearcherId == id))
            .OrderByDescending(p => p.Year)
            .ThenBy(p => p.Title)
            .Select(p => new AidxLinkedPublicationResponse(p.Id, p.Title, p.Year))
            .ToListAsync(cancellationToken);

        return Results.Ok(new AidxAdminPersonDetailResponse(
            person.Id,
            person.Slug,
            person.DisplayName,
            person.Category.ToString(),
            person.Position,
            person.Biography,
            person.OrcidUrl,
            person.GoogleScholarUrl,
            person.LinkedInUrl,
            person.WebsiteUrl,
            person.Published,
            projects,
            publications));
    }

    private static async Task<IResult> ListPublicationsAdminAsync(
        string? q,
        string? type,
        int? year,
        bool? published,
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

        if (year is { } filterYear && (filterYear < 1900 || filterYear > 2100))
        {
            AidxEndpointHelpers.AddError(errors, "year", "Year must be between 1900 and 2100.");
        }

        if (errors.Count > 0)
        {
            return Results.ValidationProblem(errors);
        }

        var query = db.AidxPublications
            .AsNoTracking()
            .Include(p => p.Authors).ThenInclude(a => a.Researcher)
            .Where(p => p.RevisionOfId == null)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(q))
        {
            var term = $"%{q.Trim()}%";
            query = query.Where(p => EF.Functions.ILike(p.Title, term) || (p.Abstract != null && EF.Functions.ILike(p.Abstract, term)));
        }

        if (AidxEndpointHelpers.TryParseEnum<AidxPublicationType>(type, out var parsedType))
        {
            query = query.Where(p => p.PublicationType == parsedType);
        }

        if (year is { } yearFilter)
        {
            query = query.Where(p => p.Year == yearFilter);
        }

        if (published is { } isPublished)
        {
            query = query.Where(p => p.Published == isPublished);
        }

        query = query.OrderByDescending(p => p.Year).ThenBy(p => p.Title).ThenBy(p => p.Id);

        var (publications, total) = await AidxEndpointHelpers.PageAsync(query, page ?? 1, pageSize ?? AidxEndpointHelpers.DefaultPageSize, cancellationToken);

        return Results.Ok(new AidxPageResponse<AidxAdminPublicationSummaryResponse>(
            publications.Select(p => new AidxAdminPublicationSummaryResponse(
                p.Id,
                p.Title,
                p.PublicationType.ToString(),
                p.Year,
                p.Venue,
                p.Doi,
                p.Published,
                p.Authors.OrderBy(a => a.Position).Select(AuthorDisplayName).ToList())).ToList(),
            page ?? 1,
            pageSize ?? AidxEndpointHelpers.DefaultPageSize,
            total));
    }

    /// <summary>
    /// The admin edit view. Authors keep their order, and their researcher's public flag is included so the
    /// form can say when an author is currently hidden. Hidden authors stay linked, as the public list does.
    /// </summary>
    private static async Task<IResult> GetPublicationAdminAsync(
        Guid id, IApplicationDbContext db, AidxContentEditingService editing, CancellationToken cancellationToken)
    {
        var publication = await db.AidxPublications
            .Include(p => p.Authors).ThenInclude(a => a.Researcher)
            .Include(p => p.ResearchAreas)
            .Include(p => p.Projects).ThenInclude(l => l.Project)
            .FirstOrDefaultAsync(p => p.Id == id && p.RevisionOfId == null, cancellationToken);

        if (publication is null)
        {
            return Results.NotFound();
        }

        return Results.Ok(new AidxAdminPublicationDetailResponse(
            publication.Id,
            publication.Title,
            publication.Abstract,
            publication.PublicationType.ToString(),
            publication.Venue,
            publication.Year,
            publication.Doi,
            publication.ExternalUrl,
            publication.Published,
            publication.Authors
                .OrderBy(a => a.Position)
                .Select(a => new AidxAdminPublicationAuthorResponse(
                    a.Position,
                    a.ResearcherId,
                    a.ExternalAuthorName,
                    AuthorDisplayName(a),
                    a.Researcher?.Published))
                .ToList(),
            publication.ResearchAreas.Select(r => r.ResearchAreaId).ToList(),
            publication.Projects
                .Select(l => new AidxLinkedProjectResponse(l.ProjectId, l.Project.Title, l.Project.Slug, l.Project.Status.ToString()))
                .OrderBy(p => p.Title)
                .ToList(),
            publication.Status.ToString(),
            editing.GetVersion(publication)));
    }

    private static async Task<IResult> ListNewsAdminAsync(
        string? q,
        string? status,
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

        var query = db.AidxNews.AsNoTracking().Where(n => n.RevisionOfId == null).AsQueryable();

        if (!string.IsNullOrWhiteSpace(q))
        {
            var term = $"%{q.Trim()}%";
            query = query.Where(n => EF.Functions.ILike(n.Title, term) || EF.Functions.ILike(n.Summary, term));
        }

        if (AidxEndpointHelpers.TryParseEnum<AidxContentStatus>(status, out var parsedStatus))
        {
            query = query.Where(n => n.Status == parsedStatus);
        }

        query = query.OrderByDescending(n => n.CreatedAt).ThenBy(n => n.Id);

        var (items, total) = await AidxEndpointHelpers.PageAsync(query, page ?? 1, pageSize ?? AidxEndpointHelpers.DefaultPageSize, cancellationToken);

        return Results.Ok(new AidxPageResponse<AidxAdminNewsSummaryResponse>(
            items.Select(n => new AidxAdminNewsSummaryResponse(
                n.Id,
                n.Slug,
                n.Title,
                n.Summary,
                n.Status.ToString(),
                n.PublishedAt,
                n.CreatedAt,
                n.UpdatedAt)).ToList(),
            page ?? 1,
            pageSize ?? AidxEndpointHelpers.DefaultPageSize,
            total));
    }

    private static async Task<IResult> GetNewsAdminAsync(
        Guid id, IApplicationDbContext db, AidxContentEditingService editing, CancellationToken cancellationToken)
    {
        var news = await db.AidxNews
            .FirstOrDefaultAsync(n => n.Id == id && n.RevisionOfId == null, cancellationToken);

        if (news is null)
        {
            return Results.NotFound();
        }

        // The author is a researcher record, not an account. Only the display name is returned.
        string? authorName = null;
        if (news.AuthorResearcherId is { } authorId)
        {
            authorName = await db.AidxResearchers
                .AsNoTracking()
                .Where(r => r.Id == authorId)
                .Select(r => r.DisplayName)
                .FirstOrDefaultAsync(cancellationToken);
        }

        return Results.Ok(new AidxAdminNewsDetailResponse(
            news.Id,
            news.Slug,
            news.Title,
            news.Summary,
            news.Body,
            news.Status.ToString(),
            news.PublishedAt,
            news.CreatedAt,
            news.UpdatedAt,
            news.AuthorResearcherId,
            authorName,
            editing.GetVersion(news)));
    }

    private static async Task<IResult> ListEventsAdminAsync(
        string? q,
        string? status,
        bool? upcoming,
        int? page,
        int? pageSize,
        IDateTimeProvider clock,
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

        var query = db.AidxEvents.AsNoTracking().Where(e => e.RevisionOfId == null).AsQueryable();

        if (!string.IsNullOrWhiteSpace(q))
        {
            var term = $"%{q.Trim()}%";
            query = query.Where(e => EF.Functions.ILike(e.Title, term)
                || EF.Functions.ILike(e.Description, term)
                || (e.Location != null && EF.Functions.ILike(e.Location, term)));
        }

        if (AidxEndpointHelpers.TryParseEnum<AidxContentStatus>(status, out var parsedStatus))
        {
            query = query.Where(e => e.Status == parsedStatus);
        }

        if (upcoming == true)
        {
            var now = clock.UtcNow;
            query = query.Where(e => e.StartsAt >= now);
        }

        query = query.OrderBy(e => e.StartsAt).ThenBy(e => e.Id);

        var (items, total) = await AidxEndpointHelpers.PageAsync(query, page ?? 1, pageSize ?? AidxEndpointHelpers.DefaultPageSize, cancellationToken);

        return Results.Ok(new AidxPageResponse<AidxAdminEventSummaryResponse>(
            items.Select(e => new AidxAdminEventSummaryResponse(
                e.Id,
                e.Slug,
                e.Title,
                e.Status.ToString(),
                e.StartsAt,
                e.EndsAt,
                e.Location,
                e.SpeakerName)).ToList(),
            page ?? 1,
            pageSize ?? AidxEndpointHelpers.DefaultPageSize,
            total));
    }

    private static async Task<IResult> GetEventAdminAsync(
        Guid id, IApplicationDbContext db, AidxContentEditingService editing, CancellationToken cancellationToken)
    {
        var evt = await db.AidxEvents
            .FirstOrDefaultAsync(e => e.Id == id && e.RevisionOfId == null, cancellationToken);

        if (evt is null)
        {
            return Results.NotFound();
        }

        // The image key is not returned: there is no media upload in this phase.
        return Results.Ok(new AidxAdminEventDetailResponse(
            evt.Id,
            evt.Slug,
            evt.Title,
            evt.Description,
            evt.Status.ToString(),
            evt.StartsAt,
            evt.EndsAt,
            evt.Location,
            evt.RegistrationUrl,
            evt.SpeakerName,
            evt.CreatedAt,
            evt.UpdatedAt,
            editing.GetVersion(evt)));
    }

    /// <summary>Researcher name when linked, otherwise the external name. Shared by list and detail.</summary>
    private static string AuthorDisplayName(AidxPublicationAuthor author) =>
        author.Researcher is not null ? author.Researcher.DisplayName : author.ExternalAuthorName ?? string.Empty;

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

    /// <summary>
    /// Draft/ChangesRequested projects are edited in place — nothing public is at stake yet.
    /// A Published project's content fields are never touched directly:
    /// <see cref="AidxContentEditingService.BeginEditAsync{TContent}"/> opens (or reuses) a
    /// pending revision, and the field updates below (other than slug) land on that revision row
    /// instead of the live canonical one. The response's <c>RevisionOfId</c> tells the caller
    /// which happened.
    /// <para>
    /// The slug is the one exception, applied straight to the canonical row regardless of status:
    /// it is a routing/addressing detail, not reviewable editorial content, and admins have always
    /// been able to rename a published item's URL in one step — a real, tested capability this
    /// change must not remove.
    /// </para>
    /// </summary>
    private static async Task<IResult> UpdateProjectAsync(
        Guid id, AidxProjectRequest request, ApplicationDbContext db, AidxContentEditingService editing, CancellationToken cancellationToken)
    {
        var canonical = await db.AidxProjects
            .Include(p => p.ResearchAreas)
            .Include(p => p.Technologies)
            .Include(p => p.Researchers)
            .FirstOrDefaultAsync(p => p.Id == id && p.RevisionOfId == null, cancellationToken);

        if (canonical is null)
        {
            return Results.NotFound();
        }

        if (request.ExpectedVersion is { } expected && !string.Equals(editing.GetVersion(canonical), expected, StringComparison.Ordinal))
        {
            return VersionConflict();
        }

        var errors = await ValidateProjectAsync(request, db, cancellationToken);
        if (errors.Count > 0)
        {
            return Results.ValidationProblem(errors);
        }

        var slug = ResolveSlug(request.Slug, request.Title);
        if (slug != canonical.Slug && await db.AidxProjects.AnyAsync(p => p.Slug == slug && p.Id != id && p.RevisionOfId == null, cancellationToken))
        {
            return SlugInUse();
        }

        canonical.Slug = slug;

        var target = await editing.BeginEditAsync(db.AidxProjects, canonical, AidxWorkflowSharedHelpers.ReparentProject, cancellationToken);

        target.Title = request.Title.Trim();
        target.ShortDescription = request.ShortDescription.Trim();
        target.Description = request.Description.Trim();
        target.StartDate = request.StartDate;
        target.EndDate = request.EndDate;
        target.ExternalUrl = AidxEndpointHelpers.Clean(request.ExternalUrl);
        target.Featured = request.Featured;

        SyncProjectRelations(target, request, db);
        await db.SaveChangesAsync(cancellationToken);

        return Results.Ok(new AidxIdResponse(target.Id, canonical.Slug, target.Status.ToString(), editing.GetVersion(target), target.RevisionOfId));
    }

    /// <summary>
    /// Publishes a canonical project: a Draft/ChangesRequested one chains Submit then Approve
    /// through the shared engine, so this one-click admin action still produces a real, audited
    /// snapshot. An Archived one republishes directly (no re-review — its content is exactly what
    /// was already approved; see <see cref="AidxEditorialWorkflow"/>). An already-Published project
    /// is refused (409) — approving a *pending revision* of an already-published project is a
    /// different action, done via <c>POST /api/v1/aidx/content/project/{id}/approve</c>, which
    /// Admin already has access to.
    /// </summary>
    private static Task<IResult> PublishProjectAsync(
        Guid id, ClaimsPrincipal principal, ApplicationDbContext db, AidxContentEditingService editing, CancellationToken cancellationToken) =>
        PublishThroughWorkflowAsync(db.AidxProjects, id, AidxContentType.Project, onFirstPublish: null, principal, db, editing, cancellationToken);

    private static Task<IResult> ArchiveProjectAsync(
        Guid id, ClaimsPrincipal principal, ApplicationDbContext db, AidxContentEditingService editing, CancellationToken cancellationToken) =>
        ArchiveThroughWorkflowAsync(db.AidxProjects, id, AidxContentType.Project, onArchive: null, principal, db, editing, cancellationToken);

    internal static async Task<Dictionary<string, string[]>> ValidateProjectAsync(
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
    internal static void SyncProjectRelations(AidxProject project, AidxProjectRequest request, IApplicationDbContext db)
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

        // A missing slug keeps the existing public URL. A name change never rewrites it silently.
        var slug = string.IsNullOrWhiteSpace(request.Slug) ? researcher.Slug : ResolveSlug(request.Slug, request.DisplayName);
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

    /// <summary>
    /// Always creates a Draft. There is no "create as published" shortcut — publishing is a
    /// separate, explicit, audited action (<see cref="PublishPublicationAsync"/>).
    /// </summary>
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
            Published = false,
        };

        SyncAuthors(publication, request.Authors ?? [], db);
        SyncPublicationLinks(publication, request, db);
        db.Add(publication);
        await db.SaveChangesAsync(cancellationToken);

        return Results.Created((string?)null, new AidxIdResponse(publication.Id, null, publication.Status.ToString()));
    }

    /// <summary>
    /// Draft/ChangesRequested publications are edited in place. Editing an already-Published
    /// publication opens (or reuses) a pending revision instead — see <see cref="UpdateProjectAsync"/>
    /// for the shared mechanics. <c>Published</c> is never settable from the request; it moves only
    /// via <see cref="PublishPublicationAsync"/>/<see cref="ArchivePublicationAsync"/>.
    /// </summary>
    private static async Task<IResult> UpdatePublicationAsync(
        Guid id, AidxPublicationRequest request, ApplicationDbContext db, AidxContentEditingService editing, CancellationToken cancellationToken)
    {
        var canonical = await db.AidxPublications
            .Include(p => p.Authors)
            .Include(p => p.ResearchAreas)
            .Include(p => p.Projects)
            .FirstOrDefaultAsync(p => p.Id == id && p.RevisionOfId == null, cancellationToken);

        if (canonical is null)
        {
            return Results.NotFound();
        }

        if (request.ExpectedVersion is { } expected && !string.Equals(editing.GetVersion(canonical), expected, StringComparison.Ordinal))
        {
            return VersionConflict();
        }

        var errors = await ValidatePublicationAsync(request, id, db, cancellationToken);
        if (errors.Count > 0)
        {
            return Results.ValidationProblem(errors);
        }

        var target = await editing.BeginEditAsync(db.AidxPublications, canonical, AidxWorkflowSharedHelpers.ReparentPublication, cancellationToken);

        target.Title = request.Title.Trim();
        target.Abstract = AidxEndpointHelpers.Clean(request.Abstract);
        target.PublicationType = Enum.Parse<AidxPublicationType>(request.PublicationType, ignoreCase: true);
        target.Venue = AidxEndpointHelpers.Clean(request.Venue);
        target.Year = request.Year;
        target.Doi = AidxEndpointHelpers.Clean(request.Doi);
        target.ExternalUrl = AidxEndpointHelpers.Clean(request.ExternalUrl);

        // An omitted collection means "unchanged", not "empty". A title-only edit must never remove
        // authors, areas or projects, so each missing list is rebuilt from the rows already on the
        // row actually being edited (the revision, once one has been opened — not the canonical).
        var effective = request with
        {
            Authors = request.Authors ?? target.Authors
                .OrderBy(a => a.Position)
                .Select(a => new AidxAuthorInput(a.ResearcherId, a.ExternalAuthorName))
                .ToList(),
            ResearchAreaIds = request.ResearchAreaIds ?? target.ResearchAreas.Select(r => r.ResearchAreaId).ToList(),
            ProjectIds = request.ProjectIds ?? target.Projects.Select(p => p.ProjectId).ToList(),
        };

        SyncAuthors(target, effective.Authors ?? [], db);
        SyncPublicationLinks(target, effective, db);
        await db.SaveChangesAsync(cancellationToken);

        return Results.Ok(new AidxIdResponse(target.Id, null, target.Status.ToString(), editing.GetVersion(target), target.RevisionOfId));
    }

    /// <summary>
    /// First publish of a never-yet-published Draft publication: chains Submit then Approve through
    /// the shared engine, which sets <c>Status = Published</c> and, via the engine's
    /// <c>onFirstPublish</c> hook, <c>Published = true</c> in the same save — the two fields can no
    /// longer diverge, since nothing else is allowed to write either of them.
    /// </summary>
    private static Task<IResult> PublishPublicationAsync(
        Guid id, ClaimsPrincipal principal, ApplicationDbContext db, AidxContentEditingService editing, CancellationToken cancellationToken) =>
        PublishThroughWorkflowAsync(db.AidxPublications, id, AidxContentType.Publication, onFirstPublish: p => p.Published = true, principal, db, editing, cancellationToken);

    private static Task<IResult> ArchivePublicationAsync(
        Guid id, ClaimsPrincipal principal, ApplicationDbContext db, AidxContentEditingService editing, CancellationToken cancellationToken) =>
        ArchiveThroughWorkflowAsync(db.AidxPublications, id, AidxContentType.Publication, onArchive: p => p.Published = false, principal, db, editing, cancellationToken);

    internal static async Task<Dictionary<string, string[]>> ValidatePublicationAsync(
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
        // RevisionOfId == null: a revision clone of the row being edited carries the same DOI and
        // must never be mistaken for a collision with itself.
        if (doi is not null && await db.AidxPublications.AnyAsync(p => p.Doi == doi && p.Id != excludeId && p.RevisionOfId == null, cancellationToken))
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

        var linkedIds = authors.Where(a => a.ResearcherId is not null).Select(a => a.ResearcherId!.Value).ToList();
        if (linkedIds.Count != linkedIds.Distinct().Count())
        {
            AidxEndpointHelpers.AddError(errors, "authors", "The same researcher cannot be listed as an author twice.");
        }

        var researcherIds = linkedIds.Distinct().ToList();
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
    internal static void SyncPublicationLinks(AidxPublication publication, AidxPublicationRequest request, IApplicationDbContext db)
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
    internal static void SyncAuthors(AidxPublication publication, IReadOnlyList<AidxAuthorInput> authors, IApplicationDbContext db)
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

    private static async Task<IResult> UpdateNewsAsync(
        Guid id, AidxNewsRequest request, ApplicationDbContext db, AidxContentEditingService editing, CancellationToken cancellationToken)
    {
        var canonical = await db.AidxNews.FirstOrDefaultAsync(n => n.Id == id && n.RevisionOfId == null, cancellationToken);
        if (canonical is null)
        {
            return Results.NotFound();
        }

        if (request.ExpectedVersion is { } expected && !string.Equals(editing.GetVersion(canonical), expected, StringComparison.Ordinal))
        {
            return VersionConflict();
        }

        var errors = await ValidateNewsAsync(request, id, db, cancellationToken);
        if (errors.Count > 0)
        {
            return Results.ValidationProblem(errors);
        }

        // A missing slug keeps the stored one, so a title change never rewrites it. Renaming it is
        // applied straight to the canonical row regardless of status — it is a routing detail, not
        // reviewable editorial content, and admins have always been able to move a published item's
        // URL in one step.
        var slug = string.IsNullOrWhiteSpace(request.Slug) ? canonical.Slug : ResolveSlug(request.Slug, request.Title);
        if (slug != canonical.Slug && await db.AidxNews.AnyAsync(n => n.Slug == slug && n.Id != id && n.RevisionOfId == null, cancellationToken))
        {
            return SlugInUse();
        }

        canonical.Slug = slug;

        var target = await editing.BeginEditAsync(db.AidxNews, canonical, null, cancellationToken);

        target.Title = request.Title.Trim();
        target.Summary = request.Summary.Trim();
        target.Body = request.Body.Trim();
        target.AuthorResearcherId = request.AuthorResearcherId;

        await db.SaveChangesAsync(cancellationToken);

        return Results.Ok(new AidxIdResponse(target.Id, canonical.Slug, target.Status.ToString(), editing.GetVersion(target), target.RevisionOfId));
    }

    private static Task<IResult> PublishNewsAsync(
        Guid id, ClaimsPrincipal principal, ApplicationDbContext db, AidxContentEditingService editing, CancellationToken cancellationToken) =>
        PublishThroughWorkflowAsync(db.AidxNews, id, AidxContentType.News, onFirstPublish: null, principal, db, editing, cancellationToken);

    private static Task<IResult> ArchiveNewsAsync(
        Guid id, ClaimsPrincipal principal, ApplicationDbContext db, AidxContentEditingService editing, CancellationToken cancellationToken) =>
        ArchiveThroughWorkflowAsync(db.AidxNews, id, AidxContentType.News, onArchive: null, principal, db, editing, cancellationToken);

    internal static async Task<Dictionary<string, string[]>> ValidateNewsAsync(
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

    private static async Task<IResult> UpdateEventAsync(
        Guid id, AidxEventRequest request, ApplicationDbContext db, AidxContentEditingService editing, CancellationToken cancellationToken)
    {
        var canonical = await db.AidxEvents.FirstOrDefaultAsync(e => e.Id == id && e.RevisionOfId == null, cancellationToken);
        if (canonical is null)
        {
            return Results.NotFound();
        }

        if (request.ExpectedVersion is { } expected && !string.Equals(editing.GetVersion(canonical), expected, StringComparison.Ordinal))
        {
            return VersionConflict();
        }

        var errors = ValidateEvent(request);
        if (errors.Count > 0)
        {
            return Results.ValidationProblem(errors);
        }

        // A missing slug keeps the stored one, so a title change never rewrites it. Renaming it is
        // applied straight to the canonical row regardless of status — it is a routing detail, not
        // reviewable editorial content, and admins have always been able to move a published item's
        // URL in one step.
        var slug = string.IsNullOrWhiteSpace(request.Slug) ? canonical.Slug : ResolveSlug(request.Slug, request.Title);
        if (slug != canonical.Slug && await db.AidxEvents.AnyAsync(e => e.Slug == slug && e.Id != id && e.RevisionOfId == null, cancellationToken))
        {
            return SlugInUse();
        }

        canonical.Slug = slug;

        var target = await editing.BeginEditAsync(db.AidxEvents, canonical, null, cancellationToken);

        target.Title = request.Title.Trim();
        target.Description = request.Description.Trim();
        target.StartsAt = request.StartsAt;
        target.EndsAt = request.EndsAt;
        target.Location = AidxEndpointHelpers.Clean(request.Location);
        target.RegistrationUrl = AidxEndpointHelpers.Clean(request.RegistrationUrl);
        target.SpeakerName = AidxEndpointHelpers.Clean(request.SpeakerName);

        await db.SaveChangesAsync(cancellationToken);

        return Results.Ok(new AidxIdResponse(target.Id, canonical.Slug, target.Status.ToString(), editing.GetVersion(target), target.RevisionOfId));
    }

    private static Task<IResult> PublishEventAsync(
        Guid id, ClaimsPrincipal principal, ApplicationDbContext db, AidxContentEditingService editing, CancellationToken cancellationToken) =>
        PublishThroughWorkflowAsync(db.AidxEvents, id, AidxContentType.Event, onFirstPublish: null, principal, db, editing, cancellationToken);

    private static Task<IResult> ArchiveEventAsync(
        Guid id, ClaimsPrincipal principal, ApplicationDbContext db, AidxContentEditingService editing, CancellationToken cancellationToken) =>
        ArchiveThroughWorkflowAsync(db.AidxEvents, id, AidxContentType.Event, onArchive: null, principal, db, editing, cancellationToken);

    internal static Dictionary<string, string[]> ValidateEvent(AidxEventRequest request)
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

    /// <summary>
    /// Only drafts can be deleted. A published or archived project may already be linked from a public page,
    /// and deleting it would silently unlink any published opportunity that points at it. Archive takes it down instead.
    /// </summary>
    private static async Task<IResult> DeleteProjectAsync(Guid id, IApplicationDbContext db, CancellationToken cancellationToken)
    {
        var project = await db.AidxProjects.FirstOrDefaultAsync(p => p.Id == id, cancellationToken);
        if (project is null)
        {
            return Results.NotFound();
        }

        if (project.Status != AidxContentStatus.Draft)
        {
            return Results.Problem(
                title: "Only draft projects can be deleted. Archive published projects instead.",
                statusCode: StatusCodes.Status409Conflict);
        }

        db.RemoveRange(new[] { project });
        await db.SaveChangesAsync(cancellationToken);

        return Results.NoContent();
    }

    private static Task<IResult> DeletePublicationAsync(Guid id, IApplicationDbContext db, CancellationToken cancellationToken) =>
        DeleteEntityAsync(db.AidxPublications, id, db, cancellationToken);

    /// <summary>
    /// Only drafts can be deleted. A published or archived item may already be linked from outside the site,
    /// so removing it would break a public URL. Archive takes it down instead.
    /// </summary>
    private static async Task<IResult> DeleteNewsAsync(Guid id, IApplicationDbContext db, CancellationToken cancellationToken)
    {
        var news = await db.AidxNews.FirstOrDefaultAsync(n => n.Id == id, cancellationToken);
        if (news is null)
        {
            return Results.NotFound();
        }

        if (news.Status != AidxContentStatus.Draft)
        {
            return Results.Problem(
                title: "Only draft news can be deleted. Archive published news instead.",
                statusCode: StatusCodes.Status409Conflict);
        }

        db.RemoveRange(new[] { news });
        await db.SaveChangesAsync(cancellationToken);

        return Results.NoContent();
    }

    /// <summary>
    /// Only drafts can be deleted. A published or archived event may already be shared, so removing it would
    /// break a public URL and its registration link. Archive takes it down instead.
    /// </summary>
    private static async Task<IResult> DeleteEventAsync(Guid id, IApplicationDbContext db, CancellationToken cancellationToken)
    {
        var evt = await db.AidxEvents.FirstOrDefaultAsync(e => e.Id == id, cancellationToken);
        if (evt is null)
        {
            return Results.NotFound();
        }

        if (evt.Status != AidxContentStatus.Draft)
        {
            return Results.Problem(
                title: "Only draft events can be deleted. Archive published events instead.",
                statusCode: StatusCodes.Status409Conflict);
        }

        db.RemoveRange(new[] { evt });
        await db.SaveChangesAsync(cancellationToken);

        return Results.NoContent();
    }

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

    private static IResult VersionConflict() =>
        Results.Problem(title: "This was edited elsewhere. Reload and retry.", statusCode: StatusCodes.Status409Conflict);

    private static IResult EditOutcomeToResult(AidxEditOutcome outcome) => outcome switch
    {
        AidxEditOutcome.VersionConflict => VersionConflict(),
        AidxEditOutcome.InvalidTransition => Results.Problem(
            title: "Already published, or not eligible to be published directly — approve its pending revision instead.",
            statusCode: StatusCodes.Status409Conflict),
        _ => Results.Forbid(),
    };

    /// <summary>
    /// Drives a never-yet-published Draft/ChangesRequested row straight to Published in one admin
    /// call, by chaining the same Submit→Approve transitions the membership-gated workflow uses —
    /// so this one-click action still creates a real submission snapshot and a Published audit
    /// entry, rather than a bare field flip with no history. Refuses (409) a row that is already
    /// Published (use the workflow's approve route for a pending revision) or Archived (the state
    /// machine has no direct path back to Published).
    /// </summary>
    private static async Task<IResult> PublishThroughWorkflowAsync<TContent>(
        DbSet<TContent> set, Guid id, AidxContentType contentType, Action<TContent>? onFirstPublish,
        ClaimsPrincipal principal, ApplicationDbContext db, AidxContentEditingService editing, CancellationToken cancellationToken)
        where TContent : class, IAidxWorkflowContent
    {
        var (user, membership, failure) = await AidxWorkflowSharedHelpers.ResolveActorAsync(principal, db, cancellationToken);
        if (failure is not null)
        {
            return failure;
        }

        var canonical = await set.FirstOrDefaultAsync(c => c.Id == id && c.RevisionOfId == null, cancellationToken);
        if (canonical is null)
        {
            return Results.NotFound();
        }

        if (canonical.Status is AidxContentStatus.Draft or AidxContentStatus.ChangesRequested)
        {
            var submitOutcome = await editing.SubmitAsync(
                canonical, editing.GetVersion(canonical), contentType, user!.Id, membership?.Id, cancellationToken);
            if (submitOutcome != AidxEditOutcome.Success)
            {
                return EditOutcomeToResult(submitOutcome);
            }
        }

        if (canonical.Status is not (AidxContentStatus.Submitted or AidxContentStatus.UnderReview or AidxContentStatus.Archived))
        {
            return EditOutcomeToResult(AidxEditOutcome.InvalidTransition);
        }

        var approveOutcome = await editing.ApproveAsync(
            canonical, null, editing.GetVersion(canonical), contentType, copyOntoCanonical: null,
            user!.Id, membership?.Id, cancellationToken, onFirstPublish);
        if (approveOutcome != AidxEditOutcome.Success)
        {
            return EditOutcomeToResult(approveOutcome);
        }

        return Results.Ok(new AidxIdResponse(canonical.Id, null, canonical.Status.ToString(), editing.GetVersion(canonical)));
    }

    private static async Task<IResult> ArchiveThroughWorkflowAsync<TContent>(
        DbSet<TContent> set, Guid id, AidxContentType contentType, Action<TContent>? onArchive,
        ClaimsPrincipal principal, ApplicationDbContext db, AidxContentEditingService editing, CancellationToken cancellationToken)
        where TContent : class, IAidxWorkflowContent
    {
        var (user, membership, failure) = await AidxWorkflowSharedHelpers.ResolveActorAsync(principal, db, cancellationToken);
        if (failure is not null)
        {
            return failure;
        }

        var canonical = await set.FirstOrDefaultAsync(c => c.Id == id && c.RevisionOfId == null, cancellationToken);
        if (canonical is null)
        {
            return Results.NotFound();
        }

        var outcome = await editing.ArchiveAsync(
            canonical, editing.GetVersion(canonical), contentType, user!.Id, membership?.Id, cancellationToken, onArchive);
        if (outcome != AidxEditOutcome.Success)
        {
            return EditOutcomeToResult(outcome);
        }

        return Results.Ok(new AidxIdResponse(canonical.Id, null, canonical.Status.ToString(), editing.GetVersion(canonical)));
    }

    internal static IResult SlugInUse() =>
        Results.Problem(title: "Slug is already in use.", statusCode: StatusCodes.Status409Conflict);

    internal static string ResolveSlug(string? requested, string fallbackText) =>
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

    internal static List<string> NormalizeTechnologies(IReadOnlyList<string>? names) =>
        (names ?? [])
            .Select(n => n.Trim())
            .Where(n => n.Length > 0)
            .Distinct(StringComparer.Ordinal)
            .ToList();
}
