using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using StepIn.Api.Operations;
using StepIn.Domain.Aidx;
using StepIn.Domain.Common;
using StepIn.Infrastructure.Persistence;

namespace StepIn.Api.Tests;

public sealed class AidxBootstrapManagerCommandTests(AuthApiFactory factory) : IClassFixture<AuthApiFactory>
{
    private readonly AuthApiFactory _factory = factory;

    private async Task<(string Output, int ExitCode)> RunAsync(string? userId = null, string? clerkUserId = null)
    {
        using var writer = new StringWriter();
        var exitCode = await AidxBootstrapManagerCommand.RunAsync(
            _factory.Services, writer, userId, clerkUserId, TestContext.Current.CancellationToken);
        return (writer.ToString(), exitCode);
    }

    /// <summary>
    /// Test methods in this class share one Postgres container for the class's lifetime (xUnit's
    /// IClassFixture scope), so "is there already an Active LabManager" — the one piece of truly
    /// class-wide state this command cares about — must be reset explicitly at the start of any test
    /// that depends on a clean slate, rather than assuming execution order.
    /// </summary>
    private async Task ClearAnyExistingLabManagersAsync()
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        await db.AidxMemberships.Where(m => m.Role == AidxLabRole.LabManager)
            .ExecuteUpdateAsync(s => s.SetProperty(m => m.Status, AidxMembershipStatus.Revoked), TestContext.Current.CancellationToken);
    }

    private async Task<Guid> CreateActiveUserAsync(string? clerkUserId = null)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var user = new StepIn.Domain.Users.ApplicationUser
        {
            ClerkUserId = clerkUserId ?? $"user_{Guid.NewGuid():N}",
            Email = $"{Guid.NewGuid()}@example.com",
            FirstName = "Test",
            LastName = "User",
            AccountStatus = AccountStatus.Active,
        };
        db.Users.Add(user);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        return user.Id;
    }

    [Fact]
    public async Task Bootstrapping_with_no_arguments_fails_with_missing_argument()
    {
        var (_, exitCode) = await RunAsync();
        exitCode.Should().Be(AidxBootstrapManagerCommand.ExitMissingArgument);
    }

    [Fact]
    public async Task Bootstrapping_with_both_selectors_at_once_fails_with_missing_argument()
    {
        var (_, exitCode) = await RunAsync(userId: Guid.NewGuid().ToString(), clerkUserId: "user_x");
        exitCode.Should().Be(AidxBootstrapManagerCommand.ExitMissingArgument);
    }

    [Fact]
    public async Task Email_is_never_accepted_as_a_selector()
    {
        // The command's own usage text documents this; confirmed structurally
        // by RunAsync's signature only accepting --user-id/--clerk-user-id.
        var method = typeof(AidxBootstrapManagerCommand).GetMethod(nameof(AidxBootstrapManagerCommand.RunAsync));
        var parameterNames = method!.GetParameters().Select(p => p.Name).ToArray();
        parameterNames.Should().NotContain(n => n!.Contains("email", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task First_bootstrap_grants_LabManager_and_writes_an_audit_entry()
    {
        await ClearAnyExistingLabManagersAsync();
        var userId = await CreateActiveUserAsync();

        var (_, exitCode) = await RunAsync(userId: userId.ToString());

        exitCode.Should().Be(AidxBootstrapManagerCommand.ExitOk);

        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var membership = await db.AidxMemberships.SingleAsync(m => m.UserId == userId, TestContext.Current.CancellationToken);
        membership.Role.Should().Be(AidxLabRole.LabManager);
        membership.Status.Should().Be(AidxMembershipStatus.Active);

        var audit = await db.AidxMembershipAuditEntries
            .SingleAsync(a => a.MembershipId == membership.Id, TestContext.Current.CancellationToken);
        audit.Action.Should().Be(AidxMembershipAuditAction.Granted);
    }

    [Fact]
    public async Task Bootstrapping_refuses_once_an_Active_LabManager_already_exists()
    {
        await ClearAnyExistingLabManagersAsync();
        var firstUserId = await CreateActiveUserAsync();
        (await RunAsync(userId: firstUserId.ToString())).ExitCode.Should().Be(AidxBootstrapManagerCommand.ExitOk);

        var secondUserId = await CreateActiveUserAsync();
        var (_, exitCode) = await RunAsync(userId: secondUserId.ToString());

        exitCode.Should().Be(AidxBootstrapManagerCommand.ExitUnsafePrecondition);

        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        (await db.AidxMemberships.AnyAsync(m => m.UserId == secondUserId, TestContext.Current.CancellationToken))
            .Should().BeFalse("no changes should be made once a LabManager already exists");
    }

    [Fact]
    public async Task Recovery_re_run_succeeds_after_the_only_LabManager_is_revoked()
    {
        await ClearAnyExistingLabManagersAsync();
        var userId = await CreateActiveUserAsync();
        await RunAsync(userId: userId.ToString());

        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            await db.AidxMemberships.Where(m => m.UserId == userId)
                .ExecuteUpdateAsync(
                    s => s.SetProperty(m => m.Status, AidxMembershipStatus.Revoked),
                    TestContext.Current.CancellationToken);
        }

        var newManagerId = await CreateActiveUserAsync();
        var (_, exitCode) = await RunAsync(userId: newManagerId.ToString());

        exitCode.Should().Be(AidxBootstrapManagerCommand.ExitOk, "the guard is 'no Active LabManager', not 'has never run'");
    }

    [Fact]
    public async Task Bootstrapping_a_suspended_account_is_refused()
    {
        await ClearAnyExistingLabManagersAsync();
        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var user = new StepIn.Domain.Users.ApplicationUser
        {
            ClerkUserId = $"user_{Guid.NewGuid():N}",
            Email = $"{Guid.NewGuid()}@example.com",
            FirstName = "Test",
            LastName = "User",
            AccountStatus = AccountStatus.Suspended,
        };
        db.Users.Add(user);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var (_, exitCode) = await RunAsync(userId: user.Id.ToString());

        exitCode.Should().Be(AidxBootstrapManagerCommand.ExitUnsafePrecondition);
    }

    [Fact]
    public async Task Bootstrapping_an_unknown_user_id_is_refused()
    {
        await ClearAnyExistingLabManagersAsync();
        var (_, exitCode) = await RunAsync(userId: Guid.NewGuid().ToString());
        exitCode.Should().Be(AidxBootstrapManagerCommand.ExitUnsafePrecondition);
    }
}
