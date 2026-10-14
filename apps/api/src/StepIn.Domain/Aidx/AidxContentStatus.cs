namespace StepIn.Domain.Aidx;

/// <summary>
/// Editorial lifecycle for AIDX content. Stored as a string-converted column, so widening this
/// enum (as this one was, from the original Draft/Published/Archived) is additive at the schema
/// level. Only <see cref="Published"/> rows are ever returned by public endpoints.
/// </summary>
public enum AidxContentStatus
{
    Draft,
    Submitted,
    UnderReview,
    ChangesRequested,
    Published,
    Archived,
}
