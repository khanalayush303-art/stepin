using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using StepIn.Api.Endpoints;
using StepIn.Domain.Common;
using StepIn.Infrastructure.Persistence;

namespace StepIn.Api.Tests;

/// <summary>
/// Experience Passport, candidate side: CRUD, ownership isolation, Private-by-default visibility,
/// field validation, and the candidate-reachable half of the verification state machine
/// (submit/withdraw/resubmit, with editability and deletability gated by status). The
/// admin-reachable half (begin-review/approve/reject) is covered by
/// <see cref="ExperienceAdminEndpointsTests"/>.
/// </summary>
public sealed class ExperienceEndpointsTests(AuthApiFactory factory) : IClassFixture<AuthApiFactory>
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

    private async Task<string> IssueApplicantWithProfileAsync(CancellationToken ct)
    {
        var token = await IssueApplicantAsync(ct);
        using var client = CreateClient();
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
            new UpdateRecruiterProfileRequest(null, null, null, new CompanyDto(null, $"Passport Co {Guid.NewGuid():N}", null, null, null, null, null)));
        (await client.SendAsync(profile, ct)).EnsureSuccessStatusCode();

        return token;
    }

    /// <summary>Admin is never client-settable — promotes an account directly in the database,
    /// mirroring the documented psql promotion step for real admins.</summary>
    private async Task<string> IssueAdminAsync(CancellationToken ct)
    {
        var clerkUserId = $"admin_{Guid.NewGuid():N}";
        var token = AuthApiFactory.IssueToken(clerkUserId, $"{Guid.NewGuid()}@example.com");

        using (var client = CreateClient())
        {
            using var setup = BuildRequest(HttpMethod.Post, "/api/v1/auth/account-setup", token, new AccountSetupRequest("Applicant"));
            (await client.SendAsync(setup, ct)).EnsureSuccessStatusCode();
        }

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        await db.Users.Where(u => u.ClerkUserId == clerkUserId).ExecuteUpdateAsync(s => s.SetProperty(u => u.Role, UserRole.Admin), ct);

        return token;
    }

    private static ExperienceRecordInput ValidInput(
        string title = "Backend internship", string? visibility = null, string recordType = "Internship") =>
        new(recordType, title, "Acme Corp", "Worked on the payments team.", null, null, ["C#", "SQL"], "https://example.com/reference", visibility);

    private async Task<ExperienceRecordResponse> CreateRecordAsync(string token, ExperienceRecordInput? request, CancellationToken ct)
    {
        using var client = CreateClient();
        using var create = BuildRequest(HttpMethod.Post, "/api/v1/experience-passport", token, request ?? ValidInput());
        var response = await client.SendAsync(create, ct);
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        return (await response.Content.ReadFromJsonAsync<ExperienceRecordResponse>(ct))!;
    }

    // --------------------------------------------------------------- auth boundary ---

    [Fact]
    public async Task Anonymous_cannot_list_or_create_records()
    {
        var ct = TestContext.Current.CancellationToken;
        using var client = CreateClient();

        (await client.GetAsync("/api/v1/experience-passport", ct)).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await client.PostAsJsonAsync("/api/v1/experience-passport", ValidInput(), ct)).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Recruiter_cannot_use_candidate_experience_endpoints()
    {
        var ct = TestContext.Current.CancellationToken;
        var recruiter = await IssueRecruiterWithCompanyAsync(ct);
        using var client = CreateClient();
        using var request = BuildRequest(HttpMethod.Get, "/api/v1/experience-passport", recruiter);

        (await client.SendAsync(request, ct)).StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Applicant_cannot_reach_admin_endpoints()
    {
        var ct = TestContext.Current.CancellationToken;
        var applicant = await IssueApplicantWithProfileAsync(ct);
        using var client = CreateClient();

        using var queue = BuildRequest(HttpMethod.Get, "/api/v1/admin/experience-passport/queue", applicant);
        (await client.SendAsync(queue, ct)).StatusCode.Should().Be(HttpStatusCode.Forbidden);

        using var approve = BuildRequest(HttpMethod.Post, $"/api/v1/admin/experience-passport/{Guid.NewGuid()}/approve", applicant);
        (await client.SendAsync(approve, ct)).StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Creating_a_record_without_a_candidate_profile_is_a_validation_problem_not_a_crash()
    {
        var ct = TestContext.Current.CancellationToken;
        var applicant = await IssueApplicantWithoutProfileAsync(ct);
        using var client = CreateClient();
        using var request = BuildRequest(HttpMethod.Post, "/api/v1/experience-passport", applicant, ValidInput());

        (await client.SendAsync(request, ct)).StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Listing_records_without_a_profile_returns_an_empty_list_not_an_error()
    {
        var ct = TestContext.Current.CancellationToken;
        var applicant = await IssueApplicantWithoutProfileAsync(ct);
        using var client = CreateClient();
        using var request = BuildRequest(HttpMethod.Get, "/api/v1/experience-passport", applicant);

        var response = await client.SendAsync(request, ct);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await response.Content.ReadFromJsonAsync<List<ExperienceRecordSummaryResponse>>(ct))!.Should().BeEmpty();
    }

    // --------------------------------------------------------------------- CRUD ---

    [Fact]
    public async Task A_record_defaults_to_Draft_status_and_Private_visibility()
    {
        var ct = TestContext.Current.CancellationToken;
        var applicant = await IssueApplicantWithProfileAsync(ct);

        var record = await CreateRecordAsync(applicant, ValidInput(), ct);

        record.Status.Should().Be("Draft");
        record.Visibility.Should().Be("Private");
        record.CanEdit.Should().BeTrue();
        record.Timeline.Should().BeEmpty("nothing has happened to a brand-new draft yet");
    }

    [Fact]
    public async Task A_draft_can_be_edited_and_deleted()
    {
        var ct = TestContext.Current.CancellationToken;
        var applicant = await IssueApplicantWithProfileAsync(ct);
        var record = await CreateRecordAsync(applicant, ValidInput("Original title"), ct);

        using var client = CreateClient();
        using var update = BuildRequest(HttpMethod.Put, $"/api/v1/experience-passport/{record.Id}", applicant, ValidInput("Updated title"));
        var updateResponse = await client.SendAsync(update, ct);
        updateResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        (await updateResponse.Content.ReadFromJsonAsync<ExperienceRecordResponse>(ct))!.Title.Should().Be("Updated title");

        using var delete = BuildRequest(HttpMethod.Delete, $"/api/v1/experience-passport/{record.Id}", applicant);
        (await client.SendAsync(delete, ct)).StatusCode.Should().Be(HttpStatusCode.NoContent);

        using var get = BuildRequest(HttpMethod.Get, $"/api/v1/experience-passport/{record.Id}", applicant);
        (await client.SendAsync(get, ct)).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task A_candidate_cannot_read_update_or_delete_another_candidates_record()
    {
        var ct = TestContext.Current.CancellationToken;
        var owner = await IssueApplicantWithProfileAsync(ct);
        var record = await CreateRecordAsync(owner, ValidInput("Owner's record"), ct);

        var intruder = await IssueApplicantWithProfileAsync(ct);
        using var client = CreateClient();

        using var get = BuildRequest(HttpMethod.Get, $"/api/v1/experience-passport/{record.Id}", intruder);
        (await client.SendAsync(get, ct)).StatusCode.Should().Be(HttpStatusCode.NotFound);

        using var update = BuildRequest(HttpMethod.Put, $"/api/v1/experience-passport/{record.Id}", intruder, ValidInput("Hijacked"));
        (await client.SendAsync(update, ct)).StatusCode.Should().Be(HttpStatusCode.NotFound);

        using var delete = BuildRequest(HttpMethod.Delete, $"/api/v1/experience-passport/{record.Id}", intruder);
        (await client.SendAsync(delete, ct)).StatusCode.Should().Be(HttpStatusCode.NotFound);

        using var verify = BuildRequest(HttpMethod.Get, $"/api/v1/experience-passport/{record.Id}", owner);
        var verified = (await (await client.SendAsync(verify, ct)).Content.ReadFromJsonAsync<ExperienceRecordResponse>(ct))!;
        verified.Title.Should().Be("Owner's record");
    }

    // ------------------------------------------------------------------ validation ---

    [Theory]
    [InlineData("NotARealType")]
    [InlineData("")]
    public async Task An_unknown_or_blank_record_type_is_rejected(string recordType)
    {
        var ct = TestContext.Current.CancellationToken;
        var applicant = await IssueApplicantWithProfileAsync(ct);
        using var client = CreateClient();
        using var request = BuildRequest(HttpMethod.Post, "/api/v1/experience-passport", applicant, ValidInput(recordType: recordType));

        (await client.SendAsync(request, ct)).StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Theory]
    [InlineData("ftp://example.com/file")]
    [InlineData("javascript:alert(1)")]
    [InlineData("not-a-url")]
    public async Task Non_http_or_malformed_evidence_urls_are_rejected(string badUrl)
    {
        var ct = TestContext.Current.CancellationToken;
        var applicant = await IssueApplicantWithProfileAsync(ct);
        using var client = CreateClient();
        using var request = BuildRequest(HttpMethod.Post, "/api/v1/experience-passport", applicant, ValidInput() with { EvidenceUrl = badUrl });

        (await client.SendAsync(request, ct)).StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task An_end_date_before_the_start_date_is_rejected()
    {
        var ct = TestContext.Current.CancellationToken;
        var applicant = await IssueApplicantWithProfileAsync(ct);
        using var client = CreateClient();
        var request = ValidInput() with { StartDate = new DateOnly(2024, 6, 1), EndDate = new DateOnly(2024, 1, 1) };
        using var httpRequest = BuildRequest(HttpMethod.Post, "/api/v1/experience-passport", applicant, request);

        (await client.SendAsync(httpRequest, ct)).StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Blank_title_is_rejected()
    {
        var ct = TestContext.Current.CancellationToken;
        var applicant = await IssueApplicantWithProfileAsync(ct);
        using var client = CreateClient();
        using var request = BuildRequest(HttpMethod.Post, "/api/v1/experience-passport", applicant, ValidInput() with { Title = "   " });

        (await client.SendAsync(request, ct)).StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    // ------------------------------------------------------------------ state machine ---

    [Fact]
    public async Task Submitting_a_draft_locks_it_from_further_edits()
    {
        var ct = TestContext.Current.CancellationToken;
        var applicant = await IssueApplicantWithProfileAsync(ct);
        var record = await CreateRecordAsync(applicant, ValidInput(), ct);

        using var client = CreateClient();
        using var submit = BuildRequest(HttpMethod.Post, $"/api/v1/experience-passport/{record.Id}/submit", applicant);
        var submitResponse = await client.SendAsync(submit, ct);
        submitResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var submitted = (await submitResponse.Content.ReadFromJsonAsync<ExperienceRecordResponse>(ct))!;
        submitted.Status.Should().Be("Submitted");
        submitted.CanEdit.Should().BeFalse();
        submitted.SubmittedAt.Should().NotBeNull();
        submitted.Timeline.Should().ContainSingle(t => t.Action == "Submitted" && t.PreviousStatus == "Draft" && t.NewStatus == "Submitted");

        using var update = BuildRequest(HttpMethod.Put, $"/api/v1/experience-passport/{record.Id}", applicant, ValidInput("Trying to edit"));
        (await client.SendAsync(update, ct)).StatusCode.Should().Be(HttpStatusCode.BadRequest);

        using var delete = BuildRequest(HttpMethod.Delete, $"/api/v1/experience-passport/{record.Id}", applicant);
        (await client.SendAsync(delete, ct)).StatusCode.Should().Be(HttpStatusCode.BadRequest, "only a draft can be hard-deleted");
    }

    [Fact]
    public async Task Submitting_a_non_draft_record_is_rejected()
    {
        var ct = TestContext.Current.CancellationToken;
        var applicant = await IssueApplicantWithProfileAsync(ct);
        var record = await CreateRecordAsync(applicant, ValidInput(), ct);

        using var client = CreateClient();
        using var submit = BuildRequest(HttpMethod.Post, $"/api/v1/experience-passport/{record.Id}/submit", applicant);
        (await client.SendAsync(submit, ct)).EnsureSuccessStatusCode();

        using var submitAgain = BuildRequest(HttpMethod.Post, $"/api/v1/experience-passport/{record.Id}/submit", applicant);
        (await client.SendAsync(submitAgain, ct)).StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task A_candidate_cannot_set_verification_status_via_the_update_endpoint()
    {
        // ExperienceRecordInput has no status field at all, so there is nothing for arbitrary
        // JSON to override — this test proves the shape, not just the behaviour: posting a raw
        // JSON body with a "status" field is silently ignored because no bound property exists
        // for it to land on.
        var ct = TestContext.Current.CancellationToken;
        var applicant = await IssueApplicantWithProfileAsync(ct);
        var record = await CreateRecordAsync(applicant, ValidInput(), ct);

        using var client = CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Put, $"/api/v1/experience-passport/{record.Id}")
        {
            Content = JsonContent.Create(new { recordType = "Internship", title = "Still a draft", status = "Verified" }),
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", applicant);
        request.Headers.Add("X-Requested-With", "fetch");
        var response = await client.SendAsync(request, ct);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var updated = (await response.Content.ReadFromJsonAsync<ExperienceRecordResponse>(ct))!;
        updated.Status.Should().Be("Draft");
    }

    [Fact]
    public async Task Withdrawing_a_submitted_record_moves_it_to_Withdrawn_and_it_can_be_resubmitted()
    {
        var ct = TestContext.Current.CancellationToken;
        var applicant = await IssueApplicantWithProfileAsync(ct);
        var record = await CreateRecordAsync(applicant, ValidInput(), ct);

        using var client = CreateClient();
        using var submit = BuildRequest(HttpMethod.Post, $"/api/v1/experience-passport/{record.Id}/submit", applicant);
        (await client.SendAsync(submit, ct)).EnsureSuccessStatusCode();

        using var withdraw = BuildRequest(HttpMethod.Post, $"/api/v1/experience-passport/{record.Id}/withdraw", applicant,
            new ExperienceTransitionRequest("Need to fix a date."));
        var withdrawResponse = await client.SendAsync(withdraw, ct);
        withdrawResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var withdrawn = (await withdrawResponse.Content.ReadFromJsonAsync<ExperienceRecordResponse>(ct))!;
        withdrawn.Status.Should().Be("Withdrawn");
        withdrawn.CanEdit.Should().BeTrue();

        using var edit = BuildRequest(HttpMethod.Put, $"/api/v1/experience-passport/{record.Id}", applicant, ValidInput("Corrected title"));
        (await client.SendAsync(edit, ct)).EnsureSuccessStatusCode();

        using var resubmit = BuildRequest(HttpMethod.Post, $"/api/v1/experience-passport/{record.Id}/resubmit", applicant);
        var resubmitResponse = await client.SendAsync(resubmit, ct);
        resubmitResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var resubmitted = (await resubmitResponse.Content.ReadFromJsonAsync<ExperienceRecordResponse>(ct))!;
        resubmitted.Status.Should().Be("Submitted");
        resubmitted.Title.Should().Be("Corrected title");
        resubmitted.Timeline.Should().HaveCount(3);
        resubmitted.Timeline.Select(t => t.Action).Should().BeEquivalentTo(["Submitted", "Withdrawn", "Resubmitted"], options => options.WithStrictOrdering());
    }

    [Fact]
    public async Task A_draft_cannot_be_withdrawn_or_resubmitted()
    {
        var ct = TestContext.Current.CancellationToken;
        var applicant = await IssueApplicantWithProfileAsync(ct);
        var record = await CreateRecordAsync(applicant, ValidInput(), ct);

        using var client = CreateClient();
        using var withdraw = BuildRequest(HttpMethod.Post, $"/api/v1/experience-passport/{record.Id}/withdraw", applicant);
        (await client.SendAsync(withdraw, ct)).StatusCode.Should().Be(HttpStatusCode.BadRequest);

        using var resubmit = BuildRequest(HttpMethod.Post, $"/api/v1/experience-passport/{record.Id}/resubmit", applicant);
        (await client.SendAsync(resubmit, ct)).StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task A_verified_record_can_be_withdrawn_for_correction_preserving_the_approval_in_the_timeline()
    {
        var ct = TestContext.Current.CancellationToken;
        var applicant = await IssueApplicantWithProfileAsync(ct);
        var record = await CreateRecordAsync(applicant, ValidInput(), ct);
        var admin = await IssueAdminAsync(ct);

        using var client = CreateClient();
        using var submit = BuildRequest(HttpMethod.Post, $"/api/v1/experience-passport/{record.Id}/submit", applicant);
        (await client.SendAsync(submit, ct)).EnsureSuccessStatusCode();

        using var beginReview = BuildRequest(HttpMethod.Post, $"/api/v1/admin/experience-passport/{record.Id}/begin-review", admin);
        (await client.SendAsync(beginReview, ct)).EnsureSuccessStatusCode();

        using var approve = BuildRequest(HttpMethod.Post, $"/api/v1/admin/experience-passport/{record.Id}/approve", admin);
        (await client.SendAsync(approve, ct)).EnsureSuccessStatusCode();

        using var withdraw = BuildRequest(HttpMethod.Post, $"/api/v1/experience-passport/{record.Id}/withdraw", applicant);
        var withdrawResponse = await client.SendAsync(withdraw, ct);
        withdrawResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var withdrawn = (await withdrawResponse.Content.ReadFromJsonAsync<ExperienceRecordResponse>(ct))!;

        withdrawn.Status.Should().Be("Withdrawn");
        withdrawn.Timeline.Select(t => t.Action).Should().BeEquivalentTo(
            ["Submitted", "ReviewStarted", "Approved", "Withdrawn"], options => options.WithStrictOrdering());
        withdrawn.Timeline.Should().ContainSingle(t => t.Action == "Approved", "the original verification decision must stay in history");
    }

    [Fact]
    public async Task A_candidates_own_timeline_never_includes_the_reviewers_identity()
    {
        var ct = TestContext.Current.CancellationToken;
        var applicant = await IssueApplicantWithProfileAsync(ct);
        var record = await CreateRecordAsync(applicant, ValidInput(), ct);
        var admin = await IssueAdminAsync(ct);

        using var client = CreateClient();
        using var submit = BuildRequest(HttpMethod.Post, $"/api/v1/experience-passport/{record.Id}/submit", applicant);
        (await client.SendAsync(submit, ct)).EnsureSuccessStatusCode();

        using var beginReview = BuildRequest(HttpMethod.Post, $"/api/v1/admin/experience-passport/{record.Id}/begin-review", admin);
        (await client.SendAsync(beginReview, ct)).EnsureSuccessStatusCode();

        using var get = BuildRequest(HttpMethod.Get, $"/api/v1/experience-passport/{record.Id}", applicant);
        var response = await client.SendAsync(get, ct);
        var raw = await response.Content.ReadAsStringAsync(ct);

        raw.Should().NotContain("ActorName").And.NotContain("actorName");
        var reloaded = (await response.Content.ReadFromJsonAsync<ExperienceRecordResponse>(ct))!;
        reloaded.Status.Should().Be("UnderReview");
    }

    // ------------------------------------------------------- recruiter visibility ---

    [Fact]
    public async Task A_recruiter_sees_only_the_applicants_Public_records_with_their_real_status_never_Private_ones()
    {
        var ct = TestContext.Current.CancellationToken;
        var applicant = await IssueApplicantWithProfileAsync(ct);
        var publicRecord = await CreateRecordAsync(applicant, ValidInput("Public internship", visibility: "Public"), ct);
        var privateRecord = await CreateRecordAsync(applicant, ValidInput("Private project", visibility: "Private"), ct);

        var recruiter = await IssueRecruiterWithCompanyAsync(ct);
        using var client = CreateClient();
        using var createJob = BuildRequest(HttpMethod.Post, "/api/v1/recruiter/jobs", recruiter,
            new CreateJobRequest("Graduate role", "Build things.", "FullTime", "Hybrid", "Sydney, NSW", "$70k-80k", ["C#"]));
        var job = (await (await client.SendAsync(createJob, ct)).Content.ReadFromJsonAsync<JobResponse>(ct))!;
        using var publish = BuildRequest(HttpMethod.Post, $"/api/v1/recruiter/jobs/{job.Id}/publish", recruiter);
        (await client.SendAsync(publish, ct)).EnsureSuccessStatusCode();

        using var apply = new HttpRequestMessage(HttpMethod.Post, $"/api/v1/jobs/{job.Id}/applications");
        var form = new MultipartFormDataContent();
        var fileContent = new ByteArrayContent("%PDF-1.4 minimal test resume"u8.ToArray());
        fileContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/pdf");
        form.Add(fileContent, "Resume", "resume.pdf");
        apply.Content = form;
        apply.Headers.Add("X-Requested-With", "fetch");
        apply.Headers.Authorization = new AuthenticationHeaderValue("Bearer", applicant);
        var applyResponse = await client.SendAsync(apply, ct);
        applyResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var application = (await applyResponse.Content.ReadFromJsonAsync<ApplicationResponse>(ct))!;

        using var getApplication = BuildRequest(HttpMethod.Get, $"/api/v1/recruiter/applications/{application.Id}", recruiter);
        var detailResponse = await client.SendAsync(getApplication, ct);
        detailResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var detail = (await detailResponse.Content.ReadFromJsonAsync<RecruiterApplicationResponse>(ct))!;

        detail.ExperienceRecords.Should().ContainSingle(r => r.Id == publicRecord.Id && r.Title == "Public internship" && r.Status == "Draft");
        detail.ExperienceRecords.Should().NotContain(r => r.Id == privateRecord.Id);
    }
}
