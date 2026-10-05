using System.Net;
using System.Net.Http.Headers;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using StepIn.Domain.Aidx;
using StepIn.Domain.Common;
using StepIn.Domain.Companies;
using StepIn.Domain.Jobs;
using StepIn.Domain.Profiles;
using StepIn.Domain.Users;
using StepIn.Infrastructure.Aidx;
using StepIn.Infrastructure.Persistence;

namespace StepIn.Api.Tests;

/// <summary>
/// Phase 4.4B ownership foundation, against a real PostgreSQL container. These tests share one
/// database per class, and xUnit runs a class's tests sequentially, so the tests that alter the
/// system owner restore it in a finally block.
/// </summary>
public sealed class AidxSystemOwnershipTests(AuthApiFactory factory) : IClassFixture<AuthApiFactory>
{
    private readonly AuthApiFactory _factory = factory;

    private AsyncServiceScope NewScope() => _factory.Services.CreateAsyncScope();

    private static AidxSystemOwnershipService ServiceFor(IServiceProvider services) =>
        new(services.GetRequiredService<ApplicationDbContext>());

    private async Task<AidxSystemOwnership> EnsureAsync()
    {
        await using var scope = NewScope();
        return await ServiceFor(scope.ServiceProvider).EnsureAsync(TestContext.Current.CancellationToken);
    }

    private static Job NewJob(AidxSystemOwnership owner, JobCategory category, Guid? companyOverride = null) => new()
    {
        RecruiterProfileId = owner.RecruiterProfileId,
        CompanyId = companyOverride ?? owner.CompanyId,
        Title = "Ownership test role",
        Description = "Ownership test.",
        Location = "Sydney, NSW",
        Category = category,
    };

    // ------------------------------------------------------------------ identity ---

    [Fact]
    public async Task Ensure_creates_the_system_owner_and_find_returns_the_same_ids()
    {
        var created = await EnsureAsync();

        await using var scope = NewScope();
        var found = await ServiceFor(scope.ServiceProvider).FindAsync(TestContext.Current.CancellationToken);

        found.Should().Be(created);
    }

    [Fact]
    public async Task Repeated_initialisation_does_not_create_duplicates()
    {
        var first = await EnsureAsync();
        var second = await EnsureAsync();
        var third = await EnsureAsync();

        second.Should().Be(first);
        third.Should().Be(first);

        await using var scope = NewScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var ct = TestContext.Current.CancellationToken;

        (await db.Users.CountAsync(u => u.ClerkUserId == AidxSystemIdentity.ClerkUserId, ct)).Should().Be(1);
        (await db.RecruiterProfiles.CountAsync(r => r.UserId == first.UserId, ct)).Should().Be(1);
        (await db.Companies.CountAsync(c => c.Id == first.CompanyId, ct)).Should().Be(1);
    }

    [Fact]
    public async Task The_system_recruiter_points_at_the_system_user_and_the_aidx_lab_company()
    {
        var owner = await EnsureAsync();

        await using var scope = NewScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var ct = TestContext.Current.CancellationToken;

        var profile = await db.RecruiterProfiles.SingleAsync(r => r.Id == owner.RecruiterProfileId, ct);
        profile.UserId.Should().Be(owner.UserId);
        profile.CompanyId.Should().Be(owner.CompanyId);

        var company = await db.Companies.SingleAsync(c => c.Id == owner.CompanyId, ct);
        company.Name.Should().Be(AidxSystemIdentity.CompanyName);
    }

    // ----------------------------------------------------------- system identity ---

    [Fact]
    public async Task The_system_user_is_suspended_and_holds_no_platform_role()
    {
        var owner = await EnsureAsync();

        await using var scope = NewScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var user = await db.Users.SingleAsync(u => u.Id == owner.UserId, TestContext.Current.CancellationToken);

        user.Role.Should().BeNull("the system owner must never be Applicant, Recruiter or Admin");
        user.AccountStatus.Should().Be(AccountStatus.Suspended);
        user.ClerkUserId.Should().StartWith("system:");
        user.ClerkUserId.Should().NotStartWith("user_");
    }

    [Fact]
    public async Task A_system_owner_holding_a_role_is_refused_rather_than_repaired()
    {
        var owner = await EnsureAsync();
        var ct = TestContext.Current.CancellationToken;

        await using (var scope = NewScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            await db.Users.Where(u => u.Id == owner.UserId).ExecuteUpdateAsync(s => s.SetProperty(u => u.Role, UserRole.Admin), ct);
        }

        try
        {
            await using var scope = NewScope();
            var act = () => ServiceFor(scope.ServiceProvider).EnsureAsync(ct);
            await act.Should().ThrowAsync<InvalidOperationException>();
        }
        finally
        {
            await using var scope = NewScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            await db.Users.Where(u => u.Id == owner.UserId).ExecuteUpdateAsync(s => s.SetProperty(u => u.Role, (UserRole?)null), ct);
        }
    }

    [Fact]
    public async Task A_profile_pointing_at_a_different_company_is_refused_rather_than_repaired()
    {
        var owner = await EnsureAsync();
        var ct = TestContext.Current.CancellationToken;
        Guid otherCompany;

        await using (var scope = NewScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var company = new Company { Name = "Some Other Employer" };
            db.Add(company);
            await db.SaveChangesAsync(ct);
            otherCompany = company.Id;
            await db.RecruiterProfiles.Where(r => r.Id == owner.RecruiterProfileId)
                .ExecuteUpdateAsync(s => s.SetProperty(r => r.CompanyId, otherCompany), ct);
        }

        try
        {
            await using var scope = NewScope();
            var act = () => ServiceFor(scope.ServiceProvider).EnsureAsync(ct);
            await act.Should().ThrowAsync<InvalidOperationException>();
        }
        finally
        {
            await using var scope = NewScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            await db.RecruiterProfiles.Where(r => r.Id == owner.RecruiterProfileId)
                .ExecuteUpdateAsync(s => s.SetProperty(r => r.CompanyId, owner.CompanyId), ct);
            await db.Companies.Where(c => c.Id == otherCompany).ExecuteDeleteAsync(ct);
        }
    }

    // ----------------------------------------------------------------- sign-in ---

    [Fact]
    public async Task A_token_carrying_the_system_subject_cannot_authenticate_as_a_person()
    {
        await EnsureAsync();

        using var client = _factory.CreateClient();
        var token = AuthApiFactory.IssueToken(AidxSystemIdentity.ClerkUserId, AidxSystemIdentity.Email);
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/auth/me");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized, "the system owner is never a signed-in person");
    }

    // ---------------------------------------------------------------- ownership ---

    [Fact]
    public async Task A_research_job_owned_by_the_system_recruiter_is_an_aidx_research_job()
    {
        var owner = await EnsureAsync();
        var ct = TestContext.Current.CancellationToken;

        Job job;
        await using (var scope = NewScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            job = NewJob(owner, JobCategory.Research);
            db.Add(job);
            await db.SaveChangesAsync(ct);
        }

        AidxOwnershipRules.IsAidxResearchJob(job, owner).Should().BeTrue();
    }

    [Fact]
    public async Task A_career_job_owned_by_the_system_recruiter_is_not_an_aidx_research_job()
    {
        var owner = await EnsureAsync();
        var career = NewJob(owner, JobCategory.Career);

        AidxOwnershipRules.IsAidxResearchJob(career, owner).Should().BeFalse("AIDX must never manage Career jobs");
    }

    [Fact]
    public async Task A_research_job_owned_by_a_normal_recruiter_is_not_an_aidx_research_job()
    {
        var owner = await EnsureAsync();
        var ct = TestContext.Current.CancellationToken;

        Guid recruiterProfileId;
        Guid companyId;
        await using (var scope = NewScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var user = new ApplicationUser
            {
                ClerkUserId = $"user_{Guid.NewGuid():N}",
                Email = $"{Guid.NewGuid()}@example.com",
                FirstName = "Normal",
                LastName = "Recruiter",
                Role = UserRole.Recruiter,
            };
            var company = new Company { Name = "Ordinary Employer" };
            db.Add(user);
            db.Add(company);
            await db.SaveChangesAsync(ct);

            var profile = new RecruiterProfile { UserId = user.Id, CompanyId = company.Id };
            db.Add(profile);
            await db.SaveChangesAsync(ct);
            recruiterProfileId = profile.Id;
            companyId = company.Id;
        }

        var job = new Job
        {
            RecruiterProfileId = recruiterProfileId,
            CompanyId = companyId,
            Title = "Ordinary role",
            Description = "Not AIDX.",
            Location = "Melbourne, VIC",
            Category = JobCategory.Research,
        };

        AidxOwnershipRules.IsAidxResearchJob(job, owner).Should().BeFalse("only the system recruiter owns AIDX research");
    }

    [Fact]
    public async Task A_research_job_under_the_system_profile_but_another_company_is_not_an_aidx_research_job()
    {
        var owner = await EnsureAsync();
        var job = NewJob(owner, JobCategory.Research, companyOverride: Guid.NewGuid());

        AidxOwnershipRules.IsAidxResearchJob(job, owner).Should().BeFalse();
    }
}

/// <summary>Pure rules and identifiers. No database or container, so these run without Docker.</summary>
public sealed class AidxSystemIdentityRulesTests
{
    [Theory]
    [InlineData("system:aidx-lab", true)]
    [InlineData("system:anything", true)]
    [InlineData("user_2abc", false)]
    [InlineData("SYSTEM:aidx-lab", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void Only_the_reserved_system_namespace_is_recognised(string? subject, bool expected)
    {
        AidxSystemIdentity.IsSystemClerkUserId(subject).Should().Be(expected);
    }

    [Fact]
    public void The_canonical_system_identifier_is_within_the_clerk_id_column_limit()
    {
        // ApplicationUser.ClerkUserId is limited to 64 characters.
        AidxSystemIdentity.ClerkUserId.Length.Should().BeLessThanOrEqualTo(64);
    }
}
