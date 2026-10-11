using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using StepIn.Api.Infrastructure;
using StepIn.Application.Common.Interfaces;
using StepIn.Domain.Career;
using StepIn.Domain.Jobs;
using StepIn.Domain.Profiles;

namespace StepIn.Api.Endpoints;

/// <summary>
/// Career GPS: a candidate's own career goals and the roadmap items under each. Ownership
/// (<see cref="CareerGoal.CandidateProfileId"/>) is always resolved server-side from the
/// authenticated principal's own <see cref="CandidateProfile"/> — never from a client-supplied
/// id — and every single-item query scopes by both the route id and that profile id in the same
/// WHERE clause, so another candidate's goal is indistinguishable from one that doesn't exist
/// (404, never 403). A roadmap item is reached only through its own goal's ownership check: no
/// endpoint here ever trusts a route's goalId without first verifying it belongs to the caller.
/// </summary>
public static class CareerGoalEndpoints
{
    public static IEndpointRouteBuilder MapCareerGoalEndpoints(this IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        var goals = app.MapGroup("/api/v1/career-goals")
            .WithTags("CareerGoals")
            .RequireAuthorization("RequireApplicant");

        goals.MapGet("/", ListGoalsAsync).WithName("ListCareerGoals");
        goals.MapPost("/", CreateGoalAsync).WithName("CreateCareerGoal");
        goals.MapGet("/{id:guid}", GetGoalAsync).WithName("GetCareerGoal");
        goals.MapPut("/{id:guid}", UpdateGoalAsync).WithName("UpdateCareerGoal");
        goals.MapPost("/{id:guid}/complete", CompleteGoalAsync).WithName("CompleteCareerGoal");
        goals.MapPost("/{id:guid}/archive", ArchiveGoalAsync).WithName("ArchiveCareerGoal");
        goals.MapPost("/{id:guid}/reactivate", ReactivateGoalAsync).WithName("ReactivateCareerGoal");
        goals.MapDelete("/{id:guid}", DeleteGoalAsync).WithName("DeleteCareerGoal");
        goals.MapGet("/{id:guid}/skill-gap", GetSkillGapAsync).WithName("GetCareerGoalSkillGap");

        goals.MapGet("/{id:guid}/roadmap-items", ListRoadmapItemsAsync).WithName("ListRoadmapItems");
        goals.MapPost("/{id:guid}/roadmap-items", CreateRoadmapItemAsync).WithName("CreateRoadmapItem");
        goals.MapPut("/{id:guid}/roadmap-items/{itemId:guid}", UpdateRoadmapItemAsync).WithName("UpdateRoadmapItem");
        goals.MapPost("/{id:guid}/roadmap-items/{itemId:guid}/start", StartRoadmapItemAsync).WithName("StartRoadmapItem");
        goals.MapPost("/{id:guid}/roadmap-items/{itemId:guid}/complete", CompleteRoadmapItemAsync).WithName("CompleteRoadmapItem");
        goals.MapPost("/{id:guid}/roadmap-items/{itemId:guid}/reopen", ReopenRoadmapItemAsync).WithName("ReopenRoadmapItem");
        goals.MapDelete("/{id:guid}/roadmap-items/{itemId:guid}", DeleteRoadmapItemAsync).WithName("DeleteRoadmapItem");

        return app;
    }

    // ------------------------------------------------------------------ goals ---

    private static async Task<IResult> ListGoalsAsync(ClaimsPrincipal principal, IApplicationDbContext db, CancellationToken cancellationToken)
    {
        var user = await principal.GetCurrentUserAsync(db, cancellationToken);
        if (user is null)
        {
            return Results.Unauthorized();
        }

        var profile = await FindCandidateProfileAsync(db, user.Id, cancellationToken);
        if (profile is null)
        {
            return Results.Ok(Array.Empty<CareerGoalResponse>());
        }

        var rows = await LoadGoalRowsAsync(db, g => g.CandidateProfileId == profile.Id, cancellationToken);
        return Results.Ok(rows.Select(ToResponse).ToList());
    }

    private static async Task<IResult> CreateGoalAsync(
        CreateCareerGoalRequest request, ClaimsPrincipal principal, IApplicationDbContext db, CancellationToken cancellationToken)
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
                ["profile"] = ["Complete your candidate profile before setting a career goal."],
            });
        }

        var errors = ValidateGoalFields(request.TargetRole, request.Description);
        if (errors.Count > 0)
        {
            return Results.ValidationProblem(errors);
        }

        if (request.TargetJobId is { } targetJobId && !await IsEligibleTargetJobAsync(db, targetJobId, cancellationToken))
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["targetJobId"] = ["Must be a published Career job."],
            });
        }

        var goal = new CareerGoal
        {
            CandidateProfileId = profile.Id,
            TargetRole = request.TargetRole.Trim(),
            Description = Trim(request.Description),
            TargetDate = request.TargetDate,
            TargetJobId = request.TargetJobId,
        };

        db.Add(goal);
        await db.SaveChangesAsync(cancellationToken);

        var row = (await LoadGoalRowsAsync(db, g => g.Id == goal.Id, cancellationToken)).Single();
        return Results.Created($"/api/v1/career-goals/{goal.Id}", ToResponse(row));
    }

    private static async Task<IResult> GetGoalAsync(Guid id, ClaimsPrincipal principal, IApplicationDbContext db, CancellationToken cancellationToken)
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

        var row = (await LoadGoalRowsAsync(db, g => g.Id == id && g.CandidateProfileId == profile.Id, cancellationToken)).SingleOrDefault();
        return row is null ? Results.NotFound() : Results.Ok(ToResponse(row));
    }

    private static async Task<IResult> UpdateGoalAsync(
        Guid id, UpdateCareerGoalRequest request, ClaimsPrincipal principal, IApplicationDbContext db, CancellationToken cancellationToken)
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

        var goal = await FindOwnedGoalAsync(db, profile.Id, id, cancellationToken);
        if (goal is null)
        {
            return Results.NotFound();
        }

        var errors = ValidateGoalFields(request.TargetRole, request.Description);
        if (errors.Count > 0)
        {
            return Results.ValidationProblem(errors);
        }

        if (request.TargetJobId is { } targetJobId && !await IsEligibleTargetJobAsync(db, targetJobId, cancellationToken))
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["targetJobId"] = ["Must be a published Career job."],
            });
        }

        goal.TargetRole = request.TargetRole.Trim();
        goal.Description = Trim(request.Description);
        goal.TargetDate = request.TargetDate;
        goal.TargetJobId = request.TargetJobId;

        if (request.IsPrimary && !goal.IsPrimary)
        {
            // At most one primary goal per candidate — clear it on every other goal rather than
            // relying on a database constraint, so this stays a simple, explainable rule.
            await db.CareerGoals
                .Where(g => g.CandidateProfileId == profile.Id && g.Id != goal.Id && g.IsPrimary)
                .ExecuteUpdateAsync(s => s.SetProperty(g => g.IsPrimary, false), cancellationToken);
        }

        goal.IsPrimary = request.IsPrimary;

        await db.SaveChangesAsync(cancellationToken);

        var row = (await LoadGoalRowsAsync(db, g => g.Id == goal.Id, cancellationToken)).Single();
        return Results.Ok(ToResponse(row));
    }

    private static Task<IResult> CompleteGoalAsync(Guid id, ClaimsPrincipal principal, IApplicationDbContext db, CancellationToken cancellationToken) =>
        SetGoalStatusAsync(id, CareerGoalStatus.Completed, principal, db, cancellationToken);

    private static Task<IResult> ArchiveGoalAsync(Guid id, ClaimsPrincipal principal, IApplicationDbContext db, CancellationToken cancellationToken) =>
        SetGoalStatusAsync(id, CareerGoalStatus.Archived, principal, db, cancellationToken);

    private static Task<IResult> ReactivateGoalAsync(Guid id, ClaimsPrincipal principal, IApplicationDbContext db, CancellationToken cancellationToken) =>
        SetGoalStatusAsync(id, CareerGoalStatus.Active, principal, db, cancellationToken);

    private static async Task<IResult> SetGoalStatusAsync(
        Guid id, CareerGoalStatus status, ClaimsPrincipal principal, IApplicationDbContext db, CancellationToken cancellationToken)
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

        var goal = await FindOwnedGoalAsync(db, profile.Id, id, cancellationToken);
        if (goal is null)
        {
            return Results.NotFound();
        }

        goal.Status = status;
        await db.SaveChangesAsync(cancellationToken);

        var row = (await LoadGoalRowsAsync(db, g => g.Id == goal.Id, cancellationToken)).Single();
        return Results.Ok(ToResponse(row));
    }

    private static async Task<IResult> DeleteGoalAsync(Guid id, ClaimsPrincipal principal, IApplicationDbContext db, CancellationToken cancellationToken)
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

        var goal = await FindOwnedGoalAsync(db, profile.Id, id, cancellationToken);
        if (goal is null)
        {
            return Results.NotFound();
        }

        // Cascade delete (configured on the FK) removes the goal's roadmap items with it.
        db.RemoveRange([goal]);
        await db.SaveChangesAsync(cancellationToken);

        return Results.NoContent();
    }

    private static async Task<IResult> GetSkillGapAsync(Guid id, ClaimsPrincipal principal, IApplicationDbContext db, CancellationToken cancellationToken)
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

        var goal = await db.CareerGoals
            .AsNoTracking()
            .Include(g => g.TargetJob).ThenInclude(j => j!.Company)
            .FirstOrDefaultAsync(g => g.Id == id && g.CandidateProfileId == profile.Id, cancellationToken);

        if (goal is null)
        {
            return Results.NotFound();
        }

        if (goal.TargetJob is null)
        {
            // Career GPS stays usable without a target job — this is the honest "nothing to
            // compare" response, not an error, and not an invented score.
            return Results.Problem(
                title: "No target job selected",
                detail: "Set a target job on this goal to see skill-gap guidance.",
                statusCode: StatusCodes.Status404NotFound);
        }

        var result = JobMatchScorer.Score(profile.Skills, goal.TargetJob.Skills);
        return Results.Ok(new CareerGoalSkillGapResponse(
            goal.TargetJob.Id,
            goal.TargetJob.Title,
            goal.TargetJob.Company.Name,
            result.MatchedSkills,
            result.SkillsNotYetDemonstrated,
            result.SkillCoverage,
            result.Quality.ToString()));
    }

    // ------------------------------------------------------------ roadmap items ---

    private static async Task<IResult> ListRoadmapItemsAsync(
        Guid id, string? status, ClaimsPrincipal principal, IApplicationDbContext db, CancellationToken cancellationToken)
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

        var ownsGoal = await db.CareerGoals.AnyAsync(g => g.Id == id && g.CandidateProfileId == profile.Id, cancellationToken);
        if (!ownsGoal)
        {
            return Results.NotFound();
        }

        if (!string.IsNullOrWhiteSpace(status) && !Enum.TryParse<RoadmapItemStatus>(status, true, out _))
        {
            return Results.ValidationProblem(new Dictionary<string, string[]> { ["status"] = ["Unknown status."] });
        }

        var query = db.RoadmapItems.AsNoTracking().Where(i => i.CareerGoalId == id);
        if (Enum.TryParse<RoadmapItemStatus>(status, true, out var parsedStatus))
        {
            query = query.Where(i => i.Status == parsedStatus);
        }

        var items = await query
            .OrderBy(i => i.Status == RoadmapItemStatus.Completed)
            .ThenBy(i => i.TargetDate)
            .ThenByDescending(i => i.CreatedAt)
            .ToListAsync(cancellationToken);

        return Results.Ok(items.Select(ToResponse).ToList());
    }

    private static async Task<IResult> CreateRoadmapItemAsync(
        Guid id, CreateRoadmapItemRequest request, ClaimsPrincipal principal, IApplicationDbContext db, CancellationToken cancellationToken)
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

        var ownsGoal = await db.CareerGoals.AnyAsync(g => g.Id == id && g.CandidateProfileId == profile.Id, cancellationToken);
        if (!ownsGoal)
        {
            return Results.NotFound();
        }

        var errors = ValidateItemFields(request.Title, request.Description, request.Priority, out var priority);
        if (errors.Count > 0)
        {
            return Results.ValidationProblem(errors);
        }

        var item = new RoadmapItem
        {
            CareerGoalId = id,
            Title = request.Title.Trim(),
            Description = Trim(request.Description),
            TargetDate = request.TargetDate,
            Priority = priority,
        };

        db.Add(item);
        await db.SaveChangesAsync(cancellationToken);

        return Results.Created($"/api/v1/career-goals/{id}/roadmap-items/{item.Id}", ToResponse(item));
    }

    private static async Task<IResult> UpdateRoadmapItemAsync(
        Guid id, Guid itemId, UpdateRoadmapItemRequest request, ClaimsPrincipal principal, IApplicationDbContext db, CancellationToken cancellationToken)
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

        var item = await FindOwnedRoadmapItemAsync(db, profile.Id, id, itemId, cancellationToken);
        if (item is null)
        {
            return Results.NotFound();
        }

        var errors = ValidateItemFields(request.Title, request.Description, request.Priority, out var priority);
        if (errors.Count > 0)
        {
            return Results.ValidationProblem(errors);
        }

        item.Title = request.Title.Trim();
        item.Description = Trim(request.Description);
        item.TargetDate = request.TargetDate;
        item.Priority = priority;

        await db.SaveChangesAsync(cancellationToken);
        return Results.Ok(ToResponse(item));
    }

    private static Task<IResult> StartRoadmapItemAsync(
        Guid id, Guid itemId, ClaimsPrincipal principal, IApplicationDbContext db, CancellationToken cancellationToken) =>
        SetItemStatusAsync(id, itemId, RoadmapItemStatus.InProgress, principal, db, cancellationToken);

    private static Task<IResult> CompleteRoadmapItemAsync(
        Guid id, Guid itemId, ClaimsPrincipal principal, IApplicationDbContext db, CancellationToken cancellationToken) =>
        SetItemStatusAsync(id, itemId, RoadmapItemStatus.Completed, principal, db, cancellationToken);

    private static Task<IResult> ReopenRoadmapItemAsync(
        Guid id, Guid itemId, ClaimsPrincipal principal, IApplicationDbContext db, CancellationToken cancellationToken) =>
        SetItemStatusAsync(id, itemId, RoadmapItemStatus.NotStarted, principal, db, cancellationToken);

    private static async Task<IResult> SetItemStatusAsync(
        Guid id, Guid itemId, RoadmapItemStatus status, ClaimsPrincipal principal, IApplicationDbContext db, CancellationToken cancellationToken)
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

        var item = await FindOwnedRoadmapItemAsync(db, profile.Id, id, itemId, cancellationToken);
        if (item is null)
        {
            return Results.NotFound();
        }

        item.Status = status;
        // Server-controlled, never client-supplied: set exactly on the transition into
        // Completed, cleared on any other transition (reopen or restart).
        item.CompletedAt = status == RoadmapItemStatus.Completed ? DateTimeOffset.UtcNow : null;

        await db.SaveChangesAsync(cancellationToken);
        return Results.Ok(ToResponse(item));
    }

    private static async Task<IResult> DeleteRoadmapItemAsync(
        Guid id, Guid itemId, ClaimsPrincipal principal, IApplicationDbContext db, CancellationToken cancellationToken)
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

        var item = await FindOwnedRoadmapItemAsync(db, profile.Id, id, itemId, cancellationToken);
        if (item is null)
        {
            return Results.NotFound();
        }

        db.RemoveRange([item]);
        await db.SaveChangesAsync(cancellationToken);
        return Results.NoContent();
    }

    // -------------------------------------------------------------------- shared ---

    private static async Task<CandidateProfile?> FindCandidateProfileAsync(IApplicationDbContext db, Guid userId, CancellationToken cancellationToken) =>
        await db.CandidateProfiles.FirstOrDefaultAsync(c => c.UserId == userId, cancellationToken);

    private static async Task<CareerGoal?> FindOwnedGoalAsync(
        IApplicationDbContext db, Guid candidateProfileId, Guid goalId, CancellationToken cancellationToken) =>
        await db.CareerGoals.FirstOrDefaultAsync(g => g.Id == goalId && g.CandidateProfileId == candidateProfileId, cancellationToken);

    /// <summary>
    /// Scopes a roadmap item by both its own id and its goal's ownership in one query — a
    /// roadmap item belonging to a goal the caller doesn't own is indistinguishable from one that
    /// doesn't exist.
    /// </summary>
    private static async Task<RoadmapItem?> FindOwnedRoadmapItemAsync(
        IApplicationDbContext db, Guid candidateProfileId, Guid goalId, Guid itemId, CancellationToken cancellationToken) =>
        await db.RoadmapItems
            .Join(db.CareerGoals, i => i.CareerGoalId, g => g.Id, (i, g) => new { Item = i, Goal = g })
            .Where(x => x.Item.Id == itemId && x.Goal.Id == goalId && x.Goal.CandidateProfileId == candidateProfileId)
            .Select(x => x.Item)
            .FirstOrDefaultAsync(cancellationToken);

    private static async Task<bool> IsEligibleTargetJobAsync(IApplicationDbContext db, Guid jobId, CancellationToken cancellationToken) =>
        await db.Jobs.AnyAsync(j => j.Id == jobId && j.Status == JobStatus.Published && j.Category == JobCategory.Career, cancellationToken);

    private sealed record GoalRow(
        Guid Id,
        string TargetRole,
        string? Description,
        DateOnly? TargetDate,
        CareerGoalStatus Status,
        bool IsPrimary,
        Guid? TargetJobId,
        string? TargetJobTitle,
        string? TargetJobCompanyName,
        int TotalItems,
        int CompletedItems,
        DateTimeOffset CreatedAt,
        DateTimeOffset? UpdatedAt);

    /// <summary>Single query, in SQL, for the list and every other place that needs a fully
    /// computed goal row — counts and the target job's display fields are never trusted from a
    /// loaded navigation the caller could have mutated elsewhere in the same request.</summary>
    private static async Task<List<GoalRow>> LoadGoalRowsAsync(
        IApplicationDbContext db, System.Linq.Expressions.Expression<Func<CareerGoal, bool>> predicate, CancellationToken cancellationToken) =>
        await db.CareerGoals
            .AsNoTracking()
            .Where(predicate)
            .OrderByDescending(g => g.IsPrimary)
            .ThenByDescending(g => g.CreatedAt)
            .Select(g => new GoalRow(
                g.Id,
                g.TargetRole,
                g.Description,
                g.TargetDate,
                g.Status,
                g.IsPrimary,
                g.TargetJobId,
                g.TargetJob == null ? null : g.TargetJob.Title,
                g.TargetJob == null ? null : g.TargetJob.Company.Name,
                g.RoadmapItems.Count,
                g.RoadmapItems.Count(i => i.Status == RoadmapItemStatus.Completed),
                g.CreatedAt,
                g.UpdatedAt))
            .ToListAsync(cancellationToken);

    private static CareerGoalResponse ToResponse(GoalRow row) => new(
        row.Id,
        row.TargetRole,
        row.Description,
        row.TargetDate,
        row.Status.ToString(),
        row.IsPrimary,
        row.TargetJobId,
        row.TargetJobTitle,
        row.TargetJobCompanyName,
        row.TotalItems,
        row.CompletedItems,
        row.TotalItems == 0 ? 0 : (int)Math.Round(100.0 * row.CompletedItems / row.TotalItems),
        row.CreatedAt,
        row.UpdatedAt);

    private static RoadmapItemResponse ToResponse(RoadmapItem item) => new(
        item.Id,
        item.CareerGoalId,
        item.Title,
        item.Description,
        item.TargetDate,
        item.Priority.ToString(),
        item.Status.ToString(),
        item.CompletedAt,
        item.CreatedAt,
        item.UpdatedAt);

    private static Dictionary<string, string[]> ValidateGoalFields(string? targetRole, string? description)
    {
        var errors = new Dictionary<string, string[]>();

        if (string.IsNullOrWhiteSpace(targetRole))
        {
            errors["targetRole"] = ["Target role is required."];
        }
        else if (targetRole.Trim().Length > 150)
        {
            errors["targetRole"] = ["Must be 150 characters or fewer."];
        }

        if (!string.IsNullOrWhiteSpace(description) && description.Length > 2000)
        {
            errors["description"] = ["Must be 2000 characters or fewer."];
        }

        return errors;
    }

    private static Dictionary<string, string[]> ValidateItemFields(
        string? title, string? description, string? priority, out RoadmapItemPriority parsedPriority)
    {
        var errors = new Dictionary<string, string[]>();
        parsedPriority = RoadmapItemPriority.Medium;

        if (string.IsNullOrWhiteSpace(title))
        {
            errors["title"] = ["Title is required."];
        }
        else if (title.Trim().Length > 200)
        {
            errors["title"] = ["Must be 200 characters or fewer."];
        }

        if (!string.IsNullOrWhiteSpace(description) && description.Length > 2000)
        {
            errors["description"] = ["Must be 2000 characters or fewer."];
        }

        if (!string.IsNullOrWhiteSpace(priority))
        {
            if (!Enum.TryParse(priority, true, out parsedPriority))
            {
                errors["priority"] = ["Unknown priority."];
            }
        }

        return errors;
    }

    private static string? Trim(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
