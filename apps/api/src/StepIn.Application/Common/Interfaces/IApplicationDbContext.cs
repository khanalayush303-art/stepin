namespace StepIn.Application.Common.Interfaces;

/// <summary>
/// The application layer's view of persistence. Deliberately narrow: it exposes
/// the unit of work and nothing else, so use cases never depend on EF Core.
/// DbSet properties are added here alongside each entity in later phases.
///
/// Identity (users/roles) is a framework/EF concern owned entirely by
/// Infrastructure via ASP.NET Core Identity's own APIs (UserManager,
/// SignInManager, RoleManager) — it deliberately does not appear on this seam.
/// </summary>
public interface IApplicationDbContext
{
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
