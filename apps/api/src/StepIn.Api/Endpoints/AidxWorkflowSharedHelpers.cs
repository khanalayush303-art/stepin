using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using StepIn.Api.Infrastructure;
using StepIn.Domain.Aidx;
using StepIn.Domain.Common;
using StepIn.Domain.Users;
using StepIn.Infrastructure.Persistence;

namespace StepIn.Api.Endpoints;

/// <summary>
/// Shared across <see cref="AidxWorkflowEndpoints"/> (membership-gated actions) and
/// <see cref="AidxAdminEndpoints"/> (the legacy Admin CMS, now routed through the same
/// <c>AidxContentEditingService</c> engine) — one implementation of "who is acting" and
/// "how to re-point a cloned revision's child rows at its own id", not two copies that could
/// silently drift apart.
/// </summary>
internal static class AidxWorkflowSharedHelpers
{
    public static async Task<(ApplicationUser? User, AidxMembership? Membership, IResult? Failure)> ResolveActorAsync(
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

    /// <summary>Clones a project's nav collections onto a freshly-created revision row. Milestones
    /// are cloned for display/history continuity on the revision, but — like every other nav
    /// collection here — there is no merge-back path onto the canonical row yet; see
    /// AidxContentAuthoringEndpoints' and AidxAdminEndpoints' doc comments for the explicitly
    /// deferred milestone-management gap.</summary>
    public static void ReparentProject(AidxProject clone, AidxProject canonical)
    {
        clone.ResearchAreas = canonical.ResearchAreas.Select(ra => new AidxProjectResearchArea { ProjectId = clone.Id, ResearchAreaId = ra.ResearchAreaId }).ToList();
        clone.Technologies = canonical.Technologies.Select(t => new AidxProjectTechnology { ProjectId = clone.Id, Name = t.Name }).ToList();
        clone.Researchers = canonical.Researchers.Select(r => new AidxProjectResearcher { ProjectId = clone.Id, ResearcherId = r.ResearcherId, Role = r.Role }).ToList();
        clone.Milestones = canonical.Milestones
            .Select(m => new AidxProjectMilestone { ProjectId = clone.Id, Title = m.Title, Description = m.Description, TargetDate = m.TargetDate, CompletedDate = m.CompletedDate, SortOrder = m.SortOrder })
            .ToList();
    }

    /// <summary>Clones a publication's nav collections onto a freshly-created revision row.</summary>
    public static void ReparentPublication(AidxPublication clone, AidxPublication canonical)
    {
        clone.Authors = canonical.Authors.Select(a => new AidxPublicationAuthor { PublicationId = clone.Id, Position = a.Position, ResearcherId = a.ResearcherId, ExternalAuthorName = a.ExternalAuthorName }).ToList();
        clone.ResearchAreas = canonical.ResearchAreas.Select(ra => new AidxPublicationResearchArea { PublicationId = clone.Id, ResearchAreaId = ra.ResearchAreaId }).ToList();
        clone.Projects = canonical.Projects.Select(p => new AidxPublicationProject { PublicationId = clone.Id, ProjectId = p.ProjectId }).ToList();
    }
}
