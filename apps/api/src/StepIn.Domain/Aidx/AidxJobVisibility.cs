using System.Linq.Expressions;
using StepIn.Domain.Jobs;

namespace StepIn.Domain.Aidx;

/// <summary>
/// Public visibility for a job. A published job is public, but a Research job linked to an AIDX
/// project is only public while that project is published. Used by direct job detail, application
/// submission and the AIDX opportunity list, so the three cannot drift apart.
/// </summary>
public static class AidxJobVisibility
{
    public static Expression<Func<Job, bool>> IsPubliclyVisible =>
        job => job.Status == JobStatus.Published
            && (job.AidxProjectId == null || job.AidxProject!.Status == AidxContentStatus.Published);
}
