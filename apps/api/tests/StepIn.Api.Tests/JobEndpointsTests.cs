using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using StepIn.Api.Endpoints;

namespace StepIn.Api.Tests;

/// <summary>
/// Exercises the recruiter job-management endpoints against the same real
/// Clerk-token-validation → user-sync → authorization pipeline
/// <see cref="AuthEndpointsTests"/> uses (see <see cref="AuthApiFactory"/>).
/// </summary>
public sealed class JobEndpointsTests(AuthApiFactory factory) : IClassFixture<AuthApiFactory>
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

    private async Task<string> IssueApplicantTokenAsync(CancellationToken ct)
    {
        using var client = CreateClient();
        var clerkUserId = $"user_{Guid.NewGuid():N}";
        var email = $"{Guid.NewGuid()}@example.com";

        using var setup = BuildRequest(
            HttpMethod.Post, "/api/v1/auth/account-setup", AuthApiFactory.IssueToken(clerkUserId, email), new AccountSetupRequest("Applicant"));
        (await client.SendAsync(setup, ct)).EnsureSuccessStatusCode();

        return AuthApiFactory.IssueToken(clerkUserId, email);
    }

    /// <summary>A recruiter with a complete profile and company — the only state jobs can be created against.</summary>
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

    /// <summary>A recruiter who completed account setup but never touched their profile — no company yet.</summary>
    private async Task<string> IssueRecruiterWithoutProfileTokenAsync(CancellationToken ct)
    {
        using var client = CreateClient();
        var clerkUserId = $"user_{Guid.NewGuid():N}";
        var email = $"{Guid.NewGuid()}@example.com";
        var token = AuthApiFactory.IssueToken(clerkUserId, email);

        using var setup = BuildRequest(HttpMethod.Post, "/api/v1/auth/account-setup", token, new AccountSetupRequest("Recruiter"));
        (await client.SendAsync(setup, ct)).EnsureSuccessStatusCode();

        return token;
    }

    private static CreateJobRequest ValidCreateRequest(string title = "Graduate Software Engineer") => new(
        title, "Build things that matter.", "FullTime", "Hybrid", "Sydney, NSW", "$70k-80k", ["C#", "SQL"]);

    private static UpdateJobRequest ValidUpdateRequest(string title = "Graduate Software Engineer II") => new(
        title, "Updated description.", "Contract", "Remote", "Melbourne, VIC", "$85k", ["C#"]);

    private async Task<JobResponse> CreateJobAsync(HttpClient client, string token, CreateJobRequest? request, CancellationToken ct)
    {
        using var create = BuildRequest(HttpMethod.Post, "/api/v1/recruiter/jobs", token, request ?? ValidCreateRequest());
        var response = await client.SendAsync(create, ct);
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        return (await response.Content.ReadFromJsonAsync<JobResponse>(ct))!;
    }

    // ---------------------------------------------------------------------------- auth ---

    [Fact]
    public async Task Job_endpoints_without_a_token_are_unauthorized()
    {
        var ct = TestContext.Current.CancellationToken;
        using var client = CreateClient();

        (await client.SendAsync(BuildRequest(HttpMethod.Get, "/api/v1/recruiter/jobs", null), ct)).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await client.SendAsync(BuildRequest(HttpMethod.Post, "/api/v1/recruiter/jobs", null, ValidCreateRequest()), ct)).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await client.SendAsync(BuildRequest(HttpMethod.Post, $"/api/v1/recruiter/jobs/{Guid.NewGuid()}/publish", null), ct)).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Applicant_cannot_access_job_endpoints()
    {
        var ct = TestContext.Current.CancellationToken;
        using var client = CreateClient();
        var token = await IssueApplicantTokenAsync(ct);

        using var request = BuildRequest(HttpMethod.Get, "/api/v1/recruiter/jobs", token);
        var response = await client.SendAsync(request, ct);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    // ------------------------------------------------------------------ creation rules ---

    [Fact]
    public async Task Creating_a_job_without_a_recruiter_profile_is_rejected()
    {
        var ct = TestContext.Current.CancellationToken;
        using var client = CreateClient();
        var token = await IssueRecruiterWithoutProfileTokenAsync(ct);

        using var request = BuildRequest(HttpMethod.Post, "/api/v1/recruiter/jobs", token, ValidCreateRequest());
        var response = await client.SendAsync(request, ct);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Creating_a_job_without_a_company_is_rejected()
    {
        var ct = TestContext.Current.CancellationToken;
        using var client = CreateClient();
        var clerkUserId = $"user_{Guid.NewGuid():N}";
        var email = $"{Guid.NewGuid()}@example.com";
        var token = AuthApiFactory.IssueToken(clerkUserId, email);

        using var setup = BuildRequest(HttpMethod.Post, "/api/v1/auth/account-setup", token, new AccountSetupRequest("Recruiter"));
        (await client.SendAsync(setup, ct)).EnsureSuccessStatusCode();

        // Recruiter profile exists but no company was ever set.
        using var profile = BuildRequest(HttpMethod.Put, "/api/v1/profile/recruiter", token, new UpdateRecruiterProfileRequest("Talent Partner", null, null, null));
        (await client.SendAsync(profile, ct)).EnsureSuccessStatusCode();

        using var request = BuildRequest(HttpMethod.Post, "/api/v1/recruiter/jobs", token, ValidCreateRequest());
        var response = await client.SendAsync(request, ct);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Theory]
    [InlineData("", "Description.", "FullTime", "Remote", "Sydney")] // blank title
    [InlineData("Title", "", "FullTime", "Remote", "Sydney")] // blank description
    [InlineData("Title", "Description.", "NotAType", "Remote", "Sydney")] // invalid employment type
    [InlineData("Title", "Description.", "FullTime", "NotAType", "Sydney")] // invalid workplace type
    [InlineData("Title", "Description.", "FullTime", "Remote", "")] // blank location
    public async Task Create_rejects_invalid_input(string title, string description, string employmentType, string workplaceType, string location)
    {
        var ct = TestContext.Current.CancellationToken;
        using var client = CreateClient();
        var token = await IssueRecruiterWithCompanyTokenAsync(ct, "Acme Pty Ltd");

        using var request = BuildRequest(
            HttpMethod.Post, "/api/v1/recruiter/jobs", token,
            new CreateJobRequest(title, description, employmentType, workplaceType, location, null, null));
        var response = await client.SendAsync(request, ct);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    // ----------------------------------------------------------------------- lifecycle ---

    [Fact]
    public async Task Job_is_created_as_draft_and_full_lifecycle_transitions_work()
    {
        var ct = TestContext.Current.CancellationToken;
        using var client = CreateClient();
        var token = await IssueRecruiterWithCompanyTokenAsync(ct, "Acme Pty Ltd");

        var created = await CreateJobAsync(client, token, null, ct);
        created.Status.Should().Be("Draft");
        created.CompanyName.Should().Be("Acme Pty Ltd");
        created.PublishedAt.Should().BeNull();

        using var publish1 = BuildRequest(HttpMethod.Post, $"/api/v1/recruiter/jobs/{created.Id}/publish", token);
        var publish1Response = await client.SendAsync(publish1, ct);
        publish1Response.StatusCode.Should().Be(HttpStatusCode.OK);
        var published = (await publish1Response.Content.ReadFromJsonAsync<JobResponse>(ct))!;
        published.Status.Should().Be("Published");
        published.PublishedAt.Should().NotBeNull();

        using var unpublish = BuildRequest(HttpMethod.Post, $"/api/v1/recruiter/jobs/{created.Id}/unpublish", token);
        var unpublishResponse = await client.SendAsync(unpublish, ct);
        unpublishResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        (await unpublishResponse.Content.ReadFromJsonAsync<JobResponse>(ct))!.Status.Should().Be("Unpublished");

        using var publish2 = BuildRequest(HttpMethod.Post, $"/api/v1/recruiter/jobs/{created.Id}/publish", token);
        var publish2Response = await client.SendAsync(publish2, ct);
        publish2Response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await publish2Response.Content.ReadFromJsonAsync<JobResponse>(ct))!.Status.Should().Be("Published");
    }

    [Fact]
    public async Task Publishing_an_already_published_job_is_rejected()
    {
        var ct = TestContext.Current.CancellationToken;
        using var client = CreateClient();
        var token = await IssueRecruiterWithCompanyTokenAsync(ct, "Acme Pty Ltd");
        var created = await CreateJobAsync(client, token, null, ct);

        using var first = BuildRequest(HttpMethod.Post, $"/api/v1/recruiter/jobs/{created.Id}/publish", token);
        (await client.SendAsync(first, ct)).EnsureSuccessStatusCode();

        using var second = BuildRequest(HttpMethod.Post, $"/api/v1/recruiter/jobs/{created.Id}/publish", token);
        var response = await client.SendAsync(second, ct);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Unpublishing_a_draft_job_is_rejected()
    {
        var ct = TestContext.Current.CancellationToken;
        using var client = CreateClient();
        var token = await IssueRecruiterWithCompanyTokenAsync(ct, "Acme Pty Ltd");
        var created = await CreateJobAsync(client, token, null, ct);

        using var request = BuildRequest(HttpMethod.Post, $"/api/v1/recruiter/jobs/{created.Id}/unpublish", token);
        var response = await client.SendAsync(request, ct);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Editing_a_job_preserves_its_status()
    {
        var ct = TestContext.Current.CancellationToken;
        using var client = CreateClient();
        var token = await IssueRecruiterWithCompanyTokenAsync(ct, "Acme Pty Ltd");
        var created = await CreateJobAsync(client, token, null, ct);

        using var publish = BuildRequest(HttpMethod.Post, $"/api/v1/recruiter/jobs/{created.Id}/publish", token);
        (await client.SendAsync(publish, ct)).EnsureSuccessStatusCode();

        using var edit = BuildRequest(HttpMethod.Put, $"/api/v1/recruiter/jobs/{created.Id}", token, ValidUpdateRequest());
        var editResponse = await client.SendAsync(edit, ct);
        var edited = (await editResponse.Content.ReadFromJsonAsync<JobResponse>(ct))!;

        editResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        edited.Status.Should().Be("Published");
        edited.Title.Should().Be("Graduate Software Engineer II");
        edited.Location.Should().Be("Melbourne, VIC");
    }

    [Fact]
    public async Task List_returns_only_the_recruiters_own_jobs_newest_first()
    {
        var ct = TestContext.Current.CancellationToken;
        using var client = CreateClient();
        var token = await IssueRecruiterWithCompanyTokenAsync(ct, "Acme Pty Ltd");

        await CreateJobAsync(client, token, ValidCreateRequest("First role"), ct);
        var second = await CreateJobAsync(client, token, ValidCreateRequest("Second role"), ct);

        using var list = BuildRequest(HttpMethod.Get, "/api/v1/recruiter/jobs", token);
        var response = await client.SendAsync(list, ct);
        var summaries = await response.Content.ReadFromJsonAsync<List<JobSummaryResponse>>(ct);

        summaries!.Should().HaveCount(2);
        summaries[0].Id.Should().Be(second.Id); // newest first
    }

    [Fact]
    public async Task A_recruiter_with_no_jobs_gets_an_empty_list_not_an_error()
    {
        var ct = TestContext.Current.CancellationToken;
        using var client = CreateClient();
        var token = await IssueRecruiterWithCompanyTokenAsync(ct, "Acme Pty Ltd");

        using var request = BuildRequest(HttpMethod.Get, "/api/v1/recruiter/jobs", token);
        var response = await client.SendAsync(request, ct);
        var summaries = await response.Content.ReadFromJsonAsync<List<JobSummaryResponse>>(ct);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        summaries.Should().BeEmpty();
    }

    // --------------------------------------------------------------------- ownership ---

    [Fact]
    public async Task Recruiter_B_cannot_read_edit_publish_or_unpublish_recruiter_As_job()
    {
        var ct = TestContext.Current.CancellationToken;
        using var client = CreateClient();
        var tokenA = await IssueRecruiterWithCompanyTokenAsync(ct, "Company A");
        var tokenB = await IssueRecruiterWithCompanyTokenAsync(ct, "Company B");
        var jobA = await CreateJobAsync(client, tokenA, null, ct);

        using var getAsB = BuildRequest(HttpMethod.Get, $"/api/v1/recruiter/jobs/{jobA.Id}", tokenB);
        (await client.SendAsync(getAsB, ct)).StatusCode.Should().Be(HttpStatusCode.NotFound);

        using var editAsB = BuildRequest(HttpMethod.Put, $"/api/v1/recruiter/jobs/{jobA.Id}", tokenB, ValidUpdateRequest());
        (await client.SendAsync(editAsB, ct)).StatusCode.Should().Be(HttpStatusCode.NotFound);

        using var publishAsB = BuildRequest(HttpMethod.Post, $"/api/v1/recruiter/jobs/{jobA.Id}/publish", tokenB);
        (await client.SendAsync(publishAsB, ct)).StatusCode.Should().Be(HttpStatusCode.NotFound);

        // Publish it as its real owner so the unpublish attempt below has a
        // meaningful (published) state to try to tamper with.
        using var publishAsA = BuildRequest(HttpMethod.Post, $"/api/v1/recruiter/jobs/{jobA.Id}/publish", tokenA);
        (await client.SendAsync(publishAsA, ct)).EnsureSuccessStatusCode();

        using var unpublishAsB = BuildRequest(HttpMethod.Post, $"/api/v1/recruiter/jobs/{jobA.Id}/unpublish", tokenB);
        (await client.SendAsync(unpublishAsB, ct)).StatusCode.Should().Be(HttpStatusCode.NotFound);

        // And A's job is completely unaffected by every rejected attempt above.
        using var getAsA = BuildRequest(HttpMethod.Get, $"/api/v1/recruiter/jobs/{jobA.Id}", tokenA);
        var finalResponse = await client.SendAsync(getAsA, ct);
        var final = await finalResponse.Content.ReadFromJsonAsync<JobResponse>(ct);
        final!.Status.Should().Be("Published");
        final.Title.Should().Be(jobA.Title);
    }

    [Fact]
    public async Task Recruiter_A_can_fully_manage_their_own_job()
    {
        var ct = TestContext.Current.CancellationToken;
        using var client = CreateClient();
        var token = await IssueRecruiterWithCompanyTokenAsync(ct, "Acme Pty Ltd");
        var job = await CreateJobAsync(client, token, null, ct);

        using var get = BuildRequest(HttpMethod.Get, $"/api/v1/recruiter/jobs/{job.Id}", token);
        (await client.SendAsync(get, ct)).StatusCode.Should().Be(HttpStatusCode.OK);

        using var edit = BuildRequest(HttpMethod.Put, $"/api/v1/recruiter/jobs/{job.Id}", token, ValidUpdateRequest());
        (await client.SendAsync(edit, ct)).StatusCode.Should().Be(HttpStatusCode.OK);

        using var publish = BuildRequest(HttpMethod.Post, $"/api/v1/recruiter/jobs/{job.Id}/publish", token);
        (await client.SendAsync(publish, ct)).StatusCode.Should().Be(HttpStatusCode.OK);

        using var unpublish = BuildRequest(HttpMethod.Post, $"/api/v1/recruiter/jobs/{job.Id}/unpublish", token);
        (await client.SendAsync(unpublish, ct)).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Malicious_ownership_fields_in_an_update_request_are_ignored_and_cannot_hijack_a_job()
    {
        var ct = TestContext.Current.CancellationToken;
        using var client = CreateClient();
        var tokenA = await IssueRecruiterWithCompanyTokenAsync(ct, "Company A");
        var tokenB = await IssueRecruiterWithCompanyTokenAsync(ct, "Company B");
        var jobA = await CreateJobAsync(client, tokenA, null, ct);

        // UpdateJobRequest has no recruiterProfileId/companyId property at all, so
        // this is exactly what a client attempting to smuggle ownership fields
        // into the JSON body would produce — extra, unbound properties.
        var maliciousPayload = new
        {
            title = "Hijacked",
            description = "Hijacked description.",
            employmentType = "FullTime",
            workplaceType = "Remote",
            location = "Nowhere",
            compensation = (string?)null,
            skills = Array.Empty<string>(),
            recruiterProfileId = Guid.NewGuid(),
            companyId = Guid.NewGuid(),
        };

        using var attack = BuildRequest(HttpMethod.Put, $"/api/v1/recruiter/jobs/{jobA.Id}", tokenB, maliciousPayload);
        var attackResponse = await client.SendAsync(attack, ct);
        attackResponse.StatusCode.Should().Be(HttpStatusCode.NotFound);

        using var verify = BuildRequest(HttpMethod.Get, $"/api/v1/recruiter/jobs/{jobA.Id}", tokenA);
        var verifyResponse = await client.SendAsync(verify, ct);
        var stillOwnedByA = await verifyResponse.Content.ReadFromJsonAsync<JobResponse>(ct);

        stillOwnedByA!.Title.Should().Be(jobA.Title);
        stillOwnedByA.CompanyName.Should().Be("Company A");
    }

    [Fact]
    public async Task Updating_one_recruiters_job_does_not_affect_another_recruiters_job()
    {
        var ct = TestContext.Current.CancellationToken;
        using var client = CreateClient();
        var tokenA = await IssueRecruiterWithCompanyTokenAsync(ct, "Company A");
        var tokenB = await IssueRecruiterWithCompanyTokenAsync(ct, "Company B");
        var jobA = await CreateJobAsync(client, tokenA, ValidCreateRequest("Job A"), ct);
        var jobB = await CreateJobAsync(client, tokenB, ValidCreateRequest("Job B"), ct);

        using var editA = BuildRequest(HttpMethod.Put, $"/api/v1/recruiter/jobs/{jobA.Id}", tokenA, ValidUpdateRequest("Job A - edited"));
        (await client.SendAsync(editA, ct)).EnsureSuccessStatusCode();

        using var getB = BuildRequest(HttpMethod.Get, $"/api/v1/recruiter/jobs/{jobB.Id}", tokenB);
        var bResponse = await client.SendAsync(getB, ct);
        var stillB = await bResponse.Content.ReadFromJsonAsync<JobResponse>(ct);

        stillB!.Title.Should().Be("Job B");
        stillB.CompanyName.Should().Be("Company B");
    }

    // ------------------------------------------------------- candidate/public ---

    private async Task<JobResponse> CreateAndPublishJobAsync(HttpClient client, string token, CreateJobRequest? request, CancellationToken ct)
    {
        var created = await CreateJobAsync(client, token, request, ct);
        using var publish = BuildRequest(HttpMethod.Post, $"/api/v1/recruiter/jobs/{created.Id}/publish", token);
        (await client.SendAsync(publish, ct)).EnsureSuccessStatusCode();
        return created;
    }

    [Fact]
    public async Task Public_list_and_details_require_no_authentication_at_all()
    {
        var ct = TestContext.Current.CancellationToken;
        using var client = CreateClient();
        var token = await IssueRecruiterWithCompanyTokenAsync(ct, "Acme Pty Ltd");
        var published = await CreateAndPublishJobAsync(client, token, null, ct);

        using var list = BuildRequest(HttpMethod.Get, "/api/v1/jobs", token: null);
        (await client.SendAsync(list, ct)).StatusCode.Should().Be(HttpStatusCode.OK);

        using var details = BuildRequest(HttpMethod.Get, $"/api/v1/jobs/{published.Id}", token: null);
        (await client.SendAsync(details, ct)).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Only_published_jobs_are_visible_in_the_public_list_and_details()
    {
        var ct = TestContext.Current.CancellationToken;
        using var client = CreateClient();
        var token = await IssueRecruiterWithCompanyTokenAsync(ct, "Acme Pty Ltd");

        var draft = await CreateJobAsync(client, token, ValidCreateRequest("Draft role"), ct);
        var published = await CreateAndPublishJobAsync(client, token, ValidCreateRequest("Published role"), ct);
        var unpublished = await CreateAndPublishJobAsync(client, token, ValidCreateRequest("Unpublished role"), ct);
        using var unpublish = BuildRequest(HttpMethod.Post, $"/api/v1/recruiter/jobs/{unpublished.Id}/unpublish", token);
        (await client.SendAsync(unpublish, ct)).EnsureSuccessStatusCode();

        using var listRequest = BuildRequest(HttpMethod.Get, "/api/v1/jobs", token: null);
        var listResponse = await client.SendAsync(listRequest, ct);
        var summaries = await listResponse.Content.ReadFromJsonAsync<List<PublicJobSummaryResponse>>(ct);

        summaries!.Select(s => s.Id).Should().Contain(published.Id);
        summaries!.Select(s => s.Id).Should().NotContain(draft.Id);
        summaries!.Select(s => s.Id).Should().NotContain(unpublished.Id);

        using var publishedDetails = BuildRequest(HttpMethod.Get, $"/api/v1/jobs/{published.Id}", token: null);
        (await client.SendAsync(publishedDetails, ct)).StatusCode.Should().Be(HttpStatusCode.OK);

        using var draftDetails = BuildRequest(HttpMethod.Get, $"/api/v1/jobs/{draft.Id}", token: null);
        (await client.SendAsync(draftDetails, ct)).StatusCode.Should().Be(HttpStatusCode.NotFound);

        using var unpublishedDetails = BuildRequest(HttpMethod.Get, $"/api/v1/jobs/{unpublished.Id}", token: null);
        (await client.SendAsync(unpublishedDetails, ct)).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Unknown_job_id_returns_404_on_the_public_details_endpoint()
    {
        var ct = TestContext.Current.CancellationToken;
        using var client = CreateClient();

        using var request = BuildRequest(HttpMethod.Get, $"/api/v1/jobs/{Guid.NewGuid()}", token: null);
        var response = await client.SendAsync(request, ct);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Public_job_response_never_includes_ownership_fields()
    {
        var ct = TestContext.Current.CancellationToken;
        using var client = CreateClient();
        var token = await IssueRecruiterWithCompanyTokenAsync(ct, "Acme Pty Ltd");
        var published = await CreateAndPublishJobAsync(client, token, null, ct);

        using var request = BuildRequest(HttpMethod.Get, $"/api/v1/jobs/{published.Id}", token: null);
        var response = await client.SendAsync(request, ct);
        var raw = await response.Content.ReadAsStringAsync(ct);

        raw.Should().NotContain("recruiterProfileId", "the public DTO has no such property");
        raw.Should().NotContain("companyId", "the public DTO exposes companyName, never the raw company id");
    }

    [Fact]
    public async Task No_mutation_route_exists_under_the_public_jobs_prefix()
    {
        var ct = TestContext.Current.CancellationToken;
        using var client = CreateClient();
        var token = await IssueRecruiterWithCompanyTokenAsync(ct, "Acme Pty Ltd");

        // Confirms at runtime what the source already shows: no MapPost exists
        // for "/api/v1/jobs" — ASP.NET Core's routing recognizes the path (GET
        // is mapped there) but refuses the method, so this is 405 rather than
        // 404 — a stronger signal that no handler for this verb exists at all,
        // regardless of whether a token is supplied.
        using var request = BuildRequest(HttpMethod.Post, "/api/v1/jobs", token, ValidCreateRequest());
        var response = await client.SendAsync(request, ct);

        response.StatusCode.Should().Be(HttpStatusCode.MethodNotAllowed);
    }

    [Fact]
    public async Task Search_filter_matches_title_or_description()
    {
        var ct = TestContext.Current.CancellationToken;
        using var client = CreateClient();
        var token = await IssueRecruiterWithCompanyTokenAsync(ct, "Acme Pty Ltd");
        var matching = await CreateAndPublishJobAsync(client, token, ValidCreateRequest("Graduate Data Analyst"), ct);
        var other = await CreateAndPublishJobAsync(client, token, ValidCreateRequest("Warehouse Operator"), ct);

        using var request = BuildRequest(HttpMethod.Get, "/api/v1/jobs?search=Data+Analyst", token: null);
        var response = await client.SendAsync(request, ct);
        var summaries = await response.Content.ReadFromJsonAsync<List<PublicJobSummaryResponse>>(ct);

        summaries!.Select(s => s.Id).Should().Contain(matching.Id);
        summaries!.Select(s => s.Id).Should().NotContain(other.Id);
    }

    [Fact]
    public async Task Employment_and_workplace_type_filters_narrow_results()
    {
        var ct = TestContext.Current.CancellationToken;
        using var client = CreateClient();
        var token = await IssueRecruiterWithCompanyTokenAsync(ct, "Acme Pty Ltd");
        var fullTimeRemote = await CreateAndPublishJobAsync(
            client, token, new CreateJobRequest("Role A", "Description.", "FullTime", "Remote", "Sydney", null, null), ct);
        var contractOnSite = await CreateAndPublishJobAsync(
            client, token, new CreateJobRequest("Role B", "Description.", "Contract", "OnSite", "Sydney", null, null), ct);

        using var employmentRequest = BuildRequest(HttpMethod.Get, "/api/v1/jobs?employmentType=FullTime", token: null);
        var employmentSummaries = await (await client.SendAsync(employmentRequest, ct)).Content.ReadFromJsonAsync<List<PublicJobSummaryResponse>>(ct);
        employmentSummaries!.Select(s => s.Id).Should().Contain(fullTimeRemote.Id);
        employmentSummaries!.Select(s => s.Id).Should().NotContain(contractOnSite.Id);

        using var workplaceRequest = BuildRequest(HttpMethod.Get, "/api/v1/jobs?workplaceType=OnSite", token: null);
        var workplaceSummaries = await (await client.SendAsync(workplaceRequest, ct)).Content.ReadFromJsonAsync<List<PublicJobSummaryResponse>>(ct);
        workplaceSummaries!.Select(s => s.Id).Should().Contain(contractOnSite.Id);
        workplaceSummaries!.Select(s => s.Id).Should().NotContain(fullTimeRemote.Id);
    }

    [Fact]
    public async Task Location_filter_partially_matches()
    {
        var ct = TestContext.Current.CancellationToken;
        using var client = CreateClient();
        var token = await IssueRecruiterWithCompanyTokenAsync(ct, "Acme Pty Ltd");
        var sydney = await CreateAndPublishJobAsync(
            client, token, new CreateJobRequest("Role A", "Description.", "FullTime", "Remote", "Sydney, NSW", null, null), ct);
        var melbourne = await CreateAndPublishJobAsync(
            client, token, new CreateJobRequest("Role B", "Description.", "FullTime", "Remote", "Melbourne, VIC", null, null), ct);

        using var request = BuildRequest(HttpMethod.Get, "/api/v1/jobs?location=Sydney", token: null);
        var response = await client.SendAsync(request, ct);
        var summaries = await response.Content.ReadFromJsonAsync<List<PublicJobSummaryResponse>>(ct);

        summaries!.Select(s => s.Id).Should().Contain(sydney.Id);
        summaries!.Select(s => s.Id).Should().NotContain(melbourne.Id);
    }

    [Theory]
    [InlineData("employmentType", "NotAType")]
    [InlineData("workplaceType", "NotAType")]
    public async Task Invalid_filter_enum_value_returns_400(string paramName, string value)
    {
        var ct = TestContext.Current.CancellationToken;
        using var client = CreateClient();

        using var request = BuildRequest(HttpMethod.Get, $"/api/v1/jobs?{paramName}={value}", token: null);
        var response = await client.SendAsync(request, ct);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
}
