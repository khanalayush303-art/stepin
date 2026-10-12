namespace StepIn.Domain.Experience;

/// <summary>
/// Deliberately independent of <see cref="ExperienceVerificationStatus"/>: Public never implies
/// Verified, and Verified never implies Public — the two concepts are orthogonal by design, so
/// wherever a Public record is shown to someone other than its owner, its real status is shown
/// alongside it rather than anything being inferred from visibility alone.
/// </summary>
public enum ExperienceVisibility
{
    Private,
    Public,
}
