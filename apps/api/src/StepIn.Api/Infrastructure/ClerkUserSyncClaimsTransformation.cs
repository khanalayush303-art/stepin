using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.EntityFrameworkCore;
using StepIn.Domain.Aidx;
using StepIn.Domain.Users;
using StepIn.Infrastructure.Persistence;

namespace StepIn.Api.Infrastructure;

/// <summary>
/// Runs once per authenticated request, before authorization policies evaluate
/// (the standard hook for this: <see cref="IClaimsTransformation"/> executes
/// right after authentication succeeds). Finds-or-creates the
/// <see cref="ApplicationUser"/> for the validated Clerk identity, idempotently,
/// then adds an internal user-id claim and — once set — a role claim, so
/// downstream code and role policies never re-derive "who is this" themselves.
/// </summary>
public sealed class ClerkUserSyncClaimsTransformation(ApplicationDbContext db) : IClaimsTransformation
{
    public const string AppUserIdClaimType = "stepin:app_user_id";

    public async Task<ClaimsPrincipal> TransformAsync(ClaimsPrincipal principal)
    {
        ArgumentNullException.ThrowIfNull(principal);

        // IClaimsTransformation can run more than once per request; make repeats a no-op.
        if (principal.Identity is not { IsAuthenticated: true } || principal.HasClaim(c => c.Type == AppUserIdClaimType))
        {
            return principal;
        }

        var clerkUserId = principal.FindFirstValue(ClaimTypes.NameIdentifier) ?? principal.FindFirstValue("sub");

        if (string.IsNullOrEmpty(clerkUserId))
        {
            return principal;
        }

        // The AIDX system owner is an ownership record, not a person. A subject from that
        // namespace is never synced into an app-user or role claim, so it cannot authenticate.
        if (AidxSystemIdentity.IsSystemClerkUserId(clerkUserId))
        {
            return principal;
        }

        var user = await FindOrCreateAsync(clerkUserId, principal);

        var identity = (ClaimsIdentity)principal.Identity;
        identity.AddClaim(new Claim(AppUserIdClaimType, user.Id.ToString()));

        if (user.Role is not null)
        {
            identity.AddClaim(new Claim(ClaimTypes.Role, user.Role.Value.ToString()));
        }

        return principal;
    }

    private async Task<ApplicationUser> FindOrCreateAsync(string clerkUserId, ClaimsPrincipal principal)
    {
        var existing = await db.Users.FirstOrDefaultAsync(u => u.ClerkUserId == clerkUserId);

        if (existing is not null)
        {
            await SyncProfileClaimsAsync(existing, principal);
            return existing;
        }

        var user = new ApplicationUser
        {
            ClerkUserId = clerkUserId,
            Email = principal.FindFirstValue(ClaimTypes.Email) ?? principal.FindFirstValue("email") ?? "",
            FirstName = principal.FindFirstValue(ClaimTypes.GivenName) ?? principal.FindFirstValue("given_name") ?? "New",
            LastName = principal.FindFirstValue(ClaimTypes.Surname) ?? principal.FindFirstValue("family_name") ?? "User",
        };

        db.Users.Add(user);

        try
        {
            await db.SaveChangesAsync();
            return user;
        }
        catch (DbUpdateException)
        {
            // Two near-simultaneous first requests for the same new user raced;
            // the loser just reads what the winner already committed.
            db.ChangeTracker.Clear();
            return await db.Users.FirstAsync(u => u.ClerkUserId == clerkUserId);
        }
    }

    /// <summary>
    /// Clerk's default session token carries none of these claims at all (see
    /// the README's Authentication section for the session-token template that
    /// adds them) — until that's configured, every new user syncs with
    /// <c>FirstName = "New"</c>, <c>LastName = "User"</c>, <c>Email = ""</c>.
    /// Re-checking on every request, not just at creation, means a row synced
    /// before the template existed self-heals on the user's very next request
    /// once it's added, with no manual data fix. Never blanks a good stored
    /// value with a missing claim, and is a no-op (no extra write) once values
    /// match.
    /// </summary>
    private async Task SyncProfileClaimsAsync(ApplicationUser user, ClaimsPrincipal principal)
    {
        var email = principal.FindFirstValue(ClaimTypes.Email) ?? principal.FindFirstValue("email");
        var firstName = principal.FindFirstValue(ClaimTypes.GivenName) ?? principal.FindFirstValue("given_name");
        var lastName = principal.FindFirstValue(ClaimTypes.Surname) ?? principal.FindFirstValue("family_name");

        var changed = false;

        if (!string.IsNullOrWhiteSpace(email) && email != user.Email)
        {
            user.Email = email;
            changed = true;
        }

        if (!string.IsNullOrWhiteSpace(firstName) && firstName != user.FirstName)
        {
            user.FirstName = firstName;
            changed = true;
        }

        if (!string.IsNullOrWhiteSpace(lastName) && lastName != user.LastName)
        {
            user.LastName = lastName;
            changed = true;
        }

        if (changed)
        {
            await db.SaveChangesAsync();
        }
    }
}
