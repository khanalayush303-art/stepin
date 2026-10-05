namespace StepIn.Domain.Aidx;

/// <summary>
/// Publishing lifecycle shared by AIDX content that is published to the public site.
/// Only <see cref="Published"/> rows are ever returned by public endpoints.
/// </summary>
public enum AidxContentStatus
{
    Draft,
    Published,
    Archived,
}
