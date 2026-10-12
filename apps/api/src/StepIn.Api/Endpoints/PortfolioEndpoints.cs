using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using StepIn.Api.Infrastructure;
using StepIn.Application.Common.Interfaces;
using StepIn.Domain.Aidx;
using StepIn.Domain.Portfolio;
using StepIn.Domain.Profiles;

namespace StepIn.Api.Endpoints;

/// <summary>
/// Proof-of-Skills Portfolio: a candidate's own evidence of work. Ownership
/// (<see cref="PortfolioProject.CandidateProfileId"/>) is always resolved server-side from the
/// authenticated principal's own <see cref="CandidateProfile"/> — never from a client-supplied id
/// — and every single-item query scopes by both the route id and that profile id in the same WHERE
/// clause, so another candidate's project is indistinguishable from one that doesn't exist (404,
/// never 403).
///
/// Visibility defaults to Private. There is no endpoint here, or anywhere else in the API, that
/// lists or browses other candidates' projects — the only way a project is ever visible to someone
/// other than its owner is <see cref="RecruiterApplicationEndpoints"/> surfacing a candidate's
/// Public projects through the existing RecruiterProfile → owned Job → JobApplication →
/// CandidateProfile ownership chain, deliberately avoiding a new public user-discovery surface.
/// </summary>
public static class PortfolioEndpoints
{
    private const int MaxTitleLength = 200;
    private const int MaxDescriptionLength = 5000;
    private const int MaxUrlLength = 500;
    private const int MaxSkillLength = 100;
    private const int MaxSkills = 50;

    public static IEndpointRouteBuilder MapPortfolioEndpoints(this IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        var projects = app.MapGroup("/api/v1/portfolio")
            .WithTags("Portfolio")
            .RequireAuthorization("RequireApplicant");

        projects.MapGet("/", ListProjectsAsync).WithName("ListPortfolioProjects");
        projects.MapPost("/", CreateProjectAsync).WithName("CreatePortfolioProject");
        projects.MapGet("/{id:guid}", GetProjectAsync).WithName("GetPortfolioProject");
        projects.MapPut("/{id:guid}", UpdateProjectAsync).WithName("UpdatePortfolioProject");
        projects.MapDelete("/{id:guid}", DeleteProjectAsync).WithName("DeletePortfolioProject");

        return app;
    }

    private static async Task<IResult> ListProjectsAsync(ClaimsPrincipal principal, IApplicationDbContext db, CancellationToken cancellationToken)
    {
        var user = await principal.GetCurrentUserAsync(db, cancellationToken);
        if (user is null)
        {
            return Results.Unauthorized();
        }

        var profile = await FindCandidateProfileAsync(db, user.Id, cancellationToken);
        if (profile is null)
        {
            return Results.Ok(Array.Empty<PortfolioProjectResponse>());
        }

        var rows = await LoadProjectRowsAsync(db, p => p.CandidateProfileId == profile.Id, cancellationToken);
        return Results.Ok(rows.Select(ToResponse).ToList());
    }

    private static async Task<IResult> CreateProjectAsync(
        PortfolioProjectInput request, ClaimsPrincipal principal, IApplicationDbContext db, CancellationToken cancellationToken)
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
                ["profile"] = ["Complete your candidate profile before adding a portfolio project."],
            });
        }

        var errors = await ValidateFieldsAsync(db, request, cancellationToken);
        if (errors.Count > 0)
        {
            return Results.ValidationProblem(errors);
        }

        var project = new PortfolioProject
        {
            CandidateProfileId = profile.Id,
            Title = request.Title.Trim(),
            Description = request.Description.Trim(),
            Skills = NormalizeSkills(request.Skills),
            StartDate = request.StartDate,
            EndDate = request.EndDate,
            RepositoryUrl = Trim(request.RepositoryUrl),
            DemoUrl = Trim(request.DemoUrl),
            Visibility = ParseVisibility(request.Visibility),
            ResearchProjectId = request.ResearchProjectId,
        };

        db.Add(project);
        await db.SaveChangesAsync(cancellationToken);

        var row = (await LoadProjectRowsAsync(db, p => p.Id == project.Id, cancellationToken)).Single();
        return Results.Created($"/api/v1/portfolio/{project.Id}", ToResponse(row));
    }

    private static async Task<IResult> GetProjectAsync(Guid id, ClaimsPrincipal principal, IApplicationDbContext db, CancellationToken cancellationToken)
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

        var row = (await LoadProjectRowsAsync(db, p => p.Id == id && p.CandidateProfileId == profile.Id, cancellationToken)).SingleOrDefault();
        return row is null ? Results.NotFound() : Results.Ok(ToResponse(row));
    }

    private static async Task<IResult> UpdateProjectAsync(
        Guid id, PortfolioProjectInput request, ClaimsPrincipal principal, IApplicationDbContext db, CancellationToken cancellationToken)
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

        var project = await FindOwnedProjectAsync(db, profile.Id, id, cancellationToken);
        if (project is null)
        {
            return Results.NotFound();
        }

        var errors = await ValidateFieldsAsync(db, request, cancellationToken);
        if (errors.Count > 0)
        {
            return Results.ValidationProblem(errors);
        }

        project.Title = request.Title.Trim();
        project.Description = request.Description.Trim();
        project.Skills = NormalizeSkills(request.Skills);
        project.StartDate = request.StartDate;
        project.EndDate = request.EndDate;
        project.RepositoryUrl = Trim(request.RepositoryUrl);
        project.DemoUrl = Trim(request.DemoUrl);
        project.Visibility = ParseVisibility(request.Visibility);
        project.ResearchProjectId = request.ResearchProjectId;

        await db.SaveChangesAsync(cancellationToken);

        var row = (await LoadProjectRowsAsync(db, p => p.Id == project.Id, cancellationToken)).Single();
        return Results.Ok(ToResponse(row));
    }

    private static async Task<IResult> DeleteProjectAsync(Guid id, ClaimsPrincipal principal, IApplicationDbContext db, CancellationToken cancellationToken)
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

        var project = await FindOwnedProjectAsync(db, profile.Id, id, cancellationToken);
        if (project is null)
        {
            return Results.NotFound();
        }

        db.RemoveRange<PortfolioProject>([project]);
        await db.SaveChangesAsync(cancellationToken);

        return Results.NoContent();
    }

    // -------------------------------------------------------------------- shared ---

    private static async Task<CandidateProfile?> FindCandidateProfileAsync(IApplicationDbContext db, Guid userId, CancellationToken cancellationToken) =>
        await db.CandidateProfiles.FirstOrDefaultAsync(c => c.UserId == userId, cancellationToken);

    private static async Task<PortfolioProject?> FindOwnedProjectAsync(
        IApplicationDbContext db, Guid candidateProfileId, Guid projectId, CancellationToken cancellationToken) =>
        await db.PortfolioProjects.FirstOrDefaultAsync(p => p.Id == projectId && p.CandidateProfileId == candidateProfileId, cancellationToken);

    private static async Task<bool> IsEligibleResearchProjectAsync(IApplicationDbContext db, Guid researchProjectId, CancellationToken cancellationToken) =>
        await db.AidxProjects.AnyAsync(p => p.Id == researchProjectId && p.Status == AidxContentStatus.Published, cancellationToken);

    private sealed record ProjectRow(
        Guid Id,
        string Title,
        string Description,
        string[] Skills,
        DateOnly? StartDate,
        DateOnly? EndDate,
        string? RepositoryUrl,
        string? DemoUrl,
        PortfolioVisibility Visibility,
        Guid? ResearchProjectId,
        string? ResearchProjectTitle,
        DateTimeOffset CreatedAt,
        DateTimeOffset? UpdatedAt);

    private static async Task<List<ProjectRow>> LoadProjectRowsAsync(
        IApplicationDbContext db, System.Linq.Expressions.Expression<Func<PortfolioProject, bool>> predicate, CancellationToken cancellationToken) =>
        await db.PortfolioProjects
            .AsNoTracking()
            .Where(predicate)
            .OrderByDescending(p => p.CreatedAt)
            .Select(p => new ProjectRow(
                p.Id,
                p.Title,
                p.Description,
                p.Skills,
                p.StartDate,
                p.EndDate,
                p.RepositoryUrl,
                p.DemoUrl,
                p.Visibility,
                p.ResearchProjectId,
                p.ResearchProject == null ? null : p.ResearchProject.Title,
                p.CreatedAt,
                p.UpdatedAt))
            .ToListAsync(cancellationToken);

    private static PortfolioProjectResponse ToResponse(ProjectRow row) => new(
        row.Id,
        row.Title,
        row.Description,
        row.Skills,
        row.StartDate,
        row.EndDate,
        row.RepositoryUrl,
        row.DemoUrl,
        row.Visibility.ToString(),
        row.ResearchProjectId,
        row.ResearchProjectTitle,
        row.CreatedAt,
        row.UpdatedAt);

    private static PortfolioVisibility ParseVisibility(string? value) =>
        !string.IsNullOrWhiteSpace(value) && Enum.TryParse<PortfolioVisibility>(value, true, out var parsed)
            ? parsed
            : PortfolioVisibility.Private;

    private static string[] NormalizeSkills(IReadOnlyList<string>? skills) =>
        skills is null
            ? []
            : skills
                .Select(s => s.Trim())
                .Where(s => s.Length > 0)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();

    private static string? Trim(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static async Task<Dictionary<string, string[]>> ValidateFieldsAsync(
        IApplicationDbContext db, PortfolioProjectInput request, CancellationToken cancellationToken)
    {
        var errors = new Dictionary<string, string[]>();

        if (string.IsNullOrWhiteSpace(request.Title))
        {
            errors["title"] = ["Title is required."];
        }
        else if (request.Title.Trim().Length > MaxTitleLength)
        {
            errors["title"] = [$"Must be {MaxTitleLength} characters or fewer."];
        }

        if (string.IsNullOrWhiteSpace(request.Description))
        {
            errors["description"] = ["Description is required."];
        }
        else if (request.Description.Trim().Length > MaxDescriptionLength)
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

        if (!string.IsNullOrWhiteSpace(request.RepositoryUrl) && !IsAllowedUrl(request.RepositoryUrl, MaxUrlLength))
        {
            errors["repositoryUrl"] = ["Must be a valid http(s) URL."];
        }

        if (!string.IsNullOrWhiteSpace(request.DemoUrl) && !IsAllowedUrl(request.DemoUrl, MaxUrlLength))
        {
            errors["demoUrl"] = ["Must be a valid http(s) URL."];
        }

        if (!string.IsNullOrWhiteSpace(request.Visibility) && !Enum.TryParse<PortfolioVisibility>(request.Visibility, true, out _))
        {
            errors["visibility"] = ["Must be either Private or Public."];
        }

        if (request.ResearchProjectId is { } researchProjectId && !await IsEligibleResearchProjectAsync(db, researchProjectId, cancellationToken))
        {
            errors["researchProjectId"] = ["Must be a published research project."];
        }

        return errors;
    }

    /// <summary>
    /// Candidate-provided links are never independently verified — this only rejects malformed or
    /// non-http(s) input (e.g. javascript:/file: schemes) before it is stored and later rendered as
    /// a link, never a claim that the destination is legitimate or still reachable.
    /// </summary>
    private static bool IsAllowedUrl(string value, int maxLength) =>
        value.Trim().Length <= maxLength
        && Uri.TryCreate(value.Trim(), UriKind.Absolute, out var uri)
        && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);
}
