namespace StepIn.Domain.Common;

/// <summary>The platform's three roles. Assigned once via account setup; Admin is never client-settable.</summary>
public enum UserRole
{
    Applicant,
    Recruiter,
    Admin,
}
