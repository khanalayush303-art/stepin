using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using StepIn.Api.Endpoints;
using StepIn.Domain.Aidx;
using StepIn.Infrastructure.Persistence;

namespace StepIn.Api.Tests;

/// <summary>
/// Proof-of-Skills Portfolio: candidate CRUD, ownership isolation, Private-by-default visibility,
/// link/date validation, research-project eligibility, and — the central privacy guarantee — that
/// a recruiter only ever sees a candidate's Public projects, and only through the existing
/// RecruiterApplicationEndpoints ownership chain, never a standalone browsing endpoint.
/// </summary>
public sealed class PortfolioEndpointsTests(AuthApiFactory factory) : IClassFixture<AuthApiFactory>
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

    private async Task<string> IssueApplicantAsync(CancellationToken ct)
    {
        var token = AuthApiFactory.IssueToken($"user_{Guid.NewGuid():N}", $"{Guid.NewGuid()}@example.com");
        using var client = CreateClient();
        using var setup = BuildRequest(HttpMethod.Post, "/api/v1/auth/account-setup", token, new AccountSetupRequest("Applicant"));
        (await client.SendAsync(setup, ct)).EnsureSuccessStatusCode();
        return token;
    }

    /// <summary>Issues an applicant token without ever creating a candidate profile.</summary>
    private async Task<string> IssueApplicantWithoutProfileAsync(CancellationToken ct) => await IssueApplicantAsync(ct);

    private async Task<string> IssueApplicantWithProfileAsync(CancellationToken ct, string firstName = "Test", string lastName = "User")
    {
        var token = AuthApiFactory.IssueToken($"user_{Guid.NewGuid():N}", $"{Guid.NewGuid()}@example.com", firstName, lastName);
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
            new UpdateRecruiterProfileRequest(null, null, null, new CompanyDto(null, Unique("Portfolio Co"), null, null, null, null, null)));
        (await client.SendAsync(profile, ct)).EnsureSuccessStatusCode();

        return token;
    }

    private async Task<JobResponse> CreateAndPublishJobAsync(string recruiterToken, CancellationToken ct)
    {
        using var client = CreateClient();
        using var create = BuildRequest(HttpMethod.Post, "/api/v1/recruiter/jobs", recruiterToken,
            new CreateJobRequest(Unique("Graduate Software Engineer"), "Build things that matter.", "FullTime", "Hybrid", "Sydney, NSW", "$70k-80k", ["C#"]));
        var createResponse = await client.SendAsync(create, ct);
        createResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var created = (await createResponse.Content.ReadFromJsonAsync<JobResponse>(ct))!;

        using var publish = BuildRequest(HttpMethod.Post, $"/api/v1/recruiter/jobs/{created.Id}/publish", recruiterToken);
        (await client.SendAsync(publish, ct)).EnsureSuccessStatusCode();
        return created;
    }

    private async Task<Guid> SubmitApplicationAsync(string applicantToken, Guid jobId, CancellationToken ct)
    {
        using var client = CreateClient();
        using var submit = BuildMultipartRequest($"/api/v1/jobs/{jobId}/applications", applicantToken, MinimalPdfBytes());
        var response = await client.SendAsync(submit, ct);
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        return (await response.Content.ReadFromJsonAsync<ApplicationResponse>(ct))!.Id;
    }

    /// <summary>Creates a published AIDX project directly in the database, mirroring ResearchMatchEndpointsTests.</summary>
    private async Task<Guid> InsertPublishedResearchProjectAsync(CancellationToken ct)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var title = Unique("Portfolio research project");
        var project = new AidxProject
        {
            Title = title,
            Slug = title.Replace(' ', '-').ToLowerInvariant(),
            ShortDescription = "Short.",
            Description = "Long.",
            Status = AidxContentStatus.Published,
            PublishedAt = DateTimeOffset.UtcNow,
        };
        db.Add(project);
        await db.SaveChangesAsync(ct);
        return project.Id;
    }

    private async Task<Guid> InsertDraftResearchProjectAsync(CancellationToken ct)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var title = Unique("Draft research project");
        var project = new AidxProject
        {
            Title = title,
            Slug = title.Replace(' ', '-').ToLowerInvariant(),
            ShortDescription = "Short.",
            Description = "Long.",
            Status = AidxContentStatus.Draft,
        };
        db.Add(project);
        await db.SaveChangesAsync(ct);
        return project.Id;
    }

    private static PortfolioProjectInput ValidInput(string title = "Personal portfolio site", string? visibility = null, Guid? researchProjectId = null) =>
        new(title, "Built a thing using real tools.", ["C#", "React"], null, null, "https://github.com/example/repo", "https://example.com/demo", visibility, researchProjectId);

    private async Task<PortfolioProjectResponse> CreateProjectAsync(string token, PortfolioProjectInput? request, CancellationToken ct)
    {
        using var client = CreateClient();
        using var create = BuildRequest(HttpMethod.Post, "/api/v1/portfolio", token, request ?? ValidInput());
        var response = await client.SendAsync(create, ct);
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        return (await response.Content.ReadFromJsonAsync<PortfolioProjectResponse>(ct))!;
    }

    // --------------------------------------------------------------- auth boundary ---

    [Fact]
    public async Task Anonymous_cannot_list_or_create_projects()
    {
        var ct = TestContext.Current.CancellationToken;
        using var client = CreateClient();

        (await client.GetAsync("/api/v1/portfolio", ct)).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await client.PostAsJsonAsync("/api/v1/portfolio", ValidInput(), ct)).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Recruiter_cannot_use_candidate_portfolio_endpoints()
    {
        var ct = TestContext.Current.CancellationToken;
        var recruiter = await IssueRecruiterWithCompanyAsync(ct);
        using var client = CreateClient();
        using var request = BuildRequest(HttpMethod.Get, "/api/v1/portfolio", recruiter);

        (await client.SendAsync(request, ct)).StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Creating_a_project_without_a_candidate_profile_is_a_validation_problem_not_a_crash()
    {
        var ct = TestContext.Current.CancellationToken;
        var applicant = await IssueApplicantWithoutProfileAsync(ct);
        using var client = CreateClient();
        using var request = BuildRequest(HttpMethod.Post, "/api/v1/portfolio", applicant, ValidInput());

        (await client.SendAsync(request, ct)).StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Listing_projects_without_a_profile_returns_an_empty_list_not_an_error()
    {
        var ct = TestContext.Current.CancellationToken;
        var applicant = await IssueApplicantWithoutProfileAsync(ct);
        using var client = CreateClient();
        using var request = BuildRequest(HttpMethod.Get, "/api/v1/portfolio", applicant);

        var response = await client.SendAsync(request, ct);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await response.Content.ReadFromJsonAsync<List<PortfolioProjectResponse>>(ct))!.Should().BeEmpty();
    }

    // --------------------------------------------------------------------- CRUD ---

    [Fact]
    public async Task A_project_defaults_to_Private_visibility()
    {
        var ct = TestContext.Current.CancellationToken;
        var applicant = await IssueApplicantWithProfileAsync(ct);

        var project = await CreateProjectAsync(applicant, ValidInput(visibility: null), ct);

        project.Visibility.Should().Be("Private");
    }

    [Fact]
    public async Task A_project_can_be_created_and_then_made_Public_via_update()
    {
        var ct = TestContext.Current.CancellationToken;
        var applicant = await IssueApplicantWithProfileAsync(ct);
        var project = await CreateProjectAsync(applicant, ValidInput(), ct);

        using var client = CreateClient();
        using var update = BuildRequest(HttpMethod.Put, $"/api/v1/portfolio/{project.Id}", applicant,
            ValidInput(project.Title, visibility: "Public"));
        var response = await client.SendAsync(update, ct);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var updated = (await response.Content.ReadFromJsonAsync<PortfolioProjectResponse>(ct))!;
        updated.Visibility.Should().Be("Public");
    }

    [Fact]
    public async Task Skills_are_trimmed_and_deduplicated_case_insensitively()
    {
        var ct = TestContext.Current.CancellationToken;
        var applicant = await IssueApplicantWithProfileAsync(ct);

        var project = await CreateProjectAsync(applicant,
            ValidInput() with { Skills = [" C#", "c#", "React ", "React"] }, ct);

        project.Skills.Should().BeEquivalentTo([" C#".Trim(), "React"], options => options.WithoutStrictOrdering());
        project.Skills.Should().HaveCount(2);
    }

    [Fact]
    public async Task A_candidate_cannot_read_update_or_delete_another_candidates_project()
    {
        var ct = TestContext.Current.CancellationToken;
        var owner = await IssueApplicantWithProfileAsync(ct);
        var project = await CreateProjectAsync(owner, ValidInput("Owner's project"), ct);

        var intruder = await IssueApplicantWithProfileAsync(ct);
        using var client = CreateClient();

        using var get = BuildRequest(HttpMethod.Get, $"/api/v1/portfolio/{project.Id}", intruder);
        (await client.SendAsync(get, ct)).StatusCode.Should().Be(HttpStatusCode.NotFound);

        using var update = BuildRequest(HttpMethod.Put, $"/api/v1/portfolio/{project.Id}", intruder, ValidInput("Hijacked"));
        (await client.SendAsync(update, ct)).StatusCode.Should().Be(HttpStatusCode.NotFound);

        using var delete = BuildRequest(HttpMethod.Delete, $"/api/v1/portfolio/{project.Id}", intruder);
        (await client.SendAsync(delete, ct)).StatusCode.Should().Be(HttpStatusCode.NotFound);

        // Prove nothing actually changed.
        using var verifyGet = BuildRequest(HttpMethod.Get, $"/api/v1/portfolio/{project.Id}", owner);
        var verified = (await (await client.SendAsync(verifyGet, ct)).Content.ReadFromJsonAsync<PortfolioProjectResponse>(ct))!;
        verified.Title.Should().Be("Owner's project");
    }

    [Fact]
    public async Task Deleting_a_project_removes_it()
    {
        var ct = TestContext.Current.CancellationToken;
        var applicant = await IssueApplicantWithProfileAsync(ct);
        var project = await CreateProjectAsync(applicant, ValidInput(), ct);

        using var client = CreateClient();
        using var delete = BuildRequest(HttpMethod.Delete, $"/api/v1/portfolio/{project.Id}", applicant);
        (await client.SendAsync(delete, ct)).StatusCode.Should().Be(HttpStatusCode.NoContent);

        using var get = BuildRequest(HttpMethod.Get, $"/api/v1/portfolio/{project.Id}", applicant);
        (await client.SendAsync(get, ct)).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // ------------------------------------------------------------------ validation ---

    [Theory]
    [InlineData("ftp://example.com/file")]
    [InlineData("javascript:alert(1)")]
    [InlineData("not-a-url")]
    public async Task Non_http_or_malformed_urls_are_rejected(string badUrl)
    {
        var ct = TestContext.Current.CancellationToken;
        var applicant = await IssueApplicantWithProfileAsync(ct);
        using var client = CreateClient();

        using var request = BuildRequest(HttpMethod.Post, "/api/v1/portfolio", applicant, ValidInput() with { RepositoryUrl = badUrl });
        (await client.SendAsync(request, ct)).StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task An_end_date_before_the_start_date_is_rejected()
    {
        var ct = TestContext.Current.CancellationToken;
        var applicant = await IssueApplicantWithProfileAsync(ct);
        using var client = CreateClient();

        var request = ValidInput() with { StartDate = new DateOnly(2024, 6, 1), EndDate = new DateOnly(2024, 1, 1) };
        using var httpRequest = BuildRequest(HttpMethod.Post, "/api/v1/portfolio", applicant, request);
        (await client.SendAsync(httpRequest, ct)).StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Blank_title_or_description_is_rejected()
    {
        var ct = TestContext.Current.CancellationToken;
        var applicant = await IssueApplicantWithProfileAsync(ct);
        using var client = CreateClient();

        using var blankTitle = BuildRequest(HttpMethod.Post, "/api/v1/portfolio", applicant, ValidInput() with { Title = "   " });
        (await client.SendAsync(blankTitle, ct)).StatusCode.Should().Be(HttpStatusCode.BadRequest);

        using var blankDescription = BuildRequest(HttpMethod.Post, "/api/v1/portfolio", applicant, ValidInput() with { Description = "" });
        (await client.SendAsync(blankDescription, ct)).StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task An_unknown_visibility_value_is_rejected()
    {
        var ct = TestContext.Current.CancellationToken;
        var applicant = await IssueApplicantWithProfileAsync(ct);
        using var client = CreateClient();
        using var request = BuildRequest(HttpMethod.Post, "/api/v1/portfolio", applicant, ValidInput() with { Visibility = "Everyone" });

        (await client.SendAsync(request, ct)).StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    // ----------------------------------------------------------- research project link ---

    [Fact]
    public async Task A_published_research_project_can_be_linked()
    {
        var ct = TestContext.Current.CancellationToken;
        var applicant = await IssueApplicantWithProfileAsync(ct);
        var researchProjectId = await InsertPublishedResearchProjectAsync(ct);

        var project = await CreateProjectAsync(applicant, ValidInput(researchProjectId: researchProjectId), ct);

        project.ResearchProjectId.Should().Be(researchProjectId);
        project.ResearchProjectTitle.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task A_draft_research_project_cannot_be_linked()
    {
        var ct = TestContext.Current.CancellationToken;
        var applicant = await IssueApplicantWithProfileAsync(ct);
        var draftProjectId = await InsertDraftResearchProjectAsync(ct);

        using var client = CreateClient();
        using var request = BuildRequest(HttpMethod.Post, "/api/v1/portfolio", applicant, ValidInput(researchProjectId: draftProjectId));
        (await client.SendAsync(request, ct)).StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task An_unknown_research_project_id_is_rejected()
    {
        var ct = TestContext.Current.CancellationToken;
        var applicant = await IssueApplicantWithProfileAsync(ct);
        using var client = CreateClient();
        using var request = BuildRequest(HttpMethod.Post, "/api/v1/portfolio", applicant, ValidInput(researchProjectId: Guid.NewGuid()));

        (await client.SendAsync(request, ct)).StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    // ------------------------------------------------------- recruiter visibility ---

    [Fact]
    public async Task A_recruiter_sees_only_the_applicants_Public_projects_never_Private_ones()
    {
        var ct = TestContext.Current.CancellationToken;
        var applicant = await IssueApplicantWithProfileAsync(ct);

        var publicProject = await CreateProjectAsync(applicant, ValidInput("Public showcase", visibility: "Public"), ct);
        var privateProject = await CreateProjectAsync(applicant, ValidInput("Private draft", visibility: "Private"), ct);

        var recruiter = await IssueRecruiterWithCompanyAsync(ct);
        var job = await CreateAndPublishJobAsync(recruiter, ct);
        var applicationId = await SubmitApplicationAsync(applicant, job.Id, ct);

        using var client = CreateClient();
        using var request = BuildRequest(HttpMethod.Get, $"/api/v1/recruiter/applications/{applicationId}", recruiter);
        var response = await client.SendAsync(request, ct);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var detail = (await response.Content.ReadFromJsonAsync<RecruiterApplicationResponse>(ct))!;

        detail.PortfolioProjects.Should().ContainSingle(p => p.Id == publicProject.Id && p.Title == "Public showcase");
        detail.PortfolioProjects.Should().NotContain(p => p.Id == privateProject.Id);
    }

    [Fact]
    public async Task A_recruiter_who_does_not_own_the_job_cannot_see_the_applicants_portfolio_at_all()
    {
        var ct = TestContext.Current.CancellationToken;
        var applicant = await IssueApplicantWithProfileAsync(ct);
        await CreateProjectAsync(applicant, ValidInput("Public showcase", visibility: "Public"), ct);

        var owningRecruiter = await IssueRecruiterWithCompanyAsync(ct);
        var job = await CreateAndPublishJobAsync(owningRecruiter, ct);
        var applicationId = await SubmitApplicationAsync(applicant, job.Id, ct);

        var otherRecruiter = await IssueRecruiterWithCompanyAsync(ct);
        using var client = CreateClient();
        using var request = BuildRequest(HttpMethod.Get, $"/api/v1/recruiter/applications/{applicationId}", otherRecruiter);

        (await client.SendAsync(request, ct)).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task There_is_no_endpoint_to_browse_another_candidates_portfolio_directly()
    {
        var ct = TestContext.Current.CancellationToken;
        var owner = await IssueApplicantWithProfileAsync(ct);
        var project = await CreateProjectAsync(owner, ValidInput("Public showcase", visibility: "Public"), ct);

        var otherCandidate = await IssueApplicantWithProfileAsync(ct);
        using var client = CreateClient();

        // The only candidate-facing portfolio routes are scoped to the caller's own profile —
        // there is no {candidateId} or {projectId}-only route reachable by a non-owner.
        using var request = BuildRequest(HttpMethod.Get, $"/api/v1/portfolio/{project.Id}", otherCandidate);
        (await client.SendAsync(request, ct)).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
