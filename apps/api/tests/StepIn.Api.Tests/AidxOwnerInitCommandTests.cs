using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using StepIn.Api.Operations;
using StepIn.Domain.Aidx;
using StepIn.Domain.Common;
using StepIn.Domain.Companies;
using StepIn.Domain.Profiles;
using StepIn.Domain.Users;
using StepIn.Infrastructure.Persistence;

namespace StepIn.Api.Tests;

/// <summary>
/// The operator command against a fresh PostgreSQL container. This class has its own container, so it
/// can create and delete the owner records freely without touching other test classes.
/// </summary>
public sealed class AidxOwnerInitCommandTests(AuthApiFactory factory) : IClassFixture<AuthApiFactory>
{
    private readonly AuthApiFactory _factory = factory;

    private async Task<(string Output, int ExitCode)> RunCommandAsync()
    {
        using var writer = new StringWriter();
        var exitCode = await AidxOwnerInitCommand.RunAsync(_factory.Services, writer, TestContext.Current.CancellationToken);
        return (writer.ToString(), exitCode);
    }

    private async Task<(int Users, int Profiles, int Companies)> CountsAsync()
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var ct = TestContext.Current.CancellationToken;
        return (
            await db.Users.CountAsync(u => u.ClerkUserId == AidxSystemIdentity.ClerkUserId, ct),
            await db.RecruiterProfiles.CountAsync(ct),
            await db.Companies.CountAsync(ct));
    }

    [Fact]
    public async Task The_first_run_creates_a_suspended_owner_with_no_role_and_reports_success()
    {
        var (output, exitCode) = await RunCommandAsync();

        // xUnit may run another test in this class first, so either success message is valid here.
        exitCode.Should().Be(AidxOwnerInitCommand.ExitOk);
        output.Should().ContainAny("initialized successfully.", "already exists and is consistent.");

        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var user = await db.Users.SingleAsync(u => u.ClerkUserId == AidxSystemIdentity.ClerkUserId, TestContext.Current.CancellationToken);
        user.AccountStatus.Should().Be(AccountStatus.Suspended);
        user.Role.Should().BeNull("the system identity must never hold a platform role");
    }

    [Fact]
    public async Task A_second_run_is_a_no_op_and_reports_the_owner_as_consistent()
    {
        await RunCommandAsync();
        var before = await CountsAsync();

        var (output, exitCode) = await RunCommandAsync();

        exitCode.Should().Be(AidxOwnerInitCommand.ExitOk);
        output.Should().Contain("already exists and is consistent.");
        (await CountsAsync()).Should().Be(before, "a repeat run must not create or remove any record");
    }

    [Fact]
    public async Task A_conflicting_owner_is_refused_and_nothing_is_written()
    {
        await RunCommandAsync();
        var ct = TestContext.Current.CancellationToken;

        // A platform role on the system identity is a conflict the service must refuse.
        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            await db.Users.Where(u => u.ClerkUserId == AidxSystemIdentity.ClerkUserId)
                .ExecuteUpdateAsync(s => s.SetProperty(u => u.Role, UserRole.Admin), ct);
        }

        try
        {
            var before = await CountsAsync();

            var (output, exitCode) = await RunCommandAsync();

            exitCode.Should().Be(AidxOwnerInitCommand.ExitInconsistent);
            output.Should().Contain("AIDX system ownership is inconsistent.");
            output.Should().Contain("No changes were made.");
            (await CountsAsync()).Should().Be(before);
        }
        finally
        {
            await using var scope = _factory.Services.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            await db.Users.Where(u => u.ClerkUserId == AidxSystemIdentity.ClerkUserId)
                .ExecuteUpdateAsync(s => s.SetProperty(u => u.Role, (UserRole?)null), ct);
        }
    }

    [Fact]
    public async Task A_user_with_a_role_and_no_profile_is_refused_before_any_company_or_profile_is_created()
    {
        await RunCommandAsync();
        var ct = TestContext.Current.CancellationToken;

        // Partial state: the user exists but its profile is gone, and the user now holds a role.
        // Before the fix, the service created a profile and company and only then refused.
        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            await db.RecruiterProfiles.Where(r => db.Users.Any(u => u.Id == r.UserId && u.ClerkUserId == AidxSystemIdentity.ClerkUserId)).ExecuteDeleteAsync(ct);
            await db.Users.Where(u => u.ClerkUserId == AidxSystemIdentity.ClerkUserId)
                .ExecuteUpdateAsync(s => s.SetProperty(u => u.Role, UserRole.Recruiter), ct);
        }

        try
        {
            var before = await CountsAsync();

            var (output, exitCode) = await RunCommandAsync();

            exitCode.Should().Be(AidxOwnerInitCommand.ExitInconsistent);
            output.Should().Contain("No changes were made.");
            (await CountsAsync()).Should().Be(before, "no profile or company may be created for a refused owner");
        }
        finally
        {
            await using var scope = _factory.Services.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            await db.Users.Where(u => u.ClerkUserId == AidxSystemIdentity.ClerkUserId)
                .ExecuteUpdateAsync(s => s.SetProperty(u => u.Role, (UserRole?)null), ct);
        }
    }

    [Fact]
    public async Task A_valid_user_with_a_missing_profile_is_completed_by_the_command()
    {
        await RunCommandAsync();
        var ct = TestContext.Current.CancellationToken;

        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            await db.RecruiterProfiles.Where(r => db.Users.Any(u => u.Id == r.UserId && u.ClerkUserId == AidxSystemIdentity.ClerkUserId)).ExecuteDeleteAsync(ct);
        }

        var (output, exitCode) = await RunCommandAsync();

        exitCode.Should().Be(AidxOwnerInitCommand.ExitOk, "a clearly supported partial state is completed, not refused");
        output.Should().Contain("initialized successfully.");
        (await CountsAsync()).Profiles.Should().Be(1, "exactly one canonical profile is restored");
    }
}
