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
/// Career GPS: goals and roadmap items. Covers persistence, cross-candidate ownership, status
/// transitions, server-computed progress, and skill-gap reuse of JobMatchScorer against a goal's
/// target job.
/// </summary>
public sealed class CareerGoalEndpointsTests(AuthApiFactory factory) : IClassFixture<AuthApiFactory>
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

    /// <summary>Issues an applicant token without ever creating a candidate profile.</summary>
    private async Task<string> IssueApplicantWithoutProfileAsync(CancellationToken ct) => await IssueApplicantAsync(ct);

    private async Task<string> IssueApplicantWithProfileAsync(string[] skills, CancellationToken ct)
    {
        var token = await IssueApplicantAsync(ct);
        using var client = CreateClient();
        using var request = BuildRequest(HttpMethod.Put, "/api/v1/profile/candidate", token,
            new UpdateCandidateProfileRequest(null, null, null, null, null, null, null, null, skills, null, null, null));
        (await client.SendAsync(request, ct)).EnsureSuccessStatusCode();
        return token;
    }

    private async Task<string> IssueRecruiterWithCompanyAsync(CancellationToken ct)
    {
        var token = AuthApiFactory.IssueToken($"user_{Guid.NewGuid():N}", $"{Guid.NewGuid()}@example.com");
        using var client = CreateClient();
        using var setup = BuildRequest(HttpMethod.Post, "/api/v1/auth/account-setup", token, new AccountSetupRequest("Recruiter"));
        (await client.SendAsync(setup, ct)).EnsureSuccessStatusCode();

        using var profile = BuildRequest(HttpMethod.Put, "/api/v1/profile/recruiter", token,
            new UpdateRecruiterProfileRequest(null, null, null, new CompanyDto(null, Unique("GPS Co"), null, null, null, null, null)));
        (await client.SendAsync(profile, ct)).EnsureSuccessStatusCode();

        return token;
    }

    private async Task<Guid> CreateJobAsync(string recruiterToken, string[] skills, bool publish, JobCategory category, CancellationToken ct)
    {
        using var client = CreateClient();
        using var create = BuildRequest(HttpMethod.Post, "/api/v1/recruiter/jobs", recruiterToken,
            new CreateJobRequest(Unique("Target role"), "Work on things.", "FullTime", "Hybrid", "Sydney, NSW", null, skills));
        var createResponse = await client.SendAsync(create, ct);
        var jobId = (await createResponse.Content.ReadFromJsonAsync<JobResponse>(ct))!.Id;

        if (category == JobCategory.Research)
        {
            using var scope = _factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            await db.Jobs.Where(j => j.Id == jobId).ExecuteUpdateAsync(s => s.SetProperty(j => j.Category, category), ct);
        }

        if (publish)
        {
            using var publishRequest = BuildRequest(HttpMethod.Post, $"/api/v1/recruiter/jobs/{jobId}/publish", recruiterToken);
            (await client.SendAsync(publishRequest, ct)).EnsureSuccessStatusCode();
        }

        return jobId;
    }

    private async Task<CareerGoalResponse> CreateGoalAsync(string token, string targetRole, Guid? targetJobId, CancellationToken ct)
    {
        using var client = CreateClient();
        using var request = BuildRequest(HttpMethod.Post, "/api/v1/career-goals", token,
            new CreateCareerGoalRequest(targetRole, null, null, targetJobId));
        var response = await client.SendAsync(request, ct);
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        return (await response.Content.ReadFromJsonAsync<CareerGoalResponse>(ct))!;
    }

    // --------------------------------------------------------------- auth boundary ---

    [Fact]
    public async Task Anonymous_cannot_list_or_create_goals()
    {
        var ct = TestContext.Current.CancellationToken;
        using var client = CreateClient();

        (await client.GetAsync("/api/v1/career-goals", ct)).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await client.PostAsJsonAsync("/api/v1/career-goals", new CreateCareerGoalRequest("Role", null, null, null), ct))
            .StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Recruiter_cannot_use_career_goal_endpoints()
    {
        var ct = TestContext.Current.CancellationToken;
        var recruiter = await IssueRecruiterWithCompanyAsync(ct);
        using var client = CreateClient();
        using var request = BuildRequest(HttpMethod.Get, "/api/v1/career-goals", recruiter);

        (await client.SendAsync(request, ct)).StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Creating_a_goal_without_a_candidate_profile_is_a_validation_problem_not_a_crash()
    {
        var ct = TestContext.Current.CancellationToken;
        var applicant = await IssueApplicantWithoutProfileAsync(ct);
        using var client = CreateClient();
        using var request = BuildRequest(HttpMethod.Post, "/api/v1/career-goals", applicant,
            new CreateCareerGoalRequest("Graduate Software Engineer", null, null, null));

        var response = await client.SendAsync(request, ct);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Listing_goals_without_a_profile_returns_an_empty_list_not_an_error()
    {
        var ct = TestContext.Current.CancellationToken;
        var applicant = await IssueApplicantWithoutProfileAsync(ct);
        using var client = CreateClient();
        using var request = BuildRequest(HttpMethod.Get, "/api/v1/career-goals", applicant);

        var response = await client.SendAsync(request, ct);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await response.Content.ReadFromJsonAsync<List<CareerGoalResponse>>(ct))!.Should().BeEmpty();
    }

    // --------------------------------------------------------------------- CRUD ---

    [Fact]
    public async Task A_goal_can_be_created_with_no_target_job_and_remains_fully_usable()
    {
        var ct = TestContext.Current.CancellationToken;
        var applicant = await IssueApplicantWithProfileAsync(["C#"], ct);

        var goal = await CreateGoalAsync(applicant, "Graduate Software Engineer", null, ct);

        goal.TargetJobId.Should().BeNull();
        goal.RoadmapItemCount.Should().Be(0);
        goal.ProgressPercent.Should().Be(0, "an empty roadmap must never show a misleading percentage");
        goal.Status.Should().Be("Active");
    }

    [Fact]
    public async Task A_candidate_cannot_read_update_or_delete_another_candidates_goal()
    {
        var ct = TestContext.Current.CancellationToken;
        var owner = await IssueApplicantWithProfileAsync(["C#"], ct);
        var goal = await CreateGoalAsync(owner, "Data Analyst", null, ct);

        var intruder = await IssueApplicantWithProfileAsync(["C#"], ct);
        using var client = CreateClient();

        using var get = BuildRequest(HttpMethod.Get, $"/api/v1/career-goals/{goal.Id}", intruder);
        (await client.SendAsync(get, ct)).StatusCode.Should().Be(HttpStatusCode.NotFound);

        using var update = BuildRequest(HttpMethod.Put, $"/api/v1/career-goals/{goal.Id}", intruder,
            new UpdateCareerGoalRequest("Hijacked", null, null, null, false));
        (await client.SendAsync(update, ct)).StatusCode.Should().Be(HttpStatusCode.NotFound);

        using var complete = BuildRequest(HttpMethod.Post, $"/api/v1/career-goals/{goal.Id}/complete", intruder);
        (await client.SendAsync(complete, ct)).StatusCode.Should().Be(HttpStatusCode.NotFound);

        using var delete = BuildRequest(HttpMethod.Delete, $"/api/v1/career-goals/{goal.Id}", intruder);
        (await client.SendAsync(delete, ct)).StatusCode.Should().Be(HttpStatusCode.NotFound);

        // Prove nothing actually changed, same shape as every other ownership test in this suite.
        using var verifyGet = BuildRequest(HttpMethod.Get, $"/api/v1/career-goals/{goal.Id}", owner);
        var verifyResponse = await client.SendAsync(verifyGet, ct);
        var verified = (await verifyResponse.Content.ReadFromJsonAsync<CareerGoalResponse>(ct))!;
        verified.TargetRole.Should().Be("Data Analyst");
        verified.Status.Should().Be("Active");
    }

    [Fact]
    public async Task Setting_a_goal_as_primary_clears_primary_on_the_candidates_other_goals()
    {
        var ct = TestContext.Current.CancellationToken;
        var applicant = await IssueApplicantWithProfileAsync(["C#"], ct);
        var first = await CreateGoalAsync(applicant, "Role A", null, ct);
        var second = await CreateGoalAsync(applicant, "Role B", null, ct);

        using var client = CreateClient();
        using var makeFirstPrimary = BuildRequest(HttpMethod.Put, $"/api/v1/career-goals/{first.Id}", applicant,
            new UpdateCareerGoalRequest(first.TargetRole, null, null, null, true));
        (await client.SendAsync(makeFirstPrimary, ct)).EnsureSuccessStatusCode();

        using var makeSecondPrimary = BuildRequest(HttpMethod.Put, $"/api/v1/career-goals/{second.Id}", applicant,
            new UpdateCareerGoalRequest(second.TargetRole, null, null, null, true));
        (await client.SendAsync(makeSecondPrimary, ct)).EnsureSuccessStatusCode();

        using var getFirst = BuildRequest(HttpMethod.Get, $"/api/v1/career-goals/{first.Id}", applicant);
        var firstNow = (await (await client.SendAsync(getFirst, ct)).Content.ReadFromJsonAsync<CareerGoalResponse>(ct))!;
        firstNow.IsPrimary.Should().BeFalse("only one goal may be primary at a time");

        using var getSecond = BuildRequest(HttpMethod.Get, $"/api/v1/career-goals/{second.Id}", applicant);
        var secondNow = (await (await client.SendAsync(getSecond, ct)).Content.ReadFromJsonAsync<CareerGoalResponse>(ct))!;
        secondNow.IsPrimary.Should().BeTrue();
    }

    [Theory]
    [InlineData("complete", "Completed")]
    [InlineData("archive", "Archived")]
    public async Task Status_transitions_apply_and_are_persisted(string action, string expectedStatus)
    {
        var ct = TestContext.Current.CancellationToken;
        var applicant = await IssueApplicantWithProfileAsync(["C#"], ct);
        var goal = await CreateGoalAsync(applicant, "Role", null, ct);

        using var client = CreateClient();
        using var transition = BuildRequest(HttpMethod.Post, $"/api/v1/career-goals/{goal.Id}/{action}", applicant);
        (await client.SendAsync(transition, ct)).EnsureSuccessStatusCode();

        using var get = BuildRequest(HttpMethod.Get, $"/api/v1/career-goals/{goal.Id}", applicant);
        var reloaded = (await (await client.SendAsync(get, ct)).Content.ReadFromJsonAsync<CareerGoalResponse>(ct))!;
        reloaded.Status.Should().Be(expectedStatus);
    }

    [Fact]
    public async Task Deleting_a_goal_removes_it_and_its_roadmap_items()
    {
        var ct = TestContext.Current.CancellationToken;
        var applicant = await IssueApplicantWithProfileAsync(["C#"], ct);
        var goal = await CreateGoalAsync(applicant, "Role", null, ct);

        using var client = CreateClient();
        using var createItem = BuildRequest(HttpMethod.Post, $"/api/v1/career-goals/{goal.Id}/roadmap-items", applicant,
            new CreateRoadmapItemRequest("Learn SQL", null, null, null));
        (await client.SendAsync(createItem, ct)).EnsureSuccessStatusCode();

        using var delete = BuildRequest(HttpMethod.Delete, $"/api/v1/career-goals/{goal.Id}", applicant);
        (await client.SendAsync(delete, ct)).StatusCode.Should().Be(HttpStatusCode.NoContent);

        using var get = BuildRequest(HttpMethod.Get, $"/api/v1/career-goals/{goal.Id}", applicant);
        (await client.SendAsync(get, ct)).StatusCode.Should().Be(HttpStatusCode.NotFound);

        using var listItems = BuildRequest(HttpMethod.Get, $"/api/v1/career-goals/{goal.Id}/roadmap-items", applicant);
        (await client.SendAsync(listItems, ct)).StatusCode.Should().Be(HttpStatusCode.NotFound, "the goal, and everything scoped to it, is gone");
    }

    // ------------------------------------------------------------ target job rules ---

    [Fact]
    public async Task A_draft_job_cannot_be_set_as_a_target_job()
    {
        var ct = TestContext.Current.CancellationToken;
        var recruiter = await IssueRecruiterWithCompanyAsync(ct);
        var draftId = await CreateJobAsync(recruiter, ["C#"], publish: false, JobCategory.Career, ct);
        var applicant = await IssueApplicantWithProfileAsync(["C#"], ct);

        using var client = CreateClient();
        using var request = BuildRequest(HttpMethod.Post, "/api/v1/career-goals", applicant,
            new CreateCareerGoalRequest("Role", null, null, draftId));

        (await client.SendAsync(request, ct)).StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task A_research_category_job_cannot_be_set_as_an_ordinary_career_gps_target_job()
    {
        var ct = TestContext.Current.CancellationToken;
        var recruiter = await IssueRecruiterWithCompanyAsync(ct);
        var researchJobId = await CreateJobAsync(recruiter, ["C#"], publish: true, JobCategory.Research, ct);
        var applicant = await IssueApplicantWithProfileAsync(["C#"], ct);

        using var client = CreateClient();
        using var request = BuildRequest(HttpMethod.Post, "/api/v1/career-goals", applicant,
            new CreateCareerGoalRequest("Role", null, null, researchJobId));

        (await client.SendAsync(request, ct)).StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task An_unknown_target_job_id_is_rejected()
    {
        var ct = TestContext.Current.CancellationToken;
        var applicant = await IssueApplicantWithProfileAsync(["C#"], ct);
        using var client = CreateClient();
        using var request = BuildRequest(HttpMethod.Post, "/api/v1/career-goals", applicant,
            new CreateCareerGoalRequest("Role", null, null, Guid.NewGuid()));

        (await client.SendAsync(request, ct)).StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    // ------------------------------------------------------------------ roadmap ---

    [Fact]
    public async Task Progress_is_computed_server_side_from_completed_items_not_trusted_from_the_client()
    {
        var ct = TestContext.Current.CancellationToken;
        var applicant = await IssueApplicantWithProfileAsync(["C#"], ct);
        var goal = await CreateGoalAsync(applicant, "Role", null, ct);

        using var client = CreateClient();
        using var createA = BuildRequest(HttpMethod.Post, $"/api/v1/career-goals/{goal.Id}/roadmap-items", applicant,
            new CreateRoadmapItemRequest("Task A", null, null, null));
        var itemA = (await (await client.SendAsync(createA, ct)).Content.ReadFromJsonAsync<RoadmapItemResponse>(ct))!;

        using var createB = BuildRequest(HttpMethod.Post, $"/api/v1/career-goals/{goal.Id}/roadmap-items", applicant,
            new CreateRoadmapItemRequest("Task B", null, null, null));
        (await client.SendAsync(createB, ct)).EnsureSuccessStatusCode();

        using var completeA = BuildRequest(HttpMethod.Post, $"/api/v1/career-goals/{goal.Id}/roadmap-items/{itemA.Id}/complete", applicant);
        (await client.SendAsync(completeA, ct)).EnsureSuccessStatusCode();

        using var get = BuildRequest(HttpMethod.Get, $"/api/v1/career-goals/{goal.Id}", applicant);
        var reloaded = (await (await client.SendAsync(get, ct)).Content.ReadFromJsonAsync<CareerGoalResponse>(ct))!;

        reloaded.RoadmapItemCount.Should().Be(2);
        reloaded.CompletedRoadmapItemCount.Should().Be(1);
        reloaded.ProgressPercent.Should().Be(50);
    }

    [Fact]
    public async Task Completing_then_reopening_an_item_sets_and_clears_CompletedAt()
    {
        var ct = TestContext.Current.CancellationToken;
        var applicant = await IssueApplicantWithProfileAsync(["C#"], ct);
        var goal = await CreateGoalAsync(applicant, "Role", null, ct);

        using var client = CreateClient();
        using var create = BuildRequest(HttpMethod.Post, $"/api/v1/career-goals/{goal.Id}/roadmap-items", applicant,
            new CreateRoadmapItemRequest("Task", null, null, "High"));
        var item = (await (await client.SendAsync(create, ct)).Content.ReadFromJsonAsync<RoadmapItemResponse>(ct))!;
        item.Priority.Should().Be("High");
        item.Status.Should().Be("NotStarted");

        using var complete = BuildRequest(HttpMethod.Post, $"/api/v1/career-goals/{goal.Id}/roadmap-items/{item.Id}/complete", applicant);
        var completed = (await (await client.SendAsync(complete, ct)).Content.ReadFromJsonAsync<RoadmapItemResponse>(ct))!;
        completed.Status.Should().Be("Completed");
        completed.CompletedAt.Should().NotBeNull();

        using var reopen = BuildRequest(HttpMethod.Post, $"/api/v1/career-goals/{goal.Id}/roadmap-items/{item.Id}/reopen", applicant);
        var reopened = (await (await client.SendAsync(reopen, ct)).Content.ReadFromJsonAsync<RoadmapItemResponse>(ct))!;
        reopened.Status.Should().Be("NotStarted");
        reopened.CompletedAt.Should().BeNull();
    }

    [Fact]
    public async Task A_candidate_cannot_create_or_modify_roadmap_items_under_another_candidates_goal()
    {
        var ct = TestContext.Current.CancellationToken;
        var owner = await IssueApplicantWithProfileAsync(["C#"], ct);
        var goal = await CreateGoalAsync(owner, "Role", null, ct);

        using var client = CreateClient();
        using var createOwn = BuildRequest(HttpMethod.Post, $"/api/v1/career-goals/{goal.Id}/roadmap-items", owner,
            new CreateRoadmapItemRequest("Task", null, null, null));
        var item = (await (await client.SendAsync(createOwn, ct)).Content.ReadFromJsonAsync<RoadmapItemResponse>(ct))!;

        var intruder = await IssueApplicantWithProfileAsync(["C#"], ct);

        using var createUnderOther = BuildRequest(HttpMethod.Post, $"/api/v1/career-goals/{goal.Id}/roadmap-items", intruder,
            new CreateRoadmapItemRequest("Hijack", null, null, null));
        (await client.SendAsync(createUnderOther, ct)).StatusCode.Should().Be(HttpStatusCode.NotFound);

        using var completeOther = BuildRequest(HttpMethod.Post, $"/api/v1/career-goals/{goal.Id}/roadmap-items/{item.Id}/complete", intruder);
        (await client.SendAsync(completeOther, ct)).StatusCode.Should().Be(HttpStatusCode.NotFound);

        using var deleteOther = BuildRequest(HttpMethod.Delete, $"/api/v1/career-goals/{goal.Id}/roadmap-items/{item.Id}", intruder);
        (await client.SendAsync(deleteOther, ct)).StatusCode.Should().Be(HttpStatusCode.NotFound);

        // Prove nothing changed.
        using var verify = BuildRequest(HttpMethod.Get, $"/api/v1/career-goals/{goal.Id}/roadmap-items", owner);
        var items = (await (await client.SendAsync(verify, ct)).Content.ReadFromJsonAsync<List<RoadmapItemResponse>>(ct))!;
        items.Should().ContainSingle(i => i.Id == item.Id && i.Status == "NotStarted");
    }

    [Fact]
    public async Task Deleting_a_roadmap_item_removes_only_that_item()
    {
        var ct = TestContext.Current.CancellationToken;
        var applicant = await IssueApplicantWithProfileAsync(["C#"], ct);
        var goal = await CreateGoalAsync(applicant, "Role", null, ct);

        using var client = CreateClient();
        using var createA = BuildRequest(HttpMethod.Post, $"/api/v1/career-goals/{goal.Id}/roadmap-items", applicant,
            new CreateRoadmapItemRequest("Task A", null, null, null));
        var itemA = (await (await client.SendAsync(createA, ct)).Content.ReadFromJsonAsync<RoadmapItemResponse>(ct))!;

        using var createB = BuildRequest(HttpMethod.Post, $"/api/v1/career-goals/{goal.Id}/roadmap-items", applicant,
            new CreateRoadmapItemRequest("Task B", null, null, null));
        var itemB = (await (await client.SendAsync(createB, ct)).Content.ReadFromJsonAsync<RoadmapItemResponse>(ct))!;

        using var delete = BuildRequest(HttpMethod.Delete, $"/api/v1/career-goals/{goal.Id}/roadmap-items/{itemA.Id}", applicant);
        (await client.SendAsync(delete, ct)).StatusCode.Should().Be(HttpStatusCode.NoContent);

        using var list = BuildRequest(HttpMethod.Get, $"/api/v1/career-goals/{goal.Id}/roadmap-items", applicant);
        var items = (await (await client.SendAsync(list, ct)).Content.ReadFromJsonAsync<List<RoadmapItemResponse>>(ct))!;
        items.Should().ContainSingle(i => i.Id == itemB.Id);
    }

    [Fact]
    public async Task Invalid_item_fields_are_rejected()
    {
        var ct = TestContext.Current.CancellationToken;
        var applicant = await IssueApplicantWithProfileAsync(["C#"], ct);
        var goal = await CreateGoalAsync(applicant, "Role", null, ct);

        using var client = CreateClient();
        using var blankTitle = BuildRequest(HttpMethod.Post, $"/api/v1/career-goals/{goal.Id}/roadmap-items", applicant,
            new CreateRoadmapItemRequest("   ", null, null, null));
        (await client.SendAsync(blankTitle, ct)).StatusCode.Should().Be(HttpStatusCode.BadRequest);

        using var badPriority = BuildRequest(HttpMethod.Post, $"/api/v1/career-goals/{goal.Id}/roadmap-items", applicant,
            new CreateRoadmapItemRequest("Task", null, null, "Urgent"));
        (await client.SendAsync(badPriority, ct)).StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    // --------------------------------------------------------------- skill gap ---

    [Fact]
    public async Task Skill_gap_reuses_JobMatchScorer_against_the_goals_target_job()
    {
        var ct = TestContext.Current.CancellationToken;
        var recruiter = await IssueRecruiterWithCompanyAsync(ct);
        var jobId = await CreateJobAsync(recruiter, ["C#", "SQL", "Azure"], publish: true, JobCategory.Career, ct);
        var applicant = await IssueApplicantWithProfileAsync(["c#", " SQL "], ct); // case/whitespace on purpose
        var goal = await CreateGoalAsync(applicant, "Role", jobId, ct);

        using var client = CreateClient();
        using var request = BuildRequest(HttpMethod.Get, $"/api/v1/career-goals/{goal.Id}/skill-gap", applicant);
        var response = await client.SendAsync(request, ct);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var gap = (await response.Content.ReadFromJsonAsync<CareerGoalSkillGapResponse>(ct))!;
        gap.MatchedSkills.Should().BeEquivalentTo("C#", "SQL");
        gap.SkillsNotYetDemonstrated.Should().BeEquivalentTo("Azure");
        gap.MatchQuality.Should().Be(nameof(JobMatchQuality.PartialMatch));
    }

    [Fact]
    public async Task Skill_gap_without_a_target_job_is_an_honest_not_found_not_an_invented_score()
    {
        var ct = TestContext.Current.CancellationToken;
        var applicant = await IssueApplicantWithProfileAsync(["C#"], ct);
        var goal = await CreateGoalAsync(applicant, "Role", null, ct);

        using var client = CreateClient();
        using var request = BuildRequest(HttpMethod.Get, $"/api/v1/career-goals/{goal.Id}/skill-gap", applicant);

        (await client.SendAsync(request, ct)).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
