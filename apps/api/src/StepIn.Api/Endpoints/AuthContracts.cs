namespace StepIn.Api.Endpoints;

public sealed record RegisterRequest(
    string Email,
    string Password,
    string ConfirmPassword,
    string FirstName,
    string LastName,
    string Role);

public sealed record LoginRequest(string Email, string Password, bool RememberMe = true);

public sealed record ForgotPasswordRequest(string Email);

public sealed record ResetPasswordRequest(string Email, string Token, string NewPassword, string ConfirmPassword);

public sealed record VerifyEmailRequest(string Email, string Token);

public sealed record ResendVerificationRequest(string Email);

public sealed record AccountSetupRequest(string Role);

public sealed record CurrentUserResponse(
    Guid Id,
    string Email,
    string FirstName,
    string LastName,
    string Role,
    bool EmailConfirmed,
    string AccountStatus);

public sealed record PasswordPolicyResponse(
    int RequiredLength,
    bool RequireDigit,
    bool RequireLowercase,
    bool RequireUppercase,
    bool RequireNonAlphanumeric);
