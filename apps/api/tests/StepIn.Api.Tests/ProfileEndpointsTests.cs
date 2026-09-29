using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using StepIn.Api.Endpoints;

namespace StepIn.Api.Tests;

/// <summary>
/// Exercises the candidate/recruiter/company profile endpoints against the same
/// real Clerk-token-validation → user-sync → authorization pipeline
/// <see cref="AuthEndpointsTests"/> uses (see <see cref="AuthApiFactory"/>).
/// </summary>
public sealed class ProfileEndpointsTests(AuthApiFactory factory) : IClassFixture<AuthApiFactory>
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

    private async Task<string> IssueRoleTokenAsync(string role, CancellationToken ct)
    {
        using var client = CreateClient();
        var clerkUserId = $"user_{Guid.NewGuid():N}";
        var email = $"{Guid.NewGuid()}@example.com";

        using var setup = BuildRequest(
            HttpMethod.Post,
            "/api/v1/auth/account-setup",
            AuthApiFactory.IssueToken(clerkUserId, email),
            new AccountSetupRequest(role));
        (await client.SendAsync(setup, ct)).EnsureSuccessStatusCode();

        return AuthApiFactory.IssueToken(clerkUserId, email);
    }

    private Task<string> IssueApplicantTokenAsync(CancellationToken ct) => IssueRoleTokenAsync("Applicant", ct);

    private Task<string> IssueRecruiterTokenAsync(CancellationToken ct) => IssueRoleTokenAsync("Recruiter", ct);

    private static readonly UpdateCandidateProfileRequest EmptyCandidateUpdate = new(
        null, null, null, null, null, null, null, null, null, null, null, null);

    // ---------------------------------------------------------------------- candidate: auth ---

    [Fact]
    public async Task Candidate_get_without_a_token_is_unauthorized()
    {
        var ct = TestContext.Current.CancellationToken;
        using var client = CreateClient();
        using var request = BuildRequest(HttpMethod.Get, "/api/v1/profile/candidate", token: null);

        var response = await client.SendAsync(request, ct);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Candidate_put_without_a_token_is_unauthorized()
    {
        var ct = TestContext.Current.CancellationToken;
        using var client = CreateClient();
        using var request = BuildRequest(HttpMethod.Put, "/api/v1/profile/candidate", token: null, EmptyCandidateUpdate);

        var response = await client.SendAsync(request, ct);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Recruiter_cannot_access_candidate_profile_endpoints()
    {
        var ct = TestContext.Current.CancellationToken;
        using var client = CreateClient();
        var token = await IssueRecruiterTokenAsync(ct);
        using var request = BuildRequest(HttpMethod.Get, "/api/v1/profile/candidate", token);

        var response = await client.SendAsync(request, ct);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Applicant_cannot_access_recruiter_profile_endpoints()
    {
        var ct = TestContext.Current.CancellationToken;
        using var client = CreateClient();
        var token = await IssueApplicantTokenAsync(ct);
        using var request = BuildRequest(HttpMethod.Get, "/api/v1/profile/recruiter", token);

        var response = await client.SendAsync(request, ct);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    // ----------------------------------------------------------------- candidate: CRUD ---

    [Fact]
    public async Task A_new_applicant_has_an_empty_zero_percent_candidate_profile()
    {
        var ct = TestContext.Current.CancellationToken;
        using var client = CreateClient();
        var token = await IssueApplicantTokenAsync(ct);
        using var request = BuildRequest(HttpMethod.Get, "/api/v1/profile/candidate", token);

        var response = await client.SendAsync(request, ct);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<CandidateProfileResponse>(ct);
        body!.Headline.Should().BeNull();
        body.Education.Should().BeEmpty();
        body.ProfileCompletionPercent.Should().Be(0);
    }

    [Fact]
    public async Task Applicant_can_update_their_candidate_profile_and_it_persists()
    {
        var ct = TestContext.Current.CancellationToken;
        using var client = CreateClient();
        var token = await IssueApplicantTokenAsync(ct);

        var update = new UpdateCandidateProfileRequest(
            "+61 400 000 000",
            "Sydney, NSW",
            "Graduate Software Engineer",
            "Building things.",
            null,
            "https://linkedin.com/in/example",
            null,
            "https://github.com/example",
            ["C#", "TypeScript", "c#"],
            [new CandidateEducationDto(null, "University of Sydney", "BSc", "Computer Science", new DateOnly(2020, 2, 1), new DateOnly(2023, 12, 1), null)],
            [new CandidateExperienceDto(null, "Acme", "Intern", new DateOnly(2023, 1, 1), null, "Did things.")],
            null);

        using var putRequest = BuildRequest(HttpMethod.Put, "/api/v1/profile/candidate", token, update);
        var putResponse = await client.SendAsync(putRequest, ct);

        putResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var putBody = await putResponse.Content.ReadFromJsonAsync<CandidateProfileResponse>(ct);
        putBody!.Headline.Should().Be("Graduate Software Engineer");
        putBody.Skills.Should().BeEquivalentTo(["C#", "TypeScript"]); // de-duplicated case-insensitively
        putBody.Education.Should().ContainSingle(e => e.Institution == "University of Sydney");
        putBody.Experience.Should().ContainSingle(e => e.CompanyName == "Acme");
        putBody.ProfileCompletionPercent.Should().BeGreaterThan(0);

        using var getRequest = BuildRequest(HttpMethod.Get, "/api/v1/profile/candidate", token);
        var getBody = await (await client.SendAsync(getRequest, ct)).Content.ReadFromJsonAsync<CandidateProfileResponse>(ct);
        getBody!.Headline.Should().Be("Graduate Software Engineer");
        getBody.Education.Should().ContainSingle(e => e.Institution == "University of Sydney");
    }

    [Fact]
    public async Task Updating_education_reconciles_add_update_and_remove_in_one_save()
    {
        var ct = TestContext.Current.CancellationToken;
        using var client = CreateClient();
        var token = await IssueApplicantTokenAsync(ct);

        var firstSave = EmptyCandidateUpdate with
        {
            Education =
            [
                new CandidateEducationDto(null, "Keep & Rename U", null, null, null, null, null),
                new CandidateEducationDto(null, "Drop U", null, null, null, null, null),
            ],
        };
        using var putFirst = BuildRequest(HttpMethod.Put, "/api/v1/profile/candidate", token, firstSave);
        var firstBody = await (await client.SendAsync(putFirst, ct)).Content.ReadFromJsonAsync<CandidateProfileResponse>(ct);
        var keepId = firstBody!.Education.Single(e => e.Institution == "Keep & Rename U").Id;

        var secondSave = EmptyCandidateUpdate with
        {
            Education =
            [
                new CandidateEducationDto(keepId, "Renamed U", null, null, null, null, null),
                new CandidateEducationDto(null, "New U", null, null, null, null, null),
            ],
        };
        using var putSecond = BuildRequest(HttpMethod.Put, "/api/v1/profile/candidate", token, secondSave);
        var secondResponse = await client.SendAsync(putSecond, ct);
        var secondBody = await secondResponse.Content.ReadFromJsonAsync<CandidateProfileResponse>(ct);

        secondResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        secondBody!.Education.Should().HaveCount(2);
        secondBody.Education.Should().ContainSingle(e => e.Id == keepId && e.Institution == "Renamed U");
        secondBody.Education.Should().ContainSingle(e => e.Institution == "New U");
        secondBody.Education.Should().NotContain(e => e.Institution == "Drop U");
    }

    [Fact]
    public async Task Candidate_update_rejects_an_invalid_url()
    {
        var ct = TestContext.Current.CancellationToken;
        using var client = CreateClient();
        var token = await IssueApplicantTokenAsync(ct);
        var update = EmptyCandidateUpdate with { LinkedInUrl = "not-a-url" };

        using var request = BuildRequest(HttpMethod.Put, "/api/v1/profile/candidate", token, update);
        var response = await client.SendAsync(request, ct);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    // ------------------------------------------------------------- candidate: isolation ---

    [Fact]
    public async Task A_user_cannot_read_or_modify_another_users_candidate_profile()
    {
        var ct = TestContext.Current.CancellationToken;
        using var client = CreateClient();
        var tokenA = await IssueApplicantTokenAsync(ct);
        var tokenB = await IssueApplicantTokenAsync(ct);

        var updateA = EmptyCandidateUpdate with { Headline = "Applicant A headline" };
        using var putA = BuildRequest(HttpMethod.Put, "/api/v1/profile/candidate", tokenA, updateA);
        (await client.SendAsync(putA, ct)).EnsureSuccessStatusCode();

        using var getB = BuildRequest(HttpMethod.Get, "/api/v1/profile/candidate", tokenB);
        var bodyB = await (await client.SendAsync(getB, ct)).Content.ReadFromJsonAsync<CandidateProfileResponse>(ct);

        bodyB!.Headline.Should().BeNull();

        using var getA = BuildRequest(HttpMethod.Get, "/api/v1/profile/candidate", tokenA);
        var bodyA = await (await client.SendAsync(getA, ct)).Content.ReadFromJsonAsync<CandidateProfileResponse>(ct);

        bodyA!.UserId.Should().NotBe(bodyB.UserId);
        bodyA.Headline.Should().Be("Applicant A headline");
    }

    // ------------------------------------------------------------- recruiter + company ---

    [Fact]
    public async Task Recruiter_can_get_and_update_their_own_recruiter_and_company_profile()
    {
        var ct = TestContext.Current.CancellationToken;
        using var client = CreateClient();
        var token = await IssueRecruiterTokenAsync(ct);

        var update = new UpdateRecruiterProfileRequest(
            "Talent Partner",
            "+61 400 111 222",
            null,
            new CompanyDto(null, "Acme Pty Ltd", "We build things.", "https://acme.example", null, "Software", "Sydney"));

        using var putRequest = BuildRequest(HttpMethod.Put, "/api/v1/profile/recruiter", token, update);
        var putResponse = await client.SendAsync(putRequest, ct);

        putResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await putResponse.Content.ReadFromJsonAsync<RecruiterProfileResponse>(ct);
        body!.JobTitle.Should().Be("Talent Partner");
        body.Company.Should().NotBeNull();
        body.Company!.Name.Should().Be("Acme Pty Ltd");

        using var getRequest = BuildRequest(HttpMethod.Get, "/api/v1/profile/recruiter", token);
        var getBody = await (await client.SendAsync(getRequest, ct)).Content.ReadFromJsonAsync<RecruiterProfileResponse>(ct);
        getBody!.Company!.Id!.Value.Should().Be(body.Company.Id!.Value);
        getBody.Company.Name.Should().Be("Acme Pty Ltd");
    }

    [Fact]
    public async Task Recruiter_update_requires_a_company_name_when_company_is_submitted()
    {
        var ct = TestContext.Current.CancellationToken;
        using var client = CreateClient();
        var token = await IssueRecruiterTokenAsync(ct);
        var update = new UpdateRecruiterProfileRequest(null, null, null, new CompanyDto(null, "  ", null, null, null, null, null));

        using var request = BuildRequest(HttpMethod.Put, "/api/v1/profile/recruiter", token, update);
        var response = await client.SendAsync(request, ct);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Two_recruiters_company_edits_never_touch_each_others_company()
    {
        var ct = TestContext.Current.CancellationToken;
        using var client = CreateClient();
        var tokenA = await IssueRecruiterTokenAsync(ct);
        var tokenB = await IssueRecruiterTokenAsync(ct);

        using var putA = BuildRequest(HttpMethod.Put, "/api/v1/profile/recruiter", tokenA,
            new UpdateRecruiterProfileRequest(null, null, null, new CompanyDto(null, "Company A", null, null, null, null, null)));
        var bodyA = await (await client.SendAsync(putA, ct)).Content.ReadFromJsonAsync<RecruiterProfileResponse>(ct);

        using var putB = BuildRequest(HttpMethod.Put, "/api/v1/profile/recruiter", tokenB,
            new UpdateRecruiterProfileRequest(null, null, null, new CompanyDto(null, "Company B", null, null, null, null, null)));
        var bodyB = await (await client.SendAsync(putB, ct)).Content.ReadFromJsonAsync<RecruiterProfileResponse>(ct);

        bodyA!.Company!.Id!.Value.Should().NotBe(bodyB!.Company!.Id!.Value);

        using var getA = BuildRequest(HttpMethod.Get, "/api/v1/profile/recruiter", tokenA);
        var getBodyA = await (await client.SendAsync(getA, ct)).Content.ReadFromJsonAsync<RecruiterProfileResponse>(ct);
        getBodyA!.Company!.Name.Should().Be("Company A");
    }
}
