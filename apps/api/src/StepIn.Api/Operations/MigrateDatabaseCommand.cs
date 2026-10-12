using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using StepIn.Infrastructure.Persistence;

namespace StepIn.Api.Operations;

/// <summary>
/// One-off operator command that applies pending EF Core migrations using the application's own,
/// already-configured <c>ConnectionStrings__Postgres</c> — exactly the same configuration the API
/// itself runs on, read from whatever environment/secret store already supplies it. No connection
/// string, password, or other secret is ever accepted as an argument, printed, or logged by this
/// command: the operator never has to know, type, or copy it anywhere for this command to run.
///
/// Usage, run inside the already-deployed container so it inherits the real production
/// configuration without that configuration ever leaving the machine:
///
///     fly ssh console -a stepin-api -C "dotnet StepIn.Api.dll migrate-database &lt;expected-final-migration-id&gt;"
///
/// The expected-final-migration-id argument is mandatory and is a deliberate safety gate, not a
/// formality: the operator names exactly the last migration they reviewed and intend to apply. The
/// command refuses — without touching the database — if the database's actual last *pending*
/// migration is anything else, whether fewer migrations are pending than expected (nothing to
/// apply yet) or more (a newer migration shipped in this image that the operator has not named and
/// therefore has not reviewed for this run). This is the same "explicit, reviewed target" discipline
/// as <c>dotnet ef database update &lt;id&gt;</c>, just runnable from inside the container where the
/// real connection string already lives, instead of requiring an operator to copy that secret
/// elsewhere to run the CLI tool.
///
/// Like <see cref="AidxOwnerInitCommand"/>: it is not an HTTP endpoint, needs no Clerk login, and is
/// not run by startup, the container entrypoint, migrations, or health checks — see
/// <c>Program.cs</c>'s operator-command dispatch, which runs this (or any other named command)
/// instead of starting Kestrel, so it can never happen as a side effect of booting the API.
///
/// A session-level PostgreSQL advisory lock (<c>pg_try_advisory_lock</c>) guards the actual
/// migration call so that two operators — or two machines — invoking this at the same moment can
/// never race each other; the loser refuses cleanly instead of applying migrations concurrently.
///
/// Exit codes: 0 = already up to date, or migrated successfully to exactly the named target;
/// 1 = failure applying migrations (nothing printed except the exception type, so no connection
/// string or secret leaks); 2 = could not connect to the configured database; 3 = a safety
/// precondition failed (unrecognised target, unexpected pending-migration state, lock contention) —
/// nothing was written to the database; 4 = the expected-final-migration-id argument was missing.
/// </summary>
public static class MigrateDatabaseCommand
{
    public const string Name = "migrate-database";

    public const int ExitOk = 0;
    public const int ExitFailed = 1;
    public const int ExitCannotConnect = 2;
    public const int ExitUnsafePrecondition = 3;
    public const int ExitMissingArgument = 4;

    /// <summary>A fixed, arbitrary key identifying "a StepIn migration is in progress" — the actual
    /// numeric value has no meaning beyond being stable across every invocation of this command.</summary>
    private const string AdvisoryLockKey = "stepin_migrate_database";

    public static async Task<int> RunAsync(
        IServiceProvider services, TextWriter output, string? expectedFinalMigrationId, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(output);

        if (string.IsNullOrWhiteSpace(expectedFinalMigrationId))
        {
            await output.WriteLineAsync($"Usage: dotnet StepIn.Api.dll {Name} <expected-final-migration-id>");
            await output.WriteLineAsync(
                "The expected final migration id must be the last migration you reviewed and intend to " +
                "apply this run — the command refuses rather than guesses if the database's actual pending " +
                "migrations end anywhere else.");
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

            // GetMigrations() is purely local — the set of migrations compiled into this image —
            // and touches no connection.
            var known = context.Database.GetMigrations().ToArray();
            if (!known.Contains(expectedFinalMigrationId, StringComparer.Ordinal))
            {
                await output.WriteLineAsync(
                    $"'{expectedFinalMigrationId}' is not a migration this build recognises. No changes were made.");
                await output.WriteLineAsync($"Migrations known to this build: {string.Join(", ", known)}");
                return ExitUnsafePrecondition;
            }

            var applied = (await context.Database.GetAppliedMigrationsAsync(cancellationToken)).ToArray();

            // The database's own history contains a migration this build's assembly has never heard
            // of — a newer or divergent schema than this image expects. Refuse rather than guess.
            var unrecognisedApplied = applied.Except(known, StringComparer.Ordinal).ToArray();
            if (unrecognisedApplied.Length > 0)
            {
                await output.WriteLineAsync(
                    "The database has applied migration(s) this build does not recognise — refusing to proceed.");
                await output.WriteLineAsync($"Unrecognised: {string.Join(", ", unrecognisedApplied)}");
                return ExitUnsafePrecondition;
            }

            var pending = (await context.Database.GetPendingMigrationsAsync(cancellationToken)).ToArray();

            await output.WriteLineAsync($"Applied migrations: {applied.Length}. Pending migrations: {pending.Length}.");

            if (pending.Length == 0)
            {
                await output.WriteLineAsync("Database schema is already up to date. No changes were made.");
                return ExitOk;
            }

            if (!string.Equals(pending[^1], expectedFinalMigrationId, StringComparison.Ordinal))
            {
                await output.WriteLineAsync(
                    $"Refusing to proceed: the last pending migration is '{pending[^1]}', not the expected " +
                    $"'{expectedFinalMigrationId}'. Re-run naming the migration you actually intend to apply, " +
                    "after reviewing what changed. No changes were made.");
                await output.WriteLineAsync($"Pending, in order: {string.Join(", ", pending)}");
                return ExitUnsafePrecondition;
            }

            await output.WriteLineAsync($"Applying {pending.Length} pending migration(s): {string.Join(", ", pending)}");

            // One physical connection held open across the lock, the migration, and the unlock — a
            // PostgreSQL advisory lock is session-scoped, so it would be silently released the
            // moment the connection it was taken on closes.
            await context.Database.OpenConnectionAsync(cancellationToken);
            try
            {
                var lockAcquired = await context.Database
                    .SqlQuery<bool>($"SELECT pg_try_advisory_lock(hashtext({AdvisoryLockKey})) AS \"Value\"")
                    .SingleAsync(cancellationToken);

                if (!lockAcquired)
                {
                    await output.WriteLineAsync(
                        "Another instance already holds the migration lock. No changes were made.");
                    return ExitUnsafePrecondition;
                }

                try
                {
                    await context.Database.MigrateAsync(cancellationToken);
                }
                finally
                {
                    await context.Database
                        .SqlQuery<bool>($"SELECT pg_advisory_unlock(hashtext({AdvisoryLockKey})) AS \"Value\"")
                        .SingleAsync(cancellationToken);
                }
            }
            finally
            {
                await context.Database.CloseConnectionAsync();
            }

            var nowApplied = (await context.Database.GetAppliedMigrationsAsync(cancellationToken)).ToArray();
            await output.WriteLineAsync(
                $"Migration succeeded. {nowApplied.Length} migration(s) now applied, ending at '{nowApplied[^1]}'.");
            return ExitOk;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            await output.WriteLineAsync(
                $"Migration failed ({ex.GetType().Name}). Check database connectivity, the migration SQL, and the " +
                "logs. The database may be in a partially migrated state — do not retry blindly; inspect " +
                "stepin.__ef_migrations_history before taking any further action.");
            return ExitFailed;
        }
    }
}
