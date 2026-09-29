using StepIn.Domain.Common;
using StepIn.Domain.Jobs;

namespace StepIn.Domain.Applications;

/// <summary>
/// A candidate's application against a published job. <see cref="CandidateProfileId"/>
/// and <see cref="JobId"/> are set once, server-side, at creation from the
/// authenticated candidate and the route's job id — never from client input —
/// and never change afterward; applications are immutable once submitted
/// (no update/delete endpoint exists in Phase 2.4). <see cref="ResumeStorageKey"/>
/// is an opaque, server-generated key into <c>IResumeStorage</c>, never a
/// filesystem path and never client-supplied. <see cref="Entity.CreatedAt"/>
/// doubles as "submitted at" — submission and creation are the same moment.
/// </summary>
public sealed class JobApplication : Entity
{
    public required Guid CandidateProfileId { get; set; }

    public required Guid JobId { get; set; }

    public Job Job { get; set; } = null!;

    public string? CoverLetter { get; set; }

    public required string ResumeStorageKey { get; set; }

    public required string ResumeOriginalFileName { get; set; }

    public required string ResumeContentType { get; set; }

    public required long ResumeSizeBytes { get; set; }

    public JobApplicationStatus Status { get; set; } = JobApplicationStatus.Submitted;
}
