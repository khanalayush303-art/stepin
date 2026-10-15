using StepIn.Domain.Common;

namespace StepIn.Domain.Aidx;

/// <summary>
/// A registration for an AIDX event, signed-in or anonymous. Anonymous cancellation requires
/// possessing the original <see cref="CancellationTokenHash"/>'s plaintext (returned once, at
/// registration time, never persisted in plaintext and never re-issued by supplying an email) —
/// not merely knowing the registrant's email, which is guessable/enumerable.
/// </summary>
public sealed class AidxEventRegistration : Entity
{
    public required Guid EventId { get; set; }

    /// <summary>Set for a signed-in registrant; null for anonymous.</summary>
    public Guid? UserId { get; set; }

    public required string Name { get; set; }

    /// <summary>Lowercased and trimmed before storage/comparison — the normalized duplicate
    /// identity for anonymous registrations.</summary>
    public required string Email { get; set; }

    public AidxEventRegistrationStatus Status { get; set; } = AidxEventRegistrationStatus.Registered;

    /// <summary>SHA-256 of a crypto-random 32-byte token. Compared with a fixed-time comparison,
    /// never the plaintext. Null for a signed-in registrant, who cancels via UserId ownership
    /// instead.</summary>
    public string? CancellationTokenHash { get; set; }
}
