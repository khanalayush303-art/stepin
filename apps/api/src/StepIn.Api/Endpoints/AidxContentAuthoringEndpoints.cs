using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using StepIn.Domain.Aidx;
using StepIn.Infrastructure.Aidx;
using StepIn.Infrastructure.Persistence;

namespace StepIn.Api.Endpoints;

/// <summary>
/// Lets a Contributor (or any higher AIDX role, or Admin) create and edit their own AIDX content —
/// the piece of the original request that was entirely missing: the workspace page could only
/// submit/withdraw content that already had <see cref="IAidxWorkflowContent.CreatedByMembershipId"/>
/// set on it, and nothing before this endpoint ever set that field except direct DB manipulation in
/// tests. Ownership is always resolved server-side from the caller's own membership — never
/// accepted from the request body, so a client can never create or claim content "as" someone else.
///
/// Shares its field validation and relationship-sync logic with the legacy Admin CMS
/// (<see cref="AidxAdminEndpoints"/>'s now-<c>internal</c> <c>ValidateProjectAsync</c>/
/// <c>SyncProjectRelations</c>/etc.) and its edit mechanics with the membership-gated workflow
/// endpoints (<see cref="AidxContentEditingService.BeginEditAsync{TContent}"/>) — there is
/// structurally one implementation of "how a project's fields get validated and written" and one
/// of "how an edit to already-published content is routed to a revision", used by every caller.
/// </summary>
public static class AidxContentAuthoringEndpoints
{
    public static IEndpointRouteBuilder MapAidxContentAuthoringEndpoints(this IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        var group = app.MapGroup("/api/v1/aidx/authoring")
            .WithTags("AIDX Authoring")
            .RequireAuthorization();

        group.MapPost("/projects", CreateProjectAsync).WithName("AidxAuthorCreateProject");
        group.MapPut("/projects/{id:guid}", UpdateProjectAsync).WithName("AidxAuthorUpdateProject");

        group.MapPost("/news", CreateNewsAsync).WithName("AidxAuthorCreateNews");
        group.MapPut("/news/{id:guid}", UpdateNewsAsync).WithName("AidxAuthorUpdateNews");

        group.MapPost("/events", CreateEventAsync).WithName("AidxAuthorCreateEvent");
        group.MapPut("/events/{id:guid}", UpdateEventAsync).WithName("AidxAuthorUpdateEvent");

        group.MapPost("/publications", CreatePublicationAsync).WithName("AidxAuthorCreatePublication");
        group.MapPut("/publications/{id:guid}", UpdatePublicationAsync).WithName("AidxAuthorUpdatePublication");

        return app;
    }

    /// <summary>Any active membership (any role) or Admin may author new Draft content — the
    /// matrix's "Create/edit own draft content: own only" row, with "own" established here by
    /// construction (<c>CreatedByMembershipId</c> is always the acting membership's own id, never
    /// a client-supplied value).</summary>
    private static bool CanCreate(StepIn.Domain.Users.ApplicationUser user, AidxMembership? membership) =>
        AidxPermissions.IsPlatformAdmin(user) || membership is { Status: AidxMembershipStatus.Active };

    private static IResult NotYourContent() => Results.Forbid();

    private static IResult ReadOnlyWhileUnderReview() => Results.Problem(
        title: "This is read-only while it is Submitted or under review. Withdraw it first, or wait for review feedback.",
        statusCode: StatusCodes.Status409Conflict);

    private static IResult VersionConflict() => Results.Problem(
        title: "This was edited elsewhere. Reload and retry.", statusCode: StatusCodes.Status409Conflict);

    /// <summary>
    /// True if a non-Admin owner may open this row for editing right now. Draft/ChangesRequested
    /// are edited in place; Published opens a revision (<see cref="AidxContentEditingService.BeginEditAsync{TContent}"/>
    /// handles that indirection). Submitted/UnderReview are read-only to the Contributor by design
    /// (Correction 1) — they must withdraw first. Archived content is not editable here at all
    /// (an Editor/LabManager/Admin can always republish it instead, with no re-review needed).
    /// </summary>
    private static bool IsEditableNow(AidxContentStatus status) =>
        AidxEditorialWorkflow.IsEditableByContributor(status) || status == AidxContentStatus.Published;

    // ---- Projects --------------------------------------------------------------------------

    private static async Task<IResult> CreateProjectAsync(
        AidxProjectRequest request, ClaimsPrincipal principal, ApplicationDbContext db, AidxContentEditingService editing, CancellationToken cancellationToken)
    {
        var (user, membership, failure) = await AidxWorkflowSharedHelpers.ResolveActorAsync(principal, db, cancellationToken);
        if (failure is not null)
        {
            return failure;
        }

        if (!CanCreate(user!, membership))
        {
            return NotYourContent();
        }

        var errors = await AidxAdminEndpoints.ValidateProjectAsync(request, db, cancellationToken);
        if (errors.Count > 0)
        {
            return Results.ValidationProblem(errors);
        }

        var slug = AidxAdminEndpoints.ResolveSlug(request.Slug, request.Title);
        if (await db.AidxProjects.AnyAsync(p => p.Slug == slug && p.RevisionOfId == null, cancellationToken))
        {
            return AidxAdminEndpoints.SlugInUse();
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
            Outcomes = request.Outcomes,
            CreatedByMembershipId = membership?.Id,
        };

        AidxAdminEndpoints.SyncProjectRelations(project, request, db);
        db.Add(project);
        await db.SaveChangesAsync(cancellationToken);

        return Results.Created((string?)null, new AidxIdResponse(project.Id, project.Slug, project.Status.ToString(), editing.GetVersion(project)));
    }

    private static async Task<IResult> UpdateProjectAsync(
        Guid id, AidxProjectRequest request, ClaimsPrincipal principal, ApplicationDbContext db,
        AidxContentEditingService editing, CancellationToken cancellationToken)
    {
        var (user, membership, failure) = await AidxWorkflowSharedHelpers.ResolveActorAsync(principal, db, cancellationToken);
        if (failure is not null)
        {
            return failure;
        }

        var canonical = await db.AidxProjects
            .Include(p => p.ResearchAreas)
            .Include(p => p.Technologies)
            .Include(p => p.Researchers)
            .FirstOrDefaultAsync(p => p.Id == id && p.RevisionOfId == null, cancellationToken);

        if (canonical is null)
        {
            return Results.NotFound();
        }

        var isAdmin = AidxPermissions.IsPlatformAdmin(user!);
        if (!isAdmin)
        {
            if (!AidxPermissions.CanEditAsOwner(membership, canonical.CreatedByMembershipId))
            {
                return NotYourContent();
            }

            if (!IsEditableNow(canonical.Status))
            {
                return ReadOnlyWhileUnderReview();
            }
        }

        if (!string.Equals(editing.GetVersion(canonical), request.ExpectedVersion, StringComparison.Ordinal))
        {
            return VersionConflict();
        }

        var errors = await AidxAdminEndpoints.ValidateProjectAsync(request, db, cancellationToken);
        if (errors.Count > 0)
        {
            return Results.ValidationProblem(errors);
        }

        var slug = AidxAdminEndpoints.ResolveSlug(request.Slug, request.Title);
        if (slug != canonical.Slug && await db.AidxProjects.AnyAsync(p => p.Slug == slug && p.Id != id && p.RevisionOfId == null, cancellationToken))
        {
            return AidxAdminEndpoints.SlugInUse();
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
        target.Outcomes = request.Outcomes;

        AidxAdminEndpoints.SyncProjectRelations(target, request, db);
        await db.SaveChangesAsync(cancellationToken);

        return Results.Ok(new AidxIdResponse(target.Id, canonical.Slug, target.Status.ToString(), editing.GetVersion(target), target.RevisionOfId));
    }

    // ---- News --------------------------------------------------------------------------------

    private static async Task<IResult> CreateNewsAsync(
        AidxNewsRequest request, ClaimsPrincipal principal, ApplicationDbContext db, AidxContentEditingService editing, CancellationToken cancellationToken)
    {
        var (user, membership, failure) = await AidxWorkflowSharedHelpers.ResolveActorAsync(principal, db, cancellationToken);
        if (failure is not null)
        {
            return failure;
        }

        if (!CanCreate(user!, membership))
        {
            return NotYourContent();
        }

        var errors = await AidxAdminEndpoints.ValidateNewsAsync(request, Guid.Empty, db, cancellationToken);
        if (errors.Count > 0)
        {
            return Results.ValidationProblem(errors);
        }

        var slug = AidxAdminEndpoints.ResolveSlug(request.Slug, request.Title);
        if (await db.AidxNews.AnyAsync(n => n.Slug == slug && n.RevisionOfId == null, cancellationToken))
        {
            return AidxAdminEndpoints.SlugInUse();
        }

        var news = new AidxNews
        {
            Slug = slug,
            Title = request.Title.Trim(),
            Summary = request.Summary.Trim(),
            Body = request.Body.Trim(),
            AuthorResearcherId = request.AuthorResearcherId,
            CreatedByMembershipId = membership?.Id,
        };

        db.Add(news);
        await db.SaveChangesAsync(cancellationToken);

        return Results.Created((string?)null, new AidxIdResponse(news.Id, news.Slug, news.Status.ToString(), editing.GetVersion(news)));
    }

    private static async Task<IResult> UpdateNewsAsync(
        Guid id, AidxNewsRequest request, ClaimsPrincipal principal, ApplicationDbContext db,
        AidxContentEditingService editing, CancellationToken cancellationToken)
    {
        var (user, membership, failure) = await AidxWorkflowSharedHelpers.ResolveActorAsync(principal, db, cancellationToken);
        if (failure is not null)
        {
            return failure;
        }

        var canonical = await db.AidxNews.FirstOrDefaultAsync(n => n.Id == id && n.RevisionOfId == null, cancellationToken);
        if (canonical is null)
        {
            return Results.NotFound();
        }

        var isAdmin = AidxPermissions.IsPlatformAdmin(user!);
        if (!isAdmin)
        {
            if (!AidxPermissions.CanEditAsOwner(membership, canonical.CreatedByMembershipId))
            {
                return NotYourContent();
            }

            if (!IsEditableNow(canonical.Status))
            {
                return ReadOnlyWhileUnderReview();
            }
        }

        if (!string.Equals(editing.GetVersion(canonical), request.ExpectedVersion, StringComparison.Ordinal))
        {
            return VersionConflict();
        }

        var errors = await AidxAdminEndpoints.ValidateNewsAsync(request, id, db, cancellationToken);
        if (errors.Count > 0)
        {
            return Results.ValidationProblem(errors);
        }

        var slug = string.IsNullOrWhiteSpace(request.Slug) ? canonical.Slug : AidxAdminEndpoints.ResolveSlug(request.Slug, request.Title);
        if (slug != canonical.Slug && await db.AidxNews.AnyAsync(n => n.Slug == slug && n.Id != id && n.RevisionOfId == null, cancellationToken))
        {
            return AidxAdminEndpoints.SlugInUse();
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

    // ---- Events ------------------------------------------------------------------------------

    private static async Task<IResult> CreateEventAsync(
        AidxEventRequest request, ClaimsPrincipal principal, ApplicationDbContext db, AidxContentEditingService editing, CancellationToken cancellationToken)
    {
        var (user, membership, failure) = await AidxWorkflowSharedHelpers.ResolveActorAsync(principal, db, cancellationToken);
        if (failure is not null)
        {
            return failure;
        }

        if (!CanCreate(user!, membership))
        {
            return NotYourContent();
        }

        var errors = AidxAdminEndpoints.ValidateEvent(request);
        if (errors.Count > 0)
        {
            return Results.ValidationProblem(errors);
        }

        var slug = AidxAdminEndpoints.ResolveSlug(request.Slug, request.Title);
        if (await db.AidxEvents.AnyAsync(e => e.Slug == slug && e.RevisionOfId == null, cancellationToken))
        {
            return AidxAdminEndpoints.SlugInUse();
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
            CreatedByMembershipId = membership?.Id,
        };

        db.Add(evt);
        await db.SaveChangesAsync(cancellationToken);

        return Results.Created((string?)null, new AidxIdResponse(evt.Id, evt.Slug, evt.Status.ToString(), editing.GetVersion(evt)));
    }

    private static async Task<IResult> UpdateEventAsync(
        Guid id, AidxEventRequest request, ClaimsPrincipal principal, ApplicationDbContext db,
        AidxContentEditingService editing, CancellationToken cancellationToken)
    {
        var (user, membership, failure) = await AidxWorkflowSharedHelpers.ResolveActorAsync(principal, db, cancellationToken);
        if (failure is not null)
        {
            return failure;
        }

        var canonical = await db.AidxEvents.FirstOrDefaultAsync(e => e.Id == id && e.RevisionOfId == null, cancellationToken);
        if (canonical is null)
        {
            return Results.NotFound();
        }

        var isAdmin = AidxPermissions.IsPlatformAdmin(user!);
        if (!isAdmin)
        {
            if (!AidxPermissions.CanEditAsOwner(membership, canonical.CreatedByMembershipId))
            {
                return NotYourContent();
            }

            if (!IsEditableNow(canonical.Status))
            {
                return ReadOnlyWhileUnderReview();
            }
        }

        if (!string.Equals(editing.GetVersion(canonical), request.ExpectedVersion, StringComparison.Ordinal))
        {
            return VersionConflict();
        }

        var errors = AidxAdminEndpoints.ValidateEvent(request);
        if (errors.Count > 0)
        {
            return Results.ValidationProblem(errors);
        }

        var slug = string.IsNullOrWhiteSpace(request.Slug) ? canonical.Slug : AidxAdminEndpoints.ResolveSlug(request.Slug, request.Title);
        if (slug != canonical.Slug && await db.AidxEvents.AnyAsync(e => e.Slug == slug && e.Id != id && e.RevisionOfId == null, cancellationToken))
        {
            return AidxAdminEndpoints.SlugInUse();
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

    // ---- Publications ------------------------------------------------------------------------

    private static async Task<IResult> CreatePublicationAsync(
        AidxPublicationRequest request, ClaimsPrincipal principal, ApplicationDbContext db, AidxContentEditingService editing, CancellationToken cancellationToken)
    {
        var (user, membership, failure) = await AidxWorkflowSharedHelpers.ResolveActorAsync(principal, db, cancellationToken);
        if (failure is not null)
        {
            return failure;
        }

        if (!CanCreate(user!, membership))
        {
            return NotYourContent();
        }

        var errors = await AidxAdminEndpoints.ValidatePublicationAsync(request, Guid.Empty, db, cancellationToken);
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
            CreatedByMembershipId = membership?.Id,
        };

        AidxAdminEndpoints.SyncAuthors(publication, request.Authors ?? [], db);
        AidxAdminEndpoints.SyncPublicationLinks(publication, request, db);
        db.Add(publication);
        await db.SaveChangesAsync(cancellationToken);

        return Results.Created((string?)null, new AidxIdResponse(publication.Id, null, publication.Status.ToString(), editing.GetVersion(publication)));
    }

    private static async Task<IResult> UpdatePublicationAsync(
        Guid id, AidxPublicationRequest request, ClaimsPrincipal principal, ApplicationDbContext db,
        AidxContentEditingService editing, CancellationToken cancellationToken)
    {
        var (user, membership, failure) = await AidxWorkflowSharedHelpers.ResolveActorAsync(principal, db, cancellationToken);
        if (failure is not null)
        {
            return failure;
        }

        var canonical = await db.AidxPublications
            .Include(p => p.Authors)
            .Include(p => p.ResearchAreas)
            .Include(p => p.Projects)
            .FirstOrDefaultAsync(p => p.Id == id && p.RevisionOfId == null, cancellationToken);

        if (canonical is null)
        {
            return Results.NotFound();
        }

        var isAdmin = AidxPermissions.IsPlatformAdmin(user!);
        if (!isAdmin)
        {
            if (!AidxPermissions.CanEditAsOwner(membership, canonical.CreatedByMembershipId))
            {
                return NotYourContent();
            }

            if (!IsEditableNow(canonical.Status))
            {
                return ReadOnlyWhileUnderReview();
            }
        }

        if (!string.Equals(editing.GetVersion(canonical), request.ExpectedVersion, StringComparison.Ordinal))
        {
            return VersionConflict();
        }

        var errors = await AidxAdminEndpoints.ValidatePublicationAsync(request, id, db, cancellationToken);
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

        var effective = request with
        {
            Authors = request.Authors ?? target.Authors
                .OrderBy(a => a.Position)
                .Select(a => new AidxAuthorInput(a.ResearcherId, a.ExternalAuthorName))
                .ToList(),
            ResearchAreaIds = request.ResearchAreaIds ?? target.ResearchAreas.Select(r => r.ResearchAreaId).ToList(),
            ProjectIds = request.ProjectIds ?? target.Projects.Select(p => p.ProjectId).ToList(),
        };

        AidxAdminEndpoints.SyncAuthors(target, effective.Authors ?? [], db);
        AidxAdminEndpoints.SyncPublicationLinks(target, effective, db);
        await db.SaveChangesAsync(cancellationToken);

        return Results.Ok(new AidxIdResponse(target.Id, null, target.Status.ToString(), editing.GetVersion(target), target.RevisionOfId));
    }
}
