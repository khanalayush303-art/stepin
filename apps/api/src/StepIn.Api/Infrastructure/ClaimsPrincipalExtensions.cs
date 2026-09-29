using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using StepIn.Application.Common.Interfaces;
using StepIn.Domain.Users;

namespace StepIn.Api.Infrastructure;

public static class ClaimsPrincipalExtensions
{
    /// <summary>The internal <c>ApplicationUser.Id</c> added by <see cref="ClerkUserSyncClaimsTransformation"/>.</summary>
    public static Guid? GetAppUserId(this ClaimsPrincipal principal) =>
        Guid.TryParse(principal.FindFirstValue(ClerkUserSyncClaimsTransformation.AppUserIdClaimType), out var id) ? id : null;

    /// <summary>
    /// Resolves the authenticated principal to its <see cref="ApplicationUser"/> row.
    /// The single place every endpoint determines "who is this" — never from a
    /// client-supplied id in the request body, route or query string.
    /// </summary>
    public static Task<ApplicationUser?> GetCurrentUserAsync(
        this ClaimsPrincipal principal,
        IApplicationDbContext db,
        CancellationToken cancellationToken = default)
    {
        var id = principal.GetAppUserId();
        return id is null
            ? Task.FromResult<ApplicationUser?>(null)
            : db.Users.FirstOrDefaultAsync(u => u.Id == id, cancellationToken);
    }
}
