using System.Security.Claims;

namespace StepIn.Api.Infrastructure;

public static class ClaimsPrincipalExtensions
{
    /// <summary>The internal <c>ApplicationUser.Id</c> added by <see cref="ClerkUserSyncClaimsTransformation"/>.</summary>
    public static Guid? GetAppUserId(this ClaimsPrincipal principal) =>
        Guid.TryParse(principal.FindFirstValue(ClerkUserSyncClaimsTransformation.AppUserIdClaimType), out var id) ? id : null;
}
