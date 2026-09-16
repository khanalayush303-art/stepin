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

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
