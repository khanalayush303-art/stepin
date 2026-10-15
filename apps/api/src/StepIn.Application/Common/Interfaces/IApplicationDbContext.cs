using StepIn.Domain.Aidx;
using StepIn.Domain.Applications;
using StepIn.Domain.Career;
using StepIn.Domain.Companies;
using StepIn.Domain.Experience;
using StepIn.Domain.Interviews;
using StepIn.Domain.Portfolio;
using StepIn.Domain.Jobs;
using StepIn.Domain.Profiles;
using StepIn.Domain.Users;

namespace StepIn.Application.Common.Interfaces;

/// <summary>
/// The application layer's view of persistence. Deliberately narrow: it exposes
/// query access and the unit of work, never EF Core types directly, so use
/// cases never depend on EF Core. More properties are added here alongside
/// each entity in later phases.
/// </summary>
public interface IApplicationDbContext
{
    IQueryable<ApplicationUser> Users { get; }

    IQueryable<CandidateProfile> CandidateProfiles { get; }

    IQueryable<CandidateEducation> CandidateEducations { get; }

    IQueryable<CandidateExperience> CandidateExperiences { get; }

    IQueryable<CandidateCertification> CandidateCertifications { get; }

    IQueryable<RecruiterProfile> RecruiterProfiles { get; }

    IQueryable<Company> Companies { get; }

    IQueryable<Job> Jobs { get; }

    IQueryable<JobApplication> JobApplications { get; }

    IQueryable<SavedJob> SavedJobs { get; }

    IQueryable<CareerGoal> CareerGoals { get; }

    IQueryable<RoadmapItem> RoadmapItems { get; }

    IQueryable<PortfolioProject> PortfolioProjects { get; }

    IQueryable<InterviewPracticeSession> InterviewPracticeSessions { get; }

    IQueryable<InterviewPracticeQuestion> InterviewPracticeQuestions { get; }

    IQueryable<ExperienceRecord> ExperienceRecords { get; }

    IQueryable<ExperienceVerificationAudit> ExperienceVerificationAudits { get; }

    IQueryable<AidxResearchArea> AidxResearchAreas { get; }

    IQueryable<AidxProject> AidxProjects { get; }

    IQueryable<AidxResearcher> AidxResearchers { get; }

    IQueryable<AidxPublication> AidxPublications { get; }

    IQueryable<AidxPublicationAuthor> AidxPublicationAuthors { get; }

    IQueryable<AidxNews> AidxNews { get; }

    IQueryable<AidxEvent> AidxEvents { get; }

    IQueryable<AidxMembership> AidxMemberships { get; }

    IQueryable<AidxMembershipAuditEntry> AidxMembershipAuditEntries { get; }

    IQueryable<AidxContentSnapshot> AidxContentSnapshots { get; }

    IQueryable<AidxEditorialAuditEntry> AidxEditorialAuditEntries { get; }

    IQueryable<AidxProjectMilestone> AidxProjectMilestones { get; }

    IQueryable<AidxCollaborationEnquiry> AidxCollaborationEnquiries { get; }

    IQueryable<AidxEventRegistration> AidxEventRegistrations { get; }

    IQueryable<AidxSavedItem> AidxSavedItems { get; }

    /// <summary>Tracks a new entity for insertion on the next <see cref="SaveChangesAsync"/>.</summary>
    void Add<TEntity>(TEntity entity)
        where TEntity : class;

    /// <summary>Tracks entities for deletion on the next <see cref="SaveChangesAsync"/>.</summary>
    void RemoveRange<TEntity>(IEnumerable<TEntity> entities)
        where TEntity : class;

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
