using System.Security.Claims;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using StepIn.Api.Infrastructure;
using StepIn.Application.Common.Interfaces;
using StepIn.Domain.Companies;
using StepIn.Domain.Profiles;
using StepIn.Domain.Users;

namespace StepIn.Api.Endpoints;

/// <summary>
/// Candidate and recruiter/company profile data — the application-specific
/// fields Clerk knows nothing about. Ownership is always the authenticated
/// principal resolved via <see cref="ClaimsPrincipalExtensions.GetCurrentUserAsync"/>;
/// no endpoint here accepts or trusts a client-supplied user id.
/// </summary>
public static partial class ProfileEndpoints
{
    public static IEndpointRouteBuilder MapProfileEndpoints(this IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        var candidate = app.MapGroup("/api/v1/profile/candidate")
            .WithTags("Profile")
            .RequireAuthorization("RequireApplicant");

        candidate.MapGet("/", GetCandidateProfileAsync)
            .WithName("GetCandidateProfile")
            .WithSummary("The signed-in candidate's profile.");

        candidate.MapPut("/", UpdateCandidateProfileAsync)
            .WithName("UpdateCandidateProfile")
            .WithSummary("Create or update the signed-in candidate's profile.");

        var recruiter = app.MapGroup("/api/v1/profile/recruiter")
            .WithTags("Profile")
            .RequireAuthorization("RequireRecruiter");

        recruiter.MapGet("/", GetRecruiterProfileAsync)
            .WithName("GetRecruiterProfile")
            .WithSummary("The signed-in recruiter's profile and company.");

        recruiter.MapPut("/", UpdateRecruiterProfileAsync)
            .WithName("UpdateRecruiterProfile")
            .WithSummary("Create or update the signed-in recruiter's profile and company.");

        return app;
    }

    // -------------------------------------------------------------- candidate ---

    private static async Task<IResult> GetCandidateProfileAsync(
        ClaimsPrincipal principal, IApplicationDbContext db, CancellationToken cancellationToken)
    {
        var user = await principal.GetCurrentUserAsync(db, cancellationToken);
        if (user is null)
        {
            return Results.Unauthorized();
        }

        var profile = await db.CandidateProfiles
            .Include(p => p.Education)
            .Include(p => p.Experience)
            .Include(p => p.Certifications)
            .FirstOrDefaultAsync(p => p.UserId == user.Id, cancellationToken);

        return Results.Ok(ToResponse(user, profile));
    }

    private static async Task<IResult> UpdateCandidateProfileAsync(
        UpdateCandidateProfileRequest request, ClaimsPrincipal principal, IApplicationDbContext db, CancellationToken cancellationToken)
    {
        var user = await principal.GetCurrentUserAsync(db, cancellationToken);
        if (user is null)
        {
            return Results.Unauthorized();
        }

        var errors = ValidateCandidateProfile(request);
        if (errors.Count > 0)
        {
            return Results.ValidationProblem(errors);
        }

        var profile = await db.CandidateProfiles
            .Include(p => p.Education)
            .Include(p => p.Experience)
            .Include(p => p.Certifications)
            .FirstOrDefaultAsync(p => p.UserId == user.Id, cancellationToken);

        if (profile is null)
        {
            profile = new CandidateProfile { UserId = user.Id };
            db.Add(profile);
        }

        profile.PhoneNumber = Trim(request.PhoneNumber);
        profile.Location = Trim(request.Location);
        profile.Headline = Trim(request.Headline);
        profile.Bio = Trim(request.Bio);
        profile.PhotoUrl = Trim(request.PhotoUrl);
        profile.LinkedInUrl = Trim(request.LinkedInUrl);
        profile.PortfolioUrl = Trim(request.PortfolioUrl);
        profile.GitHubUrl = Trim(request.GitHubUrl);
        profile.Skills = (request.Skills ?? [])
            .Select(s => s.Trim())
            .Where(s => s.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        ReconcileCollection(
            db,
            profile.Education,
            request.Education ?? [],
            dto => dto.Id,
            e => e.Id,
            dto => new CandidateEducation { CandidateProfileId = profile.Id, Institution = dto.Institution },
            (entity, dto) =>
            {
                entity.Institution = dto.Institution;
                entity.Degree = Trim(dto.Degree);
                entity.FieldOfStudy = Trim(dto.FieldOfStudy);
                entity.StartDate = dto.StartDate;
                entity.EndDate = dto.EndDate;
                entity.Description = Trim(dto.Description);
            });

        ReconcileCollection(
            db,
            profile.Experience,
            request.Experience ?? [],
            dto => dto.Id,
            e => e.Id,
            dto => new CandidateExperience { CandidateProfileId = profile.Id, CompanyName = dto.CompanyName, Title = dto.Title },
            (entity, dto) =>
            {
                entity.CompanyName = dto.CompanyName;
                entity.Title = dto.Title;
                entity.StartDate = dto.StartDate;
                entity.EndDate = dto.EndDate;
                entity.Description = Trim(dto.Description);
            });

        ReconcileCollection(
            db,
            profile.Certifications,
            request.Certifications ?? [],
            dto => dto.Id,
            e => e.Id,
            dto => new CandidateCertification { CandidateProfileId = profile.Id, Name = dto.Name },
            (entity, dto) =>
            {
                entity.Name = dto.Name;
                entity.IssuingOrganization = Trim(dto.IssuingOrganization);
                entity.IssueDate = dto.IssueDate;
                entity.CredentialUrl = Trim(dto.CredentialUrl);
            });

        await db.SaveChangesAsync(cancellationToken);

        return Results.Ok(ToResponse(user, profile));
    }

    private static CandidateProfileResponse ToResponse(ApplicationUser user, CandidateProfile? profile)
    {
        var education = profile?.Education.Select(e => new CandidateEducationDto(
            e.Id, e.Institution, e.Degree, e.FieldOfStudy, e.StartDate, e.EndDate, e.Description)).ToList()
            ?? [];
        var experience = profile?.Experience.Select(e => new CandidateExperienceDto(
            e.Id, e.CompanyName, e.Title, e.StartDate, e.EndDate, e.Description)).ToList()
            ?? [];
        var certifications = profile?.Certifications.Select(c => new CandidateCertificationDto(
            c.Id, c.Name, c.IssuingOrganization, c.IssueDate, c.CredentialUrl)).ToList()
            ?? [];
        var skills = profile?.Skills ?? [];

        return new CandidateProfileResponse(
            user.Id,
            user.Email,
            user.FirstName,
            user.LastName,
            profile?.PhoneNumber,
            profile?.Location,
            profile?.Headline,
            profile?.Bio,
            profile?.PhotoUrl,
            profile?.LinkedInUrl,
            profile?.PortfolioUrl,
            profile?.GitHubUrl,
            skills,
            education,
            experience,
            certifications,
            ComputeCompletionPercent(profile, skills, education, experience));
    }

    private static int ComputeCompletionPercent(
        CandidateProfile? profile,
        string[] skills,
        IReadOnlyList<CandidateEducationDto> education,
        IReadOnlyList<CandidateExperienceDto> experience)
    {
        if (profile is null)
        {
            return 0;
        }

        bool[] checks =
        [
            !string.IsNullOrWhiteSpace(profile.Headline),
            !string.IsNullOrWhiteSpace(profile.Bio),
            !string.IsNullOrWhiteSpace(profile.PhoneNumber),
            !string.IsNullOrWhiteSpace(profile.Location),
            !string.IsNullOrWhiteSpace(profile.PhotoUrl),
            !string.IsNullOrWhiteSpace(profile.LinkedInUrl)
                || !string.IsNullOrWhiteSpace(profile.PortfolioUrl)
                || !string.IsNullOrWhiteSpace(profile.GitHubUrl),
            skills.Length > 0,
            education.Count > 0,
            experience.Count > 0,
        ];

        return (int)Math.Round(100.0 * checks.Count(c => c) / checks.Length);
    }

    private static Dictionary<string, string[]> ValidateCandidateProfile(UpdateCandidateProfileRequest request)
    {
        var errors = new Dictionary<string, string[]>();

        AddUrlError(errors, "linkedInUrl", request.LinkedInUrl);
        AddUrlError(errors, "portfolioUrl", request.PortfolioUrl);
        AddUrlError(errors, "gitHubUrl", request.GitHubUrl);
        AddPhoneError(errors, "phoneNumber", request.PhoneNumber);
        AddLengthError(errors, "headline", request.Headline, 200);
        AddLengthError(errors, "bio", request.Bio, 2000);
        AddLengthError(errors, "location", request.Location, 200);

        if (request.Education?.Any(e => string.IsNullOrWhiteSpace(e.Institution)) == true)
        {
            errors["education"] = ["Institution is required for every education entry."];
        }

        if (request.Experience?.Any(e => string.IsNullOrWhiteSpace(e.CompanyName) || string.IsNullOrWhiteSpace(e.Title)) == true)
        {
            errors["experience"] = ["Company and title are required for every experience entry."];
        }

        if (request.Certifications?.Any(c => string.IsNullOrWhiteSpace(c.Name)) == true)
        {
            errors["certifications"] = ["Name is required for every certification entry."];
        }

        return errors;
    }

    // -------------------------------------------------------------- recruiter ---

    private static async Task<IResult> GetRecruiterProfileAsync(
        ClaimsPrincipal principal, IApplicationDbContext db, CancellationToken cancellationToken)
    {
        var user = await principal.GetCurrentUserAsync(db, cancellationToken);
        if (user is null)
        {
            return Results.Unauthorized();
        }

        var profile = await db.RecruiterProfiles
            .Include(r => r.Company)
            .FirstOrDefaultAsync(r => r.UserId == user.Id, cancellationToken);

        return Results.Ok(ToResponse(user, profile));
    }

    private static async Task<IResult> UpdateRecruiterProfileAsync(
        UpdateRecruiterProfileRequest request, ClaimsPrincipal principal, IApplicationDbContext db, CancellationToken cancellationToken)
    {
        var user = await principal.GetCurrentUserAsync(db, cancellationToken);
        if (user is null)
        {
            return Results.Unauthorized();
        }

        var errors = ValidateRecruiterProfile(request);
        if (errors.Count > 0)
        {
            return Results.ValidationProblem(errors);
        }

        var profile = await db.RecruiterProfiles
            .Include(r => r.Company)
            .FirstOrDefaultAsync(r => r.UserId == user.Id, cancellationToken);

        if (profile is null)
        {
            profile = new RecruiterProfile { UserId = user.Id };
            db.Add(profile);
        }

        profile.JobTitle = Trim(request.JobTitle);
        profile.PhoneNumber = Trim(request.PhoneNumber);
        profile.PhotoUrl = Trim(request.PhotoUrl);

        if (request.Company is not null)
        {
            var company = profile.Company;

            if (company is null)
            {
                company = new Company { Name = request.Company.Name.Trim() };
                db.Add(company);
                profile.Company = company;
                profile.CompanyId = company.Id;
            }

            company.Name = request.Company.Name.Trim();
            company.Description = Trim(request.Company.Description);
            company.Website = Trim(request.Company.Website);
            company.LogoUrl = Trim(request.Company.LogoUrl);
            company.Industry = Trim(request.Company.Industry);
            company.Location = Trim(request.Company.Location);
        }

        await db.SaveChangesAsync(cancellationToken);

        return Results.Ok(ToResponse(user, profile));
    }

    private static RecruiterProfileResponse ToResponse(ApplicationUser user, RecruiterProfile? profile) => new(
        user.Id,
        user.Email,
        user.FirstName,
        user.LastName,
        profile?.JobTitle,
        profile?.PhoneNumber,
        profile?.PhotoUrl,
        profile?.Company is { } company ? ToDto(company) : null);

    private static CompanyDto ToDto(Company company) => new(
        company.Id, company.Name, company.Description, company.Website, company.LogoUrl, company.Industry, company.Location);

    private static Dictionary<string, string[]> ValidateRecruiterProfile(UpdateRecruiterProfileRequest request)
    {
        var errors = new Dictionary<string, string[]>();

        AddPhoneError(errors, "phoneNumber", request.PhoneNumber);
        AddLengthError(errors, "jobTitle", request.JobTitle, 150);

        if (request.Company is not null)
        {
            if (string.IsNullOrWhiteSpace(request.Company.Name))
            {
                errors["company.name"] = ["Company name is required."];
            }

            AddUrlError(errors, "company.website", request.Company.Website);
            AddLengthError(errors, "company.description", request.Company.Description, 2000);
        }

        return errors;
    }

    // ----------------------------------------------------------------- shared ---

    /// <summary>
    /// Replaces a tracked child collection with an incoming DTO list: entries
    /// missing an id are inserted, entries whose id matches an existing row are
    /// updated in place, and existing rows whose id is no longer present are
    /// removed. The one save-a-whole-profile-form reconciliation strategy used
    /// by every candidate sub-collection (education/experience/certifications),
    /// which is why this is generic rather than three near-duplicate methods.
    /// </summary>
    private static void ReconcileCollection<TEntity, TDto>(
        IApplicationDbContext db,
        List<TEntity> existing,
        IReadOnlyList<TDto> incoming,
        Func<TDto, Guid?> dtoId,
        Func<TEntity, Guid> entityId,
        Func<TDto, TEntity> create,
        Action<TEntity, TDto> update)
        where TEntity : class
    {
        var incomingIds = incoming.Where(d => dtoId(d) is not null).Select(d => dtoId(d)!.Value).ToHashSet();
        var toRemove = existing.Where(e => !incomingIds.Contains(entityId(e))).ToList();

        // Removing from the parent's collection is enough: the FK is required,
        // so EF Core deletes the orphaned row on SaveChanges without an
        // explicit RemoveRange — calling both marks the same row for deletion
        // through two paths and corrupts the update batch.
        foreach (var entity in toRemove)
        {
            existing.Remove(entity);
        }

        foreach (var dto in incoming)
        {
            var id = dtoId(dto);
            var target = id is null ? null : existing.FirstOrDefault(e => entityId(e) == id);

            if (target is null)
            {
                target = create(dto);
                existing.Add(target);

                // Explicit, not implicit graph-fixup: our ids are client-
                // generated (Entity.Id defaults to a fresh Guid at
                // construction, never Guid.Empty), so EF Core can't tell a
                // brand-new entity apart from an existing, unmodified one
                // just by reaching it through the parent's collection — it
                // tracks it as Unchanged, and the property assignment below
                // then makes it look Modified instead of Added, producing an
                // UPDATE for a row that was never inserted.
                db.Add(target);
            }

            update(target, dto);
        }
    }

    private static string? Trim(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static void AddUrlError(Dictionary<string, string[]> errors, string field, string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return;
        }

        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri)
            || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            errors[field] = ["Enter a valid web address starting with http:// or https://."];
        }
    }

    // Deliberately permissive — digits, spaces and common separators only —
    // so a legitimate international number is never rejected.
    [GeneratedRegex(@"^[0-9+\-() \t]{7,20}$")]
    private static partial Regex PhoneRegex();

    private static void AddPhoneError(Dictionary<string, string[]> errors, string field, string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return;
        }

        if (!PhoneRegex().IsMatch(value))
        {
            errors[field] = ["Enter a valid phone number."];
        }
    }

    private static void AddLengthError(Dictionary<string, string[]> errors, string field, string? value, int max)
    {
        if (value is { Length: > 0 } && value.Length > max)
        {
            errors[field] = [$"Must be {max} characters or fewer."];
        }
    }
}
