using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using StepIn.Api.Endpoints;

namespace StepIn.Api.Tests;

/// <summary>
/// Exercises the recruiter-side application-review and status-update
/// endpoints against the same real Clerk-token-validation → user-sync →
/// authorization pipeline <see cref="AuthEndpointsTests"/> uses (see
/// <see cref="AuthApiFactory"/>). The central concern of this file is
/// proving the RecruiterProfile → owned Job → JobApplication ownership
/// chain — deliberately distinct from <see cref="ApplicationEndpointsTests"/>'s
/// candidate-scoped ownership tests.
/// </summary>
public sealed class RecruiterApplicationEndpointsTests(AuthApiFactory factory) : IClassFixture<AuthApiFactory>
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

    private static HttpRequestMessage BuildMultipartRequest(string url, string? token, byte[]? resumeBytes, string fileName = "resume.pdf")
    {
        var request = new HttpRequestMessage(HttpMethod.Post, url);
        var form = new MultipartFormDataContent();

        if (resumeBytes is not null)
        {
            var fileContent = new ByteArrayContent(resumeBytes);
            fileContent.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");
            form.Add(fileContent, "Resume", fileName);
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

    private async Task<string> IssueApplicantWithProfileTokenAsync(CancellationToken ct, string firstName = "Test", string lastName = "User")
    {
        using var client = CreateClient();
        var clerkUserId = $"user_{Guid.NewGuid():N}";
        var email = $"{Guid.NewGuid()}@example.com";
        var token = AuthApiFactory.IssueToken(clerkUserId, email, firstName, lastName);

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

    private async Task<JobResponse> CreateAndPublishJobAsync(HttpClient client, string recruiterToken, CreateJobRequest? request, CancellationToken ct)
    {
        using var create = BuildRequest(HttpMethod.Post, "/api/v1/recruiter/jobs", recruiterToken, request ?? ValidCreateRequest());
        var createResponse = await client.SendAsync(create, ct);
        createResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var created = (await createResponse.Content.ReadFromJsonAsync<JobResponse>(ct))!;

        using var publish = BuildRequest(HttpMethod.Post, $"/api/v1/recruiter/jobs/{created.Id}/publish", recruiterToken);
        (await client.SendAsync(publish, ct)).EnsureSuccessStatusCode();
        return created;
    }

    private async Task<ApplicationResponse> SubmitApplicationAsync(HttpClient client, string applicantToken, Guid jobId, CancellationToken ct)
    {
        using var submit = BuildMultipartRequest($"/api/v1/jobs/{jobId}/applications", applicantToken, MinimalPdfBytes());
        var response = await client.SendAsync(submit, ct);
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        return (await response.Content.ReadFromJsonAsync<ApplicationResponse>(ct))!;
    }

    // ---------------------------------------------------------------------------- auth ---

    [Fact]
    public async Task Recruiter_application_endpoints_without_a_token_are_unauthorized()
    {
        var ct = TestContext.Current.CancellationToken;
        using var client = CreateClient();
        var jobId = Guid.NewGuid();
        var applicationId = Guid.NewGuid();

        (await client.SendAsync(BuildRequest(HttpMethod.Get, $"/api/v1/recruiter/jobs/{jobId}/applications", null), ct))
            .StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await client.SendAsync(BuildRequest(HttpMethod.Get, $"/api/v1/recruiter/applications/{applicationId}", null), ct))
            .StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await client.SendAsync(BuildRequest(HttpMethod.Get, $"/api/v1/recruiter/applications/{applicationId}/resume", null), ct))
            .StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // ---------------------------------------------------------------------------- role ---

    [Fact]
    public async Task Applicant_cannot_access_recruiter_application_endpoints()
    {
        var ct = TestContext.Current.CancellationToken;
        using var client = CreateClient();
        var applicantToken = await IssueApplicantWithProfileTokenAsync(ct);
        var jobId = Guid.NewGuid();
        var applicationId = Guid.NewGuid();

        (await client.SendAsync(BuildRequest(HttpMethod.Get, $"/api/v1/recruiter/jobs/{jobId}/applications", applicantToken), ct))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await client.SendAsync(BuildRequest(HttpMethod.Get, $"/api/v1/recruiter/applications/{applicationId}", applicantToken), ct))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await client.SendAsync(BuildRequest(HttpMethod.Get, $"/api/v1/recruiter/applications/{applicationId}/resume", applicantToken), ct))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    // -------------------------------------------------------------------- happy path ---

    [Fact]
    public async Task Recruiter_can_list_view_and_download_resume_for_an_application_on_their_own_job()
    {
        var ct = TestContext.Current.CancellationToken;
        using var client = CreateClient();
        var recruiterToken = await IssueRecruiterWithCompanyTokenAsync(ct, "Acme Pty Ltd");
        var job = await CreateAndPublishJobAsync(client, recruiterToken, null, ct);
        var applicantToken = await IssueApplicantWithProfileTokenAsync(ct, "Jane", "Doe");
        var application = await SubmitApplicationAsync(client, applicantToken, job.Id, ct);

        using var list = BuildRequest(HttpMethod.Get, $"/api/v1/recruiter/jobs/{job.Id}/applications", recruiterToken);
        var listResponse = await client.SendAsync(list, ct);
        listResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var summaries = await listResponse.Content.ReadFromJsonAsync<List<RecruiterApplicationSummaryResponse>>(ct);
        summaries!.Should().ContainSingle(a => a.Id == application.Id);
        summaries![0].ApplicantName.Should().Be("Jane Doe");
        summaries[0].JobTitle.Should().Be(job.Title);

        using var detail = BuildRequest(HttpMethod.Get, $"/api/v1/recruiter/applications/{application.Id}", recruiterToken);
        var detailResponse = await client.SendAsync(detail, ct);
        detailResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var fetched = await detailResponse.Content.ReadFromJsonAsync<RecruiterApplicationResponse>(ct);
        fetched!.Id.Should().Be(application.Id);
        fetched.ApplicantName.Should().Be("Jane Doe");
        fetched.Status.Should().Be("Submitted");

        using var resume = BuildRequest(HttpMethod.Get, $"/api/v1/recruiter/applications/{application.Id}/resume", recruiterToken);
        var resumeResponse = await client.SendAsync(resume, ct);
        resumeResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        (await resumeResponse.Content.ReadAsByteArrayAsync(ct)).Should().BeEquivalentTo(MinimalPdfBytes());
    }

    [Fact]
    public async Task A_job_with_no_applications_yet_returns_an_empty_list_not_an_error()
    {
        var ct = TestContext.Current.CancellationToken;
        using var client = CreateClient();
        var recruiterToken = await IssueRecruiterWithCompanyTokenAsync(ct, "Acme Pty Ltd");
        var job = await CreateAndPublishJobAsync(client, recruiterToken, null, ct);

        using var list = BuildRequest(HttpMethod.Get, $"/api/v1/recruiter/jobs/{job.Id}/applications", recruiterToken);
        var response = await client.SendAsync(list, ct);
        var summaries = await response.Content.ReadFromJsonAsync<List<RecruiterApplicationSummaryResponse>>(ct);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        summaries.Should().BeEmpty();
    }

    // ------------------------------------------------------- cross-recruiter isolation ---

    [Fact]
    public async Task Recruiter_A_cannot_list_applications_for_recruiter_Bs_job()
    {
        var ct = TestContext.Current.CancellationToken;
        using var client = CreateClient();
        var recruiterA = await IssueRecruiterWithCompanyTokenAsync(ct, "Company A");
        var recruiterB = await IssueRecruiterWithCompanyTokenAsync(ct, "Company B");
        var jobB = await CreateAndPublishJobAsync(client, recruiterB, ValidCreateRequest("Job B"), ct);

        using var listAsA = BuildRequest(HttpMethod.Get, $"/api/v1/recruiter/jobs/{jobB.Id}/applications", recruiterA);
        (await client.SendAsync(listAsA, ct)).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Recruiter_A_cannot_view_recruiter_Bs_application_even_knowing_its_id()
    {
        var ct = TestContext.Current.CancellationToken;
        using var client = CreateClient();
        var recruiterA = await IssueRecruiterWithCompanyTokenAsync(ct, "Company A");
        var recruiterB = await IssueRecruiterWithCompanyTokenAsync(ct, "Company B");
        var jobB = await CreateAndPublishJobAsync(client, recruiterB, ValidCreateRequest("Job B"), ct);
        var applicantToken = await IssueApplicantWithProfileTokenAsync(ct);
        var applicationB = await SubmitApplicationAsync(client, applicantToken, jobB.Id, ct);

        using var detailAsA = BuildRequest(HttpMethod.Get, $"/api/v1/recruiter/applications/{applicationB.Id}", recruiterA);
        (await client.SendAsync(detailAsA, ct)).StatusCode.Should().Be(HttpStatusCode.NotFound);

        // B's application is completely unaffected by A's rejected attempt.
        using var detailAsB = BuildRequest(HttpMethod.Get, $"/api/v1/recruiter/applications/{applicationB.Id}", recruiterB);
        var detailAsBResponse = await client.SendAsync(detailAsB, ct);
        detailAsBResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        (await detailAsBResponse.Content.ReadFromJsonAsync<RecruiterApplicationResponse>(ct))!.Id.Should().Be(applicationB.Id);
    }

    [Fact]
    public async Task Recruiter_A_cannot_download_recruiter_Bs_applicant_resume_even_knowing_the_application_id()
    {
        var ct = TestContext.Current.CancellationToken;
        using var client = CreateClient();
        var recruiterA = await IssueRecruiterWithCompanyTokenAsync(ct, "Company A");
        var recruiterB = await IssueRecruiterWithCompanyTokenAsync(ct, "Company B");
        var jobB = await CreateAndPublishJobAsync(client, recruiterB, ValidCreateRequest("Job B"), ct);
        var applicantToken = await IssueApplicantWithProfileTokenAsync(ct);
        var applicationB = await SubmitApplicationAsync(client, applicantToken, jobB.Id, ct);

        using var resumeAsA = BuildRequest(HttpMethod.Get, $"/api/v1/recruiter/applications/{applicationB.Id}/resume", recruiterA);
        (await client.SendAsync(resumeAsA, ct)).StatusCode.Should().Be(HttpStatusCode.NotFound);

        // Confirms B can still download their own applicant's resume — proves
        // the rejection above was ownership, not a broken endpoint.
        using var resumeAsB = BuildRequest(HttpMethod.Get, $"/api/v1/recruiter/applications/{applicationB.Id}/resume", recruiterB);
        (await client.SendAsync(resumeAsB, ct)).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Unknown_job_id_and_unknown_application_id_both_return_404_for_a_real_recruiter()
    {
        var ct = TestContext.Current.CancellationToken;
        using var client = CreateClient();
        var recruiterToken = await IssueRecruiterWithCompanyTokenAsync(ct, "Acme Pty Ltd");

        using var list = BuildRequest(HttpMethod.Get, $"/api/v1/recruiter/jobs/{Guid.NewGuid()}/applications", recruiterToken);
        (await client.SendAsync(list, ct)).StatusCode.Should().Be(HttpStatusCode.NotFound);

        using var detail = BuildRequest(HttpMethod.Get, $"/api/v1/recruiter/applications/{Guid.NewGuid()}", recruiterToken);
        (await client.SendAsync(detail, ct)).StatusCode.Should().Be(HttpStatusCode.NotFound);

        using var resume = BuildRequest(HttpMethod.Get, $"/api/v1/recruiter/applications/{Guid.NewGuid()}/resume", recruiterToken);
        (await client.SendAsync(resume, ct)).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // ----------------------------------------------------------- response shape safety ---

    [Fact]
    public async Task Recruiter_application_responses_never_expose_internal_identifiers_or_storage_details()
    {
        var ct = TestContext.Current.CancellationToken;
        using var client = CreateClient();
        var recruiterToken = await IssueRecruiterWithCompanyTokenAsync(ct, "Acme Pty Ltd");
        var job = await CreateAndPublishJobAsync(client, recruiterToken, null, ct);
        var applicantToken = await IssueApplicantWithProfileTokenAsync(ct);
        var application = await SubmitApplicationAsync(client, applicantToken, job.Id, ct);

        using var detail = BuildRequest(HttpMethod.Get, $"/api/v1/recruiter/applications/{application.Id}", recruiterToken);
        var response = await client.SendAsync(detail, ct);
        var raw = await response.Content.ReadAsStringAsync(ct);

        raw.Should().NotContain("candidateProfileId", "the recruiter DTO has no such property");
        raw.Should().NotContain("recruiterProfileId", "the recruiter DTO has no such property");
        raw.Should().NotContain("resumeStorageKey", "only the display filename is exposed, never the opaque storage key");
        raw.Should().NotContain("/app/", "no physical filesystem path is ever returned");
    }

    // ----------------------------------------------------------------- status update ---

    [Fact]
    public async Task Status_update_without_a_token_is_unauthorized()
    {
        var ct = TestContext.Current.CancellationToken;
        using var client = CreateClient();

        using var request = BuildRequest(HttpMethod.Put, $"/api/v1/recruiter/applications/{Guid.NewGuid()}/status", null, new UpdateApplicationStatusRequest("Reviewed"));
        var response = await client.SendAsync(request, ct);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Applicant_cannot_update_an_application_status()
    {
        var ct = TestContext.Current.CancellationToken;
        using var client = CreateClient();
        var applicantToken = await IssueApplicantWithProfileTokenAsync(ct);

        using var request = BuildRequest(HttpMethod.Put, $"/api/v1/recruiter/applications/{Guid.NewGuid()}/status", applicantToken, new UpdateApplicationStatusRequest("Reviewed"));
        var response = await client.SendAsync(request, ct);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Recruiter_can_update_the_status_of_an_application_on_their_own_job_and_it_persists()
    {
        var ct = TestContext.Current.CancellationToken;
        using var client = CreateClient();
        var recruiterToken = await IssueRecruiterWithCompanyTokenAsync(ct, "Acme Pty Ltd");
        var job = await CreateAndPublishJobAsync(client, recruiterToken, null, ct);
        var applicantToken = await IssueApplicantWithProfileTokenAsync(ct);
        var application = await SubmitApplicationAsync(client, applicantToken, job.Id, ct);

        using var update = BuildRequest(
            HttpMethod.Put, $"/api/v1/recruiter/applications/{application.Id}/status", recruiterToken, new UpdateApplicationStatusRequest("Shortlisted"));
        var updateResponse = await client.SendAsync(update, ct);
        updateResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var updated = await updateResponse.Content.ReadFromJsonAsync<RecruiterApplicationResponse>(ct);
        updated!.Status.Should().Be("Shortlisted");

        // A subsequent read reflects the persisted change, not just the response of the write itself.
        using var reread = BuildRequest(HttpMethod.Get, $"/api/v1/recruiter/applications/{application.Id}", recruiterToken);
        var rereadResponse = await client.SendAsync(reread, ct);
        (await rereadResponse.Content.ReadFromJsonAsync<RecruiterApplicationResponse>(ct))!.Status.Should().Be("Shortlisted");
    }

    [Fact]
    public async Task Recruiter_A_cannot_update_the_status_of_recruiter_Bs_application()
    {
        var ct = TestContext.Current.CancellationToken;
        using var client = CreateClient();
        var recruiterA = await IssueRecruiterWithCompanyTokenAsync(ct, "Company A");
        var recruiterB = await IssueRecruiterWithCompanyTokenAsync(ct, "Company B");
        var jobB = await CreateAndPublishJobAsync(client, recruiterB, ValidCreateRequest("Job B"), ct);
        var applicantToken = await IssueApplicantWithProfileTokenAsync(ct);
        var applicationB = await SubmitApplicationAsync(client, applicantToken, jobB.Id, ct);

        using var updateAsA = BuildRequest(
            HttpMethod.Put, $"/api/v1/recruiter/applications/{applicationB.Id}/status", recruiterA, new UpdateApplicationStatusRequest("Rejected"));
        (await client.SendAsync(updateAsA, ct)).StatusCode.Should().Be(HttpStatusCode.NotFound);

        // B's application is completely unaffected by A's rejected attempt.
        using var reread = BuildRequest(HttpMethod.Get, $"/api/v1/recruiter/applications/{applicationB.Id}", recruiterB);
        var rereadResponse = await client.SendAsync(reread, ct);
        (await rereadResponse.Content.ReadFromJsonAsync<RecruiterApplicationResponse>(ct))!.Status.Should().Be("Submitted");
    }

    [Fact]
    public async Task Invalid_status_value_is_rejected_and_does_not_change_the_stored_status()
    {
        var ct = TestContext.Current.CancellationToken;
        using var client = CreateClient();
        var recruiterToken = await IssueRecruiterWithCompanyTokenAsync(ct, "Acme Pty Ltd");
        var job = await CreateAndPublishJobAsync(client, recruiterToken, null, ct);
        var applicantToken = await IssueApplicantWithProfileTokenAsync(ct);
        var application = await SubmitApplicationAsync(client, applicantToken, job.Id, ct);

        using var update = BuildRequest(
            HttpMethod.Put, $"/api/v1/recruiter/applications/{application.Id}/status", recruiterToken, new UpdateApplicationStatusRequest("Hired"));
        var response = await client.SendAsync(update, ct);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        using var reread = BuildRequest(HttpMethod.Get, $"/api/v1/recruiter/applications/{application.Id}", recruiterToken);
        var rereadResponse = await client.SendAsync(reread, ct);
        (await rereadResponse.Content.ReadFromJsonAsync<RecruiterApplicationResponse>(ct))!.Status.Should().Be("Submitted");
    }

    [Fact]
    public async Task Missing_request_body_on_status_update_is_rejected()
    {
        var ct = TestContext.Current.CancellationToken;
        using var client = CreateClient();
        var recruiterToken = await IssueRecruiterWithCompanyTokenAsync(ct, "Acme Pty Ltd");
        var job = await CreateAndPublishJobAsync(client, recruiterToken, null, ct);
        var applicantToken = await IssueApplicantWithProfileTokenAsync(ct);
        var application = await SubmitApplicationAsync(client, applicantToken, job.Id, ct);

        using var update = BuildRequest(HttpMethod.Put, $"/api/v1/recruiter/applications/{application.Id}/status", recruiterToken, body: null);
        var response = await client.SendAsync(update, ct);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Unknown_application_id_on_status_update_returns_404_for_a_real_recruiter()
    {
        var ct = TestContext.Current.CancellationToken;
        using var client = CreateClient();
        var recruiterToken = await IssueRecruiterWithCompanyTokenAsync(ct, "Acme Pty Ltd");

        using var update = BuildRequest(
            HttpMethod.Put, $"/api/v1/recruiter/applications/{Guid.NewGuid()}/status", recruiterToken, new UpdateApplicationStatusRequest("Reviewed"));
        var response = await client.SendAsync(update, ct);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
