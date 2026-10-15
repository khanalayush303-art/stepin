using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using StepIn.Api.Infrastructure;
using StepIn.Domain.Aidx;
using StepIn.Domain.Common;
using StepIn.Domain.Users;
using StepIn.Infrastructure.Aidx;
using StepIn.Infrastructure.Persistence;

namespace StepIn.Api.Endpoints;

/// <summary>
/// Membership-gated editorial-workflow actions — submit/review/approve/archive/withdraw/
/// claim-ownership/revision-history — for every AIDX content type that goes through the
/// workflow (Project, News, Event, Publication). One generic handler per action, dispatched
/// by a <c>{contentType}</c> route segment to the correctly-typed call into
/// <see cref="AidxContentEditingService"/>, so the state machine/snapshot/concurrency logic
/// is implemented once rather than four times.
///
/// Every handler resolves the caller's own membership fresh (same reasoning as
/// <c>AidxMembershipEndpoints</c>: no claims-based caching, so revocation is immediate) and
/// checks <see cref="AidxPermissions"/> before calling into the editing service, which itself
/// performs no authorization — only mechanics.
/// </summary>
public static class AidxWorkflowEndpoints
{
    public static IEndpointRouteBuilder MapAidxWorkflowEndpoints(this IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        var group = app.MapGroup("/api/v1/aidx/content/{contentType}/{id:guid}")
            .WithTags("AIDX Workflow")
            .RequireAuthorization();

        group.MapPost("/begin-edit", BeginEditAsync).WithName("AidxBeginEdit");
        group.MapPost("/submit", SubmitAsync).WithName("AidxSubmit");
        group.MapPost("/start-review", StartReviewAsync).WithName("AidxStartReview");
        group.MapPost("/request-changes", RequestChangesAsync).WithName("AidxRequestChanges");
        group.MapPost("/approve", ApproveAsync).WithName("AidxApprove");
        group.MapPost("/archive", ArchiveAsync).WithName("AidxArchive");
        group.MapPost("/withdraw", WithdrawAsync).WithName("AidxWithdraw");
        group.MapPost("/discard", DiscardAsync).WithName("AidxDiscardRevision");
        group.MapPost("/claim-ownership", ClaimOwnershipAsync).WithName("AidxClaimOwnership");
        group.MapGet("/revisions", ListRevisionsAsync).WithName("AidxListRevisions");

        return app;
    }

    private static bool TryParseContentType(string contentType, out AidxContentType parsed) =>
        AidxEndpointHelpers.TryParseEnum(contentType, out parsed);

    private static Task<(ApplicationUser? User, AidxMembership? Membership, IResult? Failure)> ResolveActorAsync(
        ClaimsPrincipal principal, ApplicationDbContext db, CancellationToken cancellationToken) =>
        AidxWorkflowSharedHelpers.ResolveActorAsync(principal, db, cancellationToken);

    private static IResult OutcomeToResult(AidxEditOutcome outcome) => outcome switch
    {
        AidxEditOutcome.Success => Results.Ok(),
        AidxEditOutcome.VersionConflict => Results.Problem(
            title: "This was edited elsewhere. Reload and retry.", statusCode: StatusCodes.Status409Conflict),
        AidxEditOutcome.InvalidTransition => Results.Problem(
            title: "That action isn't valid from the content's current status.", statusCode: StatusCodes.Status409Conflict),
        _ => Results.Forbid(),
    };

    // ---- begin-edit ------------------------------------------------------------------------

    private static async Task<IResult> BeginEditAsync(
        string contentType, Guid id, ClaimsPrincipal principal, ApplicationDbContext db,
        AidxContentEditingService editing, CancellationToken cancellationToken)
    {
        if (!TryParseContentType(contentType, out var type))
        {
            return Results.NotFound();
        }

        var (user, membership, failure) = await ResolveActorAsync(principal, db, cancellationToken);
        if (failure is not null)
        {
            return failure;
        }

        return type switch
        {
            AidxContentType.Project => await BeginEditProjectAsync(id, user!, membership, db, editing, cancellationToken),
            AidxContentType.News => await BeginEditGenericAsync(db.AidxNews, id, user!, membership, editing, null, cancellationToken),
            AidxContentType.Event => await BeginEditGenericAsync(db.AidxEvents, id, user!, membership, editing, null, cancellationToken),
            AidxContentType.Publication => await BeginEditPublicationAsync(id, user!, membership, db, editing, cancellationToken),
            _ => Results.NotFound(),
        };
    }

    private static async Task<IResult> BeginEditGenericAsync<TContent>(
        DbSet<TContent> set, Guid id, ApplicationUser user, AidxMembership? membership,
        AidxContentEditingService editing, Action<TContent, TContent>? reparent, CancellationToken cancellationToken)
        where TContent : class, IAidxWorkflowContent
    {
        var canonical = await set.FirstOrDefaultAsync(c => c.Id == id && c.RevisionOfId == null, cancellationToken);
        if (canonical is null)
        {
            return Results.NotFound();
        }

        if (!AidxPermissions.CanEditAsOwner(membership, canonical.CreatedByMembershipId)
            && !AidxPermissions.IsPlatformAdmin(user))
        {
            return Results.Forbid();
        }

        var editable = await editing.BeginEditAsync(set, canonical, reparent, cancellationToken);
        return Results.Ok(new AidxWorkflowStatusResponse(
            editable.Id, editable.Status.ToString(), editing.GetVersion(editable), editable.RevisionOfId, editable.RevisionNumber));
    }

    private static async Task<IResult> BeginEditProjectAsync(
        Guid id, ApplicationUser user, AidxMembership? membership, ApplicationDbContext db,
        AidxContentEditingService editing, CancellationToken cancellationToken)
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

        if (!AidxPermissions.CanEditAsOwner(membership, canonical.CreatedByMembershipId) && !AidxPermissions.IsPlatformAdmin(user))
        {
            return Results.Forbid();
        }

        var editable = await editing.BeginEditAsync(db.AidxProjects, canonical, AidxWorkflowSharedHelpers.ReparentProject, cancellationToken);
        return Results.Ok(new AidxWorkflowStatusResponse(
            editable.Id, editable.Status.ToString(), editing.GetVersion(editable), editable.RevisionOfId, editable.RevisionNumber));
    }

    private static async Task<IResult> BeginEditPublicationAsync(
        Guid id, ApplicationUser user, AidxMembership? membership, ApplicationDbContext db,
        AidxContentEditingService editing, CancellationToken cancellationToken)
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

        if (!AidxPermissions.CanEditAsOwner(membership, canonical.CreatedByMembershipId) && !AidxPermissions.IsPlatformAdmin(user))
        {
            return Results.Forbid();
        }

        var editable = await editing.BeginEditAsync(db.AidxPublications, canonical, AidxWorkflowSharedHelpers.ReparentPublication, cancellationToken);
        return Results.Ok(new AidxWorkflowStatusResponse(
            editable.Id, editable.Status.ToString(), editing.GetVersion(editable), editable.RevisionOfId, editable.RevisionNumber));
    }

    // ---- submit / withdraw (contributor actions — ownership, not review scope) -------------

    private static async Task<IResult> SubmitAsync(
        string contentType, Guid id, AidxWorkflowActionRequest request, ClaimsPrincipal principal,
        ApplicationDbContext db, AidxContentEditingService editing, CancellationToken cancellationToken)
    {
        if (!TryParseContentType(contentType, out var type))
        {
            return Results.NotFound();
        }

        var (user, membership, failure) = await ResolveActorAsync(principal, db, cancellationToken);
        if (failure is not null)
        {
            return failure;
        }

        return await DispatchOwnerActionAsync(type, id, request.ExpectedVersion, user!, membership, db, editing,
            (content, version, uid, mid, ct) => type switch
            {
                AidxContentType.Project => editing.SubmitAsync((AidxProject)content, version, type, uid, mid, ct),
                AidxContentType.News => editing.SubmitAsync((AidxNews)content, version, type, uid, mid, ct),
                AidxContentType.Event => editing.SubmitAsync((AidxEvent)content, version, type, uid, mid, ct),
                AidxContentType.Publication => editing.SubmitAsync((AidxPublication)content, version, type, uid, mid, ct),
                _ => Task.FromResult(AidxEditOutcome.Forbidden),
            },
            cancellationToken);
    }

    private static async Task<IResult> WithdrawAsync(
        string contentType, Guid id, AidxWorkflowActionRequest request, ClaimsPrincipal principal,
        ApplicationDbContext db, AidxContentEditingService editing, CancellationToken cancellationToken)
    {
        if (!TryParseContentType(contentType, out var type))
        {
            return Results.NotFound();
        }

        var (user, membership, failure) = await ResolveActorAsync(principal, db, cancellationToken);
        if (failure is not null)
        {
            return failure;
        }

        return await DispatchOwnerActionAsync(type, id, request.ExpectedVersion, user!, membership, db, editing,
            (content, version, uid, mid, ct) => type switch
            {
                AidxContentType.Project => editing.WithdrawAsync((AidxProject)content, version, type, uid, mid, ct),
                AidxContentType.News => editing.WithdrawAsync((AidxNews)content, version, type, uid, mid, ct),
                AidxContentType.Event => editing.WithdrawAsync((AidxEvent)content, version, type, uid, mid, ct),
                AidxContentType.Publication => editing.WithdrawAsync((AidxPublication)content, version, type, uid, mid, ct),
                _ => Task.FromResult(AidxEditOutcome.Forbidden),
            },
            cancellationToken);
    }

    private static async Task<IResult> DiscardAsync(
        string contentType, Guid id, AidxWorkflowActionRequest request, ClaimsPrincipal principal,
        ApplicationDbContext db, AidxContentEditingService editing, CancellationToken cancellationToken)
    {
        if (!TryParseContentType(contentType, out var type))
        {
            return Results.NotFound();
        }

        var (user, membership, failure) = await ResolveActorAsync(principal, db, cancellationToken);
        if (failure is not null)
        {
            return failure;
        }

        return await DispatchOwnerActionAsync(type, id, request.ExpectedVersion, user!, membership, db, editing,
            (content, version, uid, mid, ct) => type switch
            {
                AidxContentType.Project => editing.DiscardRevisionAsync((AidxProject)content, version, type, uid, mid, ct),
                AidxContentType.News => editing.DiscardRevisionAsync((AidxNews)content, version, type, uid, mid, ct),
                AidxContentType.Event => editing.DiscardRevisionAsync((AidxEvent)content, version, type, uid, mid, ct),
                AidxContentType.Publication => editing.DiscardRevisionAsync((AidxPublication)content, version, type, uid, mid, ct),
                _ => Task.FromResult(AidxEditOutcome.Forbidden),
            },
            cancellationToken);
    }

    /// <summary>Shared dispatch for Submit/Withdraw/Discard: these are Contributor-side actions
    /// gated by ownership (own content only), never by reviewer scope.</summary>
    private static async Task<IResult> DispatchOwnerActionAsync(
        AidxContentType type, Guid id, string expectedVersion, ApplicationUser user, AidxMembership? membership,
        ApplicationDbContext db, AidxContentEditingService editing,
        Func<IAidxWorkflowContent, string, Guid, Guid?, CancellationToken, Task<AidxEditOutcome>> action,
        CancellationToken cancellationToken)
    {
        var content = await LoadByIdAsync(type, id, db, cancellationToken);
        if (content is null)
        {
            return Results.NotFound();
        }

        if (!AidxPermissions.CanEditAsOwner(membership, content.CreatedByMembershipId) && !AidxPermissions.IsPlatformAdmin(user))
        {
            return Results.Forbid();
        }

        var outcome = await action(content, expectedVersion, user.Id, membership?.Id, cancellationToken);
        return OutcomeToResult(outcome);
    }

    // ---- review actions (editor/manager/admin — scope-gated) -------------------------------

    private static async Task<IResult> StartReviewAsync(
        string contentType, Guid id, AidxWorkflowActionRequest request, ClaimsPrincipal principal,
        ApplicationDbContext db, AidxContentEditingService editing, CancellationToken cancellationToken) =>
        await DispatchReviewerActionAsync(contentType, id, request.ExpectedVersion, null, principal, db, editing,
            (content, version, uid, mid, comment, ct) => content switch
            {
                AidxProject p => editing.MoveToUnderReviewAsync(p, version, AidxContentType.Project, uid, mid, ct),
                AidxNews n => editing.MoveToUnderReviewAsync(n, version, AidxContentType.News, uid, mid, ct),
                AidxEvent e => editing.MoveToUnderReviewAsync(e, version, AidxContentType.Event, uid, mid, ct),
                AidxPublication pub => editing.MoveToUnderReviewAsync(pub, version, AidxContentType.Publication, uid, mid, ct),
                _ => Task.FromResult(AidxEditOutcome.Forbidden),
            },
            cancellationToken);

    private static async Task<IResult> RequestChangesAsync(
        string contentType, Guid id, AidxRequestChangesRequest request, ClaimsPrincipal principal,
        ApplicationDbContext db, AidxContentEditingService editing, CancellationToken cancellationToken) =>
        await DispatchReviewerActionAsync(contentType, id, request.ExpectedVersion, request.Comment, principal, db, editing,
            (content, version, uid, mid, comment, ct) => content switch
            {
                AidxProject p => editing.RequestChangesAsync(p, version, AidxContentType.Project, uid, mid, comment!, ct),
                AidxNews n => editing.RequestChangesAsync(n, version, AidxContentType.News, uid, mid, comment!, ct),
                AidxEvent e => editing.RequestChangesAsync(e, version, AidxContentType.Event, uid, mid, comment!, ct),
                AidxPublication pub => editing.RequestChangesAsync(pub, version, AidxContentType.Publication, uid, mid, comment!, ct),
                _ => Task.FromResult(AidxEditOutcome.Forbidden),
            },
            cancellationToken);

    private static async Task<IResult> ArchiveAsync(
        string contentType, Guid id, AidxWorkflowActionRequest request, ClaimsPrincipal principal,
        ApplicationDbContext db, AidxContentEditingService editing, CancellationToken cancellationToken) =>
        await DispatchReviewerActionAsync(contentType, id, request.ExpectedVersion, null, principal, db, editing,
            (content, version, uid, mid, comment, ct) => content switch
            {
                AidxProject p => editing.ArchiveAsync(p, version, AidxContentType.Project, uid, mid, ct),
                AidxNews n => editing.ArchiveAsync(n, version, AidxContentType.News, uid, mid, ct),
                AidxEvent e => editing.ArchiveAsync(e, version, AidxContentType.Event, uid, mid, ct),
                AidxPublication pub => editing.ArchiveAsync(pub, version, AidxContentType.Publication, uid, mid, ct, onArchive: p => p.Published = false),
                _ => Task.FromResult(AidxEditOutcome.Forbidden),
            },
            cancellationToken);

    private static async Task<IResult> ApproveAsync(
        string contentType, Guid id, AidxWorkflowActionRequest request, ClaimsPrincipal principal,
        ApplicationDbContext db, AidxContentEditingService editing, CancellationToken cancellationToken)
    {
        if (!TryParseContentType(contentType, out var type))
        {
            return Results.NotFound();
        }

        var (user, membership, failure) = await ResolveActorAsync(principal, db, cancellationToken);
        if (failure is not null)
        {
            return failure;
        }

        var content = await LoadByIdAsync(type, id, db, cancellationToken);
        if (content is null)
        {
            return Results.NotFound();
        }

        var areaIds = await GetContentAreaIdsAsync(type, content.RevisionOfId ?? content.Id, db, cancellationToken);
        if (!CanReview(user!, membership, content, areaIds))
        {
            return Results.Forbid();
        }

        AidxEditOutcome outcome;
        switch (type)
        {
            case AidxContentType.Project:
                var proj = (AidxProject)content;
                var projCanonical = proj.RevisionOfId is { } pcid ? await db.AidxProjects.Include(x => x.ResearchAreas).Include(x => x.Technologies).Include(x => x.Researchers).FirstOrDefaultAsync(x => x.Id == pcid, cancellationToken) : null;
                outcome = await editing.ApproveAsync(proj, projCanonical, request.ExpectedVersion, type, CopyProjectFields, user!.Id, membership?.Id, cancellationToken);
                break;
            case AidxContentType.News:
                var news = (AidxNews)content;
                var newsCanonical = news.RevisionOfId is { } ncid ? await db.AidxNews.FirstOrDefaultAsync(x => x.Id == ncid, cancellationToken) : null;
                outcome = await editing.ApproveAsync(news, newsCanonical, request.ExpectedVersion, type, CopyNewsFields, user!.Id, membership?.Id, cancellationToken);
                break;
            case AidxContentType.Event:
                var evt = (AidxEvent)content;
                var evtCanonical = evt.RevisionOfId is { } ecid ? await db.AidxEvents.FirstOrDefaultAsync(x => x.Id == ecid, cancellationToken) : null;
                outcome = await editing.ApproveAsync(evt, evtCanonical, request.ExpectedVersion, type, CopyEventFields, user!.Id, membership?.Id, cancellationToken);
                break;
            case AidxContentType.Publication:
                var pub = (AidxPublication)content;
                var pubCanonical = pub.RevisionOfId is { } pubcid ? await db.AidxPublications.Include(x => x.Authors).Include(x => x.ResearchAreas).Include(x => x.Projects).FirstOrDefaultAsync(x => x.Id == pubcid, cancellationToken) : null;
                outcome = await editing.ApproveAsync(pub, pubCanonical, request.ExpectedVersion, type, CopyPublicationFields, user!.Id, membership?.Id, cancellationToken, onFirstPublish: p => p.Published = true);
                break;
            default:
                return Results.NotFound();
        }

        return OutcomeToResult(outcome);
    }

    private static void CopyProjectFields(AidxProject revision, AidxProject canonical)
    {
        canonical.Title = revision.Title;
        canonical.ShortDescription = revision.ShortDescription;
        canonical.Description = revision.Description;
        canonical.StartDate = revision.StartDate;
        canonical.EndDate = revision.EndDate;
        canonical.ExternalUrl = revision.ExternalUrl;
        canonical.Featured = revision.Featured;
        canonical.Outcomes = revision.Outcomes;
        canonical.PublishedAt ??= DateTimeOffset.UtcNow;
        // Slug intentionally NOT copied — the canonical row's slug is its stable public URL.
    }

    private static void CopyNewsFields(AidxNews revision, AidxNews canonical)
    {
        canonical.Title = revision.Title;
        canonical.Summary = revision.Summary;
        canonical.Body = revision.Body;
        canonical.ImageKey = revision.ImageKey;
        canonical.AuthorResearcherId = revision.AuthorResearcherId;
        canonical.PublishedAt ??= DateTimeOffset.UtcNow;
    }

    private static void CopyEventFields(AidxEvent revision, AidxEvent canonical)
    {
        canonical.Title = revision.Title;
        canonical.Description = revision.Description;
        canonical.StartsAt = revision.StartsAt;
        canonical.EndsAt = revision.EndsAt;
        canonical.Location = revision.Location;
        canonical.RegistrationUrl = revision.RegistrationUrl;
        canonical.SpeakerName = revision.SpeakerName;
        canonical.ImageKey = revision.ImageKey;
    }

    private static void CopyPublicationFields(AidxPublication revision, AidxPublication canonical)
    {
        canonical.Title = revision.Title;
        canonical.Abstract = revision.Abstract;
        canonical.PublicationType = revision.PublicationType;
        canonical.Venue = revision.Venue;
        canonical.Year = revision.Year;
        canonical.ExternalUrl = revision.ExternalUrl;
        canonical.PdfKey = revision.PdfKey;
        canonical.Published = true; // dual-write compatibility field — see aidx-migration-plan.md
        canonical.PublishedAt ??= DateTimeOffset.UtcNow;
        // Doi intentionally NOT copied via this path in the minimal-viable form — changing a
        // published paper's DOI post-publish is not a supported edit in this delivery.
    }

    private static async Task<IResult> DispatchReviewerActionAsync(
        string contentType, Guid id, string expectedVersion, string? comment, ClaimsPrincipal principal,
        ApplicationDbContext db, AidxContentEditingService editing,
        Func<IAidxWorkflowContent, string, Guid, Guid?, string?, CancellationToken, Task<AidxEditOutcome>> action,
        CancellationToken cancellationToken)
    {
        if (!TryParseContentType(contentType, out var type))
        {
            return Results.NotFound();
        }

        var (user, membership, failure) = await ResolveActorAsync(principal, db, cancellationToken);
        if (failure is not null)
        {
            return failure;
        }

        var content = await LoadByIdAsync(type, id, db, cancellationToken);
        if (content is null)
        {
            return Results.NotFound();
        }

        var areaIds = await GetContentAreaIdsAsync(type, content.RevisionOfId ?? content.Id, db, cancellationToken);
        if (!CanReview(user!, membership, content, areaIds))
        {
            return Results.Forbid();
        }

        var outcome = await action(content, expectedVersion, user!.Id, membership?.Id, comment, cancellationToken);
        return OutcomeToResult(outcome);
    }

    private static bool CanReview(ApplicationUser user, AidxMembership? membership, IAidxWorkflowContent content, IReadOnlyCollection<Guid> areaIds)
    {
        if (AidxPermissions.IsPlatformAdmin(user))
        {
            return true;
        }

        if (membership is not { Status: AidxMembershipStatus.Active } || membership.Role == AidxLabRole.Contributor)
        {
            return false;
        }

        // Self-review is forbidden against both the original creator and whoever actually
        // submitted the round under review — not only the row's original creator.
        if (content.CreatedByMembershipId == membership.Id)
        {
            return false;
        }

        return AidxPermissions.IsWithinEditorScope(membership, areaIds);
    }

    private static async Task<IReadOnlyCollection<Guid>> GetContentAreaIdsAsync(
        AidxContentType type, Guid canonicalId, ApplicationDbContext db, CancellationToken cancellationToken) => type switch
    {
        AidxContentType.Project => await db.Set<AidxProjectResearchArea>()
            .Where(x => x.ProjectId == canonicalId).Select(x => x.ResearchAreaId).ToListAsync(cancellationToken),
        AidxContentType.Publication => await db.Set<AidxPublicationResearchArea>()
            .Where(x => x.PublicationId == canonicalId).Select(x => x.ResearchAreaId).ToListAsync(cancellationToken),
        _ => [], // News/Event have no research-area association — scoped editors never match
    };

    private static async Task<IAidxWorkflowContent?> LoadByIdAsync(
        AidxContentType type, Guid id, ApplicationDbContext db, CancellationToken cancellationToken) => type switch
    {
        AidxContentType.Project => await db.AidxProjects.FirstOrDefaultAsync(p => p.Id == id, cancellationToken),
        AidxContentType.News => await db.AidxNews.FirstOrDefaultAsync(n => n.Id == id, cancellationToken),
        AidxContentType.Event => await db.AidxEvents.FirstOrDefaultAsync(e => e.Id == id, cancellationToken),
        AidxContentType.Publication => await db.AidxPublications.FirstOrDefaultAsync(p => p.Id == id, cancellationToken),
        _ => null,
    };

    // ---- ownership -------------------------------------------------------------------------

    private static async Task<IResult> ClaimOwnershipAsync(
        string contentType, Guid id, ClaimsPrincipal principal, ApplicationDbContext db,
        AidxContentEditingService editing, CancellationToken cancellationToken)
    {
        if (!TryParseContentType(contentType, out var type))
        {
            return Results.NotFound();
        }

        var (user, membership, failure) = await ResolveActorAsync(principal, db, cancellationToken);
        if (failure is not null)
        {
            return failure;
        }

        var content = await LoadByIdAsync(type, id, db, cancellationToken);
        if (content is null)
        {
            return Results.NotFound();
        }

        var areaIds = await GetContentAreaIdsAsync(type, content.Id, db, cancellationToken);
        if (!AidxPermissions.CanClaimOwnership(user!, membership, content.CreatedByMembershipId, areaIds))
        {
            return Results.Forbid();
        }

        await editing.ClaimOwnershipAsync(content, membership!.Id, type, user!.Id, cancellationToken);
        return Results.Ok();
    }

    // ---- revision history --------------------------------------------------------------------

    private static async Task<IResult> ListRevisionsAsync(
        string contentType, Guid id, ClaimsPrincipal principal, ApplicationDbContext db, CancellationToken cancellationToken)
    {
        if (!TryParseContentType(contentType, out var type))
        {
            return Results.NotFound();
        }

        var (user, membership, failure) = await ResolveActorAsync(principal, db, cancellationToken);
        if (failure is not null)
        {
            return failure;
        }

        var content = await LoadByIdAsync(type, id, db, cancellationToken);
        if (content is null)
        {
            return Results.NotFound();
        }

        var canonicalId = content.RevisionOfId ?? content.Id;
        var areaIds = await GetContentAreaIdsAsync(type, canonicalId, db, cancellationToken);
        var isOwner = AidxPermissions.CanEditAsOwner(membership, content.CreatedByMembershipId);
        if (!isOwner && !CanReview(user!, membership, content, areaIds))
        {
            return Results.Forbid();
        }

        var snapshots = await db.AidxContentSnapshots
            .Where(s => s.ContentType == type && s.SubjectId == id)
            .OrderBy(s => s.SnapshotNumber)
            .Select(s => new AidxContentSnapshotResponse(s.Id, s.SnapshotNumber, s.SubmittedByMembershipId, s.CreatedAt, s.ContentJson))
            .ToListAsync(cancellationToken);

        return Results.Ok(snapshots);
    }
}
