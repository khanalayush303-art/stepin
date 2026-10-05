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
/// Integration fix for AIDX opportunities. The shared job detail endpoint serves any
/// published job (Career or Research), while career discovery stays Career-only. The
/// existing application workflow must accept published Research jobs unchanged.
/// </summary>
public sealed class JobDetailIntegrationTests(AuthApiFactory factory) : IClassFixture<AuthApiFactory>
{
    private readonly AuthApiFactory _factory = factory;

    private HttpClient CreateClient() => _factory.CreateClient();

    private static string Unique(string prefix) => $"{prefix} {Guid.NewGuid():N}";

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

    private static HttpRequestMessage BuildResumeRequest(string url, string token)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        request.Headers.Add("X-Requested-With", "fetch");

        var form = new MultipartFormDataContent();
        var file = new ByteArrayContent("%PDF-1.4 minimal test resume"u8.ToArray());
        file.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");
        form.Add(file, "Resume", "resume.pdf");
        request.Content = form;
        return request;
    }

    private async Task<string> IssueApplicantWithProfileAsync(CancellationToken ct)
    {
        var token = AuthApiFactory.IssueToken($"user_{Guid.NewGuid():N}", $"{Guid.NewGuid()}@example.com");
        using var client = CreateClient();

        using var setup = BuildRequest(HttpMethod.Post, "/api/v1/auth/account-setup", token, new AccountSetupRequest("Applicant"));
        (await client.SendAsync(setup, ct)).EnsureSuccessStatusCode();

        using var profile = BuildRequest(HttpMethod.Put, "/api/v1/profile/candidate", token,
            new UpdateCandidateProfileRequest(null, null, "Graduate", null, null, null, null, null, null, null, null, null));
        (await client.SendAsync(profile, ct)).EnsureSuccessStatusCode();

        return token;
    }

    private async Task<string> IssueRecruiterWithCompanyAsync(CancellationToken ct)
    {
        var token = AuthApiFactory.IssueToken($"user_{Guid.NewGuid():N}", $"{Guid.NewGuid()}@example.com");
        using var client = CreateClient();

        using var setup = BuildRequest(HttpMethod.Post, "/api/v1/auth/account-setup", token, new AccountSetupRequest("Recruiter"));
        (await client.SendAsync(setup, ct)).EnsureSuccessStatusCode();

        using var profile = BuildRequest(HttpMethod.Put, "/api/v1/profile/recruiter", token,
            new UpdateRecruiterProfileRequest(null, null, null, new CompanyDto(null, Unique("Detail Co"), null, null, null, null, null)));
        (await client.SendAsync(profile, ct)).EnsureSuccessStatusCode();

        return token;
    }

    /// <summary>Creates a job as a draft through the existing recruiter endpoint.</summary>
    private async Task<Guid> CreateDraftJobAsync(string recruiterToken, CancellationToken ct)
    {
        using var client = CreateClient();
        using var create = BuildRequest(HttpMethod.Post, "/api/v1/recruiter/jobs", recruiterToken,
            new CreateJobRequest(Unique("Role"), "Work on things.", "FullTime", "Hybrid", "Sydney, NSW", null, ["C#"]));
        var response = await client.SendAsync(create, ct);
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        return (await response.Content.ReadFromJsonAsync<JobResponse>(ct))!.Id;
    }

    private async Task PublishAsync(string recruiterToken, Guid jobId, CancellationToken ct)
    {
        using var client = CreateClient();
        using var publish = BuildRequest(HttpMethod.Post, $"/api/v1/recruiter/jobs/{jobId}/publish", recruiterToken);
        (await client.SendAsync(publish, ct)).EnsureSuccessStatusCode();
    }

    /// <summary>Recategorises a job directly in the database. No API sets Category to Research.</summary>
    private async Task SetCategoryAsync(Guid jobId, JobCategory category, CancellationToken ct)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        await db.Jobs.Where(j => j.Id == jobId)
            .ExecuteUpdateAsync(s => s.SetProperty(j => j.Category, category), ct);
    }

    private async Task<HttpStatusCode> GetDetailAsync(Guid jobId, CancellationToken ct)
    {
        using var client = CreateClient();
        return (await client.GetAsync($"/api/v1/jobs/{jobId}", ct)).StatusCode;
    }

    // ------------------------------------------------------------ detail matrix ---

    [Fact]
    public async Task Published_career_job_detail_returns_200_and_reports_career()
    {
        var ct = TestContext.Current.CancellationToken;
        var recruiter = await IssueRecruiterWithCompanyAsync(ct);
        var jobId = await CreateDraftJobAsync(recruiter, ct);
        await PublishAsync(recruiter, jobId, ct);

        using var client = CreateClient();
        var body = await client.GetFromJsonAsync<PublicJobResponse>($"/api/v1/jobs/{jobId}", ct);

        body!.Category.Should().Be("Career");
    }

    [Fact]
    public async Task Draft_career_job_detail_returns_404()
    {
        var ct = TestContext.Current.CancellationToken;
        var recruiter = await IssueRecruiterWithCompanyAsync(ct);
        var jobId = await CreateDraftJobAsync(recruiter, ct);

        (await GetDetailAsync(jobId, ct)).Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Published_research_job_detail_returns_200_and_reports_research()
    {
        var ct = TestContext.Current.CancellationToken;
        var recruiter = await IssueRecruiterWithCompanyAsync(ct);
        var jobId = await CreateDraftJobAsync(recruiter, ct);
        await SetCategoryAsync(jobId, JobCategory.Research, ct);
        await PublishAsync(recruiter, jobId, ct);

        using var client = CreateClient();
        var body = await client.GetFromJsonAsync<PublicJobResponse>($"/api/v1/jobs/{jobId}", ct);

        body!.Category.Should().Be("Research");
    }

    [Fact]
    public async Task Draft_research_job_detail_returns_404()
    {
        var ct = TestContext.Current.CancellationToken;
        var recruiter = await IssueRecruiterWithCompanyAsync(ct);
        var jobId = await CreateDraftJobAsync(recruiter, ct);
        await SetCategoryAsync(jobId, JobCategory.Research, ct);

        (await GetDetailAsync(jobId, ct)).Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Unpublished_jobs_of_either_category_return_404()
    {
        var ct = TestContext.Current.CancellationToken;
        var recruiter = await IssueRecruiterWithCompanyAsync(ct);

        var career = await CreateDraftJobAsync(recruiter, ct);
        await PublishAsync(recruiter, career, ct);
        using (var client = CreateClient())
        {
            using var unpublish = BuildRequest(HttpMethod.Post, $"/api/v1/recruiter/jobs/{career}/unpublish", recruiter);
            (await client.SendAsync(unpublish, ct)).EnsureSuccessStatusCode();
        }

        var research = await CreateDraftJobAsync(recruiter, ct);
        await SetCategoryAsync(research, JobCategory.Research, ct);
        await PublishAsync(recruiter, research, ct);
        using (var client = CreateClient())
        {
            using var unpublish = BuildRequest(HttpMethod.Post, $"/api/v1/recruiter/jobs/{research}/unpublish", recruiter);
            (await client.SendAsync(unpublish, ct)).EnsureSuccessStatusCode();
        }

        (await GetDetailAsync(career, ct)).Should().Be(HttpStatusCode.NotFound);
        (await GetDetailAsync(research, ct)).Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Unknown_job_id_returns_404()
    {
        (await GetDetailAsync(Guid.NewGuid(), TestContext.Current.CancellationToken)).Should().Be(HttpStatusCode.NotFound);
    }

    // ------------------------------------------------------- career discovery ---

    [Fact]
    public async Task Career_discovery_lists_career_jobs_and_never_research_jobs()
    {
        var ct = TestContext.Current.CancellationToken;
        var recruiter = await IssueRecruiterWithCompanyAsync(ct);

        var career = await CreateDraftJobAsync(recruiter, ct);
        await PublishAsync(recruiter, career, ct);

        var research = await CreateDraftJobAsync(recruiter, ct);
        await SetCategoryAsync(research, JobCategory.Research, ct);
        await PublishAsync(recruiter, research, ct);

        using var client = CreateClient();
        var list = (await client.GetFromJsonAsync<List<PublicJobSummaryResponse>>("/api/v1/jobs", ct))!;

        list.Select(j => j.Id).Should().Contain(career);
        list.Select(j => j.Id).Should().NotContain(research);
    }

    // ----------------------------------------------------------- application ---

    [Fact]
    public async Task A_candidate_can_apply_to_a_published_research_job_through_the_existing_flow()
    {
        var ct = TestContext.Current.CancellationToken;
        var recruiter = await IssueRecruiterWithCompanyAsync(ct);
        var jobId = await CreateDraftJobAsync(recruiter, ct);
        await SetCategoryAsync(jobId, JobCategory.Research, ct);
        await PublishAsync(recruiter, jobId, ct);

        var applicant = await IssueApplicantWithProfileAsync(ct);
        using var client = CreateClient();
        using var apply = BuildResumeRequest($"/api/v1/jobs/{jobId}/applications", applicant);
        var response = await client.SendAsync(apply, ct);

        response.StatusCode.Should().Be(HttpStatusCode.Created);
    }
}
