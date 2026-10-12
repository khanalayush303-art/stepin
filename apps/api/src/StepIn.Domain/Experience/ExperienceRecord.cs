using StepIn.Domain.Common;

namespace StepIn.Domain.Experience;

/// <summary>
/// A candidate's own record of experience, submitted for optional StepIn admin verification.
/// <see cref="CandidateProfileId"/> is set once, server-side, from the authenticated candidate's
/// own profile — never from client input — the same FK-only ownership pattern as
/// <see cref="StepIn.Domain.Portfolio.PortfolioProject.CandidateProfileId"/>.
///
/// <see cref="Status"/> only ever changes through the explicit transitions documented on
/// <see cref="ExperienceVerificationStatus"/>, enforced server-side — never accepted directly
/// from a client request. The record is editable by its owner only while Draft, Rejected, or
/// Withdrawn; it is never editable in place while Submitted, UnderReview, or Verified.
///
/// <see cref="EvidenceUrl"/> is candidate-provided and never independently verified — supplying
/// one is not itself proof of anything, and nothing in this type or its endpoints claims
/// otherwise. A Verified status reflects only that an authorized StepIn administrator reviewed
/// and approved the record through <see cref="ExperienceVerificationAudit"/> — never that an
/// employer or institution confirmed it, unless that confirmation genuinely occurred through some
/// other channel this phase does not implement.
///
/// <see cref="Visibility"/> defaults to Private and is independent of <see cref="Status"/> — see
/// <see cref="ExperienceVisibility"/>. A Public record's real <see cref="Status"/> is always shown
/// alongside it; nothing ever presents Public as a synonym for Verified.
/// </summary>
public sealed class ExperienceRecord : Entity
{
    public required Guid CandidateProfileId { get; set; }

    public required ExperienceRecordType RecordType { get; set; }

    public required string Title { get; set; }

    public string? Organization { get; set; }

    public string? Description { get; set; }

    public DateOnly? StartDate { get; set; }

    public DateOnly? EndDate { get; set; }

    public string[] Skills { get; set; } = [];

    public string? EvidenceUrl { get; set; }

    public ExperienceVisibility Visibility { get; set; } = ExperienceVisibility.Private;

    public ExperienceVerificationStatus Status { get; set; } = ExperienceVerificationStatus.Draft;

    /// <summary>Set server-side exactly on the transition into Submitted (including a resubmit);
    /// never trusted from client input.</summary>
    public DateTimeOffset? SubmittedAt { get; set; }

    public List<ExperienceVerificationAudit> AuditEntries { get; set; } = [];
}
