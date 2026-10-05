using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using StepIn.Api.Endpoints;
using StepIn.Domain.Aidx;
using StepIn.Domain.Common;
using StepIn.Domain.Companies;
using StepIn.Domain.Jobs;
using StepIn.Domain.Profiles;
using StepIn.Domain.Users;
using StepIn.Infrastructure.Aidx;
using StepIn.Infrastructure.Persistence;

namespace StepIn.Api.Tests;

/// <summary>
/// Phase 4.4C admin API for AIDX Research Opportunities, against a real PostgreSQL container.
/// The AIDX system owner is initialised explicitly in the test setup, as the production design requires.
/// </summary>
public sealed class AidxOpportunityAdminTests(AuthApiFactory factory) : IClassFixture<AuthApiFactory>
{
    private const string Route = "/api/v1/admin/aidx/opportunities";

    private readonly AuthApiFactory _factory = factory;

    private static string Unique(string prefix) => $"{prefix} {Guid.NewGuid():N}";

    private static HttpRequestMessage Build(HttpMethod method, string url, string? token, object? body = null)
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

    private async Task<HttpResponseMessage> SendAsync(HttpMethod method, string url, string? token, object? body = null)
    {
        using var client = _factory.CreateClient();
        using var request = Build(method, url, token, body);
        return await client.SendAsync(request, TestContext.Current.CancellationToken);
    }

    private async Task<string> IssueAsync(string role)
    {
        var token = AuthApiFactory.IssueToken($"user_{Guid.NewGuid():N}", $"{Guid.NewGuid()}@example.com");
        using var setup = Build(HttpMethod.Post, "/api/v1/auth/account-setup", token, new AccountSetupRequest(role));
        using var client = _factory.CreateClient();
        (await client.SendAsync(setup, TestContext.Current.CancellationToken)).EnsureSuccessStatusCode();
        return token;
    }

    /// <summary>Admin is never client-settable, so the test promotes the account directly, as the documented process does.</summary>
    private async Task<string> AdminTokenAsync()
    {
        var clerkUserId = $"admin_{Guid.NewGuid():N}";
        var token = AuthApiFactory.IssueToken(clerkUserId, $"{Guid.NewGuid()}@example.com");
        using (var client = _factory.CreateClient())
        {
            using var setup = Build(HttpMethod.Post, "/api/v1/auth/account-setup", token, new AccountSetupRequest("Applicant"));
            (await client.SendAsync(setup, TestContext.Current.CancellationToken)).EnsureSuccessStatusCode();
        }

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        await db.Users.Where(u => u.ClerkUserId == clerkUserId)
            .ExecuteUpdateAsync(s => s.SetProperty(u => u.Role, UserRole.Admin), TestContext.Current.CancellationToken);
        return token;
    }

    /// <summary>Initialises the system owner explicitly, as an operator would. Idempotent, so repeat calls are harmless.</summary>
    private async Task<AidxSystemOwnership> EnsureOwnerAsync()
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var service = new AidxSystemOwnershipService(scope.ServiceProvider.GetRequiredService<ApplicationDbContext>());
        return await service.EnsureAsync(TestContext.Current.CancellationToken);
    }

    private async Task<Guid> InsertJobAsync(Guid recruiterProfileId, Guid companyId, JobCategory category, JobStatus status, Guid? projectId = null)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var job = new Job
        {
            RecruiterProfileId = recruiterProfileId,
            CompanyId = companyId,
            Title = Unique("Seeded role"),
            Description = "Seeded for a test.",
            Location = "Sydney, NSW",
            Category = category,
            Status = status,
            PublishedAt = status == JobStatus.Published ? DateTimeOffset.UtcNow : null,
            AidxProjectId = projectId,
        };
        db.Add(job);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        return job.Id;
    }

    /// <summary>A normal recruiter with their own company, used as the "wrong owner" in isolation tests.</summary>
    private async Task<(Guid ProfileId, Guid CompanyId)> NormalRecruiterAsync()
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var ct = TestContext.Current.CancellationToken;

        var user = new ApplicationUser
        {
            ClerkUserId = $"user_{Guid.NewGuid():N}",
            Email = $"{Guid.NewGuid()}@example.com",
            FirstName = "Other",
            LastName = "Recruiter",
            Role = UserRole.Recruiter,
        };
        var company = new Company { Name = Unique("Ordinary Employer") };
        db.Add(user);
        db.Add(company);
        await db.SaveChangesAsync(ct);

        var profile = new RecruiterProfile { UserId = user.Id, CompanyId = company.Id };
        db.Add(profile);
        await db.SaveChangesAsync(ct);
        return (profile.Id, company.Id);
    }

    private async Task<Guid> InsertProjectAsync(AidxContentStatus status)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var project = new AidxProject
        {
            Title = Unique("Opportunity project"),
            Slug = Unique("opportunity-project").Replace(' ', '-').ToLowerInvariant(),
            ShortDescription = "Short.",
            Description = "Long.",
            Status = status,
            PublishedAt = status == AidxContentStatus.Published ? DateTimeOffset.UtcNow : null,
        };
        db.Add(project);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        return project.Id;
    }

    private async Task SetProjectStatusAsync(Guid projectId, AidxContentStatus status)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        await db.AidxProjects.Where(p => p.Id == projectId)
            .ExecuteUpdateAsync(s => s.SetProperty(p => p.Status, status), TestContext.Current.CancellationToken);
    }

    private async Task<JobStatus> StatusOfAsync(Guid jobId)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        return await db.Jobs.Where(j => j.Id == jobId).Select(j => j.Status).SingleAsync(TestContext.Current.CancellationToken);
    }

    private static object ValidBody(string title = "Research engineer", Guid? projectId = null) => new
    {
        title,
        description = "Work on research.",
        employmentType = "FullTime",
        workplaceType = "Hybrid",
        location = "Sydney, NSW",
        compensation = (string?)null,
        skills = new[] { "Python" },
        aidxProjectId = projectId,
    };

    private async Task<AidxOpportunityAdminResponse> CreateOpportunityAsync(string adminToken, object? body = null)
    {
        var response = await SendAsync(HttpMethod.Post, Route, adminToken, body ?? ValidBody());
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        return (await response.Content.ReadFromJsonAsync<AidxOpportunityAdminResponse>(TestContext.Current.CancellationToken))!;
    }

    // -------------------------------------------------------------- authorization ---

    [Fact]
    public async Task Every_route_requires_admin_anonymous_401_applicant_403_recruiter_403_admin_allowed()
    {
        await EnsureOwnerAsync();
        var applicant = await IssueAsync("Applicant");
        var recruiter = await IssueAsync("Recruiter");
        var admin = await AdminTokenAsync();

        (await SendAsync(HttpMethod.Get, Route, null)).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await SendAsync(HttpMethod.Get, Route, applicant)).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await SendAsync(HttpMethod.Get, Route, recruiter)).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await SendAsync(HttpMethod.Get, Route, admin)).StatusCode.Should().Be(HttpStatusCode.OK);

        (await SendAsync(HttpMethod.Post, Route, null, ValidBody())).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await SendAsync(HttpMethod.Post, Route, applicant, ValidBody())).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await SendAsync(HttpMethod.Post, Route, recruiter, ValidBody())).StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    // -------------------------------------------------------------------- create ---

    [Fact]
    public async Task Create_makes_a_draft_research_job_owned_by_the_aidx_system_recruiter()
    {
        var owner = await EnsureOwnerAsync();
        var admin = await AdminTokenAsync();

        var created = await CreateOpportunityAsync(admin);

        created.Status.Should().Be("Draft");
        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var job = await db.Jobs.SingleAsync(j => j.Id == created.Id, TestContext.Current.CancellationToken);
        job.Category.Should().Be(JobCategory.Research);
        job.RecruiterProfileId.Should().Be(owner.RecruiterProfileId);
        job.CompanyId.Should().Be(owner.CompanyId);
    }

    [Fact]
    public async Task Client_supplied_ownership_category_and_status_are_ignored_on_create()
    {
        var owner = await EnsureOwnerAsync();
        var admin = await AdminTokenAsync();
        var (otherProfile, otherCompany) = await NormalRecruiterAsync();

        var created = await CreateOpportunityAsync(admin, new
        {
            title = "Spoofed owner",
            description = "Attempts to choose its own owner.",
            employmentType = "FullTime",
            workplaceType = "Remote",
            location = "Anywhere",
            recruiterProfileId = otherProfile,
            companyId = otherCompany,
            userId = Guid.NewGuid(),
            clerkUserId = "user_spoof",
            category = "Career",
            status = "Published",
        });

        created.Status.Should().Be("Draft", "a client cannot publish on create");
        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var job = await db.Jobs.SingleAsync(j => j.Id == created.Id, TestContext.Current.CancellationToken);
        job.RecruiterProfileId.Should().Be(owner.RecruiterProfileId);
        job.CompanyId.Should().Be(owner.CompanyId);
        job.Category.Should().Be(JobCategory.Research);
    }

    [Fact]
    public async Task Create_rejects_an_unknown_project_and_invalid_content_with_a_validation_problem()
    {
        await EnsureOwnerAsync();
        var admin = await AdminTokenAsync();

        (await SendAsync(HttpMethod.Post, Route, admin, ValidBody(projectId: Guid.NewGuid())))
            .StatusCode.Should().Be(HttpStatusCode.BadRequest);

        (await SendAsync(HttpMethod.Post, Route, admin, new { title = "", description = "", location = "", employmentType = "Nope", workplaceType = "Remote" }))
            .StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    // ----------------------------------------------------------- ownership scope ---

    [Fact]
    public async Task Career_jobs_and_other_owners_research_jobs_return_404_on_every_operation()
    {
        var owner = await EnsureOwnerAsync();
        var admin = await AdminTokenAsync();
        var (normalProfile, normalCompany) = await NormalRecruiterAsync();

        var career = await InsertJobAsync(normalProfile, normalCompany, JobCategory.Career, JobStatus.Draft);
        var careerUnderSystem = await InsertJobAsync(owner.RecruiterProfileId, owner.CompanyId, JobCategory.Career, JobStatus.Draft);
        var otherResearch = await InsertJobAsync(normalProfile, normalCompany, JobCategory.Research, JobStatus.Draft);
        var otherCompanyResearch = await InsertJobAsync(owner.RecruiterProfileId, (await NormalRecruiterAsync()).CompanyId, JobCategory.Research, JobStatus.Draft);

        foreach (var id in new[] { career, careerUnderSystem, otherResearch, otherCompanyResearch })
        {
            (await SendAsync(HttpMethod.Get, $"{Route}/{id}", admin)).StatusCode.Should().Be(HttpStatusCode.NotFound);
            (await SendAsync(HttpMethod.Put, $"{Route}/{id}", admin, ValidBody())).StatusCode.Should().Be(HttpStatusCode.NotFound);
            (await SendAsync(HttpMethod.Delete, $"{Route}/{id}", admin)).StatusCode.Should().Be(HttpStatusCode.NotFound);
            (await SendAsync(HttpMethod.Post, $"{Route}/{id}/publish", admin)).StatusCode.Should().Be(HttpStatusCode.NotFound);
            (await SendAsync(HttpMethod.Post, $"{Route}/{id}/unpublish", admin)).StatusCode.Should().Be(HttpStatusCode.NotFound);
        }

        (await SendAsync(HttpMethod.Get, $"{Route}/{Guid.NewGuid()}", admin)).StatusCode.Should().Be(HttpStatusCode.NotFound);

        // Failed attempts must not have changed anything.
        (await StatusOfAsync(career)).Should().Be(JobStatus.Draft);
        (await StatusOfAsync(otherResearch)).Should().Be(JobStatus.Draft);
    }

    [Fact]
    public async Task List_returns_only_aidx_owned_research_opportunities()
    {
        var owner = await EnsureOwnerAsync();
        var admin = await AdminTokenAsync();
        var (normalProfile, normalCompany) = await NormalRecruiterAsync();
        var mine = await CreateOpportunityAsync(admin, ValidBody(Unique("Listed mine")));
        var career = await InsertJobAsync(normalProfile, normalCompany, JobCategory.Career, JobStatus.Draft);
        var notMine = await InsertJobAsync(normalProfile, normalCompany, JobCategory.Research, JobStatus.Draft);

        var page = (await (await SendAsync(HttpMethod.Get, $"{Route}?pageSize=50", admin))
            .Content.ReadFromJsonAsync<AidxPageResponse<AidxOpportunityAdminResponse>>(TestContext.Current.CancellationToken))!;

        page.Items.Select(i => i.Id).Should().Contain(mine.Id);
        page.Items.Select(i => i.Id).Should().NotContain(career);
        page.Items.Select(i => i.Id).Should().NotContain(notMine);
        owner.RecruiterProfileId.Should().NotBe(normalProfile);
    }

    [Fact]
    public async Task List_filters_by_status_and_rejects_unknown_status()
    {
        await EnsureOwnerAsync();
        var admin = await AdminTokenAsync();
        var draft = await CreateOpportunityAsync(admin, ValidBody(Unique("Filter draft")));

        var drafts = (await (await SendAsync(HttpMethod.Get, $"{Route}?status=Draft&pageSize=50", admin))
            .Content.ReadFromJsonAsync<AidxPageResponse<AidxOpportunityAdminResponse>>(TestContext.Current.CancellationToken))!;
        drafts.Items.Should().OnlyContain(i => i.Status == "Draft");
        drafts.Items.Select(i => i.Id).Should().Contain(draft.Id);

        (await SendAsync(HttpMethod.Get, $"{Route}?status=Sideways", admin)).StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    // ------------------------------------------------------------------- update ---

    [Fact]
    public async Task Update_changes_content_and_never_ownership()
    {
        var owner = await EnsureOwnerAsync();
        var admin = await AdminTokenAsync();
        var created = await CreateOpportunityAsync(admin);

        var response = await SendAsync(HttpMethod.Put, $"{Route}/{created.Id}", admin, ValidBody("Renamed role"));
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await response.Content.ReadFromJsonAsync<AidxOpportunityAdminResponse>(TestContext.Current.CancellationToken))!
            .Title.Should().Be("Renamed role");

        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var job = await db.Jobs.SingleAsync(j => j.Id == created.Id, TestContext.Current.CancellationToken);
        job.RecruiterProfileId.Should().Be(owner.RecruiterProfileId);
        job.CompanyId.Should().Be(owner.CompanyId);
        job.Category.Should().Be(JobCategory.Research);
    }

    [Fact]
    public async Task A_published_opportunity_cannot_be_relinked_to_a_draft_project()
    {
        await EnsureOwnerAsync();
        var admin = await AdminTokenAsync();
        var published = await InsertProjectAsync(AidxContentStatus.Published);
        var draft = await InsertProjectAsync(AidxContentStatus.Draft);
        var opportunity = await CreateOpportunityAsync(admin, ValidBody(Unique("Relink test"), published));
        (await SendAsync(HttpMethod.Post, $"{Route}/{opportunity.Id}/publish", admin)).StatusCode.Should().Be(HttpStatusCode.OK);

        (await SendAsync(HttpMethod.Put, $"{Route}/{opportunity.Id}", admin, ValidBody("Still published", draft)))
            .StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await StatusOfAsync(opportunity.Id)).Should().Be(JobStatus.Published);
    }

    // ------------------------------------------------------------------- delete ---

    [Fact]
    public async Task Only_drafts_can_be_deleted()
    {
        await EnsureOwnerAsync();
        var admin = await AdminTokenAsync();
        var draft = await CreateOpportunityAsync(admin, ValidBody(Unique("Deletable")));
        var published = await CreateOpportunityAsync(admin, ValidBody(Unique("Not deletable")));
        (await SendAsync(HttpMethod.Post, $"{Route}/{published.Id}/publish", admin)).StatusCode.Should().Be(HttpStatusCode.OK);

        (await SendAsync(HttpMethod.Delete, $"{Route}/{draft.Id}", admin)).StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await SendAsync(HttpMethod.Get, $"{Route}/{draft.Id}", admin)).StatusCode.Should().Be(HttpStatusCode.NotFound);

        (await SendAsync(HttpMethod.Delete, $"{Route}/{published.Id}", admin)).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await StatusOfAsync(published.Id)).Should().Be(JobStatus.Published);
    }

    [Fact]
    public async Task Unpublished_opportunities_cannot_be_deleted()
    {
        await EnsureOwnerAsync();
        var admin = await AdminTokenAsync();
        var opportunity = await CreateOpportunityAsync(admin, ValidBody(Unique("Unpublished delete")));
        (await SendAsync(HttpMethod.Post, $"{Route}/{opportunity.Id}/publish", admin)).StatusCode.Should().Be(HttpStatusCode.OK);
        (await SendAsync(HttpMethod.Post, $"{Route}/{opportunity.Id}/unpublish", admin)).StatusCode.Should().Be(HttpStatusCode.OK);

        (await SendAsync(HttpMethod.Delete, $"{Route}/{opportunity.Id}", admin)).StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    // ---------------------------------------------------------------- publishing ---

    [Fact]
    public async Task Publish_without_a_project_succeeds_and_sets_publishedAt_once()
    {
        await EnsureOwnerAsync();
        var admin = await AdminTokenAsync();
        var opportunity = await CreateOpportunityAsync(admin, ValidBody(Unique("No project")));

        var response = await SendAsync(HttpMethod.Post, $"{Route}/{opportunity.Id}/publish", admin);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var published = (await response.Content.ReadFromJsonAsync<AidxOpportunityAdminResponse>(TestContext.Current.CancellationToken))!;
        published.Status.Should().Be("Published");
        published.PublishedAt.Should().NotBeNull();

        (await SendAsync(HttpMethod.Post, $"{Route}/{opportunity.Id}/publish", admin))
            .StatusCode.Should().Be(HttpStatusCode.BadRequest, "a duplicate publish is rejected");
    }

    [Fact]
    public async Task Publish_with_a_published_project_succeeds()
    {
        await EnsureOwnerAsync();
        var admin = await AdminTokenAsync();
        var project = await InsertProjectAsync(AidxContentStatus.Published);
        var opportunity = await CreateOpportunityAsync(admin, ValidBody(Unique("Published project"), project));

        (await SendAsync(HttpMethod.Post, $"{Route}/{opportunity.Id}/publish", admin)).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Theory]
    [InlineData(AidxContentStatus.Draft)]
    [InlineData(AidxContentStatus.Archived)]
    public async Task Publish_is_rejected_for_a_linked_project_that_is_not_published(AidxContentStatus projectStatus)
    {
        await EnsureOwnerAsync();
        var admin = await AdminTokenAsync();
        var project = await InsertProjectAsync(projectStatus);
        var opportunity = await CreateOpportunityAsync(admin, ValidBody(Unique("Blocked project"), project));

        (await SendAsync(HttpMethod.Post, $"{Route}/{opportunity.Id}/publish", admin)).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await StatusOfAsync(opportunity.Id)).Should().Be(JobStatus.Draft, "publishing must not succeed partially");
    }

    [Fact]
    public async Task Publishing_never_changes_the_linked_project_status()
    {
        await EnsureOwnerAsync();
        var admin = await AdminTokenAsync();
        var project = await InsertProjectAsync(AidxContentStatus.Draft);
        var opportunity = await CreateOpportunityAsync(admin, ValidBody(Unique("Leave project alone"), project));
        (await SendAsync(HttpMethod.Post, $"{Route}/{opportunity.Id}/publish", admin)).StatusCode.Should().Be(HttpStatusCode.BadRequest);

        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        (await db.AidxProjects.SingleAsync(p => p.Id == project, TestContext.Current.CancellationToken)).Status
            .Should().Be(AidxContentStatus.Draft);
    }

    // -------------------------------------------------------------- unpublishing ---

    [Fact]
    public async Task Unpublish_only_works_from_published_and_keeps_the_project_link()
    {
        await EnsureOwnerAsync();
        var admin = await AdminTokenAsync();
        var project = await InsertProjectAsync(AidxContentStatus.Published);
        var opportunity = await CreateOpportunityAsync(admin, ValidBody(Unique("Unpublish me"), project));

        (await SendAsync(HttpMethod.Post, $"{Route}/{opportunity.Id}/unpublish", admin))
            .StatusCode.Should().Be(HttpStatusCode.BadRequest, "a draft cannot be unpublished");

        (await SendAsync(HttpMethod.Post, $"{Route}/{opportunity.Id}/publish", admin)).StatusCode.Should().Be(HttpStatusCode.OK);
        var unpublished = await SendAsync(HttpMethod.Post, $"{Route}/{opportunity.Id}/unpublish", admin);
        unpublished.StatusCode.Should().Be(HttpStatusCode.OK);
        (await unpublished.Content.ReadFromJsonAsync<AidxOpportunityAdminResponse>(TestContext.Current.CancellationToken))!
            .AidxProjectId.Should().Be(project);

        (await SendAsync(HttpMethod.Post, $"{Route}/{opportunity.Id}/unpublish", admin))
            .StatusCode.Should().Be(HttpStatusCode.BadRequest, "an unpublished opportunity cannot be unpublished again");
    }

    // ----------------------------------------------------------- public visibility ---

    [Fact]
    public async Task Public_listing_shows_published_opportunities_and_hides_drafts_and_hidden_projects()
    {
        await EnsureOwnerAsync();
        var admin = await AdminTokenAsync();
        var published = await CreateOpportunityAsync(admin, ValidBody(Unique("Public published")));
        (await SendAsync(HttpMethod.Post, $"{Route}/{published.Id}/publish", admin)).StatusCode.Should().Be(HttpStatusCode.OK);
        var draft = await CreateOpportunityAsync(admin, ValidBody(Unique("Public draft")));

        var projectId = await InsertProjectAsync(AidxContentStatus.Published);
        var hidden = await CreateOpportunityAsync(admin, ValidBody(Unique("Public hidden project"), projectId));
        (await SendAsync(HttpMethod.Post, $"{Route}/{hidden.Id}/publish", admin)).StatusCode.Should().Be(HttpStatusCode.OK);
        await SetProjectStatusAsync(projectId, AidxContentStatus.Archived);

        var listing = (await (await SendAsync(HttpMethod.Get, "/api/v1/aidx/opportunities?pageSize=50", null))
            .Content.ReadFromJsonAsync<AidxPageResponse<AidxOpportunityResponse>>(TestContext.Current.CancellationToken))!;
        var ids = listing.Items.Select(o => o.Id).ToList();

        ids.Should().Contain(published.Id);
        ids.Should().NotContain(draft.Id);
        ids.Should().NotContain(hidden.Id, "a published opportunity is hidden while its project is not public");
    }

    // ---------------------------------------------------------- application flow ---

    [Fact]
    public async Task A_published_aidx_opportunity_accepts_an_application_through_the_existing_endpoint()
    {
        await EnsureOwnerAsync();
        var admin = await AdminTokenAsync();
        var opportunity = await CreateOpportunityAsync(admin, ValidBody(Unique("Applicable")));
        (await SendAsync(HttpMethod.Post, $"{Route}/{opportunity.Id}/publish", admin)).StatusCode.Should().Be(HttpStatusCode.OK);

        var applicant = await IssueAsync("Applicant");
        using (var profile = Build(HttpMethod.Put, "/api/v1/profile/candidate", applicant,
                   new UpdateCandidateProfileRequest(null, null, "Graduate", null, null, null, null, null, null, null, null, null)))
        using (var client = _factory.CreateClient())
        {
            (await client.SendAsync(profile, TestContext.Current.CancellationToken)).EnsureSuccessStatusCode();
        }

        using var form = new HttpRequestMessage(HttpMethod.Post, $"/api/v1/jobs/{opportunity.Id}/applications");
        form.Headers.Authorization = new AuthenticationHeaderValue("Bearer", applicant);
        form.Headers.Add("X-Requested-With", "fetch");
        var multipart = new MultipartFormDataContent();
        var file = new ByteArrayContent("%PDF-1.4 minimal test resume"u8.ToArray());
        file.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");
        multipart.Add(file, "Resume", "resume.pdf");
        form.Content = multipart;

        using var applyClient = _factory.CreateClient();
        (await applyClient.SendAsync(form, TestContext.Current.CancellationToken)).StatusCode.Should().Be(HttpStatusCode.Created);
    }
}

/// <summary>
/// The admin API must not create the system owner. With no owner initialised it answers 503 and
/// writes nothing. This uses its own container, so the owner has never been created.
/// </summary>
public sealed class AidxOpportunityAdminUninitialisedTests(AuthApiFactory factory) : IClassFixture<AuthApiFactory>
{
    private readonly AuthApiFactory _factory = factory;

    [Fact]
    public async Task Admin_routes_return_503_and_do_not_create_the_system_owner()
    {
        var ct = TestContext.Current.CancellationToken;
        var clerkUserId = $"admin_{Guid.NewGuid():N}";
        var token = AuthApiFactory.IssueToken(clerkUserId, $"{Guid.NewGuid()}@example.com");
        using (var client = _factory.CreateClient())
        {
            using var setup = new HttpRequestMessage(HttpMethod.Post, "/api/v1/auth/account-setup")
            {
                Content = JsonContent.Create(new AccountSetupRequest("Applicant")),
            };
            setup.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            setup.Headers.Add("X-Requested-With", "fetch");
            (await client.SendAsync(setup, ct)).EnsureSuccessStatusCode();
        }

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            await db.Users.Where(u => u.ClerkUserId == clerkUserId).ExecuteUpdateAsync(s => s.SetProperty(u => u.Role, UserRole.Admin), ct);
        }

        using var get = new HttpRequestMessage(HttpMethod.Get, "/api/v1/admin/aidx/opportunities");
        get.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        using var getClient = _factory.CreateClient();
        (await getClient.SendAsync(get, ct)).StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);

        using var scopeAfter = _factory.Services.CreateScope();
        var dbAfter = scopeAfter.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        (await dbAfter.Users.CountAsync(u => u.ClerkUserId == AidxSystemIdentity.ClerkUserId, ct)).Should().Be(0);
    }
}
