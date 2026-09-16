using StepIn.Domain.Common;

namespace StepIn.Domain.Users;

/// <summary>
/// The application's own user record, keyed to a Clerk identity via <see cref="ClerkUserId"/>.
/// Clerk owns credentials/sessions; this is the authorization- and business-facing profile.
/// </summary>
public sealed class ApplicationUser : Entity
{
    public required string ClerkUserId { get; set; }

    public required string Email { get; set; }

    public required string FirstName { get; set; }

    public required string LastName { get; set; }

    /// <summary>Null until the user completes account setup after their first sign-in.</summary>
    public UserRole? Role { get; set; }

    public AccountStatus AccountStatus { get; set; } = AccountStatus.Active;
}
