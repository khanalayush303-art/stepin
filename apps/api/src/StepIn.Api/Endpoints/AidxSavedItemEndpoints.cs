using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using StepIn.Api.Infrastructure;
using StepIn.Application.Common.Interfaces;
using StepIn.Domain.Aidx;

namespace StepIn.Api.Endpoints;

/// <summary>
/// Saved Projects/Publications for signed-in users. Mirrors SavedJobEndpoints's ownership/
/// uniqueness/404-not-403 shape exactly: ownership always resolved server-side from the
/// authenticated principal, never a client-supplied id; an unpublished/unknown target is 404,
/// never 403, so a draft's existence is never revealed; a fast existence pre-check plus the
/// real unique index as the actual race guarantee.
/// </summary>
public static class AidxSavedItemEndpoints
{
    public static IEndpointRouteBuilder MapAidxSavedItemEndpoints(this IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        var group = app.MapGroup("/api/v1/aidx").WithTags("AIDX Saved").RequireAuthorization();

        group.MapPost("/{contentType}/{id:guid}/saved", SaveAsync).WithName("AidxSaveItem");
        group.MapDelete("/{contentType}/{id:guid}/saved", UnsaveAsync).WithName("AidxUnsaveItem");
        group.MapGet("/saved-items", ListAsync).WithName("AidxListSavedItems");

        return app;
    }

    private static bool TryParseSavableType(string contentType, out AidxContentType parsed)
    {
        if (AidxEndpointHelpers.TryParseEnum(contentType, out parsed)
            && parsed is AidxContentType.Project or AidxContentType.Publication)
        {
            return true;
        }

        parsed = default;
        return false;
    }

    private static async Task<bool> TargetIsPublishedAsync(IApplicationDbContext db, AidxContentType type, Guid id, CancellationToken cancellationToken) => type switch
    {
        AidxContentType.Project => await db.AidxProjects.AnyAsync(p => p.Id == id && p.Status == AidxContentStatus.Published && p.RevisionOfId == null, cancellationToken),
        AidxContentType.Publication => await db.AidxPublications.AnyAsync(p => p.Id == id && p.Status == AidxContentStatus.Published && p.RevisionOfId == null, cancellationToken),
        _ => false,
    };

    private static async Task<string?> TitleForAsync(IApplicationDbContext db, AidxContentType type, Guid id, CancellationToken cancellationToken) => type switch
    {
        AidxContentType.Project => await db.AidxProjects.Where(p => p.Id == id).Select(p => p.Title).FirstOrDefaultAsync(cancellationToken),
        AidxContentType.Publication => await db.AidxPublications.Where(p => p.Id == id).Select(p => p.Title).FirstOrDefaultAsync(cancellationToken),
        _ => null,
    };

    private static async Task<IResult> SaveAsync(
        string contentType, Guid id, ClaimsPrincipal principal, IApplicationDbContext db, CancellationToken cancellationToken)
    {
        if (!TryParseSavableType(contentType, out var type))
        {
            return Results.NotFound();
        }

        var user = await principal.GetCurrentUserAsync(db, cancellationToken);
        if (user is null)
        {
            return Results.Unauthorized();
        }

        if (!await TargetIsPublishedAsync(db, type, id, cancellationToken))
        {
            return Results.NotFound();
        }

        var alreadySaved = await db.AidxSavedItems
            .AnyAsync(s => s.UserId == user.Id && s.ContentType == type && s.ContentId == id, cancellationToken);
        if (alreadySaved)
        {
            return Results.Conflict();
        }

        var saved = new AidxSavedItem { UserId = user.Id, ContentType = type, ContentId = id };
        db.Add(saved);

        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            return Results.Conflict();
        }

        var title = await TitleForAsync(db, type, id, cancellationToken) ?? "";
        return Results.Created((string?)null, new AidxSavedItemResponse(saved.Id, saved.CreatedAt, type.ToString(), id, title));
    }

    private static async Task<IResult> UnsaveAsync(
        string contentType, Guid id, ClaimsPrincipal principal, IApplicationDbContext db, CancellationToken cancellationToken)
    {
        if (!TryParseSavableType(contentType, out var type))
        {
            return Results.NotFound();
        }

        var user = await principal.GetCurrentUserAsync(db, cancellationToken);
        if (user is null)
        {
            return Results.Unauthorized();
        }

        var saved = await db.AidxSavedItems
            .Where(s => s.UserId == user.Id && s.ContentType == type && s.ContentId == id)
            .ToListAsync(cancellationToken);
        if (saved.Count == 0)
        {
            return Results.NotFound();
        }

        db.RemoveRange(saved);
        await db.SaveChangesAsync(cancellationToken);
        return Results.NoContent();
    }

    private static async Task<IResult> ListAsync(
        ClaimsPrincipal principal, IApplicationDbContext db, CancellationToken cancellationToken)
    {
        var user = await principal.GetCurrentUserAsync(db, cancellationToken);
        if (user is null)
        {
            return Results.Unauthorized();
        }

        var saved = await db.AidxSavedItems
            .Where(s => s.UserId == user.Id)
            .OrderByDescending(s => s.CreatedAt)
            .ToListAsync(cancellationToken);

        var results = new List<AidxSavedItemResponse>();
        foreach (var item in saved)
        {
            var title = await TitleForAsync(db, item.ContentType, item.ContentId, cancellationToken);
            results.Add(new AidxSavedItemResponse(item.Id, item.CreatedAt, item.ContentType.ToString(), item.ContentId, title ?? ""));
        }

        return Results.Ok(results);
    }
}
