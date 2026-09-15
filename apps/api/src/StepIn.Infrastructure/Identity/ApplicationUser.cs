using Microsoft.AspNetCore.Identity;
using StepIn.Domain.Common;

namespace StepIn.Infrastructure.Identity;

/// <summary>
/// The platform account. Identity owns credentials, email confirmation and
/// lockout state; these are the fields Phase 1 needs beyond that.
/// </summary>
public sealed class ApplicationUser : IdentityUser<Guid>
{
    public required string FirstName { get; set; }

    public required string LastName { get; set; }

    public AccountStatus AccountStatus { get; set; } = AccountStatus.Active;

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset? UpdatedAt { get; set; }
}
