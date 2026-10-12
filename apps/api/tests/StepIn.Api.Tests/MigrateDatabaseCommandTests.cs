using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using StepIn.Api.Operations;
using StepIn.Application.Common.Interfaces;
using StepIn.Infrastructure.Persistence;
using StepIn.Infrastructure.Services;
using Testcontainers.PostgreSql;

namespace StepIn.Api.Tests;

/// <summary>
/// MigrateDatabaseCommand against a fresh PostgreSQL container this class controls directly —
/// deliberately not <see cref="AuthApiFactory"/>, which migrates its container to the latest
/// schema at startup. These tests need to construct specific "some migrations already applied,
/// some still pending" states, which that shared fixture can't produce.
/// </summary>
public sealed class MigrateDatabaseCommandTests : IAsyncLifetime
{
    // The last migration before the four Career Intelligence ones — migrating a fresh container
    // only this far leaves exactly AddCareerGoals/AddPortfolioProjects/AddInterviewPractice/
    // AddExperiencePassport pending, the same state the production database is actually in.
    private const string LastPreCareerIntelligenceMigration = "20261005021348_AddAidxFoundations";
    private const string FinalExpectedMigration = "20261012131143_AddExperiencePassport";

    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder("postgres:17-alpine")
        .WithDatabase("stepin_migrate_test")
        .WithUsername("stepin")
        .WithPassword("stepin")
        .Build();

    public async ValueTask InitializeAsync() => await _container.StartAsync();

    public async ValueTask DisposeAsync() => await _container.DisposeAsync();

    private ApplicationDbContext BuildContext(string? connectionStringOverride = null)
    {
        var services = new ServiceCollection();
        services.AddSingleton<IDateTimeProvider, SystemDateTimeProvider>();
        services.AddDbContext<ApplicationDbContext>(options =>
            options.UseNpgsql(connectionStringOverride ?? _container.GetConnectionString(), npgsql =>
                npgsql.MigrationsHistoryTable("__ef_migrations_history", ApplicationDbContext.Schema)));

        return services.BuildServiceProvider().GetRequiredService<ApplicationDbContext>();
    }

    private IServiceProvider BuildServices(string? connectionStringOverride = null)
    {
        var services = new ServiceCollection();
        services.AddSingleton<IDateTimeProvider, SystemDateTimeProvider>();
        services.AddDbContext<ApplicationDbContext>(options =>
            options.UseNpgsql(connectionStringOverride ?? _container.GetConnectionString(), npgsql =>
                npgsql.MigrationsHistoryTable("__ef_migrations_history", ApplicationDbContext.Schema)));

        return services.BuildServiceProvider();
    }

    private static async Task<(string Output, int ExitCode)> RunCommandAsync(
        IServiceProvider services, string? expectedFinalMigrationId, CancellationToken ct)
    {
        using var writer = new StringWriter();
        var exitCode = await MigrateDatabaseCommand.RunAsync(services, writer, expectedFinalMigrationId, ct);
        return (writer.ToString(), exitCode);
    }

    [Fact]
    public async Task A_missing_expected_migration_argument_is_refused_before_touching_the_database()
    {
        var ct = TestContext.Current.CancellationToken;
        var services = BuildServices();

        var (output, exitCode) = await RunCommandAsync(services, null, ct);

        exitCode.Should().Be(MigrateDatabaseCommand.ExitMissingArgument);
        output.Should().Contain("Usage:");
    }

    [Fact]
    public async Task An_unreachable_database_is_reported_without_a_crash()
    {
        var ct = TestContext.Current.CancellationToken;
        // A syntactically valid but unreachable connection string — the test must not depend on a
        // real server actually existing at this address.
        var services = BuildServices("Host=127.0.0.1;Port=1;Database=nope;Username=nope;Password=nope;Timeout=2");

        var (output, exitCode) = await RunCommandAsync(services, FinalExpectedMigration, ct);

        exitCode.Should().Be(MigrateDatabaseCommand.ExitCannotConnect);
        output.Should().Contain("Cannot connect");
    }

    [Fact]
    public async Task An_unrecognised_expected_migration_id_is_refused()
    {
        var ct = TestContext.Current.CancellationToken;
        var services = BuildServices();

        var (output, exitCode) = await RunCommandAsync(services, "not_a_real_migration", ct);

        exitCode.Should().Be(MigrateDatabaseCommand.ExitUnsafePrecondition);
        output.Should().Contain("not a migration this build recognises");
    }

    [Fact]
    public async Task Naming_a_migration_earlier_than_the_actual_last_pending_one_is_refused_and_nothing_is_applied()
    {
        var ct = TestContext.Current.CancellationToken;
        await using (var context = BuildContext())
        {
            await context.Database.MigrateAsync(LastPreCareerIntelligenceMigration, ct);
        }

        var services = BuildServices();

        // AddCareerGoals is pending, but it is not the *last* pending migration — Portfolio,
        // Interview Practice and Experience Passport all come after it and would also be pending.
        var (output, exitCode) = await RunCommandAsync(services, "20261011140004_AddCareerGoals", ct);

        exitCode.Should().Be(MigrateDatabaseCommand.ExitUnsafePrecondition);
        output.Should().Contain("Refusing to proceed");
        output.Should().Contain("No changes were made");

        await using var verify = BuildContext();
        (await verify.Database.GetAppliedMigrationsAsync(ct)).Should().NotContain("20261011140004_AddCareerGoals");
    }

    [Fact]
    public async Task Naming_the_correct_final_pending_migration_applies_all_four_in_order()
    {
        var ct = TestContext.Current.CancellationToken;
        await using (var context = BuildContext())
        {
            await context.Database.MigrateAsync(LastPreCareerIntelligenceMigration, ct);
        }

        var services = BuildServices();
        var (output, exitCode) = await RunCommandAsync(services, FinalExpectedMigration, ct);

        exitCode.Should().Be(MigrateDatabaseCommand.ExitOk, output);
        output.Should().Contain("Migration succeeded");

        await using var verify = BuildContext();
        var applied = (await verify.Database.GetAppliedMigrationsAsync(ct)).ToArray();
        applied.Should().Contain([
            "20261011140004_AddCareerGoals",
            "20261012000657_AddPortfolioProjects",
            "20261012031355_AddInterviewPractice",
            "20261012131143_AddExperiencePassport",
        ]);
        (await verify.Database.GetPendingMigrationsAsync(ct)).Should().BeEmpty();
    }

    [Fact]
    public async Task Re_running_after_a_successful_migration_is_a_safe_no_op()
    {
        var ct = TestContext.Current.CancellationToken;
        await using (var context = BuildContext())
        {
            await context.Database.MigrateAsync(LastPreCareerIntelligenceMigration, ct);
        }

        await RunCommandAsync(BuildServices(), FinalExpectedMigration, ct);

        var (output, exitCode) = await RunCommandAsync(BuildServices(), FinalExpectedMigration, ct);

        exitCode.Should().Be(MigrateDatabaseCommand.ExitOk);
        output.Should().Contain("already up to date");
    }

    [Fact]
    public async Task A_database_with_an_unrecognised_applied_migration_is_refused()
    {
        var ct = TestContext.Current.CancellationToken;
        await using (var context = BuildContext())
        {
            await context.Database.MigrateAsync(LastPreCareerIntelligenceMigration, ct);
            // Simulates a schema from a newer or divergent build: a history row this assembly has
            // never heard of.
            await context.Database.ExecuteSqlRawAsync(
                """INSERT INTO stepin.__ef_migrations_history ("MigrationId", "ProductVersion") VALUES ('99999999999999_FromTheFuture', '10.0.0')""",
                ct);
        }

        var (output, exitCode) = await RunCommandAsync(BuildServices(), FinalExpectedMigration, ct);

        exitCode.Should().Be(MigrateDatabaseCommand.ExitUnsafePrecondition);
        output.Should().Contain("does not recognise");

        await using var verify = BuildContext();
        (await verify.Database.GetAppliedMigrationsAsync(ct)).Should().NotContain("20261011140004_AddCareerGoals");
    }
}
