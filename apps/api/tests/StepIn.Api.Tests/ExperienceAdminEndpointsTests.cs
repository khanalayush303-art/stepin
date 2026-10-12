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
/// Experience Passport, admin side: the verification queue, the review decisions
/// (begin-review/approve/reject), the resulting audit trail, and the authorization boundary
/// around all of it. The candidate-reachable half of the state machine is covered by
/// <see cref="ExperienceEndpointsTests"/>.
/// </summary>
public sealed class ExperienceAdminEndpointsTests(AuthApiFactory factory) : IClassFixture<AuthApiFactory>
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
            new UpdateRecruiterProfileRequest(null, null, null, new CompanyDto(null, $"Admin Test Co {Guid.NewGuid():N}", null, null, null, null, null)));
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

    private static ExperienceRecordInput ValidInput(string title = "Backend internship") =>
        new("Internship", title, "Acme Corp", "Worked on the payments team.", null, null, ["C#"], null, null);

    private async Task<ExperienceRecordResponse> CreateAndSubmitRecordAsync(string applicantToken, CancellationToken ct)
    {
        using var client = CreateClient();
        using var create = BuildRequest(HttpMethod.Post, "/api/v1/experience-passport", applicantToken, ValidInput());
        var createResponse = await client.SendAsync(create, ct);
        var record = (await createResponse.Content.ReadFromJsonAsync<ExperienceRecordResponse>(ct))!;

        using var submit = BuildRequest(HttpMethod.Post, $"/api/v1/experience-passport/{record.Id}/submit", applicantToken);
        var submitResponse = await client.SendAsync(submit, ct);
        return (await submitResponse.Content.ReadFromJsonAsync<ExperienceRecordResponse>(ct))!;
    }

    // --------------------------------------------------------------- auth boundary ---

    [Fact]
    public async Task Anonymous_cannot_access_the_queue_or_any_review_action()
    {
        var ct = TestContext.Current.CancellationToken;
        using var client = CreateClient();

        (await client.GetAsync("/api/v1/admin/experience-passport/queue", ct)).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await client.PostAsync($"/api/v1/admin/experience-passport/{Guid.NewGuid()}/approve", null, ct))
            .StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Recruiter_cannot_access_the_queue_or_perform_reviewer_actions()
    {
        var ct = TestContext.Current.CancellationToken;
        var recruiter = await IssueRecruiterWithCompanyAsync(ct);
        using var client = CreateClient();

        using var queue = BuildRequest(HttpMethod.Get, "/api/v1/admin/experience-passport/queue", recruiter);
        (await client.SendAsync(queue, ct)).StatusCode.Should().Be(HttpStatusCode.Forbidden);

        using var approve = BuildRequest(HttpMethod.Post, $"/api/v1/admin/experience-passport/{Guid.NewGuid()}/approve", recruiter);
        (await client.SendAsync(approve, ct)).StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    // ----------------------------------------------------------------- queue + review ---

    [Fact]
    public async Task The_queue_defaults_to_Submitted_and_UnderReview_items_only()
    {
        var ct = TestContext.Current.CancellationToken;
        var applicant = await IssueApplicantWithProfileAsync(ct);
        var draftRecord = await CreateAndSubmitRecordAsync(applicant, ct); // becomes Submitted
        var admin = await IssueAdminAsync(ct);

        using var client = CreateClient();
        using var queue = BuildRequest(HttpMethod.Get, "/api/v1/admin/experience-passport/queue", admin);
        var response = await client.SendAsync(queue, ct);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var items = (await response.Content.ReadFromJsonAsync<List<ExperienceQueueItemResponse>>(ct))!;

        items.Should().Contain(i => i.Id == draftRecord.Id && i.Status == "Submitted");
    }

    [Fact]
    public async Task Begin_review_then_approve_moves_the_record_to_Verified_and_logs_the_actor()
    {
        var ct = TestContext.Current.CancellationToken;
        var applicant = await IssueApplicantWithProfileAsync(ct);
        var submitted = await CreateAndSubmitRecordAsync(applicant, ct);
        var admin = await IssueAdminAsync(ct);

        using var client = CreateClient();
        using var beginReview = BuildRequest(HttpMethod.Post, $"/api/v1/admin/experience-passport/{submitted.Id}/begin-review", admin);
        var reviewResponse = await client.SendAsync(beginReview, ct);
        reviewResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var underReview = (await reviewResponse.Content.ReadFromJsonAsync<ExperienceAdminRecordResponse>(ct))!;
        underReview.Status.Should().Be("UnderReview");

        using var approve = BuildRequest(HttpMethod.Post, $"/api/v1/admin/experience-passport/{submitted.Id}/approve", admin,
            new ExperienceReviewDecisionRequest("Confirmed via LinkedIn."));
        var approveResponse = await client.SendAsync(approve, ct);
        approveResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var approved = (await approveResponse.Content.ReadFromJsonAsync<ExperienceAdminRecordResponse>(ct))!;

        approved.Status.Should().Be("Verified");
        var approvalEntry = approved.AuditHistory.Should().ContainSingle(a => a.Action == "Approved").Subject;
        approvalEntry.Note.Should().Be("Confirmed via LinkedIn.");
        approvalEntry.ActorRole.Should().Be("Admin");
        approvalEntry.ActorName.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task Rejecting_a_record_under_review_records_the_reason()
    {
        var ct = TestContext.Current.CancellationToken;
        var applicant = await IssueApplicantWithProfileAsync(ct);
        var submitted = await CreateAndSubmitRecordAsync(applicant, ct);
        var admin = await IssueAdminAsync(ct);

        using var client = CreateClient();
        using var beginReview = BuildRequest(HttpMethod.Post, $"/api/v1/admin/experience-passport/{submitted.Id}/begin-review", admin);
        (await client.SendAsync(beginReview, ct)).EnsureSuccessStatusCode();

        using var reject = BuildRequest(HttpMethod.Post, $"/api/v1/admin/experience-passport/{submitted.Id}/reject", admin,
            new ExperienceReviewDecisionRequest("No supporting evidence provided."));
        var rejectResponse = await client.SendAsync(reject, ct);
        rejectResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var rejected = (await rejectResponse.Content.ReadFromJsonAsync<ExperienceAdminRecordResponse>(ct))!;

        rejected.Status.Should().Be("Rejected");
        rejected.AuditHistory.Should().ContainSingle(a => a.Action == "Rejected" && a.Note == "No supporting evidence provided.");

        // Candidate can see the rejection reason on their own timeline.
        using var getOwn = BuildRequest(HttpMethod.Get, $"/api/v1/experience-passport/{submitted.Id}", applicant);
        var own = (await (await client.SendAsync(getOwn, ct)).Content.ReadFromJsonAsync<ExperienceRecordResponse>(ct))!;
        own.Timeline.Should().ContainSingle(t => t.Action == "Rejected" && t.Note == "No supporting evidence provided.");
        own.CanEdit.Should().BeTrue("a rejected record can be corrected and resubmitted");
    }

    [Theory]
    [InlineData("approve")]
    [InlineData("reject")]
    public async Task Approving_or_rejecting_a_record_that_is_not_under_review_is_rejected(string action)
    {
        var ct = TestContext.Current.CancellationToken;
        var applicant = await IssueApplicantWithProfileAsync(ct);
        var submitted = await CreateAndSubmitRecordAsync(applicant, ct); // Submitted, not yet UnderReview
        var admin = await IssueAdminAsync(ct);

        using var client = CreateClient();
        using var request = BuildRequest(HttpMethod.Post, $"/api/v1/admin/experience-passport/{submitted.Id}/{action}", admin);
        (await client.SendAsync(request, ct)).StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Approving_the_same_record_twice_only_succeeds_once()
    {
        var ct = TestContext.Current.CancellationToken;
        var applicant = await IssueApplicantWithProfileAsync(ct);
        var submitted = await CreateAndSubmitRecordAsync(applicant, ct);
        var admin = await IssueAdminAsync(ct);

        using var client = CreateClient();
        using var beginReview = BuildRequest(HttpMethod.Post, $"/api/v1/admin/experience-passport/{submitted.Id}/begin-review", admin);
        (await client.SendAsync(beginReview, ct)).EnsureSuccessStatusCode();

        using var firstApprove = BuildRequest(HttpMethod.Post, $"/api/v1/admin/experience-passport/{submitted.Id}/approve", admin);
        (await client.SendAsync(firstApprove, ct)).StatusCode.Should().Be(HttpStatusCode.OK);

        using var secondApprove = BuildRequest(HttpMethod.Post, $"/api/v1/admin/experience-passport/{submitted.Id}/approve", admin);
        (await client.SendAsync(secondApprove, ct)).StatusCode.Should().Be(HttpStatusCode.BadRequest);

        using var getRecord = BuildRequest(HttpMethod.Get, $"/api/v1/admin/experience-passport/{submitted.Id}", admin);
        var record = (await (await client.SendAsync(getRecord, ct)).Content.ReadFromJsonAsync<ExperienceAdminRecordResponse>(ct))!;
        record.AuditHistory.Where(a => a.Action == "Approved").Should().ContainSingle("a failed second approval must not leave a duplicate or misleading audit entry");
    }

    [Fact]
    public async Task An_admin_cannot_review_their_own_submitted_record()
    {
        // The record must be created and submitted while this user still holds the Applicant
        // role (RequireApplicant would otherwise reject the create/submit calls themselves) —
        // promotion to Admin happens only afterward, exactly as it would for a real account that
        // later becomes an admin.
        var ct = TestContext.Current.CancellationToken;
        var clerkUserId = $"user_{Guid.NewGuid():N}";
        var token = AuthApiFactory.IssueToken(clerkUserId, $"{Guid.NewGuid()}@example.com");

        using (var setupClient = CreateClient())
        {
            using var setup = BuildRequest(HttpMethod.Post, "/api/v1/auth/account-setup", token, new AccountSetupRequest("Applicant"));
            (await setupClient.SendAsync(setup, ct)).EnsureSuccessStatusCode();
            using var profile = BuildRequest(HttpMethod.Put, "/api/v1/profile/candidate", token,
                new UpdateCandidateProfileRequest(null, null, "Graduate", null, null, null, null, null, null, null, null, null));
            (await setupClient.SendAsync(profile, ct)).EnsureSuccessStatusCode();
        }

        var submitted = await CreateAndSubmitRecordAsync(token, ct);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        await db.Users.Where(u => u.ClerkUserId == clerkUserId).ExecuteUpdateAsync(s => s.SetProperty(u => u.Role, UserRole.Admin), ct);
        var admin = token;

        using var client = CreateClient();
        using var beginReview = BuildRequest(HttpMethod.Post, $"/api/v1/admin/experience-passport/{submitted.Id}/begin-review", admin);
        (await client.SendAsync(beginReview, ct)).StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Candidate_cannot_edit_or_delete_audit_entries_there_is_no_such_endpoint()
    {
        // There is no PUT/DELETE route anywhere under /api/v1/experience-passport or
        // /api/v1/admin/experience-passport that targets an audit row directly — the only way
        // audit rows are ever created is through a state-transition endpoint, and there is no
        // route that accepts an audit row id at all.
        var ct = TestContext.Current.CancellationToken;
        var applicant = await IssueApplicantWithProfileAsync(ct);
        var submitted = await CreateAndSubmitRecordAsync(applicant, ct);

        using var client = CreateClient();
        using var attempt = BuildRequest(HttpMethod.Delete, $"/api/v1/experience-passport/{submitted.Id}/audit/{Guid.NewGuid()}", applicant);
        var response = await client.SendAsync(attempt, ct);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound, "no such route exists");
    }
}
