using StepIn.Domain.Jobs;

namespace StepIn.Domain.Aidx;

/// <summary>The resolved ownership records for AIDX Lab. Never built from client input.</summary>
public sealed record AidxSystemOwnership(Guid UserId, Guid CompanyId, Guid RecruiterProfileId);

/// <summary>
/// Pure ownership rules shared by every AIDX opportunity operation. No EF Core, so they can be
/// tested directly and reused by future endpoints without re-deriving the rule.
/// </summary>
public static class AidxOwnershipRules
{
    /// <summary>
    /// True only for a Research Job owned by the AIDX system recruiter at the AIDX Lab company.
    /// Career jobs and jobs owned by any other recruiter return false, even when the category matches.
    /// </summary>
    public static bool IsAidxResearchJob(Job job, AidxSystemOwnership ownership)
    {
        ArgumentNullException.ThrowIfNull(job);
        ArgumentNullException.ThrowIfNull(ownership);

        return OwnedByAidx(ownership).Compile()(job);
    }

    /// <summary>
    /// The same rule as an EF-translatable predicate, so queries and single-item checks cannot drift apart.
    /// </summary>
    public static System.Linq.Expressions.Expression<Func<Job, bool>> OwnedByAidx(AidxSystemOwnership ownership)
    {
        ArgumentNullException.ThrowIfNull(ownership);

        var recruiterProfileId = ownership.RecruiterProfileId;
        var companyId = ownership.CompanyId;

        return job => job.Category == JobCategory.Research
            && job.RecruiterProfileId == recruiterProfileId
            && job.CompanyId == companyId;
    }
}
