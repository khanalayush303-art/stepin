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
/// Phase 4.4E.1 admin read endpoints for research areas and projects. Admins can see drafts and
/// archived rows here, while the public endpoints keep hiding them.
/// </summary>
public sealed class AidxCmsReadTests(AuthApiFactory factory) : IClassFixture<AuthApiFactory>
{
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

    private async Task<string> AdminAsync()
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

    private async Task<AidxIdResponse> CreateProjectAsync(string admin, string title, IReadOnlyList<Guid>? areaIds = null, IReadOnlyList<string>? technologies = null)
    {
        var response = await SendAsync(HttpMethod.Post, "/api/v1/admin/aidx/projects", admin,
            new AidxProjectRequest(title, null, "Short.", "Long.", null, null, null, false, areaIds, technologies, null));
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        return (await response.Content.ReadFromJsonAsync<AidxIdResponse>(TestContext.Current.CancellationToken))!;
    }

    [Fact]
    public async Task Admin_project_reads_follow_the_admin_role_boundary()
    {
        var applicant = await IssueAsync("Applicant");
        var admin = await AdminAsync();

        (await SendAsync(HttpMethod.Get, "/api/v1/admin/aidx/projects", null)).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await SendAsync(HttpMethod.Get, "/api/v1/admin/aidx/projects", applicant)).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await SendAsync(HttpMethod.Get, "/api/v1/admin/aidx/projects", admin)).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Admin_project_list_includes_drafts_while_the_public_list_does_not()
    {
        var admin = await AdminAsync();
        var title = Unique("Cms draft project");
        var created = await CreateProjectAsync(admin, title);

        var adminPage = (await (await SendAsync(HttpMethod.Get, "/api/v1/admin/aidx/projects?status=Draft&pageSize=50", admin))
            .Content.ReadFromJsonAsync<AidxPageResponse<AidxAdminProjectSummaryResponse>>(TestContext.Current.CancellationToken))!;
        adminPage.Items.Select(p => p.Id).Should().Contain(created.Id);
        adminPage.Items.Should().OnlyContain(p => p.Status == "Draft");

        var publicPage = (await (await SendAsync(HttpMethod.Get, "/api/v1/aidx/projects?pageSize=50", null))
            .Content.ReadFromJsonAsync<AidxPageResponse<AidxProjectSummaryResponse>>(TestContext.Current.CancellationToken))!;
        publicPage.Items.Select(p => p.Id).Should().NotContain(created.Id);

        (await SendAsync(HttpMethod.Get, $"/api/v1/aidx/projects/{created.Slug}", null))
            .StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Admin_project_detail_returns_the_relationships_the_edit_form_needs()
    {
        var admin = await AdminAsync();
        var areaResponse = await SendAsync(HttpMethod.Post, "/api/v1/admin/aidx/research", admin,
            new AidxResearchAreaRequest(Unique("Cms area"), null, "Area.", 1));
        var area = (await areaResponse.Content.ReadFromJsonAsync<AidxIdResponse>(TestContext.Current.CancellationToken))!;

        var project = await CreateProjectAsync(admin, Unique("Cms detail project"), [area.Id], ["Python", "Pandas"]);

        var detail = (await (await SendAsync(HttpMethod.Get, $"/api/v1/admin/aidx/projects/{project.Id}", admin))
            .Content.ReadFromJsonAsync<AidxAdminProjectDetailResponse>(TestContext.Current.CancellationToken))!;

        detail.Status.Should().Be("Draft");
        detail.ResearchAreaIds.Should().ContainSingle().Which.Should().Be(area.Id);
        detail.Technologies.Should().BeEquivalentTo(["Pandas", "Python"]);
    }

    [Fact]
    public async Task Unknown_project_and_research_area_ids_return_404()
    {
        var admin = await AdminAsync();

        (await SendAsync(HttpMethod.Get, $"/api/v1/admin/aidx/projects/{Guid.NewGuid()}", admin)).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await SendAsync(HttpMethod.Get, $"/api/v1/admin/aidx/research/{Guid.NewGuid()}", admin)).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Admin_project_list_rejects_an_unknown_status()
    {
        var admin = await AdminAsync();

        (await SendAsync(HttpMethod.Get, "/api/v1/admin/aidx/projects?status=Sideways", admin)).StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Admin_research_area_read_returns_the_area()
    {
        var admin = await AdminAsync();
        var name = Unique("Readable area");
        var created = (await (await SendAsync(HttpMethod.Post, "/api/v1/admin/aidx/research", admin,
                new AidxResearchAreaRequest(name, null, "Desc.", 2)))
            .Content.ReadFromJsonAsync<AidxIdResponse>(TestContext.Current.CancellationToken))!;

        var read = (await (await SendAsync(HttpMethod.Get, $"/api/v1/admin/aidx/research/{created.Id}", admin))
            .Content.ReadFromJsonAsync<AidxResearchAreaResponse>(TestContext.Current.CancellationToken))!;

        read.Name.Should().Be(name);
        read.Slug.Should().Be(created.Slug);
    }
}
