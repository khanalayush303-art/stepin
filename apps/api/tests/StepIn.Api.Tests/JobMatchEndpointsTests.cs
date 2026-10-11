using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using StepIn.Api.Endpoints;
using StepIn.Domain.Jobs;
using StepIn.Infrastructure.Persistence;

namespace StepIn.Api.Tests;

/// <summary>
/// GET /api/v1/jobs/matches. Verifies the endpoint reuses the existing Job and CandidateProfile
/// models (no new table), excludes anything a candidate shouldn't see through Career discovery,
/// and that the response is explainable (matched/missing skills), not an opaque score.
/// </summary>
public sealed class JobMatchEndpointsTests(AuthApiFactory factory) : IClassFixture<AuthApiFactory>
{
    private readonly AuthApiFactory _factory = factory;

    private static string Unique(string prefix) => $"{prefix} {Guid.NewGuid():N}";

    private HttpClient CreateClient() => _factory.CreateClient();

    private static HttpRequestMessage BuildRequest(HttpMethod method, string url, string? token, object? body = null)
    {
        var request = new HttpRequestMessage(method, url);
        if (body is not null)
        {
            request.Content = JsonContent.Create(body);
        }

        if (token is not null)
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }

        if (method != HttpMethod.Get)
        {
            request.Headers.Add("X-Requested-With", "fetch");
        }

        return request;
    }

    private async Task<string> IssueApplicantAsync(CancellationToken ct)
    {
        var token = AuthApiFactory.IssueToken($"user_{Guid.NewGuid():N}", $"{Guid.NewGuid()}@example.com");
        using var client = CreateClient();
        using var setup = BuildRequest(HttpMethod.Post, "/api/v1/auth/account-setup", token, new AccountSetupRequest("Applicant"));
        (await client.SendAsync(setup, ct)).EnsureSuccessStatusCode();
        return token;
    }

    private async Task SetCandidateSkillsAsync(string applicantToken, string[] skills, CancellationToken ct)
    {
        using var client = CreateClient();
        using var request = BuildRequest(HttpMethod.Put, "/api/v1/profile/candidate", applicantToken,
            new UpdateCandidateProfileRequest(null, null, null, null, null, null, null, null, skills, null, null, null));
        (await client.SendAsync(request, ct)).EnsureSuccessStatusCode();
    }

    private async Task<string> IssueRecruiterWithCompanyAsync(CancellationToken ct)
    {
        var token = AuthApiFactory.IssueToken($"user_{Guid.NewGuid():N}", $"{Guid.NewGuid()}@example.com");
        using var client = CreateClient();
        using var setup = BuildRequest(HttpMethod.Post, "/api/v1/auth/account-setup", token, new AccountSetupRequest("Recruiter"));
        (await client.SendAsync(setup, ct)).EnsureSuccessStatusCode();

        using var profile = BuildRequest(HttpMethod.Put, "/api/v1/profile/recruiter", token,
            new UpdateRecruiterProfileRequest(null, null, null, new CompanyDto(null, Unique("Match Co"), null, null, null, null, null)));
        (await client.SendAsync(profile, ct)).EnsureSuccessStatusCode();

        return token;
    }

    /// <summary>Creates and publishes a Career job with the given skills, as the given recruiter.</summary>
    private async Task<Guid> CreatePublishedJobAsync(string recruiterToken, string[] skills, CancellationToken ct)
    {
        using var client = CreateClient();
        using var create = BuildRequest(HttpMethod.Post, "/api/v1/recruiter/jobs", recruiterToken,
            new CreateJobRequest(Unique("Role"), "Work on things.", "FullTime", "Hybrid", "Sydney, NSW", null, skills));
        var createResponse = await client.SendAsync(create, ct);
        createResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var jobId = (await createResponse.Content.ReadFromJsonAsync<JobResponse>(ct))!.Id;

        using var publish = BuildRequest(HttpMethod.Post, $"/api/v1/recruiter/jobs/{jobId}/publish", recruiterToken);
        (await client.SendAsync(publish, ct)).EnsureSuccessStatusCode();

        return jobId;
    }

    private async Task SetCategoryAsync(Guid jobId, JobCategory category, CancellationToken ct)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        await db.Jobs.Where(j => j.Id == jobId)
            .ExecuteUpdateAsync(s => s.SetProperty(j => j.Category, category), ct);
    }

    [Fact]
    public async Task Anonymous_request_is_unauthorized()
    {
        var ct = TestContext.Current.CancellationToken;
        using var client = CreateClient();

        var response = await client.GetAsync("/api/v1/jobs/matches", ct);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Recruiter_token_cannot_read_job_matches()
    {
        var ct = TestContext.Current.CancellationToken;
        var recruiter = await IssueRecruiterWithCompanyAsync(ct);
        using var client = CreateClient();
        using var request = BuildRequest(HttpMethod.Get, "/api/v1/jobs/matches", recruiter);

        var response = await client.SendAsync(request, ct);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Matching_skills_are_explained_with_matched_and_missing_lists()
    {
        var ct = TestContext.Current.CancellationToken;
        var recruiter = await IssueRecruiterWithCompanyAsync(ct);
        var jobId = await CreatePublishedJobAsync(recruiter, ["C#", "SQL", "Azure"], ct);

        var applicant = await IssueApplicantAsync(ct);
        await SetCandidateSkillsAsync(applicant, ["C#", "SQL"], ct);

        using var client = CreateClient();
        using var request = BuildRequest(HttpMethod.Get, "/api/v1/jobs/matches", applicant);
        var response = await client.SendAsync(request, ct);
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var matches = (await response.Content.ReadFromJsonAsync<List<JobMatchResponse>>(ct))!;
        var match = matches.Should().ContainSingle(m => m.Id == jobId).Subject;

        match.MatchedSkills.Should().BeEquivalentTo("C#", "SQL");
        match.SkillsNotYetDemonstrated.Should().BeEquivalentTo("Azure");
        match.SkillCoverage.Should().BeApproximately(2.0 / 3.0, 0.0001);
        match.MatchQuality.Should().Be(nameof(JobMatchQuality.PartialMatch));
    }

    [Fact]
    public async Task A_candidate_with_no_profile_yet_gets_every_job_skill_as_not_yet_demonstrated_not_an_error()
    {
        var ct = TestContext.Current.CancellationToken;
        var recruiter = await IssueRecruiterWithCompanyAsync(ct);
        var jobId = await CreatePublishedJobAsync(recruiter, ["C#"], ct);

        var applicant = await IssueApplicantAsync(ct); // no PUT to /profile/candidate at all

        using var client = CreateClient();
        using var request = BuildRequest(HttpMethod.Get, "/api/v1/jobs/matches", applicant);
        var response = await client.SendAsync(request, ct);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var matches = (await response.Content.ReadFromJsonAsync<List<JobMatchResponse>>(ct))!;
        var match = matches.Should().ContainSingle(m => m.Id == jobId).Subject;
        match.MatchedSkills.Should().BeEmpty();
        match.SkillsNotYetDemonstrated.Should().BeEquivalentTo("C#");
        match.SkillCoverage.Should().Be(0.0);
    }

    [Fact]
    public async Task A_job_with_no_recorded_skills_is_unscored_not_a_zero()
    {
        var ct = TestContext.Current.CancellationToken;
        var recruiter = await IssueRecruiterWithCompanyAsync(ct);
        var jobId = await CreatePublishedJobAsync(recruiter, [], ct);

        var applicant = await IssueApplicantAsync(ct);
        await SetCandidateSkillsAsync(applicant, ["C#"], ct);

        using var client = CreateClient();
        using var request = BuildRequest(HttpMethod.Get, "/api/v1/jobs/matches", applicant);
        var response = await client.SendAsync(request, ct);

        var matches = (await response.Content.ReadFromJsonAsync<List<JobMatchResponse>>(ct))!;
        var match = matches.Should().ContainSingle(m => m.Id == jobId).Subject;
        match.SkillCoverage.Should().BeNull();
        match.MatchQuality.Should().Be(nameof(JobMatchQuality.Unscored));
    }

    [Fact]
    public async Task Draft_jobs_and_research_jobs_never_appear_in_matches()
    {
        var ct = TestContext.Current.CancellationToken;
        var recruiter = await IssueRecruiterWithCompanyAsync(ct);

        // Draft: created but never published.
        using var client = CreateClient();
        using var createDraft = BuildRequest(HttpMethod.Post, "/api/v1/recruiter/jobs", recruiter,
            new CreateJobRequest(Unique("Draft role"), "Work on things.", "FullTime", "Hybrid", "Sydney, NSW", null, ["C#"]));
        var draftResponse = await client.SendAsync(createDraft, ct);
        var draftId = (await draftResponse.Content.ReadFromJsonAsync<JobResponse>(ct))!.Id;

        // Published, but Research category.
        var researchJobId = await CreatePublishedJobAsync(recruiter, ["C#"], ct);
        await SetCategoryAsync(researchJobId, JobCategory.Research, ct);

        var applicant = await IssueApplicantAsync(ct);
        await SetCandidateSkillsAsync(applicant, ["C#"], ct);

        using var request = BuildRequest(HttpMethod.Get, "/api/v1/jobs/matches", applicant);
        var response = await client.SendAsync(request, ct);
        var matches = (await response.Content.ReadFromJsonAsync<List<JobMatchResponse>>(ct))!;

        matches.Should().NotContain(m => m.Id == draftId);
        matches.Should().NotContain(m => m.Id == researchJobId);
    }

    [Fact]
    public async Task Higher_coverage_ranks_above_lower_coverage()
    {
        var ct = TestContext.Current.CancellationToken;
        var recruiter = await IssueRecruiterWithCompanyAsync(ct);
        var strongJobId = await CreatePublishedJobAsync(recruiter, ["C#"], ct); // 100%
        var weakJobId = await CreatePublishedJobAsync(recruiter, ["C#", "Rust", "Go", "Java"], ct); // 25%

        var applicant = await IssueApplicantAsync(ct);
        await SetCandidateSkillsAsync(applicant, ["C#"], ct);

        using var client = CreateClient();
        using var request = BuildRequest(HttpMethod.Get, "/api/v1/jobs/matches", applicant);
        var response = await client.SendAsync(request, ct);
        var matches = (await response.Content.ReadFromJsonAsync<List<JobMatchResponse>>(ct))!;

        var strongIndex = matches.FindIndex(m => m.Id == strongJobId);
        var weakIndex = matches.FindIndex(m => m.Id == weakJobId);
        strongIndex.Should().BeLessThan(weakIndex, "100% coverage must rank above 25% coverage");
    }

    [Fact]
    public async Task Invalid_limit_returns_a_validation_problem()
    {
        var ct = TestContext.Current.CancellationToken;
        var applicant = await IssueApplicantAsync(ct);
        using var client = CreateClient();
        using var request = BuildRequest(HttpMethod.Get, "/api/v1/jobs/matches?limit=0", applicant);

        var response = await client.SendAsync(request, ct);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
}
