using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using StepIn.Api.Infrastructure;
using StepIn.Application.Common.Interfaces;
using StepIn.Domain.Common;
using StepIn.Domain.Users;

namespace StepIn.Api.Endpoints;

/// <summary>
/// The only auth-adjacent surface ASP.NET Core still owns: reading the
/// app-level profile Clerk doesn't know about, and letting a user pick
/// Applicant/Recruiter once after their first sign-in. Credentials, sessions,
/// Google, email verification and password reset are all Clerk's.
/// </summary>
public static class AuthEndpoints
{
    public static IEndpointRouteBuilder MapAuthEndpoints(this IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        var group = app.MapGroup("/api/v1/auth").WithTags("Auth").RequireAuthorization();

        group.MapGet("/me", GetCurrentUserAsync)
            .WithName("GetCurrentUser")
            .WithSummary("The signed-in user's application profile and role.");

        group.MapPost("/account-setup", CompleteAccountSetupAsync)
            .WithName("CompleteAccountSetup")
            .WithSummary("Confirm the account's role after first sign-in (Applicant or Recruiter only).");

        return app;
    }

    private static async Task<IResult> GetCurrentUserAsync(
        ClaimsPrincipal principal,
        IApplicationDbContext db,
        CancellationToken cancellationToken)
    {
        var user = await principal.GetCurrentUserAsync(db, cancellationToken);

        if (user is null)
        {
            return Results.Unauthorized();
        }

        // A disabled account still holds a valid, authenticated identity — this is a 403, not a
        // 401 — but must never see its own (possibly stale) role or any other profile data back,
        // so the frontend can reliably tell "disabled" apart from "no role yet" from the status
        // code alone, rather than trusting a field in a 200 body that could be ignored.
        if (user.AccountStatus != AccountStatus.Active)
        {
            return Results.Problem(
                title: "This account is disabled.",
                detail: "Contact support if you believe this is a mistake.",
                statusCode: StatusCodes.Status403Forbidden);
        }

        return Results.Ok(ToResponse(user, principal));
    }

    private static async Task<IResult> CompleteAccountSetupAsync(
        AccountSetupRequest request,
        ClaimsPrincipal principal,
        IApplicationDbContext db,
        CancellationToken cancellationToken)
    {
        if (!TryNormalizeRole(request.Role, out var role))
        {
            return Results.ValidationProblem(new Dictionary<string, string[]> { ["role"] = ["Select Applicant or Recruiter."] });
        }

        var user = await principal.GetCurrentUserAsync(db, cancellationToken);

        if (user is null)
        {
            return Results.Unauthorized();
        }

        if (user.AccountStatus != AccountStatus.Active)
        {
            return Results.Problem(
                title: "This account is disabled.",
                detail: "Contact support if you believe this is a mistake.",
                statusCode: StatusCodes.Status403Forbidden);
        }

        // Account setup is documented as one-time: it never overwrites an existing role,
        // including never demoting an Admin (TryNormalizeRole already rejects "Admin" as input,
        // so the only way this endpoint could ever change an Admin's role is by overwriting it
        // with Applicant/Recruiter — this check is what actually prevents that). Repeating the
        // same role is treated as idempotent success, since that's indistinguishable from the
        // client simply retrying; any other existing role is a conflict, never a silent
        // overwrite.
        if (user.Role is not null)
        {
            return user.Role == role
                ? Results.Ok(new { redirectTo = DashboardPathForRole(role!.Value) })
                : AlreadySetUpConflict(user.Role.Value);
        }

        // Atomic and race-safe: the WHERE clause only ever matches a row that still has no role,
        // so of two concurrent first-time requests for the same brand-new user, at most one
        // UPDATE actually changes anything — the loser affects zero rows rather than overwriting
        // the winner's choice an instant later.
        var updated = await db.Users
            .Where(u => u.Id == user.Id && u.Role == null)
            .ExecuteUpdateAsync(s => s.SetProperty(u => u.Role, role), cancellationToken);

        if (updated == 0)
        {
            var currentRole = await db.Users.Where(u => u.Id == user.Id).Select(u => u.Role).SingleAsync(cancellationToken);
            return currentRole == role
                ? Results.Ok(new { redirectTo = DashboardPathForRole(role!.Value) })
                : AlreadySetUpConflict(currentRole!.Value);
        }

        return Results.Ok(new { redirectTo = DashboardPathForRole(role!.Value) });
    }

    private static IResult AlreadySetUpConflict(UserRole existingRole) => Results.Problem(
        title: "Account setup has already been completed.",
        detail: $"This account already has the '{existingRole}' role, which account setup cannot change.",
        statusCode: StatusCodes.Status409Conflict);

    private static CurrentUserResponse ToResponse(ApplicationUser user, ClaimsPrincipal principal) => new(
        user.Id,
        user.Email,
        user.FirstName,
        user.LastName,
        user.Role?.ToString(),
        string.Equals(principal.FindFirstValue("email_verified"), "true", StringComparison.OrdinalIgnoreCase),
        user.AccountStatus.ToString());

    private static string DashboardPathForRole(UserRole role) => role switch
    {
        UserRole.Recruiter => "/recruiter",
        UserRole.Admin => "/admin",
        _ => "/dashboard",
    };

    private static bool TryNormalizeRole(string? role, out UserRole? normalized)
    {
        normalized = role?.Trim() switch
        {
            var r when string.Equals(r, nameof(UserRole.Applicant), StringComparison.OrdinalIgnoreCase) => UserRole.Applicant,
            var r when string.Equals(r, nameof(UserRole.Recruiter), StringComparison.OrdinalIgnoreCase) => UserRole.Recruiter,
            _ => null,
        };

        return normalized is not null;
    }
}
