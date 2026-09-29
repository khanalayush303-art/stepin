using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using StepIn.Api.Endpoints;

namespace StepIn.Api.Tests;

/// <summary>
/// Exercises the candidate job-application endpoints against the same real
/// Clerk-token-validation → user-sync → authorization pipeline
/// <see cref="AuthEndpointsTests"/> uses (see <see cref="AuthApiFactory"/>).
/// </summary>
public sealed class ApplicationEndpointsTests(AuthApiFactory factory) : IClassFixture<AuthApiFactory>
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

    private static HttpRequestMessage BuildMultipartRequest(
        string url, string? token, byte[]? resumeBytes, string fileName = "resume.pdf", string contentType = "application/pdf",
        string? coverLetter = null, IReadOnlyDictionary<string, string>? extraFields = null)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, url);
        var form = new MultipartFormDataContent();

        if (resumeBytes is not null)
        {
            var fileContent = new ByteArrayContent(resumeBytes);
            fileContent.Headers.ContentType = new MediaTypeHeaderValue(contentType);
            form.Add(fileContent, "Resume", fileName);
        }

        if (coverLetter is not null)
        {
            form.Add(new StringContent(coverLetter), "CoverLetter");
        }

        if (extraFields is not null)
        {
            foreach (var (name, value) in extraFields)
            {
                form.Add(new StringContent(value), name);
            }
        }

        request.Content = form;
        request.Headers.Add("X-Requested-With", "fetch");

        if (token is not null)
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }

        return request;
    }

    private static byte[] MinimalPdfBytes() => "%PDF-1.4 minimal test resume"u8.ToArray();

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

    /// <summary>A recruiter with a complete profile and company, needed to create/publish jobs.</summary>
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
    public async Task Application_endpoints_without_a_token_are_unauthorized()
    {
        var ct = TestContext.Current.CancellationToken;
        using var client = CreateClient();

        (await client.SendAsync(BuildRequest(HttpMethod.Get, "/api/v1/applications", null), ct)).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await client.SendAsync(BuildRequest(HttpMethod.Get, $"/api/v1/applications/{Guid.NewGuid()}", null), ct)).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await client.SendAsync(BuildMultipartRequest($"/api/v1/jobs/{Guid.NewGuid()}/applications", null, MinimalPdfBytes()), ct)).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // ------------------------------------------------------------------ creation rules ---

    [Fact]
    public async Task Applying_without_a_candidate_profile_is_rejected()
    {
        var ct = TestContext.Current.CancellationToken;
        using var client = CreateClient();
        var applicantToken = await IssueApplicantWithoutProfileTokenAsync(ct);
        var recruiterToken = await IssueRecruiterWithCompanyTokenAsync(ct, "Acme Pty Ltd");
        var job = await CreateAndPublishJobAsync(client, recruiterToken, null, ct);

        using var request = BuildMultipartRequest($"/api/v1/jobs/{job.Id}/applications", applicantToken, MinimalPdfBytes());
        var response = await client.SendAsync(request, ct);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Applying_to_a_draft_job_returns_404()
    {
        var ct = TestContext.Current.CancellationToken;
        using var client = CreateClient();
        var applicantToken = await IssueApplicantWithProfileTokenAsync(ct);
        var recruiterToken = await IssueRecruiterWithCompanyTokenAsync(ct, "Acme Pty Ltd");
        var draft = await CreateJobAsync(client, recruiterToken, null, ct);

        using var request = BuildMultipartRequest($"/api/v1/jobs/{draft.Id}/applications", applicantToken, MinimalPdfBytes());
        var response = await client.SendAsync(request, ct);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Applying_to_an_unpublished_job_returns_404()
    {
        var ct = TestContext.Current.CancellationToken;
        using var client = CreateClient();
        var applicantToken = await IssueApplicantWithProfileTokenAsync(ct);
        var recruiterToken = await IssueRecruiterWithCompanyTokenAsync(ct, "Acme Pty Ltd");
        var job = await CreateAndPublishJobAsync(client, recruiterToken, null, ct);
        using var unpublish = BuildRequest(HttpMethod.Post, $"/api/v1/recruiter/jobs/{job.Id}/unpublish", recruiterToken);
        (await client.SendAsync(unpublish, ct)).EnsureSuccessStatusCode();

        using var request = BuildMultipartRequest($"/api/v1/jobs/{job.Id}/applications", applicantToken, MinimalPdfBytes());
        var response = await client.SendAsync(request, ct);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Applying_to_an_unknown_job_returns_404()
    {
        var ct = TestContext.Current.CancellationToken;
        using var client = CreateClient();
        var applicantToken = await IssueApplicantWithProfileTokenAsync(ct);

        using var request = BuildMultipartRequest($"/api/v1/jobs/{Guid.NewGuid()}/applications", applicantToken, MinimalPdfBytes());
        var response = await client.SendAsync(request, ct);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Applying_without_a_resume_is_rejected()
    {
        var ct = TestContext.Current.CancellationToken;
        using var client = CreateClient();
        var applicantToken = await IssueApplicantWithProfileTokenAsync(ct);
        var recruiterToken = await IssueRecruiterWithCompanyTokenAsync(ct, "Acme Pty Ltd");
        var job = await CreateAndPublishJobAsync(client, recruiterToken, null, ct);

        using var request = BuildMultipartRequest($"/api/v1/jobs/{job.Id}/applications", applicantToken, resumeBytes: null);
        var response = await client.SendAsync(request, ct);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Applying_with_an_invalid_resume_extension_is_rejected()
    {
        var ct = TestContext.Current.CancellationToken;
        using var client = CreateClient();
        var applicantToken = await IssueApplicantWithProfileTokenAsync(ct);
        var recruiterToken = await IssueRecruiterWithCompanyTokenAsync(ct, "Acme Pty Ltd");
        var job = await CreateAndPublishJobAsync(client, recruiterToken, null, ct);

        using var request = BuildMultipartRequest(
            $"/api/v1/jobs/{job.Id}/applications", applicantToken, "not a resume"u8.ToArray(),
            fileName: "resume.exe", contentType: "application/octet-stream");
        var response = await client.SendAsync(request, ct);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Applying_with_an_oversized_resume_is_rejected()
    {
        var ct = TestContext.Current.CancellationToken;
        using var client = CreateClient();
        var applicantToken = await IssueApplicantWithProfileTokenAsync(ct);
        var recruiterToken = await IssueRecruiterWithCompanyTokenAsync(ct, "Acme Pty Ltd");
        var job = await CreateAndPublishJobAsync(client, recruiterToken, null, ct);
        var oversized = new byte[6 * 1024 * 1024];

        using var request = BuildMultipartRequest($"/api/v1/jobs/{job.Id}/applications", applicantToken, oversized);
        var response = await client.SendAsync(request, ct);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Ownership_injection_fields_in_the_form_body_are_ignored()
    {
        var ct = TestContext.Current.CancellationToken;
        using var client = CreateClient();
        var applicantToken = await IssueApplicantWithProfileTokenAsync(ct);
        var recruiterToken = await IssueRecruiterWithCompanyTokenAsync(ct, "Acme Pty Ltd");
        var job = await CreateAndPublishJobAsync(client, recruiterToken, null, ct);

        // CreateApplication has no bound parameter for any of these — there is
        // nothing on the server for a malicious client to override.
        using var request = BuildMultipartRequest(
            $"/api/v1/jobs/{job.Id}/applications", applicantToken, MinimalPdfBytes(),
            extraFields: new Dictionary<string, string>
            {
                ["candidateProfileId"] = Guid.NewGuid().ToString(),
                ["status"] = "Reviewed",
                ["createdAt"] = "2020-01-01T00:00:00Z",
            });
        var response = await client.SendAsync(request, ct);
        var created = (await response.Content.ReadFromJsonAsync<ApplicationResponse>(ct))!;

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        created.Status.Should().Be("Submitted");
        created.CreatedAt.Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromMinutes(1));
    }

    // ----------------------------------------------------------------- lifecycle ---

    [Fact]
    public async Task An_applicant_can_submit_list_and_view_their_own_application()
    {
        var ct = TestContext.Current.CancellationToken;
        using var client = CreateClient();
        var applicantToken = await IssueApplicantWithProfileTokenAsync(ct);
        var recruiterToken = await IssueRecruiterWithCompanyTokenAsync(ct, "Acme Pty Ltd");
        var job = await CreateAndPublishJobAsync(client, recruiterToken, null, ct);

        using var submit = BuildMultipartRequest($"/api/v1/jobs/{job.Id}/applications", applicantToken, MinimalPdfBytes(), coverLetter: "I'd love to join.");
        var submitResponse = await client.SendAsync(submit, ct);
        submitResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var created = (await submitResponse.Content.ReadFromJsonAsync<ApplicationResponse>(ct))!;
        created.JobId.Should().Be(job.Id);
        created.CoverLetter.Should().Be("I'd love to join.");
        created.ResumeFileName.Should().Be("resume.pdf");

        using var list = BuildRequest(HttpMethod.Get, "/api/v1/applications", applicantToken);
        var listResponse = await client.SendAsync(list, ct);
        var summaries = await listResponse.Content.ReadFromJsonAsync<List<ApplicationSummaryResponse>>(ct);
        summaries!.Should().ContainSingle(a => a.Id == created.Id);

        using var details = BuildRequest(HttpMethod.Get, $"/api/v1/applications/{created.Id}", applicantToken);
        var detailsResponse = await client.SendAsync(details, ct);
        detailsResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var fetched = await detailsResponse.Content.ReadFromJsonAsync<ApplicationResponse>(ct);
        fetched!.Id.Should().Be(created.Id);

        using var resume = BuildRequest(HttpMethod.Get, $"/api/v1/applications/{created.Id}/resume", applicantToken);
        var resumeResponse = await client.SendAsync(resume, ct);
        resumeResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        (await resumeResponse.Content.ReadAsByteArrayAsync(ct)).Should().BeEquivalentTo(MinimalPdfBytes());
    }

    [Fact]
    public async Task An_applicant_with_no_applications_gets_an_empty_list_not_an_error()
    {
        var ct = TestContext.Current.CancellationToken;
        using var client = CreateClient();
        var applicantToken = await IssueApplicantWithProfileTokenAsync(ct);

        using var request = BuildRequest(HttpMethod.Get, "/api/v1/applications", applicantToken);
        var response = await client.SendAsync(request, ct);
        var summaries = await response.Content.ReadFromJsonAsync<List<ApplicationSummaryResponse>>(ct);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        summaries.Should().BeEmpty();
    }

    [Fact]
    public async Task Eligibility_endpoint_reflects_whether_the_candidate_has_applied()
    {
        var ct = TestContext.Current.CancellationToken;
        using var client = CreateClient();
        var applicantToken = await IssueApplicantWithProfileTokenAsync(ct);
        var recruiterToken = await IssueRecruiterWithCompanyTokenAsync(ct, "Acme Pty Ltd");
        var job = await CreateAndPublishJobAsync(client, recruiterToken, null, ct);

        using var before = BuildRequest(HttpMethod.Get, $"/api/v1/jobs/{job.Id}/applications/mine", applicantToken);
        var beforeResponse = await (await client.SendAsync(before, ct)).Content.ReadFromJsonAsync<ApplicationEligibilityResponse>(ct);
        beforeResponse!.HasApplied.Should().BeFalse();

        using var submit = BuildMultipartRequest($"/api/v1/jobs/{job.Id}/applications", applicantToken, MinimalPdfBytes());
        var created = (await (await client.SendAsync(submit, ct)).Content.ReadFromJsonAsync<ApplicationResponse>(ct))!;

        using var after = BuildRequest(HttpMethod.Get, $"/api/v1/jobs/{job.Id}/applications/mine", applicantToken);
        var afterResponse = await (await client.SendAsync(after, ct)).Content.ReadFromJsonAsync<ApplicationEligibilityResponse>(ct);
        afterResponse!.HasApplied.Should().BeTrue();
        afterResponse.ApplicationId.Should().Be(created.Id);
    }

    // --------------------------------------------------------------- duplicate prevention ---

    [Fact]
    public async Task A_second_application_to_the_same_job_by_the_same_candidate_is_rejected()
    {
        var ct = TestContext.Current.CancellationToken;
        using var client = CreateClient();
        var applicantToken = await IssueApplicantWithProfileTokenAsync(ct);
        var recruiterToken = await IssueRecruiterWithCompanyTokenAsync(ct, "Acme Pty Ltd");
        var job = await CreateAndPublishJobAsync(client, recruiterToken, null, ct);

        using var first = BuildMultipartRequest($"/api/v1/jobs/{job.Id}/applications", applicantToken, MinimalPdfBytes());
        (await client.SendAsync(first, ct)).StatusCode.Should().Be(HttpStatusCode.Created);

        using var second = BuildMultipartRequest($"/api/v1/jobs/{job.Id}/applications", applicantToken, MinimalPdfBytes());
        var secondResponse = await client.SendAsync(second, ct);

        secondResponse.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Concurrent_duplicate_applications_leave_exactly_one_row_and_the_database_constraint_is_the_real_guard()
    {
        var ct = TestContext.Current.CancellationToken;
        using var client = CreateClient();
        var applicantToken = await IssueApplicantWithProfileTokenAsync(ct);
        var recruiterToken = await IssueRecruiterWithCompanyTokenAsync(ct, "Acme Pty Ltd");
        var job = await CreateAndPublishJobAsync(client, recruiterToken, null, ct);

        // Two genuinely simultaneous requests race past the in-request existence
        // check together — only the unique (CandidateProfileId, JobId) index can
        // stop both from succeeding.
        var first = client.SendAsync(BuildMultipartRequest($"/api/v1/jobs/{job.Id}/applications", applicantToken, MinimalPdfBytes()), ct);
        var second = client.SendAsync(BuildMultipartRequest($"/api/v1/jobs/{job.Id}/applications", applicantToken, MinimalPdfBytes()), ct);
        var responses = await Task.WhenAll(first, second);

        responses.Count(r => r.StatusCode == HttpStatusCode.Created).Should().Be(1);
        responses.Count(r => r.StatusCode == HttpStatusCode.Conflict).Should().Be(1);

        using var list = BuildRequest(HttpMethod.Get, "/api/v1/applications", applicantToken);
        var summaries = await (await client.SendAsync(list, ct)).Content.ReadFromJsonAsync<List<ApplicationSummaryResponse>>(ct);
        summaries!.Count(a => a.JobId == job.Id).Should().Be(1);
    }

    // --------------------------------------------------------------------- ownership ---

    [Fact]
    public async Task Candidate_B_cannot_view_or_download_candidate_As_application()
    {
        var ct = TestContext.Current.CancellationToken;
        using var client = CreateClient();
        var applicantATokenTask = IssueApplicantWithProfileTokenAsync(ct);
        var recruiterToken = await IssueRecruiterWithCompanyTokenAsync(ct, "Acme Pty Ltd");
        var job = await CreateAndPublishJobAsync(client, recruiterToken, null, ct);
        var applicantAToken = await applicantATokenTask;
        var applicantBToken = await IssueApplicantWithProfileTokenAsync(ct);

        using var submit = BuildMultipartRequest($"/api/v1/jobs/{job.Id}/applications", applicantAToken, MinimalPdfBytes());
        var applicationA = (await (await client.SendAsync(submit, ct)).Content.ReadFromJsonAsync<ApplicationResponse>(ct))!;

        using var getAsB = BuildRequest(HttpMethod.Get, $"/api/v1/applications/{applicationA.Id}", applicantBToken);
        (await client.SendAsync(getAsB, ct)).StatusCode.Should().Be(HttpStatusCode.NotFound);

        using var resumeAsB = BuildRequest(HttpMethod.Get, $"/api/v1/applications/{applicationA.Id}/resume", applicantBToken);
        (await client.SendAsync(resumeAsB, ct)).StatusCode.Should().Be(HttpStatusCode.NotFound);

        // A's application is completely unaffected by B's rejected attempts.
        using var getAsA = BuildRequest(HttpMethod.Get, $"/api/v1/applications/{applicationA.Id}", applicantAToken);
        var finalResponse = await client.SendAsync(getAsA, ct);
        var final = await finalResponse.Content.ReadFromJsonAsync<ApplicationResponse>(ct);
        final!.Id.Should().Be(applicationA.Id);
    }

    [Fact]
    public async Task Unknown_application_id_returns_404()
    {
        var ct = TestContext.Current.CancellationToken;
        using var client = CreateClient();
        var applicantToken = await IssueApplicantWithProfileTokenAsync(ct);

        using var request = BuildRequest(HttpMethod.Get, $"/api/v1/applications/{Guid.NewGuid()}", applicantToken);
        var response = await client.SendAsync(request, ct);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
