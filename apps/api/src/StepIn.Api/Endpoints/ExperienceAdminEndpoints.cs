using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using StepIn.Api.Infrastructure;
using StepIn.Application.Common.Interfaces;
using StepIn.Domain.Experience;

namespace StepIn.Api.Endpoints;

/// <summary>
/// Experience Passport, admin side: the verification queue and the review decisions that move a
/// record from Submitted through UnderReview to either Verified or Rejected. Every route requires
/// <c>RequireAdmin</c>, so an Applicant or Recruiter gets 403 and an anonymous caller gets 401 —
/// there is no separate Supervisor/Reviewer role, and nothing here ever auto-grants Admin.
///
/// An admin can never review a record belonging to their own candidate profile (if they happen to
/// have one) — <see cref="RejectsSelfReview"/> is checked before every transition below. Every
/// transition writes exactly one <see cref="ExperienceVerificationAudit"/> row recording who
/// decided, when, what the decision was, and the optional note, in the same
/// <c>SaveChangesAsync</c> call as the status change — see
/// <see cref="ExperienceEndpoints"/>'s type-level doc comment for why that call alone is enough
/// to keep the status and its audit row from ever diverging. A failed transition (wrong starting
/// status, or a self-review attempt) never reaches that call at all, so it can never leave a
/// misleading audit row behind.
/// </summary>
public static class ExperienceAdminEndpoints
{
    private const int MaxNoteLength = 1000;

    public static IEndpointRouteBuilder MapExperienceAdminEndpoints(this IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        var admin = app.MapGroup("/api/v1/admin/experience-passport")
            .WithTags("ExperiencePassport Admin")
            .RequireAuthorization("RequireAdmin");

        admin.MapGet("/queue", ListQueueAsync).WithName("ListExperienceQueue");
        admin.MapGet("/{id:guid}", GetRecordAsync).WithName("GetExperienceRecordAdmin");
        admin.MapPost("/{id:guid}/begin-review", BeginReviewAsync).WithName("BeginExperienceReview");
        admin.MapPost("/{id:guid}/approve", ApproveAsync).WithName("ApproveExperienceRecord");
        admin.MapPost("/{id:guid}/reject", RejectAsync).WithName("RejectExperienceRecord");

        return app;
    }

    private static async Task<IResult> ListQueueAsync(string? status, IApplicationDbContext db, CancellationToken cancellationToken)
    {
        IReadOnlyList<ExperienceVerificationStatus> statuses;
        if (string.IsNullOrWhiteSpace(status))
        {
            // Default view: everything actually waiting on a reviewer.
            statuses = [ExperienceVerificationStatus.Submitted, ExperienceVerificationStatus.UnderReview];
        }
        else if (Enum.TryParse<ExperienceVerificationStatus>(status, true, out var parsed))
        {
            statuses = [parsed];
        }
        else
        {
            return Results.ValidationProblem(new Dictionary<string, string[]> { ["status"] = ["Unknown status."] });
        }

        var query =
            from r in db.ExperienceRecords.AsNoTracking()
            join c in db.CandidateProfiles on r.CandidateProfileId equals c.Id
            join u in db.Users on c.UserId equals u.Id
            where statuses.Contains(r.Status)
            orderby r.SubmittedAt
            select new ExperienceQueueItemResponse(
                r.Id, r.RecordType.ToString(), r.Title, r.Organization, u.FirstName + " " + u.LastName, r.Status.ToString(), r.SubmittedAt, r.CreatedAt);

        return Results.Ok(await query.ToListAsync(cancellationToken));
    }

    private static async Task<IResult> GetRecordAsync(Guid id, IApplicationDbContext db, CancellationToken cancellationToken)
    {
        var record = await db.ExperienceRecords.AsNoTracking().FirstOrDefaultAsync(r => r.Id == id, cancellationToken);
        if (record is null)
        {
            return Results.NotFound();
        }

        var response = await ToAdminResponseAsync(db, record, cancellationToken);
        return response is null ? Results.NotFound() : Results.Ok(response);
    }

    private static async Task<IResult> BeginReviewAsync(Guid id, ClaimsPrincipal principal, IApplicationDbContext db, CancellationToken cancellationToken) =>
        await TransitionAsync(
            id, principal, db,
            allowedFrom: [ExperienceVerificationStatus.Submitted],
            to: ExperienceVerificationStatus.UnderReview,
            action: ExperienceAuditAction.ReviewStarted,
            note: null,
            invalidMessage: "Only a submitted record can enter review.",
            cancellationToken);

    private static async Task<IResult> ApproveAsync(
        Guid id, ExperienceReviewDecisionRequest? request, ClaimsPrincipal principal, IApplicationDbContext db, CancellationToken cancellationToken) =>
        await TransitionAsync(
            id, principal, db,
            allowedFrom: [ExperienceVerificationStatus.UnderReview],
            to: ExperienceVerificationStatus.Verified,
            action: ExperienceAuditAction.Approved,
            note: request?.Note,
            invalidMessage: "Only a record under review can be approved.",
            cancellationToken);

    private static async Task<IResult> RejectAsync(
        Guid id, ExperienceReviewDecisionRequest? request, ClaimsPrincipal principal, IApplicationDbContext db, CancellationToken cancellationToken) =>
        await TransitionAsync(
            id, principal, db,
            allowedFrom: [ExperienceVerificationStatus.UnderReview],
            to: ExperienceVerificationStatus.Rejected,
            action: ExperienceAuditAction.Rejected,
            note: request?.Note,
            invalidMessage: "Only a record under review can be rejected.",
            cancellationToken);

    private static async Task<IResult> TransitionAsync(
        Guid id,
        ClaimsPrincipal principal,
        IApplicationDbContext db,
        IReadOnlyList<ExperienceVerificationStatus> allowedFrom,
        ExperienceVerificationStatus to,
        ExperienceAuditAction action,
        string? note,
        string invalidMessage,
        CancellationToken cancellationToken)
    {
        var admin = await principal.GetCurrentUserAsync(db, cancellationToken);
        if (admin is null)
        {
            return Results.Unauthorized();
        }

        var record = await db.ExperienceRecords.FirstOrDefaultAsync(r => r.Id == id, cancellationToken);
        if (record is null)
        {
            return Results.NotFound();
        }

        if (await RejectsSelfReview(db, record.CandidateProfileId, admin.Id, cancellationToken))
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["record"] = ["An administrator cannot review their own submitted record."],
            });
        }

        if (!allowedFrom.Contains(record.Status))
        {
            return Results.ValidationProblem(new Dictionary<string, string[]> { ["status"] = [invalidMessage] });
        }

        if (note is { Length: > MaxNoteLength })
        {
            return Results.ValidationProblem(new Dictionary<string, string[]> { ["note"] = [$"Must be {MaxNoteLength} characters or fewer."] });
        }

        var previousStatus = record.Status;
        record.Status = to;

        db.Add(new ExperienceVerificationAudit
        {
            ExperienceRecordId = record.Id,
            ActorUserId = admin.Id,
            ActorRole = admin.Role?.ToString() ?? "Admin",
            Action = action,
            PreviousStatus = previousStatus,
            NewStatus = to,
            Note = string.IsNullOrWhiteSpace(note) ? null : note.Trim(),
        });

        await db.SaveChangesAsync(cancellationToken);

        var response = await ToAdminResponseAsync(db, record, cancellationToken);
        return response is null ? Results.NotFound() : Results.Ok(response);
    }

    /// <summary>True when the record belongs to the acting admin's own candidate profile, if
    /// they have one. Most admins never will — this only guards the edge case.</summary>
    private static async Task<bool> RejectsSelfReview(IApplicationDbContext db, Guid recordCandidateProfileId, Guid actingUserId, CancellationToken cancellationToken)
    {
        var ownCandidateProfile = await db.CandidateProfiles.FirstOrDefaultAsync(c => c.UserId == actingUserId, cancellationToken);
        return ownCandidateProfile is not null && ownCandidateProfile.Id == recordCandidateProfileId;
    }

    private static async Task<ExperienceAdminRecordResponse?> ToAdminResponseAsync(
        IApplicationDbContext db, ExperienceRecord record, CancellationToken cancellationToken)
    {
        var applicant = await db.CandidateProfiles
            .Where(c => c.Id == record.CandidateProfileId)
            .Join(db.Users, c => c.UserId, u => u.Id, (c, u) => new { u.FirstName, u.LastName, u.Email })
            .FirstOrDefaultAsync(cancellationToken);
        if (applicant is null)
        {
            return null;
        }

        var auditHistory = await db.ExperienceVerificationAudits
            .AsNoTracking()
            .Where(a => a.ExperienceRecordId == record.Id)
            .Join(db.Users, a => a.ActorUserId, u => u.Id, (a, u) => new { Audit = a, u.FirstName, u.LastName })
            .OrderBy(x => x.Audit.CreatedAt)
            .Select(x => new ExperienceAuditEntryResponse(
                x.Audit.Action.ToString(), x.Audit.PreviousStatus.ToString(), x.Audit.NewStatus.ToString(),
                x.FirstName + " " + x.LastName, x.Audit.ActorRole, x.Audit.Note, x.Audit.CreatedAt))
            .ToListAsync(cancellationToken);

        return new ExperienceAdminRecordResponse(
            record.Id,
            record.RecordType.ToString(),
            record.Title,
            record.Organization,
            record.Description,
            record.StartDate,
            record.EndDate,
            record.Skills,
            record.EvidenceUrl,
            record.Visibility.ToString(),
            record.Status.ToString(),
            $"{applicant.FirstName} {applicant.LastName}",
            applicant.Email,
            record.SubmittedAt,
            record.CreatedAt,
            record.UpdatedAt,
            auditHistory);
    }
}
