namespace StepIn.Api.Endpoints;

public sealed record AidxMembershipMeResponse(
    bool HasMembership,
    string? Role,
    string? Status,
    Guid? ScopedResearchAreaId);

public sealed record AidxMembershipResponse(
    Guid Id,
    Guid UserId,
    string UserEmail,
    string Role,
    string Status,
    Guid? ScopedResearchAreaId,
    DateTimeOffset CreatedAt,
    DateTimeOffset? RevokedAt);

public sealed record GrantAidxMembershipRequest(Guid UserId, string Role, Guid? ScopedResearchAreaId);

public sealed record UpdateAidxMembershipRequest(string Role, Guid? ScopedResearchAreaId);

public sealed record LinkResearcherToUserRequest(Guid UserId);

/// <summary>Minimal fields needed to pick the right account when granting a membership or linking
/// a researcher — never exposes anything beyond name/email, and only to someone who already passed
/// AuthorizeManagerAsync's CanManageMemberships/CanLinkResearcherToUser check.</summary>
public sealed record AidxUserSearchResultResponse(Guid Id, string Email, string FirstName, string LastName);
