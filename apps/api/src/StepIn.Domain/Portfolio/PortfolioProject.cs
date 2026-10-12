using StepIn.Domain.Aidx;
using StepIn.Domain.Common;

namespace StepIn.Domain.Portfolio;

/// <summary>
/// Evidence of a candidate's own work. <see cref="CandidateProfileId"/> is set once, server-side,
/// from the authenticated candidate — never from client input — and never changes after creation,
/// the same FK-only ownership pattern as <see cref="StepIn.Domain.Applications.JobApplication.CandidateProfileId"/>
/// and <see cref="StepIn.Domain.Career.CareerGoal.CandidateProfileId"/>.
///
/// <see cref="Visibility"/> defaults to Private: a project is never visible to anyone but its
/// owner unless the candidate explicitly sets it Public. A recruiter only ever sees a candidate's
/// Public projects, and only through the existing recruiter-application ownership chain
/// (RecruiterProfile → owned Job → JobApplication → CandidateProfile) — there is no standalone
/// public portfolio-browsing endpoint, deliberately, to avoid a new public user-discovery surface.
///
/// <see cref="RepositoryUrl"/> and <see cref="DemoUrl"/> are candidate-provided, not independently
/// verified — nothing in this type or its endpoints claims otherwise.
/// </summary>
public sealed class PortfolioProject : Entity
{
    public required Guid CandidateProfileId { get; set; }

    public required string Title { get; set; }

    public required string Description { get; set; }

    public string[] Skills { get; set; } = [];

    public DateOnly? StartDate { get; set; }

    public DateOnly? EndDate { get; set; }

    public string? RepositoryUrl { get; set; }

    public string? DemoUrl { get; set; }

    public PortfolioVisibility Visibility { get; set; } = PortfolioVisibility.Private;

    /// <summary>Optional: a published AIDX project this work relates to. Never an unpublished one
    /// — enforced server-side at creation/update, not by this FK alone.</summary>
    public Guid? ResearchProjectId { get; set; }

    public AidxProject? ResearchProject { get; set; }
}
