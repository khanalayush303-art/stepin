using Microsoft.EntityFrameworkCore;
using StepIn.Domain.Aidx;
using StepIn.Domain.Common;
using StepIn.Domain.Companies;
using StepIn.Domain.Profiles;
using StepIn.Domain.Users;
using StepIn.Infrastructure.Persistence;

namespace StepIn.Infrastructure.Aidx;

/// <summary>
/// Resolves and, when asked, creates the AIDX system owner: a system user, the AIDX Lab company,
/// and the system recruiter profile linking them.
///
/// The canonical company is the one the system recruiter profile points to, never one found by
/// name. A real recruiter may already have a company called "AIDX Lab", and that must not be
/// adopted.
///
/// Inconsistent state throws. It is never silently repaired, because ownership drift would
/// mis-attribute opportunities.
/// </summary>
public sealed class AidxSystemOwnershipService(ApplicationDbContext db)
{
    /// <summary>Read-only. Returns null when the system owner has not been initialised yet.</summary>
    public async Task<AidxSystemOwnership?> FindAsync(CancellationToken cancellationToken = default)
    {
        var user = await db.Users.SingleOrDefaultAsync(u => u.ClerkUserId == AidxSystemIdentity.ClerkUserId, cancellationToken);
        if (user is null)
        {
            return null;
        }

        var profile = await db.RecruiterProfiles.SingleOrDefaultAsync(r => r.UserId == user.Id, cancellationToken);
        if (profile is null)
        {
            return null;
        }

        return await ValidateAsync(user, profile, cancellationToken);
    }

    /// <summary>
    /// Idempotent. Creates whatever is missing and returns the same ids on every later call.
    /// Safe to run more than once, including sequentially. Two simultaneous first runs can collide
    /// on a unique index; the loser's exception is the expected outcome, and a retry then succeeds.
    /// </summary>
    public async Task<AidxSystemOwnership> EnsureAsync(CancellationToken cancellationToken = default)
    {
        var user = await db.Users.SingleOrDefaultAsync(u => u.ClerkUserId == AidxSystemIdentity.ClerkUserId, cancellationToken);

        if (user is null)
        {
            user = new ApplicationUser
            {
                ClerkUserId = AidxSystemIdentity.ClerkUserId,
                Email = AidxSystemIdentity.Email,
                FirstName = AidxSystemIdentity.FirstName,
                LastName = AidxSystemIdentity.LastName,
                // Suspended: even if this row were ever matched, no active-status check would pass it.
                AccountStatus = AccountStatus.Suspended,
            };
            db.Add(user);
            await db.SaveChangesAsync(cancellationToken);
        }

        var profile = await db.RecruiterProfiles.SingleOrDefaultAsync(r => r.UserId == user.Id, cancellationToken);

        if (profile is null)
        {
            var company = new Company { Name = AidxSystemIdentity.CompanyName };
            db.Add(company);

            profile = new RecruiterProfile { UserId = user.Id, CompanyId = company.Id };
            db.Add(profile);
            await db.SaveChangesAsync(cancellationToken);
        }

        return await ValidateAsync(user, profile, cancellationToken)
            ?? throw new InvalidOperationException("AIDX system owner could not be resolved after initialisation.");
    }

    private async Task<AidxSystemOwnership> ValidateAsync(ApplicationUser user, RecruiterProfile profile, CancellationToken cancellationToken)
    {
        if (user.Role is not null)
        {
            throw new InvalidOperationException("AIDX system identity must not hold a platform role.");
        }

        if (user.AccountStatus != AccountStatus.Suspended)
        {
            throw new InvalidOperationException("AIDX system identity must remain suspended so it can never be used as an active account.");
        }

        if (profile.CompanyId is not { } companyId)
        {
            throw new InvalidOperationException("AIDX system recruiter profile has no company.");
        }

        var company = await db.Companies.SingleOrDefaultAsync(c => c.Id == companyId, cancellationToken)
            ?? throw new InvalidOperationException("AIDX system recruiter profile points at a company that does not exist.");

        if (company.Name != AidxSystemIdentity.CompanyName)
        {
            throw new InvalidOperationException("AIDX system company has been renamed; refusing to treat it as AIDX Lab.");
        }

        return new AidxSystemOwnership(user.Id, company.Id, profile.Id);
    }
}
