using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using StepIn.Api.Infrastructure;
using StepIn.Domain.Aidx;
using StepIn.Domain.Common;
using StepIn.Domain.Users;
using StepIn.Infrastructure.Persistence;

namespace StepIn.Api.Endpoints;

/// <summary>
/// Event registration, signed-in or anonymous. Capacity (when set) is enforced atomically under
/// a transaction-scoped advisory lock keyed to the event — a request only ever reports "the
/// event is full" after it actually holds the lock and counts current registrations, never
/// confusing lock contention with capacity exhaustion (the lock is blocking, not try-and-fail).
/// Anonymous cancellation/reactivation requires possessing the original cancellation token's
/// plaintext (returned once, hashed at rest, compared in fixed time) — never merely an email,
/// which is guessable. A new registration attempt against an existing Registered row is always a
/// plain 409 with no token re-issued, closing the "re-mint a credential by supplying an email"
/// gap.
/// </summary>
public static class AidxEventRegistrationEndpoints
{
    public static IEndpointRouteBuilder MapAidxEventRegistrationEndpoints(this IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        var group = app.MapGroup("/api/v1/aidx/events/{id:guid}/registrations").WithTags("AIDX Events");

        group.MapPost("/", RegisterAsync)
            .RequireRateLimiting("aidx-public-submission")
            .WithName("RegisterForAidxEvent");

        group.MapPost("/reactivate", ReactivateAsync)
            .RequireRateLimiting("aidx-public-submission")
            .WithName("ReactivateAidxEventRegistration");

        group.MapPost("/cancel", CancelAsync).WithName("CancelAidxEventRegistration");

        group.MapGet("/summary", SummaryAsync).WithName("AidxEventRegistrationSummary");

        group.MapGet("/mine", MineAsync).RequireAuthorization().WithName("MyAidxEventRegistration");

        group.MapGet("/", ListForManagersAsync).RequireAuthorization().WithName("ListAidxEventRegistrations");

        return app;
    }

    private const string AdvisoryLockPrefix = "aidx_event_registration_";

    private static string GenerateToken() => Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))
        .Replace('+', '-').Replace('/', '_').TrimEnd('=');

    private static string HashToken(string token) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));

    private static bool TokenMatches(string? providedToken, string? storedHash)
    {
        if (string.IsNullOrEmpty(providedToken) || string.IsNullOrEmpty(storedHash))
        {
            return false;
        }

        var providedHash = Encoding.UTF8.GetBytes(HashToken(providedToken));
        var stored = Encoding.UTF8.GetBytes(storedHash);
        return providedHash.Length == stored.Length && CryptographicOperations.FixedTimeEquals(providedHash, stored);
    }

    /// <summary>The one place capacity is actually enforced: blocking (not try-lock) so contention
    /// is never confused with "full," and the count is read only after the lock is held.</summary>
    private static async Task<IResult> WithCapacityLockAsync(
        ApplicationDbContext db, Guid eventId, Func<Task<IResult>> action, CancellationToken cancellationToken)
    {
        var strategy = db.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
            await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
            await db.Database.ExecuteSqlInterpolatedAsync(
                $"SELECT pg_advisory_xact_lock(hashtext({AdvisoryLockPrefix + eventId}))", cancellationToken);

            var result = await action();
            await transaction.CommitAsync(cancellationToken);
            return result;
        });
    }

    private static async Task<bool> HasRoomAsync(ApplicationDbContext db, AidxEvent evt, CancellationToken cancellationToken)
    {
        if (evt.Capacity is not { } capacity)
        {
            return true;
        }

        var currentCount = await db.AidxEventRegistrations
            .CountAsync(r => r.EventId == evt.Id && r.Status == AidxEventRegistrationStatus.Registered, cancellationToken);
        return currentCount < capacity;
    }

    private static async Task<IResult> RegisterAsync(
        Guid id, RegisterForAidxEventRequest request, ClaimsPrincipal principal,
        ApplicationDbContext db, CancellationToken cancellationToken)
    {
        var evt = await db.AidxEvents.FirstOrDefaultAsync(e => e.Id == id && e.Status == AidxContentStatus.Published, cancellationToken);
        if (evt is null)
        {
            return Results.NotFound();
        }

        var user = await principal.GetCurrentUserAsync(db, cancellationToken);
        if (user is not null && user.AccountStatus != AccountStatus.Active)
        {
            return Results.Forbid();
        }

        string name;
        string email;
        if (user is not null)
        {
            name = $"{user.FirstName} {user.LastName}".Trim();
            email = user.Email.Trim().ToLowerInvariant();
        }
        else
        {
            name = request.Name?.Trim() ?? "";
            email = request.Email?.Trim().ToLowerInvariant() ?? "";
            if (string.IsNullOrWhiteSpace(name) || name.Length > 200 || string.IsNullOrWhiteSpace(email) || email.Length > 320)
            {
                return Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    ["name"] = ["Name is required."],
                    ["email"] = ["A valid email is required."],
                });
            }
        }

        return await WithCapacityLockAsync(db, id, async () =>
        {
            var existing = user is not null
                ? await db.AidxEventRegistrations.FirstOrDefaultAsync(r => r.EventId == id && r.UserId == user.Id, cancellationToken)
                : await db.AidxEventRegistrations.FirstOrDefaultAsync(r => r.EventId == id && r.UserId == null && r.Email == email, cancellationToken);

            if (existing is { Status: AidxEventRegistrationStatus.Registered })
            {
                // No token re-issued for an already-registered identity — only the original
                // credential (or signed-in ownership) can act on that row again.
                return Results.Problem(title: "You're already registered for this event.", statusCode: StatusCodes.Status409Conflict);
            }

            if (existing is { Status: AidxEventRegistrationStatus.Cancelled } && user is null)
            {
                // Anonymous reactivation requires the original token — a bare re-POST with just
                // an email must not silently undo a cancellation.
                return Results.Problem(
                    title: "You previously cancelled. Use your original cancellation link's reactivate action.",
                    statusCode: StatusCodes.Status409Conflict);
            }

            if (!await HasRoomAsync(db, evt, cancellationToken))
            {
                return Results.Problem(title: "This event is full.", statusCode: StatusCodes.Status409Conflict);
            }

            if (existing is { Status: AidxEventRegistrationStatus.Cancelled } && user is not null)
            {
                // Signed-in ownership is itself the credential — no token needed to reactivate.
                existing.Status = AidxEventRegistrationStatus.Registered;
                await db.SaveChangesAsync(cancellationToken);
                return Results.Ok(new AidxEventRegistrationResponse(existing.Id, existing.Status.ToString(), null));
            }

            var registration = new AidxEventRegistration
            {
                EventId = id,
                UserId = user?.Id,
                Name = name,
                Email = email,
            };

            string? rawToken = null;
            if (user is null)
            {
                rawToken = GenerateToken();
                registration.CancellationTokenHash = HashToken(rawToken);
            }

            db.AidxEventRegistrations.Add(registration);

            try
            {
                await db.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateException)
            {
                return Results.Problem(title: "You're already registered for this event.", statusCode: StatusCodes.Status409Conflict);
            }

            return Results.Created((string?)null, new AidxEventRegistrationResponse(registration.Id, registration.Status.ToString(), rawToken));
        }, cancellationToken);
    }

    private static async Task<IResult> ReactivateAsync(
        Guid id, CancelAidxEventRegistrationRequest request, ApplicationDbContext db, CancellationToken cancellationToken)
    {
        var evt = await db.AidxEvents.FirstOrDefaultAsync(e => e.Id == id, cancellationToken);
        if (evt is null)
        {
            return Results.NotFound();
        }

        var registration = await db.AidxEventRegistrations
            .FirstOrDefaultAsync(r => r.EventId == id && r.UserId == null && r.Status == AidxEventRegistrationStatus.Cancelled, cancellationToken);

        if (registration is null || !TokenMatches(request.CancellationToken, registration.CancellationTokenHash))
        {
            return Results.NotFound();
        }

        return await WithCapacityLockAsync(db, id, async () =>
        {
            if (!await HasRoomAsync(db, evt, cancellationToken))
            {
                return Results.Problem(title: "This event is full.", statusCode: StatusCodes.Status409Conflict);
            }

            registration.Status = AidxEventRegistrationStatus.Registered;
            await db.SaveChangesAsync(cancellationToken);
            return Results.Ok(new AidxEventRegistrationResponse(registration.Id, registration.Status.ToString(), null));
        }, cancellationToken);
    }

    private static async Task<IResult> CancelAsync(
        Guid id, CancelAidxEventRegistrationRequest request, ClaimsPrincipal principal,
        ApplicationDbContext db, CancellationToken cancellationToken)
    {
        var user = await principal.GetCurrentUserAsync(db, cancellationToken);

        AidxEventRegistration? registration;
        if (user is not null)
        {
            registration = await db.AidxEventRegistrations.FirstOrDefaultAsync(r => r.EventId == id && r.UserId == user.Id, cancellationToken);
        }
        else
        {
            var candidates = await db.AidxEventRegistrations
                .Where(r => r.EventId == id && r.UserId == null && r.Status == AidxEventRegistrationStatus.Registered)
                .ToListAsync(cancellationToken);
            registration = candidates.FirstOrDefault(r => TokenMatches(request.CancellationToken, r.CancellationTokenHash));
        }

        if (registration is null)
        {
            return Results.NotFound();
        }

        registration.Status = AidxEventRegistrationStatus.Cancelled;
        await db.SaveChangesAsync(cancellationToken);
        return Results.Ok();
    }

    private static async Task<IResult> SummaryAsync(Guid id, ApplicationDbContext db, CancellationToken cancellationToken)
    {
        var evt = await db.AidxEvents.FirstOrDefaultAsync(e => e.Id == id && e.Status == AidxContentStatus.Published, cancellationToken);
        if (evt is null)
        {
            return Results.NotFound();
        }

        var count = await db.AidxEventRegistrations
            .CountAsync(r => r.EventId == id && r.Status == AidxEventRegistrationStatus.Registered, cancellationToken);

        return Results.Ok(new AidxEventRegistrationSummaryResponse(count, evt.Capacity, evt.Capacity is { } cap && count >= cap));
    }

    private static async Task<IResult> MineAsync(
        Guid id, ClaimsPrincipal principal, ApplicationDbContext db, CancellationToken cancellationToken)
    {
        var user = await principal.GetCurrentUserAsync(db, cancellationToken);
        if (user is null)
        {
            return Results.Unauthorized();
        }

        var registration = await db.AidxEventRegistrations
            .FirstOrDefaultAsync(r => r.EventId == id && r.UserId == user.Id, cancellationToken);

        return Results.Ok(new { registered = registration is { Status: AidxEventRegistrationStatus.Registered } });
    }

    private static async Task<IResult> ListForManagersAsync(
        Guid id, ClaimsPrincipal principal, ApplicationDbContext db, CancellationToken cancellationToken)
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

        var evt = await db.AidxEvents.FirstOrDefaultAsync(e => e.Id == id, cancellationToken);
        if (evt is null)
        {
            return Results.NotFound();
        }

        var membership = await db.AidxMemberships.FirstOrDefaultAsync(m => m.UserId == user.Id, cancellationToken);
        var isOwner = AidxPermissions.CanEditAsOwner(membership, evt.CreatedByMembershipId);
        // Events have no research-area association — only a lab-wide (unscoped) Editor/LabManager/Admin
        // may manage registrations for one, same zero-scoped-rights rule as the editorial workflow.
        var isLabWideManager = AidxPermissions.IsPlatformAdmin(user)
            || (membership is { Status: AidxMembershipStatus.Active, ScopedResearchAreaId: null } && membership.Role != AidxLabRole.Contributor);

        if (!isOwner && !isLabWideManager)
        {
            return Results.Forbid();
        }

        var registrations = await db.AidxEventRegistrations
            .Where(r => r.EventId == id)
            .OrderByDescending(r => r.CreatedAt)
            .Select(r => new AidxEventRegistrationAdminResponse(r.Id, r.CreatedAt, r.Name, r.Email, r.Status.ToString(), r.UserId != null))
            .ToListAsync(cancellationToken);

        return Results.Ok(registrations);
    }
}
