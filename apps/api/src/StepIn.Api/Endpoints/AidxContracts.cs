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
    IReadOnlyList<AidxProjectResearcherResponse> Researchers,
    string? Outcomes = null,
    IReadOnlyList<AidxProjectMilestoneResponse>? Milestones = null);

public sealed record AidxProjectMilestoneResponse(string Title, string? Description, DateOnly? TargetDate, DateOnly? CompletedDate);

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

// ---- Admin event reads -------------------------------------------------------

/// <summary>Admin list row. Includes drafts and archived events, which the public list never returns.</summary>
public sealed record AidxAdminEventSummaryResponse(
    Guid Id,
    string Slug,
    string Title,
    string Status,
    DateTimeOffset StartsAt,
    DateTimeOffset? EndsAt,
    string? Location,
    string? SpeakerName);

/// <summary>Admin edit view. The image key is deliberately absent.</summary>
public sealed record AidxAdminEventDetailResponse(
    Guid Id,
    string Slug,
    string Title,
    string Description,
    string Status,
    DateTimeOffset StartsAt,
    DateTimeOffset? EndsAt,
    string? Location,
    string? RegistrationUrl,
    string? SpeakerName,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt,
    string? Version = null);

// ---- Admin news reads --------------------------------------------------------

/// <summary>Admin list row. Includes drafts and archived items, which the public list never returns.</summary>
public sealed record AidxAdminNewsSummaryResponse(
    Guid Id,
    string Slug,
    string Title,
    string Summary,
    string Status,
    DateTimeOffset? PublishedAt,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt);

/// <summary>Admin edit view. The author is identified by display name only. The image key is not returned.</summary>
public sealed record AidxAdminNewsDetailResponse(
    Guid Id,
    string Slug,
    string Title,
    string Summary,
    string Body,
    string Status,
    DateTimeOffset? PublishedAt,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt,
    Guid? AuthorResearcherId,
    string? AuthorName,
    string? Version = null);

// ---- Admin publication reads -------------------------------------------------

/// <summary>Admin list row. Includes unpublished publications, which the public list never returns.</summary>
public sealed record AidxAdminPublicationSummaryResponse(
    Guid Id,
    string Title,
    string PublicationType,
    int Year,
    string? Venue,
    string? Doi,
    bool Published,
    IReadOnlyList<string> Authors);

/// <summary>
/// One ordered author. Exactly one of ResearcherId or ExternalAuthorName is set. ResearcherPublished is null
/// for external authors.
/// </summary>
public sealed record AidxAdminPublicationAuthorResponse(
    int Position,
    Guid? ResearcherId,
    string? ExternalAuthorName,
    string DisplayName,
    bool? ResearcherPublished);

/// <summary>
/// Admin edit view of a publication. No PDF storage key is returned: there is no upload in this phase.
/// </summary>
public sealed record AidxAdminPublicationDetailResponse(
    Guid Id,
    string Title,
    string? Abstract,
    string PublicationType,
    string? Venue,
    int Year,
    string? Doi,
    string? ExternalUrl,
    bool Published,
    IReadOnlyList<AidxAdminPublicationAuthorResponse> Authors,
    IReadOnlyList<Guid> ResearchAreaIds,
    IReadOnlyList<AidxLinkedProjectResponse> Projects,
    string Status = "Draft",
    string? Version = null);

// ---- Admin people reads ------------------------------------------------------

/// <summary>Admin list row. Includes unpublished people, which the public list never returns.</summary>
public sealed record AidxAdminPersonSummaryResponse(
    Guid Id,
    string Slug,
    string DisplayName,
    string Category,
    string? Position,
    bool Published);

/// <summary>
/// Admin edit view of a person. No account or email fields. The storage key stays out, since there is no
/// media upload in this phase.
/// </summary>
public sealed record AidxAdminPersonDetailResponse(
    Guid Id,
    string Slug,
    string DisplayName,
    string Category,
    string? Position,
    string? Biography,
    string? OrcidUrl,
    string? GoogleScholarUrl,
    string? LinkedInUrl,
    string? WebsiteUrl,
    bool Published,
    IReadOnlyList<AidxLinkedProjectResponse> Projects,
    IReadOnlyList<AidxLinkedPublicationResponse> Publications);

public sealed record AidxLinkedProjectResponse(Guid Id, string Title, string Slug, string Status);

public sealed record AidxLinkedPublicationResponse(Guid Id, string Title, int Year);

/// <summary>Public detail for one published publication. Adds the related areas and published projects.</summary>
public sealed record AidxPublicationDetailResponse(
    Guid Id,
    string Title,
    string? Abstract,
    string PublicationType,
    string? Venue,
    int Year,
    string? Doi,
    string? ExternalUrl,
    IReadOnlyList<string> Authors,
    IReadOnlyList<AidxPublicationResearchAreaLinkResponse> ResearchAreas,
    IReadOnlyList<AidxPublicationProjectLinkResponse> Projects);

public sealed record AidxPublicationResearchAreaLinkResponse(Guid Id, string Name, string Slug);

public sealed record AidxPublicationProjectLinkResponse(Guid Id, string Title, string Slug);

// ---- Admin research area read ------------------------------------------------

/// <summary>Admin view of a research area. Adds the display order that the edit form must round-trip.</summary>
public sealed record AidxAdminResearchAreaResponse(Guid Id, string Name, string Slug, string? Description, int SortOrder);

// ---- Admin project reads ----------------------------------------------------

public sealed record AidxAdminProjectSummaryResponse(
    Guid Id,
    string Title,
    string Slug,
    string ShortDescription,
    string Status,
    bool Featured,
    DateOnly? StartDate,
    DateOnly? EndDate,
    DateTimeOffset? PublishedAt,
    DateTimeOffset? UpdatedAt,
    IReadOnlyList<string> ResearchAreas);

public sealed record AidxAdminProjectDetailResponse(
    Guid Id,
    string Title,
    string Slug,
    string ShortDescription,
    string Description,
    string Status,
    bool Featured,
    DateOnly? StartDate,
    DateOnly? EndDate,
    string? ExternalUrl,
    DateTimeOffset? PublishedAt,
    IReadOnlyList<Guid> ResearchAreaIds,
    IReadOnlyList<string> Technologies,
    IReadOnlyList<AidxAdminProjectResearcherResponse> Researchers,
    string? Version = null);

public sealed record AidxAdminProjectResearcherResponse(Guid ResearcherId, string DisplayName, string? Role);

// ---- Admin requests and responses ------------------------------------------

/// <summary>
/// <paramref name="Version"/> and <paramref name="RevisionOfId"/> are set only when the mutation
/// went through the editorial-workflow engine (<see cref="StepIn.Infrastructure.Aidx.AidxContentEditingService"/>).
/// A non-null <paramref name="RevisionOfId"/> means the write landed on a pending revision, not the
/// live canonical row — the canonical's public-facing content is unchanged until that revision is
/// approved via <c>POST /api/v1/aidx/content/{contentType}/{id}/approve</c>.
/// </summary>
public sealed record AidxIdResponse(Guid Id, string? Slug, string? Status, string? Version = null, Guid? RevisionOfId = null);

public sealed record AidxResearchAreaRequest(string Name, string? Slug, string? Description, int SortOrder);

/// <summary>
/// <paramref name="ExpectedVersion"/> is optional: when supplied, an update is rejected with 409 if
/// the canonical row has changed since the caller last read it (the same optimistic-concurrency
/// token used by the membership-gated workflow endpoints). Omitting it skips that check — a known,
/// reported gap for the legacy Admin CMS forms, which do not yet send it (see aidx-migration-plan.md).
/// </summary>
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
    IReadOnlyList<AidxProjectResearcherInput>? Researchers,
    string? ExpectedVersion = null,
    string? Outcomes = null);

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

/// <summary>
/// There is deliberately no <c>Published</c> field here: a client can never set a publication's live
/// status directly. Every publication is created Draft and moves to Published only through
/// <c>POST /api/v1/admin/aidx/publications/{id}/publish</c>, which drives it through the same
/// editorial-workflow engine as every other content type and keeps the legacy <c>Published</c> bool
/// and the new <c>Status</c> enum in lockstep — removing the two-independent-fields bypass that
/// previously let them diverge.
/// </summary>
public sealed record AidxPublicationRequest(
    string Title,
    string? Abstract,
    string PublicationType,
    string? Venue,
    int Year,
    string? Doi,
    string? ExternalUrl,
    IReadOnlyList<AidxAuthorInput>? Authors,
    IReadOnlyList<Guid>? ResearchAreaIds = null,
    IReadOnlyList<Guid>? ProjectIds = null,
    string? ExpectedVersion = null);

/// <summary>Exactly one of <see cref="ResearcherId"/> or <see cref="ExternalAuthorName"/> must be set.</summary>
public sealed record AidxAuthorInput(Guid? ResearcherId, string? ExternalAuthorName);

public sealed record AidxNewsRequest(string Title, string? Slug, string Summary, string Body, Guid? AuthorResearcherId, string? ExpectedVersion = null);

public sealed record AidxEventRequest(
    string Title,
    string? Slug,
    string Description,
    DateTimeOffset StartsAt,
    DateTimeOffset? EndsAt,
    string? Location,
    string? RegistrationUrl,
    string? SpeakerName,
    string? ExpectedVersion = null);
