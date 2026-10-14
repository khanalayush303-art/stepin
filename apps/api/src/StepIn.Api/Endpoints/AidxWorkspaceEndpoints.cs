using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using StepIn.Api.Infrastructure;
using StepIn.Domain.Aidx;
using StepIn.Domain.Common;
using StepIn.Domain.Users;
using StepIn.Infrastructure.Persistence;

namespace StepIn.Api.Endpoints;

/// <summary>
/// Real, non-fabricated summaries for the AIDX workspace (a Contributor's own content) and
/// manage (an Editor/LabManager/Admin's review queue) pages. Every count is a live query —
/// there is no placeholder or estimated figure anywhere in this file.
/// </summary>
public static class AidxWorkspaceEndpoints
{
    public static IEndpointRouteBuilder MapAidxWorkspaceEndpoints(this IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.MapGet("/api/v1/aidx/workspace/summary", WorkspaceSummaryAsync)
            .WithTags("AIDX Workspace").RequireAuthorization().WithName("AidxWorkspaceSummary");

        app.MapGet("/api/v1/aidx/workspace/items", WorkspaceItemsAsync)
            .WithTags("AIDX Workspace").RequireAuthorization().WithName("AidxWorkspaceItems");

        app.MapGet("/api/v1/aidx/manage/summary", ManageSummaryAsync)
            .WithTags("AIDX Manage").RequireAuthorization().WithName("AidxManageSummary");

        app.MapGet("/api/v1/aidx/manage/queue", ManageQueueAsync)
            .WithTags("AIDX Manage").RequireAuthorization().WithName("AidxManageQueue");

        return app;
    }

    private static async Task<(ApplicationUser? User, AidxMembership? Membership, IResult? Failure)> ResolveActorAsync(
        ClaimsPrincipal principal, ApplicationDbContext db, CancellationToken cancellationToken)
    {
        var user = await principal.GetCurrentUserAsync(db, cancellationToken);
        if (user is null)
        {
            return (null, null, Results.Unauthorized());
        }

        if (user.AccountStatus != AccountStatus.Active)
        {
            return (user, null, Results.Forbid());
        }

        var membership = await db.AidxMemberships.FirstOrDefaultAsync(m => m.UserId == user.Id, cancellationToken);
        return (user, membership, null);
    }

    private static async Task<IResult> WorkspaceSummaryAsync(
        ClaimsPrincipal principal, ApplicationDbContext db, CancellationToken cancellationToken)
    {
        var (_, membership, failure) = await ResolveActorAsync(principal, db, cancellationToken);
        if (failure is not null)
        {
            return failure;
        }

        if (membership is not { Status: AidxMembershipStatus.Active })
        {
            return Results.Ok(new AidxWorkspaceSummaryResponse(0, 0, 0, 0, 0, 0));
        }

        var mine = membership.Id;
        var statuses = await CountsByStatusForOwnerAsync(db, mine, cancellationToken);

        int Get(AidxContentStatus s) => statuses.TryGetValue(s, out var c) ? c : 0;

        return Results.Ok(new AidxWorkspaceSummaryResponse(
            MyDrafts: Get(AidxContentStatus.Draft),
            MySubmitted: Get(AidxContentStatus.Submitted),
            MyUnderReview: Get(AidxContentStatus.UnderReview),
            MyChangesRequested: Get(AidxContentStatus.ChangesRequested),
            MyPublished: Get(AidxContentStatus.Published),
            MyArchived: Get(AidxContentStatus.Archived)));
    }

    private static async Task<Dictionary<AidxContentStatus, int>> CountsByStatusForOwnerAsync(
        ApplicationDbContext db, Guid membershipId, CancellationToken cancellationToken)
    {
        var result = new Dictionary<AidxContentStatus, int>();

        void Merge(IEnumerable<(AidxContentStatus Status, int Count)> rows)
        {
            foreach (var (status, count) in rows)
            {
                result[status] = result.GetValueOrDefault(status) + count;
            }
        }

        Merge(await db.AidxProjects.Where(p => p.CreatedByMembershipId == membershipId)
            .GroupBy(p => p.Status).Select(g => new ValueTuple<AidxContentStatus, int>(g.Key, g.Count()))
            .ToListAsync(cancellationToken));
        Merge(await db.AidxNews.Where(n => n.CreatedByMembershipId == membershipId)
            .GroupBy(n => n.Status).Select(g => new ValueTuple<AidxContentStatus, int>(g.Key, g.Count()))
            .ToListAsync(cancellationToken));
        Merge(await db.AidxEvents.Where(e => e.CreatedByMembershipId == membershipId)
            .GroupBy(e => e.Status).Select(g => new ValueTuple<AidxContentStatus, int>(g.Key, g.Count()))
            .ToListAsync(cancellationToken));
        Merge(await db.AidxPublications.Where(p => p.CreatedByMembershipId == membershipId)
            .GroupBy(p => p.Status).Select(g => new ValueTuple<AidxContentStatus, int>(g.Key, g.Count()))
            .ToListAsync(cancellationToken));

        return result;
    }

    private static async Task<IResult> WorkspaceItemsAsync(
        ClaimsPrincipal principal, ApplicationDbContext db, CancellationToken cancellationToken)
    {
        var (_, membership, failure) = await ResolveActorAsync(principal, db, cancellationToken);
        if (failure is not null)
        {
            return failure;
        }

        if (membership is not { Status: AidxMembershipStatus.Active })
        {
            return Results.Ok(Array.Empty<AidxWorkspaceItemResponse>());
        }

        var mine = membership.Id;
        var items = new List<AidxWorkspaceItemResponse>();

        items.AddRange(await db.AidxProjects.Where(p => p.CreatedByMembershipId == mine)
            .Select(p => new AidxWorkspaceItemResponse("Project", p.Id, p.Title, p.Status.ToString(), p.RevisionOfId, p.UpdatedAt))
            .ToListAsync(cancellationToken));
        items.AddRange(await db.AidxNews.Where(n => n.CreatedByMembershipId == mine)
            .Select(n => new AidxWorkspaceItemResponse("News", n.Id, n.Title, n.Status.ToString(), n.RevisionOfId, n.UpdatedAt))
            .ToListAsync(cancellationToken));
        items.AddRange(await db.AidxEvents.Where(e => e.CreatedByMembershipId == mine)
            .Select(e => new AidxWorkspaceItemResponse("Event", e.Id, e.Title, e.Status.ToString(), e.RevisionOfId, e.UpdatedAt))
            .ToListAsync(cancellationToken));
        items.AddRange(await db.AidxPublications.Where(p => p.CreatedByMembershipId == mine)
            .Select(p => new AidxWorkspaceItemResponse("Publication", p.Id, p.Title, p.Status.ToString(), p.RevisionOfId, p.UpdatedAt))
            .ToListAsync(cancellationToken));

        return Results.Ok(items.OrderByDescending(i => i.UpdatedAt).ToList());
    }

    private static async Task<IResult> ManageSummaryAsync(
        ClaimsPrincipal principal, ApplicationDbContext db, CancellationToken cancellationToken)
    {
        var (user, membership, failure) = await ResolveActorAsync(principal, db, cancellationToken);
        if (failure is not null)
        {
            return failure;
        }

        if (!AidxPermissions.IsPlatformAdmin(user!)
            && (membership is not { Status: AidxMembershipStatus.Active } || membership.Role == AidxLabRole.Contributor))
        {
            return Results.Forbid();
        }

        var labWide = AidxPermissions.IsPlatformAdmin(user!) || membership!.ScopedResearchAreaId is null;
        var scopeAreaId = labWide ? (Guid?)null : membership!.ScopedResearchAreaId;

        var reviewStatuses = new[] { AidxContentStatus.Submitted, AidxContentStatus.UnderReview };

        var projectsQuery = db.AidxProjects.Where(p => reviewStatuses.Contains(p.Status));
        var publicationsQuery = db.AidxPublications.Where(p => reviewStatuses.Contains(p.Status));

        if (scopeAreaId is { } area)
        {
            projectsQuery = projectsQuery.Where(p => p.ResearchAreas.Any(ra => ra.ResearchAreaId == area));
            publicationsQuery = publicationsQuery.Where(p => p.ResearchAreas.Any(ra => ra.ResearchAreaId == area));
        }

        var projectsCount = await projectsQuery.CountAsync(cancellationToken);
        var publicationsCount = await publicationsQuery.CountAsync(cancellationToken);

        // News/Event have no research-area association — a scoped Editor has zero rights over
        // them regardless, so their counts are not "0 waiting," they are "not visible to you."
        int? newsCount = labWide
            ? await db.AidxNews.CountAsync(n => reviewStatuses.Contains(n.Status), cancellationToken)
            : null;
        int? eventsCount = labWide
            ? await db.AidxEvents.CountAsync(e => reviewStatuses.Contains(e.Status), cancellationToken)
            : null;

        return Results.Ok(new AidxManageSummaryResponse(projectsCount, newsCount, eventsCount, publicationsCount));
    }

    private static async Task<IResult> ManageQueueAsync(
        ClaimsPrincipal principal, ApplicationDbContext db, CancellationToken cancellationToken)
    {
        var (user, membership, failure) = await ResolveActorAsync(principal, db, cancellationToken);
        if (failure is not null)
        {
            return failure;
        }

        if (!AidxPermissions.IsPlatformAdmin(user!)
            && (membership is not { Status: AidxMembershipStatus.Active } || membership.Role == AidxLabRole.Contributor))
        {
            return Results.Forbid();
        }

        var labWide = AidxPermissions.IsPlatformAdmin(user!) || membership!.ScopedResearchAreaId is null;
        var scopeAreaId = labWide ? (Guid?)null : membership!.ScopedResearchAreaId;
        var reviewStatuses = new[] { AidxContentStatus.Submitted, AidxContentStatus.UnderReview };

        var items = new List<AidxWorkspaceItemResponse>();

        var projectsQuery = db.AidxProjects.Where(p => reviewStatuses.Contains(p.Status));
        var publicationsQuery = db.AidxPublications.Where(p => reviewStatuses.Contains(p.Status));
        if (scopeAreaId is { } area)
        {
            projectsQuery = projectsQuery.Where(p => p.ResearchAreas.Any(ra => ra.ResearchAreaId == area));
            publicationsQuery = publicationsQuery.Where(p => p.ResearchAreas.Any(ra => ra.ResearchAreaId == area));
        }

        items.AddRange(await projectsQuery
            .Select(p => new AidxWorkspaceItemResponse("Project", p.Id, p.Title, p.Status.ToString(), p.RevisionOfId, p.UpdatedAt))
            .ToListAsync(cancellationToken));
        items.AddRange(await publicationsQuery
            .Select(p => new AidxWorkspaceItemResponse("Publication", p.Id, p.Title, p.Status.ToString(), p.RevisionOfId, p.UpdatedAt))
            .ToListAsync(cancellationToken));

        if (labWide)
        {
            items.AddRange(await db.AidxNews.Where(n => reviewStatuses.Contains(n.Status))
                .Select(n => new AidxWorkspaceItemResponse("News", n.Id, n.Title, n.Status.ToString(), n.RevisionOfId, n.UpdatedAt))
                .ToListAsync(cancellationToken));
            items.AddRange(await db.AidxEvents.Where(e => reviewStatuses.Contains(e.Status))
                .Select(e => new AidxWorkspaceItemResponse("Event", e.Id, e.Title, e.Status.ToString(), e.RevisionOfId, e.UpdatedAt))
                .ToListAsync(cancellationToken));
        }

        return Results.Ok(items.OrderByDescending(i => i.UpdatedAt).ToList());
    }
}
