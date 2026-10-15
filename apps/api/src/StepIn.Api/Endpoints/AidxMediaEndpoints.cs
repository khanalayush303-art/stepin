using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using StepIn.Application.Common.Interfaces;
using StepIn.Domain.Aidx;
using StepIn.Infrastructure.Aidx;
using StepIn.Infrastructure.Persistence;

namespace StepIn.Api.Endpoints;

/// <summary>
/// Upload and serve AIDX media: researcher profile photos (Admin-only — researchers have no
/// Contributor concept), and news/event images and publication PDFs (Contributor-authored, routed
/// through the same <see cref="AidxContentEditingService.BeginEditAsync{TContent}"/> revision
/// mechanics as any other field edit — uploading a new image to already-published content opens a
/// revision instead of replacing the live file).
///
/// Serving is privacy- and freshness-aware rather than a flat file proxy: a published row's media
/// is served with <c>Cache-Control: no-cache</c> and an ETag derived from
/// <c>(ContentId, Status, xmin)</c> — a cheap conditional-revalidation round trip, but every
/// request still re-checks current status server-side, so unpublishing takes effect immediately
/// even against a cached validator. A Draft/revision's media is <c>private, no-store</c> and
/// requires the same ownership/review rights as editing the row itself. A PDF is always served
/// with a server-controlled <c>Content-Type: application/pdf</c>, <c>X-Content-Type-Options:
/// nosniff</c> and a restrictive CSP — never a client-supplied content type.
/// </summary>
public static class AidxMediaEndpoints
{
    public static IEndpointRouteBuilder MapAidxMediaEndpoints(this IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.MapPost("/api/v1/admin/aidx/people/{id:guid}/image", UploadResearcherImageAsync)
            .WithTags("AIDX Admin").RequireAuthorization("RequireAdmin").DisableAntiforgery();
        app.MapGet("/api/v1/aidx/media/people/{id:guid}/image", GetResearcherImageAsync).WithTags("AIDX Media");

        var authoring = app.MapGroup("/api/v1/aidx/authoring").WithTags("AIDX Authoring").RequireAuthorization();
        authoring.MapPost("/news/{id:guid}/image", UploadNewsImageAsync).DisableAntiforgery();
        authoring.MapPost("/events/{id:guid}/image", UploadEventImageAsync).DisableAntiforgery();
        authoring.MapPost("/publications/{id:guid}/pdf", UploadPublicationPdfAsync).DisableAntiforgery();

        var media = app.MapGroup("/api/v1/aidx/media").WithTags("AIDX Media");
        media.MapGet("/news/{id:guid}/image", GetNewsImageAsync);
        media.MapGet("/events/{id:guid}/image", GetEventImageAsync);
        media.MapGet("/publications/{id:guid}/pdf", GetPublicationPdfAsync);

        return app;
    }

    // ---- Researcher profile image (Admin-only, no revision system) -------------------------

    private static async Task<IResult> UploadResearcherImageAsync(
        Guid id, IFormFile file, [Microsoft.AspNetCore.Mvc.FromForm] string? altText, ApplicationDbContext db,
        IAidxMediaStorage storage, CancellationToken cancellationToken)
    {
        var researcher = await db.AidxResearchers.FirstOrDefaultAsync(r => r.Id == id, cancellationToken);
        if (researcher is null)
        {
            return Results.NotFound();
        }

        if (string.IsNullOrWhiteSpace(altText))
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["altText"] = ["Alt text is required when uploading a photo."],
            });
        }

        var validation = await AidxMediaValidation.ValidateImageAsync(file, cancellationToken);
        if (!validation.Success)
        {
            return Results.ValidationProblem(new Dictionary<string, string[]> { ["file"] = [validation.Error!] });
        }

        var oldKey = researcher.ProfileImageKey;
        var newKey = await storage.SaveAsync(new MemoryStream(validation.Content!), validation.Extension!, cancellationToken);

        researcher.ProfileImageKey = newKey;
        researcher.ImageAltText = altText.Trim();
        await db.SaveChangesAsync(cancellationToken);

        if (oldKey is not null && !await db.AidxResearchers.AnyAsync(r => r.Id != id && r.ProfileImageKey == oldKey, cancellationToken))
        {
            await storage.DeleteAsync(oldKey, cancellationToken);
        }

        return Results.Ok(new AidxMediaUploadResponse(newKey));
    }

    private static async Task<IResult> GetResearcherImageAsync(
        Guid id, HttpContext context, ApplicationDbContext db, IAidxMediaStorage storage, CancellationToken cancellationToken)
    {
        var researcher = await db.AidxResearchers.AsNoTracking().FirstOrDefaultAsync(r => r.Id == id, cancellationToken);
        if (researcher?.ProfileImageKey is null)
        {
            return Results.NotFound();
        }

        if (!researcher.Published)
        {
            var (user, failure) = await RequireAdminAsync(context.User, db, cancellationToken);
            if (failure is not null)
            {
                return failure;
            }
            _ = user;
        }

        return await ServeImageAsync(
            storage, researcher.ProfileImageKey, context,
            etagSeed: $"{researcher.Id}:{researcher.Published}:{researcher.ProfileImageKey}",
            isPublic: researcher.Published, cancellationToken);
    }

    // ---- News image (Contributor-authored, revision-aware) ---------------------------------

    private static async Task<IResult> UploadNewsImageAsync(
        Guid id, IFormFile file, [Microsoft.AspNetCore.Mvc.FromForm] string? altText, ClaimsPrincipal principal, ApplicationDbContext db,
        AidxContentEditingService editing, IAidxMediaStorage storage, CancellationToken cancellationToken)
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

        if (!AidxPermissions.IsPlatformAdmin(user!))
        {
            if (!AidxPermissions.CanEditAsOwner(membership, canonical.CreatedByMembershipId))
            {
                return Results.Forbid();
            }

            if (!(AidxEditorialWorkflow.IsEditableByContributor(canonical.Status) || canonical.Status == AidxContentStatus.Published))
            {
                return Results.Problem(title: "This is read-only while it is Submitted or under review.", statusCode: StatusCodes.Status409Conflict);
            }
        }

        if (string.IsNullOrWhiteSpace(altText))
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["altText"] = ["Alt text is required when uploading an image."],
            });
        }

        var validation = await AidxMediaValidation.ValidateImageAsync(file, cancellationToken);
        if (!validation.Success)
        {
            return Results.ValidationProblem(new Dictionary<string, string[]> { ["file"] = [validation.Error!] });
        }

        var target = await editing.BeginEditAsync(db.AidxNews, canonical, null, cancellationToken);
        var oldKey = target.ImageKey;
        var newKey = await storage.SaveAsync(new MemoryStream(validation.Content!), validation.Extension!, cancellationToken);
        target.ImageKey = newKey;
        target.ImageAltText = altText.Trim();
        await db.SaveChangesAsync(cancellationToken);

        if (oldKey is not null)
        {
            await DeleteIfUnreferencedAsync(db, storage, AidxContentType.News, oldKey, db.AidxNews.Select(n => n.ImageKey), cancellationToken);
        }

        return Results.Ok(new AidxMediaUploadResponse(newKey));
    }

    private static async Task<IResult> GetNewsImageAsync(
        Guid id, HttpContext context, ApplicationDbContext db, IAidxMediaStorage storage, CancellationToken cancellationToken)
    {
        var news = await db.AidxNews.AsNoTracking().FirstOrDefaultAsync(n => n.Id == id && n.RevisionOfId == null, cancellationToken);
        if (news?.ImageKey is null)
        {
            return Results.NotFound();
        }

        var isPublic = news.Status == AidxContentStatus.Published;
        if (!isPublic)
        {
            var (user, membership, failure) = await AidxWorkflowSharedHelpers.ResolveActorAsync(context.User, db, cancellationToken);
            if (failure is not null)
            {
                return failure;
            }
            if (!AidxPermissions.IsPlatformAdmin(user!) && !AidxPermissions.CanEditAsOwner(membership, news.CreatedByMembershipId))
            {
                return Results.Forbid();
            }
        }

        return await ServeImageAsync(
            storage, news.ImageKey, context, etagSeed: $"{news.Id}:{news.Status}:{news.ImageKey}", isPublic, cancellationToken);
    }

    // ---- Event image (Contributor-authored, revision-aware) --------------------------------

    private static async Task<IResult> UploadEventImageAsync(
        Guid id, IFormFile file, [Microsoft.AspNetCore.Mvc.FromForm] string? altText, ClaimsPrincipal principal, ApplicationDbContext db,
        AidxContentEditingService editing, IAidxMediaStorage storage, CancellationToken cancellationToken)
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

        if (!AidxPermissions.IsPlatformAdmin(user!))
        {
            if (!AidxPermissions.CanEditAsOwner(membership, canonical.CreatedByMembershipId))
            {
                return Results.Forbid();
            }

            if (!(AidxEditorialWorkflow.IsEditableByContributor(canonical.Status) || canonical.Status == AidxContentStatus.Published))
            {
                return Results.Problem(title: "This is read-only while it is Submitted or under review.", statusCode: StatusCodes.Status409Conflict);
            }
        }

        if (string.IsNullOrWhiteSpace(altText))
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["altText"] = ["Alt text is required when uploading an image."],
            });
        }

        var validation = await AidxMediaValidation.ValidateImageAsync(file, cancellationToken);
        if (!validation.Success)
        {
            return Results.ValidationProblem(new Dictionary<string, string[]> { ["file"] = [validation.Error!] });
        }

        var target = await editing.BeginEditAsync(db.AidxEvents, canonical, null, cancellationToken);
        var oldKey = target.ImageKey;
        var newKey = await storage.SaveAsync(new MemoryStream(validation.Content!), validation.Extension!, cancellationToken);
        target.ImageKey = newKey;
        target.ImageAltText = altText.Trim();
        await db.SaveChangesAsync(cancellationToken);

        if (oldKey is not null)
        {
            await DeleteIfUnreferencedAsync(db, storage, AidxContentType.Event, oldKey, db.AidxEvents.Select(e => e.ImageKey), cancellationToken);
        }

        return Results.Ok(new AidxMediaUploadResponse(newKey));
    }

    private static async Task<IResult> GetEventImageAsync(
        Guid id, HttpContext context, ApplicationDbContext db, IAidxMediaStorage storage, CancellationToken cancellationToken)
    {
        var evt = await db.AidxEvents.AsNoTracking().FirstOrDefaultAsync(e => e.Id == id && e.RevisionOfId == null, cancellationToken);
        if (evt?.ImageKey is null)
        {
            return Results.NotFound();
        }

        var isPublic = evt.Status == AidxContentStatus.Published;
        if (!isPublic)
        {
            var (user, membership, failure) = await AidxWorkflowSharedHelpers.ResolveActorAsync(context.User, db, cancellationToken);
            if (failure is not null)
            {
                return failure;
            }
            if (!AidxPermissions.IsPlatformAdmin(user!) && !AidxPermissions.CanEditAsOwner(membership, evt.CreatedByMembershipId))
            {
                return Results.Forbid();
            }
        }

        return await ServeImageAsync(
            storage, evt.ImageKey, context, etagSeed: $"{evt.Id}:{evt.Status}:{evt.ImageKey}", isPublic, cancellationToken);
    }

    // ---- Publication PDF (Contributor-authored, revision-aware) ----------------------------

    private static async Task<IResult> UploadPublicationPdfAsync(
        Guid id, IFormFile file, ClaimsPrincipal principal, ApplicationDbContext db,
        AidxContentEditingService editing, IAidxMediaStorage storage, CancellationToken cancellationToken)
    {
        var (user, membership, failure) = await AidxWorkflowSharedHelpers.ResolveActorAsync(principal, db, cancellationToken);
        if (failure is not null)
        {
            return failure;
        }

        var canonical = await db.AidxPublications
            .Include(p => p.Authors).Include(p => p.ResearchAreas).Include(p => p.Projects)
            .FirstOrDefaultAsync(p => p.Id == id && p.RevisionOfId == null, cancellationToken);
        if (canonical is null)
        {
            return Results.NotFound();
        }

        if (!AidxPermissions.IsPlatformAdmin(user!))
        {
            if (!AidxPermissions.CanEditAsOwner(membership, canonical.CreatedByMembershipId))
            {
                return Results.Forbid();
            }

            if (!(AidxEditorialWorkflow.IsEditableByContributor(canonical.Status) || canonical.Status == AidxContentStatus.Published))
            {
                return Results.Problem(title: "This is read-only while it is Submitted or under review.", statusCode: StatusCodes.Status409Conflict);
            }
        }

        var validation = await AidxMediaValidation.ValidatePdfAsync(file, cancellationToken);
        if (!validation.Success)
        {
            return Results.ValidationProblem(new Dictionary<string, string[]> { ["file"] = [validation.Error!] });
        }

        var target = await editing.BeginEditAsync(db.AidxPublications, canonical, AidxWorkflowSharedHelpers.ReparentPublication, cancellationToken);
        var oldKey = target.PdfKey;
        var newKey = await storage.SaveAsync(new MemoryStream(validation.Content!), validation.Extension!, cancellationToken);
        target.PdfKey = newKey;
        await db.SaveChangesAsync(cancellationToken);

        if (oldKey is not null)
        {
            await DeleteIfUnreferencedAsync(db, storage, AidxContentType.Publication, oldKey, db.AidxPublications.Select(p => p.PdfKey), cancellationToken);
        }

        return Results.Ok(new AidxMediaUploadResponse(newKey));
    }

    private static async Task<IResult> GetPublicationPdfAsync(
        Guid id, HttpContext context, ApplicationDbContext db, IAidxMediaStorage storage, CancellationToken cancellationToken)
    {
        var publication = await db.AidxPublications.AsNoTracking().FirstOrDefaultAsync(p => p.Id == id && p.RevisionOfId == null, cancellationToken);
        if (publication?.PdfKey is null)
        {
            return Results.NotFound();
        }

        // Published is the dual-written compatibility field, kept in lockstep with Status by the
        // engine — see AidxAdminEndpoints' doc comment. Using it here (rather than Status) matches
        // the same reason the public read endpoints still filter publications by it.
        var isPublic = publication.Published;
        if (!isPublic)
        {
            var (user, membership, failure) = await AidxWorkflowSharedHelpers.ResolveActorAsync(context.User, db, cancellationToken);
            if (failure is not null)
            {
                return failure;
            }
            if (!AidxPermissions.IsPlatformAdmin(user!) && !AidxPermissions.CanEditAsOwner(membership, publication.CreatedByMembershipId))
            {
                return Results.Forbid();
            }
        }

        var stream = await storage.OpenReadAsync(publication.PdfKey, cancellationToken);
        if (stream is null)
        {
            return Results.NotFound();
        }

        var etag = ComputeETag($"{publication.Id}:{publication.Published}:{publication.PdfKey}");
        ApplyCacheHeaders(context, etag, isPublic);
        if (IfNoneMatchHits(context, etag))
        {
            stream.Dispose();
            return Results.StatusCode(StatusCodes.Status304NotModified);
        }

        // Content-Type is always server-controlled, never the client-supplied upload Content-Type —
        // the whole point of validating this really is a PDF. nosniff + a restrictive CSP stop a
        // browser from ever treating this response as anything executable.
        context.Response.Headers.XContentTypeOptions = "nosniff";
        context.Response.Headers.ContentSecurityPolicy = "default-src 'none'; sandbox;";
        return Results.Stream(stream, "application/pdf");
    }

    // ---- Shared serving/validation mechanics ------------------------------------------------

    private static async Task<(StepIn.Domain.Users.ApplicationUser? User, IResult? Failure)> RequireAdminAsync(
        ClaimsPrincipal principal, ApplicationDbContext db, CancellationToken cancellationToken)
    {
        var (user, _, failure) = await AidxWorkflowSharedHelpers.ResolveActorAsync(principal, db, cancellationToken);
        if (failure is not null)
        {
            return (null, failure);
        }

        if (!AidxPermissions.IsPlatformAdmin(user!))
        {
            return (null, Results.Forbid());
        }

        return (user, null);
    }

    private static async Task<IResult> ServeImageAsync(
        IAidxMediaStorage storage, string key, HttpContext context, string etagSeed, bool isPublic, CancellationToken cancellationToken)
    {
        var stream = await storage.OpenReadAsync(key, cancellationToken);
        if (stream is null)
        {
            return Results.NotFound();
        }

        var etag = ComputeETag(etagSeed);
        ApplyCacheHeaders(context, etag, isPublic);
        if (IfNoneMatchHits(context, etag))
        {
            stream.Dispose();
            return Results.StatusCode(StatusCodes.Status304NotModified);
        }

        context.Response.Headers.XContentTypeOptions = "nosniff";
        var contentType = Path.GetExtension(key).ToLowerInvariant() switch
        {
            ".png" => "image/png",
            ".webp" => "image/webp",
            _ => "image/jpeg",
        };
        return Results.Stream(stream, contentType);
    }

    /// <summary>
    /// Published content: <c>no-cache</c> with an ETag — allows a cheap conditional
    /// re-validation round trip, but the server still re-checks current status on every request
    /// (see each Get*Async handler above), so unpublishing takes effect immediately even against a
    /// browser or proxy holding a previously-cached validator. Draft/revision content: always
    /// <c>private, no-store</c> — never cached anywhere, proxy-only.
    /// </summary>
    private static void ApplyCacheHeaders(HttpContext context, string etag, bool isPublic)
    {
        context.Response.Headers.ETag = etag;
        context.Response.Headers.CacheControl = isPublic ? "no-cache" : "private, no-store";
    }

    private static bool IfNoneMatchHits(HttpContext context, string etag) =>
        context.Request.Headers.IfNoneMatch.Any(v => v == etag);

    private static string ComputeETag(string seed) =>
        $"\"{Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(seed)))[..32].ToLowerInvariant()}\"";

    /// <summary>
    /// Historical-reference protection: a storage key is deleted only once nothing — not another
    /// live row, not a historical snapshot (whose <c>ContentJson</c> is searched for the key), not
    /// an unmerged or discarded revision — still refers to it. Both merged and discarded revisions
    /// are kept forever (Milestone 2's design), so their media must survive exactly as long as they
    /// do.
    /// </summary>
    private static async Task DeleteIfUnreferencedAsync(
        ApplicationDbContext db, IAidxMediaStorage storage, AidxContentType contentType, string key,
        IQueryable<string?> liveKeys, CancellationToken cancellationToken)
    {
        var stillLive = await liveKeys.AnyAsync(k => k == key, cancellationToken);
        if (stillLive)
        {
            return;
        }

        var stillInHistory = await db.AidxContentSnapshots
            .Where(s => s.ContentType == contentType)
            .AnyAsync(s => EF.Functions.ILike(s.ContentJson, $"%{key}%"), cancellationToken);
        if (stillInHistory)
        {
            return;
        }

        await storage.DeleteAsync(key, cancellationToken);
    }
}

public sealed record AidxMediaUploadResponse(string StorageKey);
