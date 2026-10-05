namespace StepIn.Api.Endpoints;

// ---- Public responses -------------------------------------------------------
// Storage keys (ImageKey, ProfileImageKey, PdfKey) are deliberately absent: AIDX has
// no media upload in this phase, so they are not exposed to clients.

public sealed record AidxPageResponse<T>(IReadOnlyList<T> Items, int Page, int PageSize, int TotalCount)
{
    public int TotalPages => PageSize == 0 ? 0 : (int)Math.Ceiling(TotalCount / (double)PageSize);
}

/// <summary>An AIDX research opportunity. It is a StepIn <c>Job</c> with <c>Category = Research</c>.</summary>
public sealed record AidxOpportunityResponse(
    Guid Id,
    string Title,
    string CompanyName,
    string Location,
    string EmploymentType,
    string WorkplaceType,
    string? Compensation,
    IReadOnlyList<string> Skills,
    DateTimeOffset? PublishedAt,
    string? ProjectSlug,
    string? ProjectTitle);

public sealed record AidxResearchAreaResponse(Guid Id, string Name, string Slug, string? Description);

public sealed record AidxProjectSummaryResponse(
    Guid Id,
    string Title,
    string Slug,
    string ShortDescription,
    bool Featured,
    DateOnly? StartDate,
    DateOnly? EndDate,
    DateTimeOffset? PublishedAt,
    IReadOnlyList<string> ResearchAreas);

public sealed record AidxProjectDetailResponse(
    Guid Id,
    string Title,
    string Slug,
    string ShortDescription,
    string Description,
    bool Featured,
    DateOnly? StartDate,
    DateOnly? EndDate,
    string? ExternalUrl,
    DateTimeOffset? PublishedAt,
    IReadOnlyList<string> ResearchAreas,
    IReadOnlyList<string> Technologies,
    IReadOnlyList<AidxProjectResearcherResponse> Researchers);

public sealed record AidxProjectResearcherResponse(Guid Id, string Slug, string DisplayName, string? Role);

public sealed record AidxResearcherResponse(
    Guid Id,
    string Slug,
    string DisplayName,
    string Category,
    string? Position,
    string? Biography,
    string? OrcidUrl,
    string? GoogleScholarUrl,
    string? LinkedInUrl,
    string? WebsiteUrl);

public sealed record AidxPublicationResponse(
    Guid Id,
    string Title,
    string? Abstract,
    string PublicationType,
    string? Venue,
    int Year,
    string? Doi,
    string? ExternalUrl,
    IReadOnlyList<string> Authors);

public sealed record AidxNewsSummaryResponse(Guid Id, string Slug, string Title, string Summary, DateTimeOffset? PublishedAt);

public sealed record AidxNewsDetailResponse(Guid Id, string Slug, string Title, string Summary, string Body, DateTimeOffset? PublishedAt);

public sealed record AidxEventResponse(
    Guid Id,
    string Slug,
    string Title,
    string Description,
    DateTimeOffset StartsAt,
    DateTimeOffset? EndsAt,
    string? Location,
    string? RegistrationUrl,
    string? SpeakerName);

// ---- Admin requests and responses ------------------------------------------

public sealed record AidxIdResponse(Guid Id, string? Slug, string? Status);

public sealed record AidxResearchAreaRequest(string Name, string? Slug, string? Description, int SortOrder);

public sealed record AidxProjectRequest(
    string Title,
    string? Slug,
    string ShortDescription,
    string Description,
    DateOnly? StartDate,
    DateOnly? EndDate,
    string? ExternalUrl,
    bool Featured,
    IReadOnlyList<Guid>? ResearchAreaIds,
    IReadOnlyList<string>? Technologies,
    IReadOnlyList<AidxProjectResearcherInput>? Researchers);

public sealed record AidxProjectResearcherInput(Guid ResearcherId, string? Role);

public sealed record AidxResearcherRequest(
    string DisplayName,
    string? Slug,
    string Category,
    string? Position,
    string? Biography,
    string? OrcidUrl,
    string? GoogleScholarUrl,
    string? LinkedInUrl,
    string? WebsiteUrl,
    bool Published);

public sealed record AidxPublicationRequest(
    string Title,
    string? Abstract,
    string PublicationType,
    string? Venue,
    int Year,
    string? Doi,
    string? ExternalUrl,
    bool Published,
    IReadOnlyList<AidxAuthorInput>? Authors,
    IReadOnlyList<Guid>? ResearchAreaIds = null,
    IReadOnlyList<Guid>? ProjectIds = null);

/// <summary>Exactly one of <see cref="ResearcherId"/> or <see cref="ExternalAuthorName"/> must be set.</summary>
public sealed record AidxAuthorInput(Guid? ResearcherId, string? ExternalAuthorName);

public sealed record AidxNewsRequest(string Title, string? Slug, string Summary, string Body, Guid? AuthorResearcherId);

public sealed record AidxEventRequest(
    string Title,
    string? Slug,
    string Description,
    DateTimeOffset StartsAt,
    DateTimeOffset? EndsAt,
    string? Location,
    string? RegistrationUrl,
    string? SpeakerName);
