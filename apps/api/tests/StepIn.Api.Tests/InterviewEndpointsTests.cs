using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using StepIn.Api.Endpoints;

namespace StepIn.Api.Tests;

/// <summary>
/// Interview Practice Studio: session lifecycle, ownership isolation, answer persistence,
/// deterministic rubric-based feedback, and the privacy boundary (no recruiter or cross-candidate
/// access). See <see cref="InterviewFeedbackEngine"/>/<see cref="InterviewQuestionBank"/> for the
/// pure logic under test here.
/// </summary>
public sealed class InterviewEndpointsTests(AuthApiFactory factory) : IClassFixture<AuthApiFactory>
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
            new UpdateRecruiterProfileRequest(null, null, null, new CompanyDto(null, $"Interview Co {Guid.NewGuid():N}", null, null, null, null, null)));
        (await client.SendAsync(profile, ct)).EnsureSuccessStatusCode();

        return token;
    }

    private async Task<InterviewSessionResponse> CreateSessionAsync(
        string token, string category = "Behavioral", int questionCount = 3, string? targetRole = null, CancellationToken ct = default)
    {
        using var client = CreateClient();
        using var request = BuildRequest(HttpMethod.Post, "/api/v1/interview-practice", token,
            new CreateInterviewSessionRequest(category, targetRole, questionCount));
        var response = await client.SendAsync(request, ct);
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        return (await response.Content.ReadFromJsonAsync<InterviewSessionResponse>(ct))!;
    }

    // --------------------------------------------------------------- auth boundary ---

    [Fact]
    public async Task Anonymous_cannot_list_or_create_sessions()
    {
        var ct = TestContext.Current.CancellationToken;
        using var client = CreateClient();

        (await client.GetAsync("/api/v1/interview-practice", ct)).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await client.PostAsJsonAsync("/api/v1/interview-practice", new CreateInterviewSessionRequest("General", null, 3), ct))
            .StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Recruiter_cannot_use_interview_practice_endpoints()
    {
        var ct = TestContext.Current.CancellationToken;
        var recruiter = await IssueRecruiterWithCompanyAsync(ct);
        using var client = CreateClient();
        using var request = BuildRequest(HttpMethod.Get, "/api/v1/interview-practice", recruiter);

        (await client.SendAsync(request, ct)).StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Creating_a_session_without_a_candidate_profile_is_a_validation_problem_not_a_crash()
    {
        var ct = TestContext.Current.CancellationToken;
        var applicant = await IssueApplicantWithoutProfileAsync(ct);
        using var client = CreateClient();
        using var request = BuildRequest(HttpMethod.Post, "/api/v1/interview-practice", applicant,
            new CreateInterviewSessionRequest("General", null, 3));

        (await client.SendAsync(request, ct)).StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Listing_sessions_without_a_profile_returns_an_empty_list_not_an_error()
    {
        var ct = TestContext.Current.CancellationToken;
        var applicant = await IssueApplicantWithoutProfileAsync(ct);
        using var client = CreateClient();
        using var request = BuildRequest(HttpMethod.Get, "/api/v1/interview-practice", applicant);

        var response = await client.SendAsync(request, ct);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await response.Content.ReadFromJsonAsync<List<InterviewSessionSummaryResponse>>(ct))!.Should().BeEmpty();
    }

    // ----------------------------------------------------------------- templates ---

    [Fact]
    public async Task Templates_endpoint_reflects_the_actual_question_bank()
    {
        var ct = TestContext.Current.CancellationToken;
        var applicant = await IssueApplicantWithProfileAsync(ct);
        using var client = CreateClient();
        using var request = BuildRequest(HttpMethod.Get, "/api/v1/interview-practice/templates", applicant);

        var response = await client.SendAsync(request, ct);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var templates = (await response.Content.ReadFromJsonAsync<InterviewTemplatesResponse>(ct))!;

        templates.Categories.Should().HaveCount(4);
        templates.Categories.Should().Contain(c => c.Category == "Behavioral" && c.AvailableQuestionCount > 0);
        templates.MinQuestionCount.Should().BeGreaterThan(0);
        templates.MaxQuestionCount.Should().BeGreaterThanOrEqualTo(templates.MinQuestionCount);
    }

    // --------------------------------------------------------------------- CRUD ---

    [Fact]
    public async Task A_session_is_created_with_ordered_snapshotted_questions_from_the_bank()
    {
        var ct = TestContext.Current.CancellationToken;
        var applicant = await IssueApplicantWithProfileAsync(ct);

        var session = await CreateSessionAsync(applicant, "Technical", 4, "Graduate Software Engineer", ct);

        session.Category.Should().Be("Technical");
        session.TargetRole.Should().Be("Graduate Software Engineer");
        session.Status.Should().Be("InProgress");
        session.QuestionCount.Should().Be(4);
        session.AnsweredCount.Should().Be(0);
        session.Questions.Should().HaveCount(4);
        session.Questions.Select(q => q.OrderIndex).Should().BeInAscendingOrder();
        session.Questions.Select(q => q.OrderIndex).Should().BeEquivalentTo([0, 1, 2, 3]);
        session.Questions.Should().OnlyContain(q => q.Category == "Technical" && !string.IsNullOrWhiteSpace(q.QuestionText));
    }

    [Theory]
    [InlineData("NotARealCategory")]
    [InlineData("")]
    public async Task An_unknown_or_blank_category_is_rejected(string category)
    {
        var ct = TestContext.Current.CancellationToken;
        var applicant = await IssueApplicantWithProfileAsync(ct);
        using var client = CreateClient();
        using var request = BuildRequest(HttpMethod.Post, "/api/v1/interview-practice", applicant,
            new CreateInterviewSessionRequest(category, null, 3));

        (await client.SendAsync(request, ct)).StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(100)]
    public async Task An_out_of_range_question_count_is_rejected(int count)
    {
        var ct = TestContext.Current.CancellationToken;
        var applicant = await IssueApplicantWithProfileAsync(ct);
        using var client = CreateClient();
        using var request = BuildRequest(HttpMethod.Post, "/api/v1/interview-practice", applicant,
            new CreateInterviewSessionRequest("General", null, count));

        (await client.SendAsync(request, ct)).StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task A_candidate_cannot_read_update_complete_or_delete_another_candidates_session()
    {
        var ct = TestContext.Current.CancellationToken;
        var owner = await IssueApplicantWithProfileAsync(ct);
        var session = await CreateSessionAsync(owner, ct: ct);
        var questionId = session.Questions[0].Id;

        var intruder = await IssueApplicantWithProfileAsync(ct);
        using var client = CreateClient();

        using var get = BuildRequest(HttpMethod.Get, $"/api/v1/interview-practice/{session.Id}", intruder);
        (await client.SendAsync(get, ct)).StatusCode.Should().Be(HttpStatusCode.NotFound);

        using var answer = BuildRequest(HttpMethod.Put, $"/api/v1/interview-practice/{session.Id}/questions/{questionId}/answer", intruder,
            new SaveInterviewAnswerRequest("Hijacked answer"));
        (await client.SendAsync(answer, ct)).StatusCode.Should().Be(HttpStatusCode.NotFound);

        using var complete = BuildRequest(HttpMethod.Post, $"/api/v1/interview-practice/{session.Id}/complete", intruder);
        (await client.SendAsync(complete, ct)).StatusCode.Should().Be(HttpStatusCode.NotFound);

        using var delete = BuildRequest(HttpMethod.Delete, $"/api/v1/interview-practice/{session.Id}", intruder);
        (await client.SendAsync(delete, ct)).StatusCode.Should().Be(HttpStatusCode.NotFound);

        // Prove nothing actually changed.
        using var verifyGet = BuildRequest(HttpMethod.Get, $"/api/v1/interview-practice/{session.Id}", owner);
        var verified = (await (await client.SendAsync(verifyGet, ct)).Content.ReadFromJsonAsync<InterviewSessionResponse>(ct))!;
        verified.Questions[0].AnswerText.Should().BeNull();
        verified.Status.Should().Be("InProgress");
    }

    [Fact]
    public async Task A_candidate_cannot_answer_a_question_that_belongs_to_a_different_session()
    {
        var ct = TestContext.Current.CancellationToken;
        var applicant = await IssueApplicantWithProfileAsync(ct);
        var sessionA = await CreateSessionAsync(applicant, ct: ct);
        var sessionB = await CreateSessionAsync(applicant, ct: ct);
        var questionFromB = sessionB.Questions[0].Id;

        using var client = CreateClient();
        using var request = BuildRequest(HttpMethod.Put, $"/api/v1/interview-practice/{sessionA.Id}/questions/{questionFromB}/answer", applicant,
            new SaveInterviewAnswerRequest("Answering the wrong session's question"));

        (await client.SendAsync(request, ct)).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Answers_persist_and_resuming_shows_them_unchanged()
    {
        var ct = TestContext.Current.CancellationToken;
        var applicant = await IssueApplicantWithProfileAsync(ct);
        var session = await CreateSessionAsync(applicant, questionCount: 3, ct: ct);
        var firstQuestionId = session.Questions[0].Id;

        using var client = CreateClient();
        using var save = BuildRequest(HttpMethod.Put, $"/api/v1/interview-practice/{session.Id}/questions/{firstQuestionId}/answer", applicant,
            new SaveInterviewAnswerRequest("When I was leading a small project, I decided to split the work and as a result we finished early."));
        var saveResponse = await client.SendAsync(save, ct);
        saveResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var saved = (await saveResponse.Content.ReadFromJsonAsync<InterviewQuestionResponse>(ct))!;
        saved.AnswerText.Should().NotBeNullOrWhiteSpace();
        saved.AnsweredAt.Should().NotBeNull();

        using var reload = BuildRequest(HttpMethod.Get, $"/api/v1/interview-practice/{session.Id}", applicant);
        var reloaded = (await (await client.SendAsync(reload, ct)).Content.ReadFromJsonAsync<InterviewSessionResponse>(ct))!;
        reloaded.AnsweredCount.Should().Be(1);
        reloaded.Questions.First(q => q.Id == firstQuestionId).AnswerText.Should().Be(saved.AnswerText);
    }

    [Fact]
    public async Task Overlong_answers_are_rejected()
    {
        var ct = TestContext.Current.CancellationToken;
        var applicant = await IssueApplicantWithProfileAsync(ct);
        var session = await CreateSessionAsync(applicant, ct: ct);
        var questionId = session.Questions[0].Id;

        using var client = CreateClient();
        using var request = BuildRequest(HttpMethod.Put, $"/api/v1/interview-practice/{session.Id}/questions/{questionId}/answer", applicant,
            new SaveInterviewAnswerRequest(new string('a', 4001)));

        (await client.SendAsync(request, ct)).StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Completing_a_session_prevents_further_answer_edits()
    {
        var ct = TestContext.Current.CancellationToken;
        var applicant = await IssueApplicantWithProfileAsync(ct);
        var session = await CreateSessionAsync(applicant, ct: ct);
        var questionId = session.Questions[0].Id;

        using var client = CreateClient();
        using var complete = BuildRequest(HttpMethod.Post, $"/api/v1/interview-practice/{session.Id}/complete", applicant);
        var completeResponse = await client.SendAsync(complete, ct);
        completeResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var completed = (await completeResponse.Content.ReadFromJsonAsync<InterviewSessionResponse>(ct))!;
        completed.Status.Should().Be("Completed");
        completed.CompletedAt.Should().NotBeNull();

        using var answerAfterComplete = BuildRequest(HttpMethod.Put, $"/api/v1/interview-practice/{session.Id}/questions/{questionId}/answer", applicant,
            new SaveInterviewAnswerRequest("Too late"));
        (await client.SendAsync(answerAfterComplete, ct)).StatusCode.Should().Be(HttpStatusCode.BadRequest);

        using var completeAgain = BuildRequest(HttpMethod.Post, $"/api/v1/interview-practice/{session.Id}/complete", applicant);
        (await client.SendAsync(completeAgain, ct)).StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Deleting_a_session_removes_it_and_its_questions()
    {
        var ct = TestContext.Current.CancellationToken;
        var applicant = await IssueApplicantWithProfileAsync(ct);
        var session = await CreateSessionAsync(applicant, ct: ct);

        using var client = CreateClient();
        using var delete = BuildRequest(HttpMethod.Delete, $"/api/v1/interview-practice/{session.Id}", applicant);
        (await client.SendAsync(delete, ct)).StatusCode.Should().Be(HttpStatusCode.NoContent);

        using var get = BuildRequest(HttpMethod.Get, $"/api/v1/interview-practice/{session.Id}", applicant);
        (await client.SendAsync(get, ct)).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task History_reports_accurate_answered_and_question_counts()
    {
        var ct = TestContext.Current.CancellationToken;
        var applicant = await IssueApplicantWithProfileAsync(ct);
        var session = await CreateSessionAsync(applicant, questionCount: 3, ct: ct);

        using var client = CreateClient();
        using var save = BuildRequest(HttpMethod.Put, $"/api/v1/interview-practice/{session.Id}/questions/{session.Questions[0].Id}/answer", applicant,
            new SaveInterviewAnswerRequest("A real answer with enough detail to count as substantive for testing."));
        (await client.SendAsync(save, ct)).EnsureSuccessStatusCode();

        using var list = BuildRequest(HttpMethod.Get, "/api/v1/interview-practice", applicant);
        var sessions = (await (await client.SendAsync(list, ct)).Content.ReadFromJsonAsync<List<InterviewSessionSummaryResponse>>(ct))!;
        var summary = sessions.Single(s => s.Id == session.Id);
        summary.QuestionCount.Should().Be(3);
        summary.AnsweredCount.Should().Be(1);
    }

    // ------------------------------------------------------------------ feedback ---

    [Fact]
    public async Task A_blank_answer_is_reported_as_unanswered_not_scored()
    {
        var ct = TestContext.Current.CancellationToken;
        var applicant = await IssueApplicantWithProfileAsync(ct);
        var session = await CreateSessionAsync(applicant, ct: ct);

        var feedback = session.Questions[0].Feedback;
        feedback.Answered.Should().BeFalse();
        feedback.Band.Should().Be("Not yet answered");
        feedback.Criteria.Should().BeEmpty();
        feedback.Disclaimer.Should().Contain("not an AI-generated");
    }

    [Fact]
    public async Task A_well_structured_behavioral_answer_meets_every_STAR_criterion()
    {
        var ct = TestContext.Current.CancellationToken;
        var applicant = await IssueApplicantWithProfileAsync(ct);
        var session = await CreateSessionAsync(applicant, "Behavioral", 3, ct: ct);
        var questionId = session.Questions[0].Id;

        const string answer =
            "During a group project at university, I decided to reorganize our task list after noticing we were behind. " +
            "As a result, we delivered on time and I learned how useful early check-ins are.";

        using var client = CreateClient();
        using var save = BuildRequest(HttpMethod.Put, $"/api/v1/interview-practice/{session.Id}/questions/{questionId}/answer", applicant,
            new SaveInterviewAnswerRequest(answer));
        var response = await client.SendAsync(save, ct);
        var result = (await response.Content.ReadFromJsonAsync<InterviewQuestionResponse>(ct))!;

        result.Feedback.Answered.Should().BeTrue();
        result.Feedback.Band.Should().Be("Well-structured");
        result.Feedback.Criteria.Should().OnlyContain(c => c.Met);
    }

    [Fact]
    public async Task A_vague_behavioral_answer_misses_criteria_and_lists_improvements()
    {
        var ct = TestContext.Current.CancellationToken;
        var applicant = await IssueApplicantWithProfileAsync(ct);
        var session = await CreateSessionAsync(applicant, "Behavioral", 3, ct: ct);
        var questionId = session.Questions[0].Id;

        using var client = CreateClient();
        using var save = BuildRequest(HttpMethod.Put, $"/api/v1/interview-practice/{session.Id}/questions/{questionId}/answer", applicant,
            new SaveInterviewAnswerRequest("Fine."));
        var response = await client.SendAsync(save, ct);
        var result = (await response.Content.ReadFromJsonAsync<InterviewQuestionResponse>(ct))!;

        result.Feedback.Answered.Should().BeTrue();
        result.Feedback.Band.Should().Be("Needs more detail");
        result.Feedback.CriteriaMetCount.Should().Be(0);
        result.Feedback.Improvements.Should().NotBeEmpty();
    }

    [Fact]
    public async Task Technical_feedback_uses_a_technical_rubric_not_STAR()
    {
        var ct = TestContext.Current.CancellationToken;
        var applicant = await IssueApplicantWithProfileAsync(ct);
        var session = await CreateSessionAsync(applicant, "Technical", 3, ct: ct);
        var questionId = session.Questions[0].Id;

        const string answer =
            "My approach is to reproduce the issue first, then add logging to narrow it down. " +
            "I chose this because it avoids guessing, and I'd also check edge cases like empty input or very large datasets for performance.";

        using var client = CreateClient();
        using var save = BuildRequest(HttpMethod.Put, $"/api/v1/interview-practice/{session.Id}/questions/{questionId}/answer", applicant,
            new SaveInterviewAnswerRequest(answer));
        var response = await client.SendAsync(save, ct);
        var result = (await response.Content.ReadFromJsonAsync<InterviewQuestionResponse>(ct))!;

        result.Feedback.Criteria.Should().Contain(c => c.Name == "Explains the approach" && c.Met);
        result.Feedback.Criteria.Should().Contain(c => c.Name == "Explains the reasoning" && c.Met);
        result.Feedback.Criteria.Should().Contain(c => c.Name == "Considers edge cases or trade-offs" && c.Met);
        result.Feedback.Criteria.Should().NotContain(c => c.Name.Contains("Situation") || c.Name.Contains("scene"));
    }

    [Fact]
    public async Task Feedback_is_deterministic_for_the_same_answer_text()
    {
        var ct = TestContext.Current.CancellationToken;
        var applicant = await IssueApplicantWithProfileAsync(ct);
        var session = await CreateSessionAsync(applicant, "General", 3, ct: ct);
        var questionId = session.Questions[0].Id;
        const string answer = "I'm interested in this field because I enjoy solving real problems that affect people's daily lives.";

        using var client = CreateClient();
        using var save = BuildRequest(HttpMethod.Put, $"/api/v1/interview-practice/{session.Id}/questions/{questionId}/answer", applicant,
            new SaveInterviewAnswerRequest(answer));
        var first = (await (await client.SendAsync(save, ct)).Content.ReadFromJsonAsync<InterviewQuestionResponse>(ct))!;

        using var reload = BuildRequest(HttpMethod.Get, $"/api/v1/interview-practice/{session.Id}", applicant);
        var reloaded = (await (await client.SendAsync(reload, ct)).Content.ReadFromJsonAsync<InterviewSessionResponse>(ct))!;
        var second = reloaded.Questions.Single(q => q.Id == questionId);

        second.Feedback.Should().BeEquivalentTo(first.Feedback);
    }
}
