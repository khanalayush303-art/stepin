using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using StepIn.Api.Endpoints;

namespace StepIn.Api.Tests;

/// <summary>
/// Exercises the candidate job-bookmark endpoints against the same real
/// Clerk-token-validation → user-sync → authorization pipeline
/// <see cref="AuthEndpointsTests"/> uses (see <see cref="AuthApiFactory"/>).
/// Mirrors <see cref="ApplicationEndpointsTests"/>'s ownership/validation
/// conventions — a saved job is a second, independent candidate-owned
/// relationship to a job, proven the same way an application is.
/// </summary>
public sealed class SavedJobEndpointsTests(AuthApiFactory factory) : IClassFixture<AuthApiFactory>
{
    private readonly AuthApiFactory _factory = factory;

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

    private async Task<string> IssueApplicantWithProfileTokenAsync(CancellationToken ct)
    {
        using var client = CreateClient();
        var clerkUserId = $"user_{Guid.NewGuid():N}";
        var email = $"{Guid.NewGuid()}@example.com";
        var token = AuthApiFactory.IssueToken(clerkUserId, email);

        using var setup = BuildRequest(HttpMethod.Post, "/api/v1/auth/account-setup", token, new AccountSetupRequest("Applicant"));
        (await client.SendAsync(setup, ct)).EnsureSuccessStatusCode();

        using var profile = BuildRequest(
            HttpMethod.Put,
            "/api/v1/profile/candidate",
            token,
            new UpdateCandidateProfileRequest(null, null, "Graduate", null, null, null, null, null, null, null, null, null));
        (await client.SendAsync(profile, ct)).EnsureSuccessStatusCode();

        return token;
    }

    private async Task<string> IssueApplicantWithoutProfileTokenAsync(CancellationToken ct)
    {
        using var client = CreateClient();
        var clerkUserId = $"user_{Guid.NewGuid():N}";
        var email = $"{Guid.NewGuid()}@example.com";
        var token = AuthApiFactory.IssueToken(clerkUserId, email);

        using var setup = BuildRequest(HttpMethod.Post, "/api/v1/auth/account-setup", token, new AccountSetupRequest("Applicant"));
        (await client.SendAsync(setup, ct)).EnsureSuccessStatusCode();

        return token;
    }

    private async Task<string> IssueRecruiterWithCompanyTokenAsync(CancellationToken ct, string companyName)
    {
        using var client = CreateClient();
        var clerkUserId = $"user_{Guid.NewGuid():N}";
        var email = $"{Guid.NewGuid()}@example.com";
        var token = AuthApiFactory.IssueToken(clerkUserId, email);

        using var setup = BuildRequest(HttpMethod.Post, "/api/v1/auth/account-setup", token, new AccountSetupRequest("Recruiter"));
        (await client.SendAsync(setup, ct)).EnsureSuccessStatusCode();

        using var profile = BuildRequest(
            HttpMethod.Put,
            "/api/v1/profile/recruiter",
            token,
            new UpdateRecruiterProfileRequest(null, null, null, new CompanyDto(null, companyName, null, null, null, null, null)));
        (await client.SendAsync(profile, ct)).EnsureSuccessStatusCode();

        return token;
    }

    private static CreateJobRequest ValidCreateRequest(string title = "Graduate Software Engineer") => new(
        title, "Build things that matter.", "FullTime", "Hybrid", "Sydney, NSW", "$70k-80k", ["C#", "SQL"]);

    private async Task<JobResponse> CreateJobAsync(HttpClient client, string recruiterToken, CreateJobRequest? request, CancellationToken ct)
    {
        using var create = BuildRequest(HttpMethod.Post, "/api/v1/recruiter/jobs", recruiterToken, request ?? ValidCreateRequest());
        var response = await client.SendAsync(create, ct);
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        return (await response.Content.ReadFromJsonAsync<JobResponse>(ct))!;
    }

    private async Task<JobResponse> CreateAndPublishJobAsync(HttpClient client, string recruiterToken, CreateJobRequest? request, CancellationToken ct)
    {
        var created = await CreateJobAsync(client, recruiterToken, request, ct);
        using var publish = BuildRequest(HttpMethod.Post, $"/api/v1/recruiter/jobs/{created.Id}/publish", recruiterToken);
        (await client.SendAsync(publish, ct)).EnsureSuccessStatusCode();
        return created;
    }

    // ---------------------------------------------------------------------------- auth ---

    [Fact]
    public async Task Saved_job_endpoints_without_a_token_are_unauthorized()
    {
        var ct = TestContext.Current.CancellationToken;
        using var client = CreateClient();
        var jobId = Guid.NewGuid();

        (await client.SendAsync(BuildRequest(HttpMethod.Post, $"/api/v1/jobs/{jobId}/saved", null), ct))
            .StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await client.SendAsync(BuildRequest(HttpMethod.Delete, $"/api/v1/jobs/{jobId}/saved", null), ct))
            .StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await client.SendAsync(BuildRequest(HttpMethod.Get, "/api/v1/saved-jobs", null), ct))
            .StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // ---------------------------------------------------------------------------- role ---

    [Fact]
    public async Task Recruiter_cannot_use_candidate_saved_job_endpoints()
    {
        var ct = TestContext.Current.CancellationToken;
        using var client = CreateClient();
        var recruiterToken = await IssueRecruiterWithCompanyTokenAsync(ct, "Acme Pty Ltd");
        var jobId = Guid.NewGuid();

        (await client.SendAsync(BuildRequest(HttpMethod.Post, $"/api/v1/jobs/{jobId}/saved", recruiterToken), ct))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await client.SendAsync(BuildRequest(HttpMethod.Delete, $"/api/v1/jobs/{jobId}/saved", recruiterToken), ct))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await client.SendAsync(BuildRequest(HttpMethod.Get, "/api/v1/saved-jobs", recruiterToken), ct))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    // ------------------------------------------------------------------ creation rules ---

    [Fact]
    public async Task Saving_without_a_candidate_profile_is_rejected()
    {
        var ct = TestContext.Current.CancellationToken;
        using var client = CreateClient();
        var applicantToken = await IssueApplicantWithoutProfileTokenAsync(ct);
        var recruiterToken = await IssueRecruiterWithCompanyTokenAsync(ct, "Acme Pty Ltd");
        var job = await CreateAndPublishJobAsync(client, recruiterToken, null, ct);

        using var request = BuildRequest(HttpMethod.Post, $"/api/v1/jobs/{job.Id}/saved", applicantToken);
        var response = await client.SendAsync(request, ct);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Saving_an_unknown_job_returns_404()
    {
        var ct = TestContext.Current.CancellationToken;
        using var client = CreateClient();
        var applicantToken = await IssueApplicantWithProfileTokenAsync(ct);

        using var request = BuildRequest(HttpMethod.Post, $"/api/v1/jobs/{Guid.NewGuid()}/saved", applicantToken);
        var response = await client.SendAsync(request, ct);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Saving_a_draft_job_returns_404()
    {
        var ct = TestContext.Current.CancellationToken;
        using var client = CreateClient();
        var applicantToken = await IssueApplicantWithProfileTokenAsync(ct);
        var recruiterToken = await IssueRecruiterWithCompanyTokenAsync(ct, "Acme Pty Ltd");
        var draft = await CreateJobAsync(client, recruiterToken, null, ct);

        using var request = BuildRequest(HttpMethod.Post, $"/api/v1/jobs/{draft.Id}/saved", applicantToken);
        var response = await client.SendAsync(request, ct);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Saving_an_unpublished_job_returns_404()
    {
        var ct = TestContext.Current.CancellationToken;
        using var client = CreateClient();
        var applicantToken = await IssueApplicantWithProfileTokenAsync(ct);
        var recruiterToken = await IssueRecruiterWithCompanyTokenAsync(ct, "Acme Pty Ltd");
        var job = await CreateAndPublishJobAsync(client, recruiterToken, null, ct);
        using var unpublish = BuildRequest(HttpMethod.Post, $"/api/v1/recruiter/jobs/{job.Id}/unpublish", recruiterToken);
        (await client.SendAsync(unpublish, ct)).EnsureSuccessStatusCode();

        using var request = BuildRequest(HttpMethod.Post, $"/api/v1/jobs/{job.Id}/saved", applicantToken);
        var response = await client.SendAsync(request, ct);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // -------------------------------------------------------------------- happy path ---

    [Fact]
    public async Task Candidate_can_save_see_in_list_and_unsave_a_job()
    {
        var ct = TestContext.Current.CancellationToken;
        using var client = CreateClient();
        var applicantToken = await IssueApplicantWithProfileTokenAsync(ct);
        var recruiterToken = await IssueRecruiterWithCompanyTokenAsync(ct, "Acme Pty Ltd");
        var job = await CreateAndPublishJobAsync(client, recruiterToken, null, ct);

        using var save = BuildRequest(HttpMethod.Post, $"/api/v1/jobs/{job.Id}/saved", applicantToken);
        var saveResponse = await client.SendAsync(save, ct);
        saveResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var saved = await saveResponse.Content.ReadFromJsonAsync<SavedJobResponse>(ct);
        saved!.JobId.Should().Be(job.Id);
        saved.JobTitle.Should().Be(job.Title);
        saved.JobStatus.Should().Be("Published");

        using var list = BuildRequest(HttpMethod.Get, "/api/v1/saved-jobs", applicantToken);
        var listResponse = await client.SendAsync(list, ct);
        listResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var summaries = await listResponse.Content.ReadFromJsonAsync<List<SavedJobResponse>>(ct);
        summaries!.Should().ContainSingle(s => s.JobId == job.Id);

        using var unsave = BuildRequest(HttpMethod.Delete, $"/api/v1/jobs/{job.Id}/saved", applicantToken);
        var unsaveResponse = await client.SendAsync(unsave, ct);
        unsaveResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        using var listAfter = BuildRequest(HttpMethod.Get, "/api/v1/saved-jobs", applicantToken);
        var afterResponse = await client.SendAsync(listAfter, ct);
        var afterSummaries = await afterResponse.Content.ReadFromJsonAsync<List<SavedJobResponse>>(ct);
        afterSummaries!.Should().NotContain(s => s.JobId == job.Id);
    }

    [Fact]
    public async Task A_candidate_with_no_saved_jobs_gets_an_empty_list_not_an_error()
    {
        var ct = TestContext.Current.CancellationToken;
        using var client = CreateClient();
        var applicantToken = await IssueApplicantWithProfileTokenAsync(ct);

        using var list = BuildRequest(HttpMethod.Get, "/api/v1/saved-jobs", applicantToken);
        var response = await client.SendAsync(list, ct);
        var summaries = await response.Content.ReadFromJsonAsync<List<SavedJobResponse>>(ct);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        summaries.Should().BeEmpty();
    }

    [Fact]
    public async Task Saved_jobs_list_is_ordered_newest_saved_first()
    {
        var ct = TestContext.Current.CancellationToken;
        using var client = CreateClient();
        var applicantToken = await IssueApplicantWithProfileTokenAsync(ct);
        var recruiterToken = await IssueRecruiterWithCompanyTokenAsync(ct, "Acme Pty Ltd");
        var first = await CreateAndPublishJobAsync(client, recruiterToken, ValidCreateRequest("First Role"), ct);
        var second = await CreateAndPublishJobAsync(client, recruiterToken, ValidCreateRequest("Second Role"), ct);

        (await client.SendAsync(BuildRequest(HttpMethod.Post, $"/api/v1/jobs/{first.Id}/saved", applicantToken), ct)).EnsureSuccessStatusCode();
        (await client.SendAsync(BuildRequest(HttpMethod.Post, $"/api/v1/jobs/{second.Id}/saved", applicantToken), ct)).EnsureSuccessStatusCode();

        using var list = BuildRequest(HttpMethod.Get, "/api/v1/saved-jobs", applicantToken);
        var summaries = await (await client.SendAsync(list, ct)).Content.ReadFromJsonAsync<List<SavedJobResponse>>(ct);

        summaries!.Select(s => s.JobId).Should().Equal(second.Id, first.Id);
    }

    [Fact]
    public async Task An_unpublished_saved_job_still_appears_in_the_list_with_its_current_status()
    {
        var ct = TestContext.Current.CancellationToken;
        using var client = CreateClient();
        var applicantToken = await IssueApplicantWithProfileTokenAsync(ct);
        var recruiterToken = await IssueRecruiterWithCompanyTokenAsync(ct, "Acme Pty Ltd");
        var job = await CreateAndPublishJobAsync(client, recruiterToken, null, ct);
        (await client.SendAsync(BuildRequest(HttpMethod.Post, $"/api/v1/jobs/{job.Id}/saved", applicantToken), ct)).EnsureSuccessStatusCode();

        using var unpublish = BuildRequest(HttpMethod.Post, $"/api/v1/recruiter/jobs/{job.Id}/unpublish", recruiterToken);
        (await client.SendAsync(unpublish, ct)).EnsureSuccessStatusCode();

        using var list = BuildRequest(HttpMethod.Get, "/api/v1/saved-jobs", applicantToken);
        var summaries = await (await client.SendAsync(list, ct)).Content.ReadFromJsonAsync<List<SavedJobResponse>>(ct);

        summaries!.Single(s => s.JobId == job.Id).JobStatus.Should().Be("Unpublished");
    }

    // --------------------------------------------------------------- duplicate handling ---

    [Fact]
    public async Task Saving_the_same_job_twice_is_rejected()
    {
        var ct = TestContext.Current.CancellationToken;
        using var client = CreateClient();
        var applicantToken = await IssueApplicantWithProfileTokenAsync(ct);
        var recruiterToken = await IssueRecruiterWithCompanyTokenAsync(ct, "Acme Pty Ltd");
        var job = await CreateAndPublishJobAsync(client, recruiterToken, null, ct);

        using var first = BuildRequest(HttpMethod.Post, $"/api/v1/jobs/{job.Id}/saved", applicantToken);
        (await client.SendAsync(first, ct)).StatusCode.Should().Be(HttpStatusCode.Created);

        using var second = BuildRequest(HttpMethod.Post, $"/api/v1/jobs/{job.Id}/saved", applicantToken);
        (await client.SendAsync(second, ct)).StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Concurrent_duplicate_saves_leave_exactly_one_row_and_the_database_constraint_is_the_real_guard()
    {
        var ct = TestContext.Current.CancellationToken;
        using var client = CreateClient();
        var applicantToken = await IssueApplicantWithProfileTokenAsync(ct);
        var recruiterToken = await IssueRecruiterWithCompanyTokenAsync(ct, "Acme Pty Ltd");
        var job = await CreateAndPublishJobAsync(client, recruiterToken, null, ct);

        // Two genuinely simultaneous requests race past the in-request existence
        // check together — only the unique (CandidateProfileId, JobId) index can
        // stop both from succeeding.
        var first = client.SendAsync(BuildRequest(HttpMethod.Post, $"/api/v1/jobs/{job.Id}/saved", applicantToken), ct);
        var second = client.SendAsync(BuildRequest(HttpMethod.Post, $"/api/v1/jobs/{job.Id}/saved", applicantToken), ct);
        var responses = await Task.WhenAll(first, second);

        responses.Count(r => r.StatusCode == HttpStatusCode.Created).Should().Be(1);
        responses.Count(r => r.StatusCode == HttpStatusCode.Conflict).Should().Be(1);

        using var list = BuildRequest(HttpMethod.Get, "/api/v1/saved-jobs", applicantToken);
        var summaries = await (await client.SendAsync(list, ct)).Content.ReadFromJsonAsync<List<SavedJobResponse>>(ct);
        summaries!.Count(s => s.JobId == job.Id).Should().Be(1);
    }

    [Fact]
    public async Task Unsaving_a_job_that_was_never_saved_returns_404()
    {
        var ct = TestContext.Current.CancellationToken;
        using var client = CreateClient();
        var applicantToken = await IssueApplicantWithProfileTokenAsync(ct);
        var recruiterToken = await IssueRecruiterWithCompanyTokenAsync(ct, "Acme Pty Ltd");
        var job = await CreateAndPublishJobAsync(client, recruiterToken, null, ct);

        using var unsave = BuildRequest(HttpMethod.Delete, $"/api/v1/jobs/{job.Id}/saved", applicantToken);
        (await client.SendAsync(unsave, ct)).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // --------------------------------------------------------------------- ownership ---

    [Fact]
    public async Task Candidate_B_cannot_see_candidate_As_saved_job()
    {
        var ct = TestContext.Current.CancellationToken;
        using var client = CreateClient();
        var applicantA = await IssueApplicantWithProfileTokenAsync(ct);
        var applicantB = await IssueApplicantWithProfileTokenAsync(ct);
        var recruiterToken = await IssueRecruiterWithCompanyTokenAsync(ct, "Acme Pty Ltd");
        var job = await CreateAndPublishJobAsync(client, recruiterToken, null, ct);

        using var save = BuildRequest(HttpMethod.Post, $"/api/v1/jobs/{job.Id}/saved", applicantA);
        (await client.SendAsync(save, ct)).EnsureSuccessStatusCode();

        using var listAsB = BuildRequest(HttpMethod.Get, "/api/v1/saved-jobs", applicantB);
        var summariesB = await (await client.SendAsync(listAsB, ct)).Content.ReadFromJsonAsync<List<SavedJobResponse>>(ct);
        summariesB!.Should().NotContain(s => s.JobId == job.Id);

        // A's own list is unaffected by B's unrelated (empty) list.
        using var listAsA = BuildRequest(HttpMethod.Get, "/api/v1/saved-jobs", applicantA);
        var summariesA = await (await client.SendAsync(listAsA, ct)).Content.ReadFromJsonAsync<List<SavedJobResponse>>(ct);
        summariesA!.Should().ContainSingle(s => s.JobId == job.Id);
    }

    [Fact]
    public async Task Candidate_B_cannot_unsave_candidate_As_saved_job()
    {
        var ct = TestContext.Current.CancellationToken;
        using var client = CreateClient();
        var applicantA = await IssueApplicantWithProfileTokenAsync(ct);
        var applicantB = await IssueApplicantWithProfileTokenAsync(ct);
        var recruiterToken = await IssueRecruiterWithCompanyTokenAsync(ct, "Acme Pty Ltd");
        var job = await CreateAndPublishJobAsync(client, recruiterToken, null, ct);

        using var save = BuildRequest(HttpMethod.Post, $"/api/v1/jobs/{job.Id}/saved", applicantA);
        (await client.SendAsync(save, ct)).EnsureSuccessStatusCode();

        using var unsaveAsB = BuildRequest(HttpMethod.Delete, $"/api/v1/jobs/{job.Id}/saved", applicantB);
        (await client.SendAsync(unsaveAsB, ct)).StatusCode.Should().Be(HttpStatusCode.NotFound);

        // A's saved job is completely unaffected by B's rejected attempt.
        using var listAsA = BuildRequest(HttpMethod.Get, "/api/v1/saved-jobs", applicantA);
        var summariesA = await (await client.SendAsync(listAsA, ct)).Content.ReadFromJsonAsync<List<SavedJobResponse>>(ct);
        summariesA!.Should().ContainSingle(s => s.JobId == job.Id);
    }
}
