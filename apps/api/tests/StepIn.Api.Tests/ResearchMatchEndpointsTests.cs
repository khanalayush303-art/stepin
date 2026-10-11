using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using StepIn.Api.Endpoints;
using StepIn.Domain.Aidx;
using StepIn.Domain.Jobs;
using StepIn.Infrastructure.Persistence;

namespace StepIn.Api.Tests;

/// <summary>
/// GET /api/v1/aidx/research-matches. Verifies the endpoint reuses JobMatchScorer and the
/// existing AidxProject/Job models (no new table), excludes unpublished content, and correctly
/// distinguishes a project with an open Research opportunity from one with none.
/// </summary>
public sealed class ResearchMatchEndpointsTests(AuthApiFactory factory) : IClassFixture<AuthApiFactory>
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
            new UpdateRecruiterProfileRequest(null, null, null, new CompanyDto(null, Unique("Bridge Co"), null, null, null, null, null)));
        (await client.SendAsync(profile, ct)).EnsureSuccessStatusCode();

        return token;
    }

    /// <summary>Creates a published AIDX project directly in the database, with the given technologies.</summary>
    private async Task<Guid> InsertPublishedProjectAsync(string[] technologies, CancellationToken ct)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var title = Unique("Bridge project");
        var project = new AidxProject
        {
            Title = title,
            Slug = title.Replace(' ', '-').ToLowerInvariant(),
            ShortDescription = "Short.",
            Description = "Long.",
            Status = AidxContentStatus.Published,
            PublishedAt = DateTimeOffset.UtcNow,
        };
        // Entity.Id is generated client-side at construction (Guid.CreateVersion7()), so it's
        // already known here — the same pattern AidxAdminEndpoints uses for an existing project.
        foreach (var name in technologies)
        {
            project.Technologies.Add(new AidxProjectTechnology { ProjectId = project.Id, Name = name });
        }

        db.Add(project);
        await db.SaveChangesAsync(ct);
        return project.Id;
    }

    private async Task<Guid> InsertDraftProjectAsync(CancellationToken ct)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var title = Unique("Draft project");
        var project = new AidxProject
        {
            Title = title,
            Slug = title.Replace(' ', '-').ToLowerInvariant(),
            ShortDescription = "Short.",
            Description = "Long.",
            Status = AidxContentStatus.Draft,
        };
        project.Technologies.Add(new AidxProjectTechnology { ProjectId = project.Id, Name = "C#" });
        db.Add(project);
        await db.SaveChangesAsync(ct);
        return project.Id;
    }

    /// <summary>Creates a published Research job linked to the given project, as the given recruiter.</summary>
    private async Task<Guid> CreateLinkedResearchJobAsync(string recruiterToken, Guid projectId, bool publish, CancellationToken ct)
    {
        using var client = CreateClient();
        using var create = BuildRequest(HttpMethod.Post, "/api/v1/recruiter/jobs", recruiterToken,
            new CreateJobRequest(Unique("Research role"), "Work on the project.", "FullTime", "Hybrid", "Sydney, NSW", null, []));
        var createResponse = await client.SendAsync(create, ct);
        var jobId = (await createResponse.Content.ReadFromJsonAsync<JobResponse>(ct))!.Id;

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        await db.Jobs.Where(j => j.Id == jobId)
            .ExecuteUpdateAsync(s => s
                .SetProperty(j => j.Category, JobCategory.Research)
                .SetProperty(j => j.AidxProjectId, projectId), ct);

        if (publish)
        {
            using var publishRequest = BuildRequest(HttpMethod.Post, $"/api/v1/recruiter/jobs/{jobId}/publish", recruiterToken);
            (await client.SendAsync(publishRequest, ct)).EnsureSuccessStatusCode();
        }

        return jobId;
    }

    [Fact]
    public async Task Anonymous_request_is_unauthorized()
    {
        var ct = TestContext.Current.CancellationToken;
        using var client = CreateClient();

        var response = await client.GetAsync("/api/v1/aidx/research-matches", ct);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Recruiter_token_cannot_read_research_matches()
    {
        var ct = TestContext.Current.CancellationToken;
        var recruiter = await IssueRecruiterWithCompanyAsync(ct);
        using var client = CreateClient();
        using var request = BuildRequest(HttpMethod.Get, "/api/v1/aidx/research-matches", recruiter);

        var response = await client.SendAsync(request, ct);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Matching_technologies_are_explained_with_matched_and_missing_lists()
    {
        var ct = TestContext.Current.CancellationToken;
        var projectId = await InsertPublishedProjectAsync(["Python", "TensorFlow", "SQL"], ct);

        var applicant = await IssueApplicantAsync(ct);
        await SetCandidateSkillsAsync(applicant, ["Python", "SQL"], ct);

        using var client = CreateClient();
        using var request = BuildRequest(HttpMethod.Get, "/api/v1/aidx/research-matches", applicant);
        var response = await client.SendAsync(request, ct);
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var matches = (await response.Content.ReadFromJsonAsync<List<ResearchMatchResponse>>(ct))!;
        var match = matches.Should().ContainSingle(m => m.ProjectId == projectId).Subject;

        match.MatchedSkills.Should().BeEquivalentTo("Python", "SQL");
        match.SkillsNotYetDemonstrated.Should().BeEquivalentTo("TensorFlow");
        match.SkillCoverage.Should().BeApproximately(2.0 / 3.0, 0.0001);
        match.MatchQuality.Should().Be(nameof(JobMatchQuality.PartialMatch));
        match.OpenOpportunities.Should().BeEmpty("no job has been linked to this project");
    }

    [Fact]
    public async Task A_candidate_with_no_profile_yet_gets_every_technology_as_not_yet_demonstrated_not_an_error()
    {
        var ct = TestContext.Current.CancellationToken;
        var projectId = await InsertPublishedProjectAsync(["Python"], ct);
        var applicant = await IssueApplicantAsync(ct); // no PUT to /profile/candidate at all

        using var client = CreateClient();
        using var request = BuildRequest(HttpMethod.Get, "/api/v1/aidx/research-matches", applicant);
        var response = await client.SendAsync(request, ct);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var matches = (await response.Content.ReadFromJsonAsync<List<ResearchMatchResponse>>(ct))!;
        var match = matches.Should().ContainSingle(m => m.ProjectId == projectId).Subject;
        match.MatchedSkills.Should().BeEmpty();
        match.SkillsNotYetDemonstrated.Should().BeEquivalentTo("Python");
        match.SkillCoverage.Should().Be(0.0);
    }

    [Fact]
    public async Task A_project_with_no_recorded_technologies_is_unscored_not_a_zero()
    {
        var ct = TestContext.Current.CancellationToken;
        var projectId = await InsertPublishedProjectAsync([], ct);
        var applicant = await IssueApplicantAsync(ct);
        await SetCandidateSkillsAsync(applicant, ["Python"], ct);

        using var client = CreateClient();
        using var request = BuildRequest(HttpMethod.Get, "/api/v1/aidx/research-matches", applicant);
        var response = await client.SendAsync(request, ct);

        var matches = (await response.Content.ReadFromJsonAsync<List<ResearchMatchResponse>>(ct))!;
        var match = matches.Should().ContainSingle(m => m.ProjectId == projectId).Subject;
        match.SkillCoverage.Should().BeNull();
        match.MatchQuality.Should().Be(nameof(JobMatchQuality.Unscored));
    }

    [Fact]
    public async Task Draft_projects_never_appear_in_matches()
    {
        var ct = TestContext.Current.CancellationToken;
        var draftId = await InsertDraftProjectAsync(ct);
        var applicant = await IssueApplicantAsync(ct);
        await SetCandidateSkillsAsync(applicant, ["C#"], ct);

        using var client = CreateClient();
        using var request = BuildRequest(HttpMethod.Get, "/api/v1/aidx/research-matches", applicant);
        var response = await client.SendAsync(request, ct);
        var matches = (await response.Content.ReadFromJsonAsync<List<ResearchMatchResponse>>(ct))!;

        matches.Should().NotContain(m => m.ProjectId == draftId);
    }

    [Fact]
    public async Task A_published_linked_research_job_is_surfaced_as_an_open_opportunity_an_unpublished_one_is_not()
    {
        var ct = TestContext.Current.CancellationToken;
        var projectId = await InsertPublishedProjectAsync(["Python"], ct);
        var recruiter = await IssueRecruiterWithCompanyAsync(ct);

        var publishedJobId = await CreateLinkedResearchJobAsync(recruiter, projectId, publish: true, ct);
        var draftJobId = await CreateLinkedResearchJobAsync(recruiter, projectId, publish: false, ct);

        var applicant = await IssueApplicantAsync(ct);
        await SetCandidateSkillsAsync(applicant, ["Python"], ct);

        using var client = CreateClient();
        using var request = BuildRequest(HttpMethod.Get, "/api/v1/aidx/research-matches", applicant);
        var response = await client.SendAsync(request, ct);
        var matches = (await response.Content.ReadFromJsonAsync<List<ResearchMatchResponse>>(ct))!;
        var match = matches.Should().ContainSingle(m => m.ProjectId == projectId).Subject;

        match.OpenOpportunities.Should().ContainSingle(o => o.JobId == publishedJobId);
        match.OpenOpportunities.Should().NotContain(o => o.JobId == draftJobId);
    }

    [Fact]
    public async Task Invalid_limit_returns_a_validation_problem()
    {
        var ct = TestContext.Current.CancellationToken;
        var applicant = await IssueApplicantAsync(ct);
        using var client = CreateClient();
        using var request = BuildRequest(HttpMethod.Get, "/api/v1/aidx/research-matches?limit=0", applicant);

        var response = await client.SendAsync(request, ct);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
}
