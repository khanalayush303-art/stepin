using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using StepIn.Api.Infrastructure;
using StepIn.Application.Common.Interfaces;
using StepIn.Domain.Interviews;
using StepIn.Domain.Profiles;

namespace StepIn.Api.Endpoints;

/// <summary>
/// Interview Practice Studio: a candidate's own practice sessions. Ownership
/// (<see cref="InterviewPracticeSession.CandidateProfileId"/>) is always resolved server-side from
/// the authenticated principal's own <see cref="CandidateProfile"/> — never from a client-supplied
/// id — and every single-item query scopes by both the route id and that profile id in the same
/// WHERE clause, so another candidate's session is indistinguishable from one that doesn't exist
/// (404, never 403). A question is reached only through its own session's ownership check: no
/// endpoint here ever trusts a route's questionId without first verifying it belongs to a session
/// the caller owns.
///
/// Feedback is computed on every read by <see cref="InterviewFeedbackEngine"/> — never persisted,
/// never stale. There is no AI provider involved anywhere in this file: every question comes from
/// the curated, static <see cref="InterviewQuestionBank"/>, and every piece of feedback is a
/// deterministic function of a question's category and the candidate's own saved answer text.
/// </summary>
public static class InterviewEndpoints
{
    public const int MaxAnswerLength = 4000;

    public static IEndpointRouteBuilder MapInterviewEndpoints(this IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        var group = app.MapGroup("/api/v1/interview-practice")
            .WithTags("InterviewPractice")
            .RequireAuthorization("RequireApplicant");

        group.MapGet("/templates", GetTemplatesAsync).WithName("GetInterviewTemplates");
        group.MapGet("/", ListSessionsAsync).WithName("ListInterviewSessions");
        group.MapPost("/", CreateSessionAsync).WithName("CreateInterviewSession");
        group.MapGet("/{id:guid}", GetSessionAsync).WithName("GetInterviewSession");
        group.MapPut("/{id:guid}/questions/{questionId:guid}/answer", SaveAnswerAsync).WithName("SaveInterviewAnswer");
        group.MapPost("/{id:guid}/complete", CompleteSessionAsync).WithName("CompleteInterviewSession");
        group.MapDelete("/{id:guid}", DeleteSessionAsync).WithName("DeleteInterviewSession");

        return app;
    }

    // Pure input-format validation — touches no data, so it's safe before any ownership check.
    private static Task<IResult> GetTemplatesAsync()
    {
        var categories = InterviewQuestionBank.Categories
            .Select(c => new InterviewCategoryOption(c.ToString(), InterviewQuestionBank.AvailableQuestionCount(c)))
            .ToList();

        return Task.FromResult(Results.Ok(new InterviewTemplatesResponse(
            categories, InterviewQuestionBank.MinQuestionCount, InterviewQuestionBank.MaxQuestionCount)));
    }

    private static async Task<IResult> ListSessionsAsync(ClaimsPrincipal principal, IApplicationDbContext db, CancellationToken cancellationToken)
    {
        var user = await principal.GetCurrentUserAsync(db, cancellationToken);
        if (user is null)
        {
            return Results.Unauthorized();
        }

        var profile = await FindCandidateProfileAsync(db, user.Id, cancellationToken);
        if (profile is null)
        {
            return Results.Ok(Array.Empty<InterviewSessionSummaryResponse>());
        }

        var sessions = await db.InterviewPracticeSessions
            .AsNoTracking()
            .Where(s => s.CandidateProfileId == profile.Id)
            .OrderByDescending(s => s.CreatedAt)
            .Select(s => new InterviewSessionSummaryResponse(
                s.Id,
                s.Category.ToString(),
                s.TargetRole,
                s.Status.ToString(),
                s.Questions.Count,
                s.Questions.Count(q => q.AnsweredAt != null),
                s.CreatedAt,
                s.CompletedAt))
            .ToListAsync(cancellationToken);

        return Results.Ok(sessions);
    }

    private static async Task<IResult> CreateSessionAsync(
        CreateInterviewSessionRequest request, ClaimsPrincipal principal, IApplicationDbContext db, CancellationToken cancellationToken)
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
                ["profile"] = ["Complete your candidate profile before starting a practice session."],
            });
        }

        var errors = ValidateCreateRequest(request, out var category);
        if (errors.Count > 0)
        {
            return Results.ValidationProblem(errors);
        }

        var questionTexts = InterviewQuestionBank.SelectQuestions(category, request.QuestionCount);
        var session = new InterviewPracticeSession
        {
            CandidateProfileId = profile.Id,
            Category = category,
            TargetRole = Trim(request.TargetRole),
        };

        for (var i = 0; i < questionTexts.Count; i++)
        {
            session.Questions.Add(new InterviewPracticeQuestion
            {
                SessionId = session.Id,
                OrderIndex = i,
                Category = category,
                QuestionText = questionTexts[i],
            });
        }

        db.Add(session);
        await db.SaveChangesAsync(cancellationToken);

        var row = await LoadSessionAsync(db, session.Id, profile.Id, cancellationToken);
        return Results.Created($"/api/v1/interview-practice/{session.Id}", ToResponse(row!));
    }

    private static async Task<IResult> GetSessionAsync(Guid id, ClaimsPrincipal principal, IApplicationDbContext db, CancellationToken cancellationToken)
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

        var session = await LoadSessionAsync(db, id, profile.Id, cancellationToken);
        return session is null ? Results.NotFound() : Results.Ok(ToResponse(session));
    }

    private static async Task<IResult> SaveAnswerAsync(
        Guid id, Guid questionId, SaveInterviewAnswerRequest request, ClaimsPrincipal principal, IApplicationDbContext db, CancellationToken cancellationToken)
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

        // Ownership is proven by joining the question back to a session the caller owns, in the
        // same query — a question under a session the caller doesn't own is indistinguishable
        // from one that doesn't exist.
        var owned = await db.InterviewPracticeQuestions
            .Join(db.InterviewPracticeSessions, q => q.SessionId, s => s.Id, (q, s) => new { Question = q, Session = s })
            .Where(x => x.Question.Id == questionId && x.Session.Id == id && x.Session.CandidateProfileId == profile.Id)
            .Select(x => new { x.Question, x.Session.Status })
            .FirstOrDefaultAsync(cancellationToken);

        if (owned is null)
        {
            return Results.NotFound();
        }

        if (owned.Status != InterviewSessionStatus.InProgress)
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["session"] = ["This session is already completed and can no longer be edited."],
            });
        }

        if (request.AnswerText is { Length: > MaxAnswerLength })
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["answerText"] = [$"Must be {MaxAnswerLength} characters or fewer."],
            });
        }

        var trimmed = Trim(request.AnswerText);
        var question = owned.Question;
        question.AnswerText = trimmed;
        question.AnsweredAt = trimmed is null ? null : DateTimeOffset.UtcNow;

        await db.SaveChangesAsync(cancellationToken);

        return Results.Ok(ToResponse(question));
    }

    private static async Task<IResult> CompleteSessionAsync(Guid id, ClaimsPrincipal principal, IApplicationDbContext db, CancellationToken cancellationToken)
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

        var session = await db.InterviewPracticeSessions
            .FirstOrDefaultAsync(s => s.Id == id && s.CandidateProfileId == profile.Id, cancellationToken);
        if (session is null)
        {
            return Results.NotFound();
        }

        if (session.Status != InterviewSessionStatus.InProgress)
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["session"] = ["This session is already completed."],
            });
        }

        session.Status = InterviewSessionStatus.Completed;
        session.CompletedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(cancellationToken);

        var row = await LoadSessionAsync(db, session.Id, profile.Id, cancellationToken);
        return Results.Ok(ToResponse(row!));
    }

    private static async Task<IResult> DeleteSessionAsync(Guid id, ClaimsPrincipal principal, IApplicationDbContext db, CancellationToken cancellationToken)
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

        var session = await db.InterviewPracticeSessions
            .FirstOrDefaultAsync(s => s.Id == id && s.CandidateProfileId == profile.Id, cancellationToken);
        if (session is null)
        {
            return Results.NotFound();
        }

        // Cascade delete (configured on the FK) removes the session's questions and their
        // answers with it.
        db.RemoveRange<InterviewPracticeSession>([session]);
        await db.SaveChangesAsync(cancellationToken);

        return Results.NoContent();
    }

    // -------------------------------------------------------------------- shared ---

    private static async Task<CandidateProfile?> FindCandidateProfileAsync(IApplicationDbContext db, Guid userId, CancellationToken cancellationToken) =>
        await db.CandidateProfiles.FirstOrDefaultAsync(c => c.UserId == userId, cancellationToken);

    private static async Task<InterviewPracticeSession?> LoadSessionAsync(
        IApplicationDbContext db, Guid sessionId, Guid candidateProfileId, CancellationToken cancellationToken) =>
        await db.InterviewPracticeSessions
            .AsNoTracking()
            .Include(s => s.Questions.OrderBy(q => q.OrderIndex))
            .FirstOrDefaultAsync(s => s.Id == sessionId && s.CandidateProfileId == candidateProfileId, cancellationToken);

    private static InterviewQuestionResponse ToResponse(InterviewPracticeQuestion question)
    {
        var feedback = InterviewFeedbackEngine.Evaluate(question.Category, question.AnswerText);
        return new InterviewQuestionResponse(
            question.Id,
            question.OrderIndex,
            question.Category.ToString(),
            question.QuestionText,
            question.AnswerText,
            question.AnsweredAt,
            ToResponse(feedback));
    }

    private static InterviewFeedbackResponse ToResponse(InterviewFeedbackResult feedback) => new(
        feedback.Answered,
        feedback.Criteria.Select(c => new InterviewFeedbackCriterionResponse(c.Name, c.Met, c.Note)).ToList(),
        feedback.CriteriaMetCount,
        feedback.CriteriaTotalCount,
        feedback.Band,
        feedback.Improvements,
        feedback.SuggestedStructure,
        feedback.Disclaimer);

    private static InterviewSessionResponse ToResponse(InterviewPracticeSession session) => new(
        session.Id,
        session.Category.ToString(),
        session.TargetRole,
        session.Status.ToString(),
        session.Questions.Count,
        session.Questions.Count(q => q.AnsweredAt != null),
        session.CreatedAt,
        session.CompletedAt,
        session.Questions.OrderBy(q => q.OrderIndex).Select(ToResponse).ToList());

    private static string? Trim(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static Dictionary<string, string[]> ValidateCreateRequest(CreateInterviewSessionRequest request, out InterviewCategory category)
    {
        var errors = new Dictionary<string, string[]>();
        category = InterviewCategory.General;

        if (string.IsNullOrWhiteSpace(request.Category) || !Enum.TryParse(request.Category, true, out category))
        {
            errors["category"] = ["Must be one of General, Behavioral, Technical, or Situational."];
            return errors;
        }

        if (!string.IsNullOrWhiteSpace(request.TargetRole) && request.TargetRole.Trim().Length > 150)
        {
            errors["targetRole"] = ["Must be 150 characters or fewer."];
        }

        var available = InterviewQuestionBank.AvailableQuestionCount(category);
        var maxForCategory = Math.Min(InterviewQuestionBank.MaxQuestionCount, available);
        if (request.QuestionCount < InterviewQuestionBank.MinQuestionCount || request.QuestionCount > maxForCategory)
        {
            errors["questionCount"] = [$"Must be between {InterviewQuestionBank.MinQuestionCount} and {maxForCategory} for this category."];
        }

        return errors;
    }
}
