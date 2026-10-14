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
