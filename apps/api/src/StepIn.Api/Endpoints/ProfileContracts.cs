namespace StepIn.Api.Endpoints;

public sealed record CandidateEducationDto(
    Guid? Id,
    string Institution,
    string? Degree,
    string? FieldOfStudy,
    DateOnly? StartDate,
    DateOnly? EndDate,
    string? Description);

public sealed record CandidateExperienceDto(
    Guid? Id,
    string CompanyName,
    string Title,
    DateOnly? StartDate,
    DateOnly? EndDate,
    string? Description);

public sealed record CandidateCertificationDto(
    Guid? Id,
    string Name,
    string? IssuingOrganization,
    DateOnly? IssueDate,
    string? CredentialUrl);

public sealed record CandidateProfileResponse(
    Guid UserId,
    string Email,
    string FirstName,
    string LastName,
    string? PhoneNumber,
    string? Location,
    string? Headline,
    string? Bio,
    string? PhotoUrl,
    string? LinkedInUrl,
    string? PortfolioUrl,
    string? GitHubUrl,
    IReadOnlyList<string> Skills,
    IReadOnlyList<CandidateEducationDto> Education,
    IReadOnlyList<CandidateExperienceDto> Experience,
    IReadOnlyList<CandidateCertificationDto> Certifications,
    int ProfileCompletionPercent);

public sealed record UpdateCandidateProfileRequest(
    string? PhoneNumber,
    string? Location,
    string? Headline,
    string? Bio,
    string? PhotoUrl,
    string? LinkedInUrl,
    string? PortfolioUrl,
    string? GitHubUrl,
    IReadOnlyList<string>? Skills,
    IReadOnlyList<CandidateEducationDto>? Education,
    IReadOnlyList<CandidateExperienceDto>? Experience,
    IReadOnlyList<CandidateCertificationDto>? Certifications);

public sealed record CompanyDto(
    Guid? Id,
    string Name,
    string? Description,
    string? Website,
    string? LogoUrl,
    string? Industry,
    string? Location);

public sealed record RecruiterProfileResponse(
    Guid UserId,
    string Email,
    string FirstName,
    string LastName,
    string? JobTitle,
    string? PhoneNumber,
    string? PhotoUrl,
    CompanyDto? Company);

public sealed record UpdateRecruiterProfileRequest(
    string? JobTitle,
    string? PhoneNumber,
    string? PhotoUrl,
    CompanyDto? Company);
