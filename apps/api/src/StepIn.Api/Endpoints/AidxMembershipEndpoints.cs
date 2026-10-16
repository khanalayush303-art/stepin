using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using StepIn.Api.Infrastructure;
using StepIn.Application.Common.Interfaces;
using StepIn.Domain.Aidx;
using StepIn.Domain.Common;
using StepIn.Domain.Users;

namespace StepIn.Api.Endpoints;

/// <summary>
/// AIDX lab membership: who holds a Contributor/Editor/LabManager permission,
/// separate from StepIn's own Applicant/Recruiter/Admin platform roles.
///
/// Authorization for the management routes is deliberately performed inside
/// each handler, not via an ASP.NET Core claims-based policy: a membership
/// policy baked into the Clerk JWT would only refresh on token renewal,
/// reintroducing exactly the staleness a Clerk role-claim change already
/// avoids elsewhere in this codebase. Instead every handler re-resolves the
/// caller's own, fresh <see cref="AidxMembership"/> row from the database on
/// every request via <see cref="AidxPermissions"/> — so a revocation takes
/// effect on the very next request, not on next token refresh.
/// </summary>
public static class AidxMembershipEndpoints
{
    public static IEndpointRouteBuilder MapAidxMembershipEndpoints(this IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.MapGet("/api/v1/aidx/membership/me", MeAsync)
            .WithTags("AIDX Membership")
            .RequireAuthorization()
            .WithName("GetAidxMembershipMe");

        var admin = app.MapGroup("/api/v1/admin/aidx/memberships")
            .WithTags("AIDX Admin")
            .RequireAuthorization();

        admin.MapGet("/", ListAsync).WithName("ListAidxMemberships");
        admin.MapPost("/", GrantAsync).WithName("GrantAidxMembership");
        admin.MapPut("/{id:guid}", UpdateAsync).WithName("UpdateAidxMembership");
        admin.MapPost("/{id:guid}/revoke", RevokeAsync).WithName("RevokeAidxMembership");

        app.MapPost("/api/v1/admin/aidx/researchers/{id:guid}/link-user", LinkResearcherToUserAsync)
            .WithTags("AIDX Admin")
            .RequireAuthorization()
            .WithName("LinkAidxResearcherToUser");

        app.MapGet("/api/v1/admin/aidx/users", SearchUsersAsync)
            .WithTags("AIDX Admin")
            .RequireAuthorization()
            .WithName("SearchAidxUsers");

        return app;
    }

    private static async Task<IResult> MeAsync(ClaimsPrincipal principal, IApplicationDbContext db, CancellationToken cancellationToken)
    {
        var user = await principal.GetCurrentUserAsync(db, cancellationToken);
        if (user is null)
        {
            return Results.Unauthorized();
        }

        // Defense in depth: ClerkUserSyncClaimsTransformation already withholds the platform role
        // claim for a Suspended account, but AidxMembership is a separate system re-resolved fresh
        // from the database here rather than from any claim — so it re-checks AccountStatus itself
        // instead of relying on that claim-withholding to also cover it.
        if (user.AccountStatus != AccountStatus.Active)
        {
            return Results.Ok(new AidxMembershipMeResponse(false, null, null, null));
        }

        var membership = await db.AidxMemberships.FirstOrDefaultAsync(m => m.UserId == user.Id, cancellationToken);

        return Results.Ok(new AidxMembershipMeResponse(
            HasMembership: membership is { Status: AidxMembershipStatus.Active },
            Role: membership?.Status == AidxMembershipStatus.Active ? membership.Role.ToString() : null,
            Status: membership?.Status.ToString(),
            ScopedResearchAreaId: membership?.Status == AidxMembershipStatus.Active ? membership.ScopedResearchAreaId : null));
    }

    /// <summary>Resolves the caller and their own membership, and checks management rights in one step.</summary>
    private static async Task<(ApplicationUser? User, AidxMembership? OwnMembership, IResult? Forbidden)> AuthorizeManagerAsync(
        ClaimsPrincipal principal, IApplicationDbContext db, CancellationToken cancellationToken)
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

        if (!AidxPermissions.CanManageMemberships(user, membership))
        {
            return (user, membership, Results.Forbid());
        }

        return (user, membership, null);
    }

    private static async Task<IResult> ListAsync(
        ClaimsPrincipal principal, IApplicationDbContext db, CancellationToken cancellationToken)
    {
        var (_, _, forbidden) = await AuthorizeManagerAsync(principal, db, cancellationToken);
        if (forbidden is not null)
        {
            return forbidden;
        }

        var memberships = await db.AidxMemberships
            .Join(db.Users, m => m.UserId, u => u.Id, (m, u) => new { m, u })
            .OrderBy(x => x.u.Email)
            .Select(x => new AidxMembershipResponse(
                x.m.Id, x.m.UserId, x.u.Email, x.m.Role.ToString(), x.m.Status.ToString(),
                x.m.ScopedResearchAreaId, x.m.CreatedAt, x.m.RevokedAt))
            .ToListAsync(cancellationToken);

        return Results.Ok(memberships);
    }

    private static async Task<IResult> GrantAsync(
        GrantAidxMembershipRequest request,
        ClaimsPrincipal principal,
        IApplicationDbContext db,
        CancellationToken cancellationToken)
    {
        var (user, _, forbidden) = await AuthorizeManagerAsync(principal, db, cancellationToken);
        if (forbidden is not null)
        {
            return forbidden;
        }

        if (!Enum.TryParse<AidxLabRole>(request.Role, ignoreCase: true, out var role) || !Enum.IsDefined(role))
        {
            return Results.ValidationProblem(new Dictionary<string, string[]> { ["role"] = ["Unknown role."] });
        }

        // No self-assignment: a LabManager/Admin cannot grant membership to themselves
        // through this endpoint — bootstrap exists exactly so this rule has no exception.
        if (request.UserId == user!.Id)
        {
            return Results.Problem(
                title: "Cannot grant membership to yourself.",
                detail: "Ask another LabManager or Admin to perform this action.",
                statusCode: StatusCodes.Status403Forbidden);
        }

        var targetUser = await db.Users.FirstOrDefaultAsync(u => u.Id == request.UserId, cancellationToken);
        if (targetUser is null)
        {
            return Results.NotFound();
        }

        var membership = await db.AidxMemberships.FirstOrDefaultAsync(m => m.UserId == request.UserId, cancellationToken);
        var previousRole = membership?.Role;
        var previousScope = membership?.ScopedResearchAreaId;

        if (membership is null)
        {
            membership = new AidxMembership
            {
                UserId = request.UserId,
                Role = role,
                Status = AidxMembershipStatus.Active,
                ScopedResearchAreaId = request.ScopedResearchAreaId,
                GrantedByUserId = user.Id,
            };
            db.Add(membership);
        }
        else
        {
            membership.Role = role;
            membership.Status = AidxMembershipStatus.Active;
            membership.ScopedResearchAreaId = request.ScopedResearchAreaId;
            membership.RevokedAt = null;
        }

        await db.SaveChangesAsync(cancellationToken);

        db.Add(new AidxMembershipAuditEntry
        {
            MembershipId = membership.Id,
            Action = AidxMembershipAuditAction.Granted,
            PerformedByUserId = user.Id,
            PreviousRole = previousRole,
            NewRole = role,
            PreviousScopeId = previousScope,
            NewScopeId = request.ScopedResearchAreaId,
        });
        await db.SaveChangesAsync(cancellationToken);

        return Results.Created((string?)null, new AidxMembershipResponse(
            membership.Id, membership.UserId, targetUser.Email, membership.Role.ToString(), membership.Status.ToString(),
            membership.ScopedResearchAreaId, membership.CreatedAt, membership.RevokedAt));
    }

    private static async Task<IResult> UpdateAsync(
        Guid id,
        UpdateAidxMembershipRequest request,
        ClaimsPrincipal principal,
        IApplicationDbContext db,
        CancellationToken cancellationToken)
    {
        var (user, _, forbidden) = await AuthorizeManagerAsync(principal, db, cancellationToken);
        if (forbidden is not null)
        {
            return forbidden;
        }

        if (!Enum.TryParse<AidxLabRole>(request.Role, ignoreCase: true, out var role) || !Enum.IsDefined(role))
        {
            return Results.ValidationProblem(new Dictionary<string, string[]> { ["role"] = ["Unknown role."] });
        }

        var membership = await db.AidxMemberships.FirstOrDefaultAsync(m => m.Id == id, cancellationToken);
        if (membership is null)
        {
            return Results.NotFound();
        }

        if (membership.UserId == user!.Id)
        {
            return Results.Problem(
                title: "Cannot change your own membership.",
                statusCode: StatusCodes.Status403Forbidden);
        }

        var previousRole = membership.Role;
        var previousScope = membership.ScopedResearchAreaId;

        membership.Role = role;
        membership.ScopedResearchAreaId = request.ScopedResearchAreaId;
        await db.SaveChangesAsync(cancellationToken);

        db.Add(new AidxMembershipAuditEntry
        {
            MembershipId = membership.Id,
            Action = previousRole != role ? AidxMembershipAuditAction.RoleChanged : AidxMembershipAuditAction.ScopeChanged,
            PerformedByUserId = user.Id,
            PreviousRole = previousRole,
            NewRole = role,
            PreviousScopeId = previousScope,
            NewScopeId = request.ScopedResearchAreaId,
        });
        await db.SaveChangesAsync(cancellationToken);

        return Results.Ok();
    }

    private static async Task<IResult> RevokeAsync(
        Guid id, ClaimsPrincipal principal, IApplicationDbContext db, CancellationToken cancellationToken)
    {
        var (user, _, forbidden) = await AuthorizeManagerAsync(principal, db, cancellationToken);
        if (forbidden is not null)
        {
            return forbidden;
        }

        var membership = await db.AidxMemberships.FirstOrDefaultAsync(m => m.Id == id, cancellationToken);
        if (membership is null)
        {
            return Results.NotFound();
        }

        if (membership.UserId == user!.Id)
        {
            return Results.Problem(
                title: "Cannot revoke your own membership.",
                statusCode: StatusCodes.Status403Forbidden);
        }

        if (membership.Status == AidxMembershipStatus.Revoked)
        {
            return Results.Ok(); // idempotent
        }

        // Revoking never deletes the row — authorship/ownership links and this
        // row's own audit history all stay intact; only access is withdrawn.
        membership.Status = AidxMembershipStatus.Revoked;
        membership.RevokedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(cancellationToken);

        db.Add(new AidxMembershipAuditEntry
        {
            MembershipId = membership.Id,
            Action = AidxMembershipAuditAction.Revoked,
            PerformedByUserId = user.Id,
            PreviousRole = membership.Role,
        });
        await db.SaveChangesAsync(cancellationToken);

        return Results.Ok();
    }

    private static async Task<IResult> LinkResearcherToUserAsync(
        Guid id,
        LinkResearcherToUserRequest request,
        ClaimsPrincipal principal,
        IApplicationDbContext db,
        CancellationToken cancellationToken)
    {
        var user = await principal.GetCurrentUserAsync(db, cancellationToken);
        if (user is null)
        {
            return Results.Unauthorized();
        }

        if (user.AccountStatus != AccountStatus.Active)
        {
            return Results.Forbid();
        }

        var membership = await db.AidxMemberships.FirstOrDefaultAsync(m => m.UserId == user.Id, cancellationToken);
        if (!AidxPermissions.CanLinkResearcherToUser(user, membership))
        {
            return Results.Forbid();
        }

        var researcher = await db.AidxResearchers.FirstOrDefaultAsync(r => r.Id == id, cancellationToken);
        if (researcher is null)
        {
            return Results.NotFound();
        }

        var targetUser = await db.Users.FirstOrDefaultAsync(u => u.Id == request.UserId, cancellationToken);
        if (targetUser is null)
        {
            return Results.NotFound();
        }

        var alreadyLinkedElsewhere = await db.AidxResearchers
            .AnyAsync(r => r.UserId == request.UserId && r.Id != id, cancellationToken);
        if (alreadyLinkedElsewhere)
        {
            return Results.Problem(
                title: "That user is already linked to a different researcher profile.",
                statusCode: StatusCodes.Status409Conflict);
        }

        researcher.UserId = request.UserId;
        await db.SaveChangesAsync(cancellationToken);

        return Results.Ok();
    }

    /// <summary>
    /// Lets someone who can grant memberships or link researchers find the right account —
    /// without this, granting a membership had no way to discover a target UserId at all short of
    /// direct DB access. Requires an actual search term (never returns the full user list) and
    /// caps results, since email is not unique on ApplicationUser and this is picker support, not
    /// a general directory.
    /// </summary>
    private static async Task<IResult> SearchUsersAsync(
        string? search, ClaimsPrincipal principal, IApplicationDbContext db, CancellationToken cancellationToken)
    {
        var user = await principal.GetCurrentUserAsync(db, cancellationToken);
        if (user is null)
        {
            return Results.Unauthorized();
        }

        if (user.AccountStatus != AccountStatus.Active)
        {
            return Results.Forbid();
        }

        var membership = await db.AidxMemberships.FirstOrDefaultAsync(m => m.UserId == user.Id, cancellationToken);
        if (!AidxPermissions.CanManageMemberships(user, membership) && !AidxPermissions.CanLinkResearcherToUser(user, membership))
        {
            return Results.Forbid();
        }

        if (string.IsNullOrWhiteSpace(search) || search.Trim().Length < 2)
        {
            return Results.Ok(Array.Empty<AidxUserSearchResultResponse>());
        }

        var term = search.Trim();
        var results = await db.Users
            .Where(u => EF.Functions.ILike(u.Email, $"%{term}%")
                || EF.Functions.ILike(u.FirstName, $"%{term}%")
                || EF.Functions.ILike(u.LastName, $"%{term}%"))
            .OrderBy(u => u.Email)
            .Take(10)
            .Select(u => new AidxUserSearchResultResponse(u.Id, u.Email, u.FirstName, u.LastName))
            .ToListAsync(cancellationToken);

        return Results.Ok(results);
    }
}
