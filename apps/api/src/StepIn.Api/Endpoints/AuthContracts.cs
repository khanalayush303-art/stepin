namespace StepIn.Api.Endpoints;

public sealed record AccountSetupRequest(string Role);

public sealed record CurrentUserResponse(
    Guid Id,
    string Email,
    string FirstName,
    string LastName,
    string? Role,
    bool EmailVerified,
    string AccountStatus);
