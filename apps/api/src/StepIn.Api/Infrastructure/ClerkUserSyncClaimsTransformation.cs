using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.EntityFrameworkCore;
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
}
