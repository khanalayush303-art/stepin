namespace StepIn.Domain.Aidx;

/// <summary>
/// A lab-scoped permission, separate from <see cref="StepIn.Domain.Common.UserRole"/>.
/// A person contributes to AIDX without ever becoming a platform Admin.
/// </summary>
public enum AidxLabRole
{
    Contributor,
    Editor,
    LabManager,
}
