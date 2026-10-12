namespace StepIn.Domain.Experience;

/// <summary>One row per state transition — see <see cref="ExperienceVerificationStatus"/> for the
/// full transition table each of these corresponds to.</summary>
public enum ExperienceAuditAction
{
    Submitted,
    ReviewStarted,
    Approved,
    Rejected,
    Withdrawn,
    Resubmitted,
}
