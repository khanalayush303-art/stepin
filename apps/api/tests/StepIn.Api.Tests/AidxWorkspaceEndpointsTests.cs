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

/// <summary>Milestone 3 — real, non-fabricated workspace/manage counts.</summary>
public sealed class AidxWorkspaceEndpointsTests(AuthApiFactory factory) : IClassFixture<AuthApiFactory>
{
    private readonly AuthApiFactory _factory = factory;

    private static HttpRequestMessage Build(HttpMethod method, string url, string? token)
    {
        var request = new HttpRequestMessage(method, url);
        if (token is not null)
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }

        return request;
    }

    private async Task<HttpResponseMessage> SendAsync(HttpMethod method, string url, string? token)
    {
        using var client = _factory.CreateClient();
        using var request = Build(method, url, token);
        return await client.SendAsync(request, TestContext.Current.CancellationToken);
    }

    private async Task<(string Token, Guid UserId)> NewUserAsync()
    {
        var clerkUserId = $"user_{Guid.NewGuid():N}";
        var token = AuthApiFactory.IssueToken(clerkUserId, $"{Guid.NewGuid()}@example.com");
        using var setup = Build(HttpMethod.Post, "/api/v1/auth/account-setup", token);
        setup.Content = JsonContent.Create(new AccountSetupRequest("Applicant"));
        using var client = _factory.CreateClient();
        (await client.SendAsync(setup, TestContext.Current.CancellationToken)).EnsureSuccessStatusCode();

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var userId = await db.Users.Where(u => u.ClerkUserId == clerkUserId).Select(u => u.Id)
            .SingleAsync(TestContext.Current.CancellationToken);
        return (token, userId);
    }

    private async Task<AidxMembership> GrantAsync(Guid userId, AidxLabRole role, Guid? scope = null)
    {
        using var s = _factory.Services.CreateScope();
        var db = s.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var membership = new AidxMembership { UserId = userId, Role = role, Status = AidxMembershipStatus.Active, ScopedResearchAreaId = scope, GrantedByUserId = userId };
        db.AidxMemberships.Add(membership);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        return membership;
    }

    [Fact]
    public async Task Workspace_summary_reflects_real_counts_of_the_callers_own_content_only()
    {
        var (token, userId) = await NewUserAsync();
        var membership = await GrantAsync(userId, AidxLabRole.Contributor);
        var (_, otherUserId) = await NewUserAsync();
        var otherMembership = await GrantAsync(otherUserId, AidxLabRole.Contributor);

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            db.AidxProjects.Add(new AidxProject { Title = "Mine draft", Slug = $"p-{Guid.NewGuid():N}", ShortDescription = "s", Description = "d", CreatedByMembershipId = membership.Id });
            db.AidxProjects.Add(new AidxProject { Title = "Mine submitted", Slug = $"p-{Guid.NewGuid():N}", ShortDescription = "s", Description = "d", CreatedByMembershipId = membership.Id, Status = AidxContentStatus.Submitted });
            db.AidxNews.Add(new AidxNews { Title = "Someone else's", Slug = $"n-{Guid.NewGuid():N}", Summary = "s", Body = "b", CreatedByMembershipId = otherMembership.Id });
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        var response = await SendAsync(HttpMethod.Get, "/api/v1/aidx/workspace/summary", token);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var summary = await response.Content.ReadFromJsonAsync<AidxWorkspaceSummaryResponse>(TestContext.Current.CancellationToken);

        summary!.MyDrafts.Should().Be(1);
        summary.MySubmitted.Should().Be(1);
        summary.MyPublished.Should().Be(0);
    }

    [Fact]
    public async Task A_Contributor_cannot_access_the_manage_summary()
    {
        var (token, userId) = await NewUserAsync();
        await GrantAsync(userId, AidxLabRole.Contributor);

        var response = await SendAsync(HttpMethod.Get, "/api/v1/aidx/manage/summary", token);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task A_scoped_editor_sees_null_for_news_and_events_but_a_real_number_for_projects()
    {
        var (token, userId) = await NewUserAsync();
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var area = new AidxResearchArea { Name = $"Area {Guid.NewGuid():N}", Slug = $"area-{Guid.NewGuid():N}" };
        db.AidxResearchAreas.Add(area);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        await GrantAsync(userId, AidxLabRole.Editor, scope: area.Id);

        var project = new AidxProject { Title = "In scope", Slug = $"p-{Guid.NewGuid():N}", ShortDescription = "s", Description = "d", Status = AidxContentStatus.Submitted };
        db.AidxProjects.Add(project);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        db.Set<AidxProjectResearchArea>().Add(new AidxProjectResearchArea { ProjectId = project.Id, ResearchAreaId = area.Id });
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var response = await SendAsync(HttpMethod.Get, "/api/v1/aidx/manage/summary", token);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var summary = await response.Content.ReadFromJsonAsync<AidxManageSummaryResponse>(TestContext.Current.CancellationToken);

        summary!.NewsAwaitingReview.Should().BeNull("a scoped editor has zero rights over News, which is distinct from zero waiting");
        summary.EventsAwaitingReview.Should().BeNull();
        summary.ProjectsAwaitingReview.Should().Be(1);
    }
}
