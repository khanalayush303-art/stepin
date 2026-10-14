using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using StepIn.Domain.Aidx;
using StepIn.Domain.Common;
using StepIn.Infrastructure.Persistence;

namespace StepIn.Api.Operations;

/// <summary>
/// One-off operator command that grants the first AIDX LabManager:
///
///     dotnet StepIn.Api.dll aidx-bootstrap-manager --user-id &lt;ApplicationUser Guid&gt;
///     dotnet StepIn.Api.dll aidx-bootstrap-manager --clerk-user-id &lt;user_...&gt;
///
/// Email is deliberately never accepted as a selector:
/// <c>20260920111926_MakeEmailIndexNonUnique.cs</c> means email cannot be
/// trusted to resolve to exactly one account.
///
/// Refuses (no write) if any Active LabManager already exists — this makes
/// the command a one-time bootstrap, not a repeatable backdoor. Recovery if
/// every LabManager is later revoked is this same command, re-run by
/// someone with server/ops access (its guard is "no currently Active
/// LabManager," not "has never run") — never a public HTTP route.
///
/// A transaction-scoped PostgreSQL advisory lock (<c>pg_advisory_xact_lock</c>)
/// guards the check-and-insert so two simultaneous invocations cannot both
/// succeed. Transaction-scoped (not session-held like
/// <see cref="MigrateDatabaseCommand"/>'s) because this is a short
/// check-and-insert from a pooled connection, not a long migration — the
/// lock releases automatically on commit, rollback, or an unhandled
/// exception, with no manual unlock bookkeeping.
///
/// Not an HTTP endpoint, needs no Clerk login, and is not run by startup.
///
/// Exit codes: 0 = granted; 1 = failure; 2 = could not connect;
/// 3 = a safety precondition failed (user not found, user not Active, an
/// Active LabManager already exists, ambiguous arguments) — nothing written;
/// 4 = missing/invalid arguments.
/// </summary>
public static class AidxBootstrapManagerCommand
{
    public const string Name = "aidx-bootstrap-manager";

    public const int ExitOk = 0;
    public const int ExitFailed = 1;
    public const int ExitCannotConnect = 2;
    public const int ExitUnsafePrecondition = 3;
    public const int ExitMissingArgument = 4;

    private const string AdvisoryLockKey = "stepin_aidx_bootstrap_manager";

    public static async Task<int> RunAsync(
        IServiceProvider services,
        TextWriter output,
        string? userIdArgument,
        string? clerkUserIdArgument,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(output);

        var haveUserId = !string.IsNullOrWhiteSpace(userIdArgument);
        var haveClerkUserId = !string.IsNullOrWhiteSpace(clerkUserIdArgument);

        if (haveUserId == haveClerkUserId) // neither, or both
        {
            await output.WriteLineAsync(
                $"Usage: dotnet StepIn.Api.dll {Name} --user-id <Guid>  OR  {Name} --clerk-user-id <user_...>");
            await output.WriteLineAsync("Exactly one selector must be given. Email is not accepted as a selector.");
            return ExitMissingArgument;
        }

        Guid parsedUserId = default;
        if (haveUserId && !Guid.TryParse(userIdArgument, out parsedUserId))
        {
            await output.WriteLineAsync($"'{userIdArgument}' is not a valid Guid.");
            return ExitMissingArgument;
        }

        await using var scope = services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        try
        {
            if (!await context.Database.CanConnectAsync(cancellationToken))
            {
                await output.WriteLineAsync("Cannot connect to the configured database. No changes were made.");
                return ExitCannotConnect;
            }

            // Npgsql's retrying execution strategy refuses a bare user-initiated transaction (it can't
            // safely retry one), so the whole lock-check-insert-commit unit runs as a single retriable
            // operation through the execution strategy, exactly as EF Core's own docs require.
            var strategy = context.Database.CreateExecutionStrategy();
            return await strategy.ExecuteAsync(async () =>
            {
                await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);

                // pg_advisory_xact_lock returns void (it blocks until acquired, then returns nothing to
                // compare), so this is executed as a command, not queried as a scalar like
                // pg_try_advisory_lock elsewhere in this codebase.
                await context.Database
                    .ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock(hashtext({AdvisoryLockKey}))", cancellationToken);

                var existingManager = await context.AidxMemberships
                    .FirstOrDefaultAsync(
                        m => m.Role == AidxLabRole.LabManager && m.Status == AidxMembershipStatus.Active,
                        cancellationToken);

                if (existingManager is not null)
                {
                    await output.WriteLineAsync(
                        "An Active LabManager already exists. Refusing to proceed — this command is a one-time " +
                        "bootstrap, not a repeatable grant. No changes were made.");
                    return ExitUnsafePrecondition;
                }

                var user = haveUserId
                    ? await context.Users.FirstOrDefaultAsync(u => u.Id == parsedUserId, cancellationToken)
                    : await context.Users.FirstOrDefaultAsync(u => u.ClerkUserId == clerkUserIdArgument, cancellationToken);

                if (user is null)
                {
                    await output.WriteLineAsync("No matching ApplicationUser found. No changes were made.");
                    return ExitUnsafePrecondition;
                }

                if (user.AccountStatus != AccountStatus.Active)
                {
                    await output.WriteLineAsync("That account is not Active. No changes were made.");
                    return ExitUnsafePrecondition;
                }

                var existingMembership = await context.AidxMemberships
                    .FirstOrDefaultAsync(m => m.UserId == user.Id, cancellationToken);

                if (existingMembership is not null)
                {
                    existingMembership.Role = AidxLabRole.LabManager;
                    existingMembership.Status = AidxMembershipStatus.Active;
                    existingMembership.ScopedResearchAreaId = null;
                    existingMembership.RevokedAt = null;
                }
                else
                {
                    existingMembership = new AidxMembership
                    {
                        UserId = user.Id,
                        Role = AidxLabRole.LabManager,
                        Status = AidxMembershipStatus.Active,
                        GrantedByUserId = user.Id, // self-bootstrap: no other actor exists yet to attribute this to
                    };
                    context.AidxMemberships.Add(existingMembership);
                }

                await context.SaveChangesAsync(cancellationToken);

                context.AidxMembershipAuditEntries.Add(new AidxMembershipAuditEntry
                {
                    MembershipId = existingMembership.Id,
                    Action = AidxMembershipAuditAction.Granted,
                    PerformedByUserId = user.Id,
                    NewRole = AidxLabRole.LabManager,
                });
                await context.SaveChangesAsync(cancellationToken);

                await transaction.CommitAsync(cancellationToken);

                await output.WriteLineAsync($"Granted AIDX LabManager to ApplicationUser {user.Id}.");
                return ExitOk;
            });
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            await output.WriteLineAsync(
                $"Bootstrap failed ({ex.GetType().Name}). Check database connectivity and the logs.");
            return ExitFailed;
        }
    }
}
