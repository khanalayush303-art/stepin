using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using StepIn.Api.Infrastructure;
using StepIn.Application.Common.Interfaces;
using StepIn.Domain.Experience;
using StepIn.Domain.Profiles;

namespace StepIn.Api.Endpoints;

/// <summary>
/// Experience Passport, candidate side: a candidate's own experience records and the explicit
/// transitions that move them through <see cref="ExperienceVerificationStatus"/>. Ownership is
/// always resolved server-side from the authenticated principal's own
/// <see cref="CandidateProfile"/> — never from a client-supplied id — and every single-item query
/// scopes by both the route id and that profile id in the same WHERE clause, so another
/// candidate's record is indistinguishable from one that doesn't exist (404, never 403).
///
/// See <see cref="StepIn.Domain.Experience.ExperienceVerificationStatus"/> for the full
/// transition table. Every transition here writes exactly one
/// <see cref="ExperienceVerificationAudit"/> row in the same <c>SaveChangesAsync</c> call as the
/// status change — EF Core wraps a single <c>SaveChangesAsync</c> call's pending changes in one
/// database transaction by default, so the status update and its audit row can never diverge:
/// either both are committed or neither is. Admin-side review operations live in
/// <see cref="ExperienceAdminEndpoints"/>; nothing here ever reaches Verified, Rejected, or
/// UnderReview.
/// </summary>
public static class ExperienceEndpoints
{
    private const int MaxTitleLength = 200;
    private const int MaxOrganizationLength = 200;
    private const int MaxDescriptionLength = 5000;
    private const int MaxUrlLength = 500;
    private const int MaxSkillLength = 100;
    private const int MaxSkills = 50;
    private const int MaxNoteLength = 1000;

    public static IEndpointRouteBuilder MapExperienceEndpoints(this IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        var records = app.MapGroup("/api/v1/experience-passport")
            .WithTags("ExperiencePassport")
            .RequireAuthorization("RequireApplicant");

        records.MapGet("/", ListRecordsAsync).WithName("ListExperienceRecords");
        records.MapPost("/", CreateRecordAsync).WithName("CreateExperienceRecord");
        records.MapGet("/{id:guid}", GetRecordAsync).WithName("GetExperienceRecord");
        records.MapPut("/{id:guid}", UpdateRecordAsync).WithName("UpdateExperienceRecord");
        records.MapPost("/{id:guid}/submit", SubmitRecordAsync).WithName("SubmitExperienceRecord");
        records.MapPost("/{id:guid}/resubmit", ResubmitRecordAsync).WithName("ResubmitExperienceRecord");
        records.MapPost("/{id:guid}/withdraw", WithdrawRecordAsync).WithName("WithdrawExperienceRecord");
        records.MapDelete("/{id:guid}", DeleteRecordAsync).WithName("DeleteExperienceRecord");

        return app;
    }

    private static async Task<IResult> ListRecordsAsync(ClaimsPrincipal principal, IApplicationDbContext db, CancellationToken cancellationToken)
    {
        var user = await principal.GetCurrentUserAsync(db, cancellationToken);
        if (user is null)
        {
            return Results.Unauthorized();
        }

        var profile = await FindCandidateProfileAsync(db, user.Id, cancellationToken);
        if (profile is null)
        {
            return Results.Ok(Array.Empty<ExperienceRecordSummaryResponse>());
        }

        var records = await db.ExperienceRecords
            .AsNoTracking()
            .Where(r => r.CandidateProfileId == profile.Id)
            .OrderByDescending(r => r.CreatedAt)
            .Select(r => new ExperienceRecordSummaryResponse(
                r.Id, r.RecordType.ToString(), r.Title, r.Organization, r.Visibility.ToString(), r.Status.ToString(),
                r.StartDate, r.EndDate, r.CreatedAt, r.UpdatedAt))
            .ToListAsync(cancellationToken);

        return Results.Ok(records);
    }

    private static async Task<IResult> CreateRecordAsync(
        ExperienceRecordInput request, ClaimsPrincipal principal, IApplicationDbContext db, CancellationToken cancellationToken)
    {
        var user = await principal.GetCurrentUserAsync(db, cancellationToken);
        if (user is null)
        {
            return Results.Unauthorized();
        }

        var profile = await FindCandidateProfileAsync(db, user.Id, cancellationToken);
        if (profile is null)
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["profile"] = ["Complete your candidate profile before adding an experience record."],
            });
        }

        var errors = ValidateFields(request, out var recordType, out var visibility);
        if (errors.Count > 0)
        {
            return Results.ValidationProblem(errors);
        }

        var record = new ExperienceRecord
        {
            CandidateProfileId = profile.Id,
            RecordType = recordType,
            Title = request.Title.Trim(),
            Organization = Trim(request.Organization),
            Description = Trim(request.Description),
            StartDate = request.StartDate,
            EndDate = request.EndDate,
            Skills = NormalizeSkills(request.Skills),
            EvidenceUrl = Trim(request.EvidenceUrl),
            Visibility = visibility,
        };

        db.Add(record);
        await db.SaveChangesAsync(cancellationToken);

        return Results.Created($"/api/v1/experience-passport/{record.Id}", await ToResponseAsync(db, record, cancellationToken));
    }

    private static async Task<IResult> GetRecordAsync(Guid id, ClaimsPrincipal principal, IApplicationDbContext db, CancellationToken cancellationToken)
    {
        var user = await principal.GetCurrentUserAsync(db, cancellationToken);
        if (user is null)
        {
            return Results.Unauthorized();
        }

        var profile = await FindCandidateProfileAsync(db, user.Id, cancellationToken);
        if (profile is null)
        {
            return Results.NotFound();
        }

        var record = await FindOwnedRecordAsync(db, profile.Id, id, cancellationToken);
        return record is null ? Results.NotFound() : Results.Ok(await ToResponseAsync(db, record, cancellationToken));
    }

    private static async Task<IResult> UpdateRecordAsync(
        Guid id, ExperienceRecordInput request, ClaimsPrincipal principal, IApplicationDbContext db, CancellationToken cancellationToken)
    {
        var user = await principal.GetCurrentUserAsync(db, cancellationToken);
        if (user is null)
        {
            return Results.Unauthorized();
        }

        var profile = await FindCandidateProfileAsync(db, user.Id, cancellationToken);
        if (profile is null)
        {
            return Results.NotFound();
        }

        var record = await FindOwnedRecordAsync(db, profile.Id, id, cancellationToken);
        if (record is null)
        {
            return Results.NotFound();
        }

        if (!CanEdit(record.Status))
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["status"] = ["This record can't be edited while it's submitted, under review, or verified."],
            });
        }

        var errors = ValidateFields(request, out var recordType, out var visibility);
        if (errors.Count > 0)
        {
            return Results.ValidationProblem(errors);
        }

        record.RecordType = recordType;
        record.Title = request.Title.Trim();
        record.Organization = Trim(request.Organization);
        record.Description = Trim(request.Description);
        record.StartDate = request.StartDate;
        record.EndDate = request.EndDate;
        record.Skills = NormalizeSkills(request.Skills);
        record.EvidenceUrl = Trim(request.EvidenceUrl);
        record.Visibility = visibility;

        await db.SaveChangesAsync(cancellationToken);

        return Results.Ok(await ToResponseAsync(db, record, cancellationToken));
    }

    private static Task<IResult> SubmitRecordAsync(
        Guid id, ExperienceTransitionRequest? request, ClaimsPrincipal principal, IApplicationDbContext db, CancellationToken cancellationToken) =>
        TransitionAsync(
            id, principal, db,
            allowedFrom: [ExperienceVerificationStatus.Draft],
            to: ExperienceVerificationStatus.Submitted,
            action: ExperienceAuditAction.Submitted,
            note: request?.Note,
            invalidMessage: "Only a draft record can be submitted.",
            cancellationToken);

    private static Task<IResult> ResubmitRecordAsync(
        Guid id, ExperienceTransitionRequest? request, ClaimsPrincipal principal, IApplicationDbContext db, CancellationToken cancellationToken) =>
        TransitionAsync(
            id, principal, db,
            allowedFrom: [ExperienceVerificationStatus.Rejected, ExperienceVerificationStatus.Withdrawn],
            to: ExperienceVerificationStatus.Submitted,
            action: ExperienceAuditAction.Resubmitted,
            note: request?.Note,
            invalidMessage: "Only a rejected or withdrawn record can be resubmitted.",
            cancellationToken);

    private static Task<IResult> WithdrawRecordAsync(
        Guid id, ExperienceTransitionRequest? request, ClaimsPrincipal principal, IApplicationDbContext db, CancellationToken cancellationToken) =>
        TransitionAsync(
            id, principal, db,
            allowedFrom: [ExperienceVerificationStatus.Submitted, ExperienceVerificationStatus.UnderReview, ExperienceVerificationStatus.Verified],
            to: ExperienceVerificationStatus.Withdrawn,
            action: ExperienceAuditAction.Withdrawn,
            note: request?.Note,
            invalidMessage: "Only a submitted, under-review, or verified record can be withdrawn.",
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
        var user = await principal.GetCurrentUserAsync(db, cancellationToken);
        if (user is null)
        {
            return Results.Unauthorized();
        }

        var profile = await FindCandidateProfileAsync(db, user.Id, cancellationToken);
        if (profile is null)
        {
            return Results.NotFound();
        }

        var record = await FindOwnedRecordAsync(db, profile.Id, id, cancellationToken);
        if (record is null)
        {
            return Results.NotFound();
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
        record.SubmittedAt = to == ExperienceVerificationStatus.Submitted ? DateTimeOffset.UtcNow : record.SubmittedAt;

        db.Add(new ExperienceVerificationAudit
        {
            ExperienceRecordId = record.Id,
            ActorUserId = user.Id,
            ActorRole = user.Role?.ToString() ?? "Applicant",
            Action = action,
            PreviousStatus = previousStatus,
            NewStatus = to,
            Note = Trim(note),
        });

        // One SaveChangesAsync call: the status change and its audit row commit together or not
        // at all (see the type-level doc comment for why a separate explicit transaction isn't
        // needed here).
        await db.SaveChangesAsync(cancellationToken);

        return Results.Ok(await ToResponseAsync(db, record, cancellationToken));
    }

    private static async Task<IResult> DeleteRecordAsync(Guid id, ClaimsPrincipal principal, IApplicationDbContext db, CancellationToken cancellationToken)
    {
        var user = await principal.GetCurrentUserAsync(db, cancellationToken);
        if (user is null)
        {
            return Results.Unauthorized();
        }

        var profile = await FindCandidateProfileAsync(db, user.Id, cancellationToken);
        if (profile is null)
        {
            return Results.NotFound();
        }

        var record = await FindOwnedRecordAsync(db, profile.Id, id, cancellationToken);
        if (record is null)
        {
            return Results.NotFound();
        }

        // Hard delete is only ever offered for a Draft record. A Draft has never been submitted,
        // so it is guaranteed to have zero audit rows — deleting it can never take audit history
        // with it. Every other status is reached only through Submit, which always logs an audit
        // row first, so once a record leaves Draft it can only ever be Withdrawn, never deleted —
        // that's the safe retention strategy, enforced here and backed by the FK's Restrict
        // behavior as defense-in-depth.
        if (record.Status != ExperienceVerificationStatus.Draft)
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["status"] = ["Only a draft record can be deleted. Withdraw a submitted record instead."],
            });
        }

        db.RemoveRange<ExperienceRecord>([record]);
        await db.SaveChangesAsync(cancellationToken);

        return Results.NoContent();
    }

    // -------------------------------------------------------------------- shared ---

    internal static async Task<CandidateProfile?> FindCandidateProfileAsync(IApplicationDbContext db, Guid userId, CancellationToken cancellationToken) =>
        await db.CandidateProfiles.FirstOrDefaultAsync(c => c.UserId == userId, cancellationToken);

    private static async Task<ExperienceRecord?> FindOwnedRecordAsync(
        IApplicationDbContext db, Guid candidateProfileId, Guid recordId, CancellationToken cancellationToken) =>
        await db.ExperienceRecords.FirstOrDefaultAsync(r => r.Id == recordId && r.CandidateProfileId == candidateProfileId, cancellationToken);

    internal static bool CanEdit(ExperienceVerificationStatus status) =>
        status is ExperienceVerificationStatus.Draft or ExperienceVerificationStatus.Rejected or ExperienceVerificationStatus.Withdrawn;

    private static async Task<ExperienceRecordResponse> ToResponseAsync(IApplicationDbContext db, ExperienceRecord record, CancellationToken cancellationToken)
    {
        var timeline = await db.ExperienceVerificationAudits
            .AsNoTracking()
            .Where(a => a.ExperienceRecordId == record.Id)
            .OrderBy(a => a.CreatedAt)
            .Select(a => new ExperienceTimelineEntryResponse(a.Action.ToString(), a.PreviousStatus.ToString(), a.NewStatus.ToString(), a.Note, a.CreatedAt))
            .ToListAsync(cancellationToken);

        return new ExperienceRecordResponse(
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
            CanEdit(record.Status),
            record.SubmittedAt,
            record.CreatedAt,
            record.UpdatedAt,
            timeline);
    }

    private static string[] NormalizeSkills(IReadOnlyList<string>? skills) =>
        skills is null
            ? []
            : skills
                .Select(s => s.Trim())
                .Where(s => s.Length > 0)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();

    private static string? Trim(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    /// <summary>Candidate-provided links are never independently verified — this only rejects
    /// malformed or non-http(s) input before it is stored, never a claim the destination is
    /// legitimate or still reachable, and never treated as proof of anything.</summary>
    private static bool IsAllowedUrl(string value, int maxLength) =>
        value.Trim().Length <= maxLength
        && Uri.TryCreate(value.Trim(), UriKind.Absolute, out var uri)
        && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);

    private static Dictionary<string, string[]> ValidateFields(
        ExperienceRecordInput request, out ExperienceRecordType recordType, out ExperienceVisibility visibility)
    {
        var errors = new Dictionary<string, string[]>();
        recordType = ExperienceRecordType.Project;
        visibility = ExperienceVisibility.Private;

        if (string.IsNullOrWhiteSpace(request.RecordType) || !Enum.TryParse(request.RecordType, true, out recordType))
        {
            errors["recordType"] = ["Must be one of Employment, Internship, Project, Research, Volunteering, or Certification."];
        }

        if (string.IsNullOrWhiteSpace(request.Title))
        {
            errors["title"] = ["Title is required."];
        }
        else if (request.Title.Trim().Length > MaxTitleLength)
        {
            errors["title"] = [$"Must be {MaxTitleLength} characters or fewer."];
        }

        if (!string.IsNullOrWhiteSpace(request.Organization) && request.Organization.Trim().Length > MaxOrganizationLength)
        {
            errors["organization"] = [$"Must be {MaxOrganizationLength} characters or fewer."];
        }

        if (!string.IsNullOrWhiteSpace(request.Description) && request.Description.Trim().Length > MaxDescriptionLength)
        {
            errors["description"] = [$"Must be {MaxDescriptionLength} characters or fewer."];
        }

        if (request.Skills is { Count: > 0 })
        {
            if (request.Skills.Count > MaxSkills)
            {
                errors["skills"] = [$"At most {MaxSkills} skills are allowed."];
            }
            else if (request.Skills.Any(s => s.Trim().Length > MaxSkillLength))
            {
                errors["skills"] = [$"Each skill must be {MaxSkillLength} characters or fewer."];
            }
        }

        if (request.StartDate is { } start && request.EndDate is { } end && start > end)
        {
            errors["endDate"] = ["End date must be on or after the start date."];
        }

        if (!string.IsNullOrWhiteSpace(request.EvidenceUrl) && !IsAllowedUrl(request.EvidenceUrl, MaxUrlLength))
        {
            errors["evidenceUrl"] = ["Must be a valid http(s) URL."];
        }

        if (!string.IsNullOrWhiteSpace(request.Visibility))
        {
            if (!Enum.TryParse(request.Visibility, true, out visibility))
            {
                errors["visibility"] = ["Must be either Private or Public."];
            }
        }

        return errors;
    }
}
