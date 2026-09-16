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
        var user = await CurrentUserAsync(principal, db, cancellationToken);

        return user is null ? Results.Unauthorized() : Results.Ok(ToResponse(user, principal));
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

        var user = await CurrentUserAsync(principal, db, cancellationToken);

        if (user is null)
        {
            return Results.Unauthorized();
        }

        user.Role = role;
        await db.SaveChangesAsync(cancellationToken);

        return Results.Ok(new { redirectTo = DashboardPathForRole(role!.Value) });
    }

    private static Task<ApplicationUser?> CurrentUserAsync(ClaimsPrincipal principal, IApplicationDbContext db, CancellationToken cancellationToken)
    {
        var id = principal.GetAppUserId();
        return id is null
            ? Task.FromResult<ApplicationUser?>(null)
            : db.Users.FirstOrDefaultAsync(u => u.Id == id, cancellationToken);
    }

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
