using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using StepIn.Domain.Aidx;
using StepIn.Infrastructure.Persistence;

namespace StepIn.Api.Tests;

/// <summary>
/// Public detail endpoint for AIDX publications. Anonymous callers can read a published publication
/// with its authors, research areas and published projects. Unpublished and unknown ids are both 404.
/// </summary>
public sealed class AidxPublicationDetailTests(AuthApiFactory factory) : IClassFixture<AuthApiFactory>
{
    private readonly AuthApiFactory _factory = factory;

    private static string Unique(string prefix) => $"{prefix} {Guid.NewGuid():N}";

    private async Task<AidxPublication> SeedPublicationAsync(
        bool published,
        AidxResearchArea? area,
        AidxProject? publishedProject,
        AidxProject? draftProject)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var ct = TestContext.Current.CancellationToken;

        if (area is not null)
        {
            db.Add(area);
        }

        if (publishedProject is not null)
        {
            db.Add(publishedProject);
        }

        if (draftProject is not null)
        {
            db.Add(draftProject);
        }

        var publication = new AidxPublication
        {
            Title = Unique("Publication"),
            Abstract = "An abstract.",
            PublicationType = AidxPublicationType.JournalArticle,
            Venue = "Journal of Tests",
            Year = 2026,
            Doi = $"10.1000/{Guid.NewGuid():N}",
            Published = published,
        };
        db.Add(publication);

        db.Add(new AidxPublicationAuthor { PublicationId = publication.Id, Position = 2, ExternalAuthorName = "Second Author" });
        db.Add(new AidxPublicationAuthor { PublicationId = publication.Id, Position = 1, ExternalAuthorName = "First Author" });

        if (area is not null)
        {
            db.Add(new AidxPublicationResearchArea { PublicationId = publication.Id, ResearchAreaId = area.Id });
        }

        if (publishedProject is not null)
        {
            db.Add(new AidxPublicationProject { PublicationId = publication.Id, ProjectId = publishedProject.Id });
        }

        if (draftProject is not null)
        {
            db.Add(new AidxPublicationProject { PublicationId = publication.Id, ProjectId = draftProject.Id });
        }

        await db.SaveChangesAsync(ct);
        return publication;
    }

    private static AidxProject NewProject(AidxContentStatus status, string title) => new()
    {
        Title = title,
        Slug = title.Replace(' ', '-').ToLowerInvariant(),
        ShortDescription = "Short.",
        Description = "Long.",
        Status = status,
        PublishedAt = status == AidxContentStatus.Published ? DateTimeOffset.UtcNow : null,
    };

    [Fact]
    public async Task Published_publication_detail_is_public_and_includes_authors_areas_and_only_published_projects()
    {
        var ct = TestContext.Current.CancellationToken;
        var area = new AidxResearchArea { Name = Unique("Area"), Slug = Unique("area").Replace(' ', '-').ToLowerInvariant() };
        var liveTitle = Unique("Live project");
        var draftTitle = Unique("Draft project");
        var publication = await SeedPublicationAsync(
            published: true,
            area: area,
            publishedProject: NewProject(AidxContentStatus.Published, liveTitle),
            draftProject: NewProject(AidxContentStatus.Draft, draftTitle));

        using var client = _factory.CreateClient();
        var response = await client.GetAsync($"/api/v1/aidx/publications/{publication.Id}", ct);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(ct);
        body.GetProperty("title").GetString().Should().Be(publication.Title);
        body.GetProperty("doi").GetString().Should().Be(publication.Doi);

        var authors = body.GetProperty("authors").EnumerateArray().Select(a => a.GetString()).ToList();
        authors.Should().Equal("First Author", "Second Author");

        var areas = body.GetProperty("researchAreas").EnumerateArray().Select(a => a.GetProperty("slug").GetString()).ToList();
        areas.Should().Contain(area.Slug);

        var projectTitles = body.GetProperty("projects").EnumerateArray().Select(p => p.GetProperty("title").GetString()).ToList();
        projectTitles.Should().Contain(liveTitle);
        projectTitles.Should().NotContain(draftTitle, "an unpublished project must never be revealed through a publication");
    }

    [Fact]
    public async Task Publication_detail_needs_no_authentication_and_is_not_found_while_unpublished()
    {
        var ct = TestContext.Current.CancellationToken;
        var draft = await SeedPublicationAsync(published: false, area: null, publishedProject: null, draftProject: null);

        using var client = _factory.CreateClient();
        var response = await client.GetAsync($"/api/v1/aidx/publications/{draft.Id}", ct);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound, "a draft publication must not be readable, and its existence must not be revealed");
    }

    [Fact]
    public async Task Unknown_publication_id_is_not_found()
    {
        var ct = TestContext.Current.CancellationToken;

        using var client = _factory.CreateClient();
        var response = await client.GetAsync($"/api/v1/aidx/publications/{Guid.NewGuid()}", ct);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
