namespace StepIn.Domain.Aidx;

/// <summary>
/// Canonical identifiers for the AIDX Lab system owner. AIDX Research Jobs are owned by a
/// backend-only recruiter profile, because the existing <c>Job</c> model requires one.
/// This identity exists for ownership only. It is never a sign-in account.
/// </summary>
public static class AidxSystemIdentity
{
    /// <summary>
    /// Stored in <c>ApplicationUser.ClerkUserId</c>. Clerk user IDs always start with <c>user_</c>,
    /// so no Clerk-issued token can ever carry this subject.
    /// </summary>
    public const string ClerkUserId = "system:aidx-lab";

    public const string Email = "aidx-lab@system.invalid";

    public const string FirstName = "AIDX";

    public const string LastName = "Lab";

    /// <summary>The single AIDX Lab company. Identified through the system recruiter profile, not by name.</summary>
    public const string CompanyName = "AIDX Lab";

    private const string ClerkUserIdPrefix = "system:";

    /// <summary>True for any ClerkUserId in the reserved system namespace. Such a subject must never be synced as a person.</summary>
    public static bool IsSystemClerkUserId(string? clerkUserId) =>
        clerkUserId is not null && clerkUserId.StartsWith(ClerkUserIdPrefix, StringComparison.Ordinal);
}
