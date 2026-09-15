namespace StepIn.Infrastructure.Identity;

/// <summary>The three platform roles. Admin is never self-assignable at registration.</summary>
public static class RoleNames
{
    public const string Applicant = "Applicant";
    public const string Recruiter = "Recruiter";
    public const string Admin = "Admin";

    public static readonly IReadOnlyList<string> All = [Applicant, Recruiter, Admin];
}
