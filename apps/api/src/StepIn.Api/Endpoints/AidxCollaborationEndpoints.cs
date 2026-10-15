using System.Security.Claims;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using StepIn.Api.Infrastructure;
using StepIn.Application.Common.Interfaces;
using StepIn.Domain.Aidx;
using StepIn.Domain.Common;
using StepIn.Domain.Users;
using StepIn.Infrastructure.Persistence;

namespace StepIn.Api.Endpoints;

/// <summary>
/// Public project-collaboration enquiries. The submit route is anonymous and rate-limited
/// (narrowly, via the "aidx-public-submission" policy — see Program.cs); the inbox is private,
/// visible only to the project's owner, an in-scope Editor, or LabManager/Admin.
/// </summary>
public static partial class AidxCollaborationEndpoints
{
    public static IEndpointRouteBuilder MapAidxCollaborationEndpoints(this IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.MapPost("/api/v1/aidx/projects/{id:guid}/enquiries", SubmitAsync)
            .WithTags("AIDX Collaboration")
            .RequireRateLimiting("aidx-public-submission")
            .WithName("SubmitAidxCollaborationEnquiry");

        app.MapGet("/api/v1/aidx/projects/{id:guid}/enquiries", ListAsync)
            .WithTags("AIDX Collaboration")
            .RequireAuthorization()
            .WithName("ListAidxCollaborationEnquiries");

        return app;
    }

    [GeneratedRegex(@"^[^@\s]+@[^@\s]+\.[^@\s]+$")]
    private static partial Regex EmailPattern();

    private static async Task<IResult> SubmitAsync(
        Guid id, SubmitCollaborationEnquiryRequest request, IApplicationDbContext db, CancellationToken cancellationToken)
    {
        var errors = new Dictionary<string, string[]>();
        var name = request.Name?.Trim() ?? "";
        var email = request.Email?.Trim().ToLowerInvariant() ?? "";
        var message = request.Message?.Trim() ?? "";

        if (string.IsNullOrWhiteSpace(name) || name.Length > 200)
        {
            AidxEndpointHelpers.AddError(errors, "name", "Name is required and must be 200 characters or fewer.");
        }

        if (string.IsNullOrWhiteSpace(email) || email.Length > 320 || !EmailPattern().IsMatch(email))
        {
            AidxEndpointHelpers.AddError(errors, "email", "A valid email address is required.");
        }

        if (string.IsNullOrWhiteSpace(message) || message.Length > 5000)
        {
            AidxEndpointHelpers.AddError(errors, "message", "Message is required and must be 5000 characters or fewer.");
        }

        if (errors.Count > 0)
        {
            return Results.ValidationProblem(errors);
        }

        // Unpublished/unknown project is 404, never revealing which is which — same convention
        // as every other public AIDX endpoint.
        var projectExists = await db.AidxProjects
            .AnyAsync(p => p.Id == id && p.Status == AidxContentStatus.Published && p.RevisionOfId == null, cancellationToken);
        if (!projectExists)
        {
            return Results.NotFound();
        }

        var enquiry = new AidxCollaborationEnquiry
        {
            ProjectId = id,
            Name = name,
            Email = email,
            Message = message,
        };
        db.Add(enquiry);
        await db.SaveChangesAsync(cancellationToken);

        // No real email delivery provider is configured for this feature — the enquiry is
        // persisted and visible in the project's private inbox; nothing claims an email was sent.
        return Results.Created((string?)null, new { id = enquiry.Id });
    }

    private static async Task<IResult> ListAsync(
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

        var project = await db.AidxProjects
            .Include(p => p.ResearchAreas)
            .FirstOrDefaultAsync(p => p.Id == id, cancellationToken);
        if (project is null)
        {
            return Results.NotFound();
        }

        var membership = await db.AidxMemberships.FirstOrDefaultAsync(m => m.UserId == user.Id, cancellationToken);
        var isOwner = AidxPermissions.CanEditAsOwner(membership, project.CreatedByMembershipId);
        var isReviewer = AidxPermissions.IsPlatformAdmin(user)
            || (membership is { Status: AidxMembershipStatus.Active } && membership.Role != AidxLabRole.Contributor
                && AidxPermissions.IsWithinEditorScope(membership, project.ResearchAreas.Select(r => r.ResearchAreaId)));

        if (!isOwner && !isReviewer)
        {
            return Results.Forbid();
        }

        var enquiries = await db.AidxCollaborationEnquiries
            .Where(e => e.ProjectId == id)
            .OrderByDescending(e => e.CreatedAt)
            .Select(e => new AidxCollaborationEnquiryResponse(e.Id, e.CreatedAt, e.Name, e.Email, e.Message, e.Status.ToString()))
            .ToListAsync(cancellationToken);

        return Results.Ok(enquiries);
    }
}
