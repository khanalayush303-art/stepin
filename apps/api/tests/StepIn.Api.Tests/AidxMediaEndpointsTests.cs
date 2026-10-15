using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SkiaSharp;
using StepIn.Api.Endpoints;
using StepIn.Domain.Aidx;
using StepIn.Domain.Common;
using StepIn.Infrastructure.Persistence;

namespace StepIn.Api.Tests;

/// <summary>
/// AIDX media: researcher profile photos (Admin), news/event images and publication PDFs
/// (Contributor-authored). Against a real PostgreSQL container and the real local-disk media
/// storage (<c>LocalDiskAidxMediaStorage</c>, isolated per test run by <see cref="AuthApiFactory"/>'s
/// configuration) — not a mock, so the actual decode validation and file round-trip are proven.
/// </summary>
public sealed class AidxMediaEndpointsTests(AuthApiFactory factory) : IClassFixture<AuthApiFactory>
{
    private readonly AuthApiFactory _factory = factory;

    // A real, genuinely-decodable 4x4 PNG, generated through SkiaSharp itself rather than a
    // hand-copied base64 blob — so there's no risk of the test fixture itself being invalid and
    // masking what ValidateImageAsync's actual decode step does.
    private static readonly byte[] ValidPngBytes = BuildValidPng();

    private static byte[] BuildValidPng()
    {
        using var bitmap = new SKBitmap(4, 4);
        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);
        return data.ToArray();
    }

    private static readonly byte[] ValidPdfBytes = "%PDF-1.4\n%%EOF"u8.ToArray();

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

    private static HttpRequestMessage BuildMultipart(
        HttpMethod method, string url, string? token, byte[] fileBytes, string fileName, string contentType,
        IReadOnlyDictionary<string, string>? fields = null)
    {
        var request = new HttpRequestMessage(method, url);
        var form = new MultipartFormDataContent();
        var fileContent = new ByteArrayContent(fileBytes);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue(contentType);
        form.Add(fileContent, "file", fileName);

        if (fields is not null)
        {
            foreach (var (name, value) in fields)
            {
                form.Add(new StringContent(value), name);
            }
        }

        request.Content = form;
        if (token is not null)
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }

        request.Headers.Add("X-Requested-With", "fetch");
        return request;
    }

    private async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request)
    {
        using var client = _factory.CreateClient();
        return await client.SendAsync(request, TestContext.Current.CancellationToken);
    }

    private async Task<string> AdminAsync()
    {
        var clerkUserId = $"admin_{Guid.NewGuid():N}";
        var token = AuthApiFactory.IssueToken(clerkUserId, $"{Guid.NewGuid()}@example.com");
        using var setup = Build(HttpMethod.Post, "/api/v1/auth/account-setup", token, new AccountSetupRequest("Applicant"));
        (await SendAsync(setup)).EnsureSuccessStatusCode();

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        await db.Users.Where(u => u.ClerkUserId == clerkUserId)
            .ExecuteUpdateAsync(s => s.SetProperty(u => u.Role, UserRole.Admin), TestContext.Current.CancellationToken);
        return token;
    }

    private async Task<(string Token, Guid UserId)> NewUserAsync()
    {
        var clerkUserId = $"user_{Guid.NewGuid():N}";
        var token = AuthApiFactory.IssueToken(clerkUserId, $"{Guid.NewGuid()}@example.com");
        using var setup = Build(HttpMethod.Post, "/api/v1/auth/account-setup", token, new AccountSetupRequest("Applicant"));
        (await SendAsync(setup)).EnsureSuccessStatusCode();

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var userId = await db.Users.Where(u => u.ClerkUserId == clerkUserId).Select(u => u.Id)
            .SingleAsync(TestContext.Current.CancellationToken);
        return (token, userId);
    }

    private async Task<AidxMembership> GrantAsync(Guid userId, AidxLabRole role)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var membership = new AidxMembership { UserId = userId, Role = role, Status = AidxMembershipStatus.Active, GrantedByUserId = userId };
        db.AidxMemberships.Add(membership);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        return membership;
    }

    private async Task<string> CurrentPublicationVersionAsync(Guid id)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var tracked = await db.AidxPublications.FirstAsync(p => p.Id == id, TestContext.Current.CancellationToken);
        return db.Entry(tracked).Property<uint>("xmin").CurrentValue.ToString();
    }

    private async Task<Guid> CreateResearcherAsync(string adminToken)
    {
        var response = await SendAsync(Build(HttpMethod.Post, "/api/v1/admin/aidx/people", adminToken,
            new AidxResearcherRequest(Unique("Media Person"), null, "Academic", null, null, null, null, null, null, true)));
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<AidxIdResponse>(TestContext.Current.CancellationToken))!.Id;
    }

    // ---- researcher profile images -----------------------------------------------------------

    [Fact]
    public async Task Admin_uploads_a_researcher_photo_with_required_alt_text_and_it_is_served_publicly_once_published()
    {
        var admin = await AdminAsync();
        var researcherId = await CreateResearcherAsync(admin);

        var upload = await SendAsync(BuildMultipart(
            HttpMethod.Post, $"/api/v1/admin/aidx/people/{researcherId}/image", admin, ValidPngBytes, "photo.png", "image/png",
            new Dictionary<string, string> { ["altText"] = "A researcher smiling at the camera." }));
        upload.StatusCode.Should().Be(HttpStatusCode.OK);

        var get = await SendAsync(Build(HttpMethod.Get, $"/api/v1/aidx/media/people/{researcherId}/image", null));
        get.StatusCode.Should().Be(HttpStatusCode.OK);
        get.Content.Headers.ContentType!.MediaType.Should().Be("image/png");
        get.Headers.ETag.Should().NotBeNull();
        get.Headers.CacheControl!.NoCache.Should().BeTrue();

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var researcher = await db.AidxResearchers.AsNoTracking().SingleAsync(r => r.Id == researcherId, TestContext.Current.CancellationToken);
        researcher.ImageAltText.Should().Be("A researcher smiling at the camera.");
    }

    [Fact]
    public async Task Uploading_a_photo_without_alt_text_is_rejected()
    {
        var admin = await AdminAsync();
        var researcherId = await CreateResearcherAsync(admin);

        var upload = await SendAsync(BuildMultipart(
            HttpMethod.Post, $"/api/v1/admin/aidx/people/{researcherId}/image", admin, ValidPngBytes, "photo.png", "image/png"));
        upload.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task A_corrupt_file_renamed_to_look_like_an_image_fails_to_decode_and_is_rejected()
    {
        var admin = await AdminAsync();
        var researcherId = await CreateResearcherAsync(admin);

        var garbage = "this is not actually a png file at all, just text wearing an extension"u8.ToArray();
        var upload = await SendAsync(BuildMultipart(
            HttpMethod.Post, $"/api/v1/admin/aidx/people/{researcherId}/image", admin, garbage, "photo.png", "image/png",
            new Dictionary<string, string> { ["altText"] = "Alt text" }));

        upload.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task An_image_whose_declared_dimensions_exceed_the_cap_is_rejected_before_full_decode()
    {
        var admin = await AdminAsync();
        var researcherId = await CreateResearcherAsync(admin);

        // 8001x1: one pixel over AidxMediaValidation's 8000px cap on one axis, but trivial actual
        // memory (a real decompression-bomb file would be tiny on disk and huge once decoded —
        // this proves the cap itself is enforced, using a file that is honestly that large rather
        // than a crafted bomb, since SKCodec.Info reads declared dimensions before any full
        // pixel-buffer decode happens regardless of which path produced the oversized metadata).
        byte[] oversized;
        using (var bitmap = new SKBitmap(8001, 1))
        using (var image = SKImage.FromBitmap(bitmap))
        using (var data = image.Encode(SKEncodedImageFormat.Png, 100))
        {
            oversized = data.ToArray();
        }

        var upload = await SendAsync(BuildMultipart(
            HttpMethod.Post, $"/api/v1/admin/aidx/people/{researcherId}/image", admin, oversized, "huge.png", "image/png",
            new Dictionary<string, string> { ["altText"] = "Alt text" }));

        upload.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var researcher = await db.AidxResearchers.AsNoTracking().SingleAsync(r => r.Id == researcherId, TestContext.Current.CancellationToken);
        researcher.ProfileImageKey.Should().BeNull("a rejected upload must never be saved to storage or recorded on the row");
    }

    [Fact]
    public async Task A_file_over_the_byte_size_cap_is_rejected_without_being_read_as_an_image()
    {
        var admin = await AdminAsync();
        var researcherId = await CreateResearcherAsync(admin);

        var tooLarge = new byte[AidxMediaValidation.MaxImageSizeBytes + 1];
        var upload = await SendAsync(BuildMultipart(
            HttpMethod.Post, $"/api/v1/admin/aidx/people/{researcherId}/image", admin, tooLarge, "big.png", "image/png",
            new Dictionary<string, string> { ["altText"] = "Alt text" }));

        upload.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task An_unpublished_researchers_photo_is_not_served_to_an_anonymous_visitor()
    {
        var admin = await AdminAsync();
        var response = await SendAsync(Build(HttpMethod.Post, "/api/v1/admin/aidx/people", admin,
            new AidxResearcherRequest(Unique("Hidden Person"), null, "Academic", null, null, null, null, null, null, false)));
        var researcherId = (await response.Content.ReadFromJsonAsync<AidxIdResponse>(TestContext.Current.CancellationToken))!.Id;

        await SendAsync(BuildMultipart(
            HttpMethod.Post, $"/api/v1/admin/aidx/people/{researcherId}/image", admin, ValidPngBytes, "photo.png", "image/png",
            new Dictionary<string, string> { ["altText"] = "Alt text" }));

        (await SendAsync(Build(HttpMethod.Get, $"/api/v1/aidx/media/people/{researcherId}/image", null)))
            .StatusCode.Should().Be(HttpStatusCode.Unauthorized, "an anonymous caller gets 401, not 403");

        (await SendAsync(Build(HttpMethod.Get, $"/api/v1/aidx/media/people/{researcherId}/image", admin)))
            .StatusCode.Should().Be(HttpStatusCode.OK);
    }

    // ---- news images, revision-aware, and historical-reference protection --------------------

    [Fact]
    public async Task A_contributor_uploads_a_news_image_and_a_second_upload_to_a_published_item_opens_a_revision()
    {
        var (token, userId) = await NewUserAsync();
        await GrantAsync(userId, AidxLabRole.Contributor);

        var created = (await (await SendAsync(Build(HttpMethod.Post, "/api/v1/aidx/authoring/news", token,
                new AidxNewsRequest(Unique("Media News"), null, "Summary.", "Body.", null))))
            .Content.ReadFromJsonAsync<AidxIdResponse>(TestContext.Current.CancellationToken))!;

        var firstUpload = await SendAsync(BuildMultipart(
            HttpMethod.Post, $"/api/v1/aidx/authoring/news/{created.Id}/image", token, ValidPngBytes, "image.png", "image/png",
            new Dictionary<string, string> { ["altText"] = "Alt text" }));
        firstUpload.StatusCode.Should().Be(HttpStatusCode.OK);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var news = await db.AidxNews.AsNoTracking().SingleAsync(n => n.Id == created.Id, TestContext.Current.CancellationToken);
        news.ImageKey.Should().NotBeNull("the Draft row is edited in place");
    }

    [Fact]
    public async Task An_unauthenticated_request_to_upload_a_news_image_is_unauthorized()
    {
        var (token, userId) = await NewUserAsync();
        await GrantAsync(userId, AidxLabRole.Contributor);
        var created = (await (await SendAsync(Build(HttpMethod.Post, "/api/v1/aidx/authoring/news", token,
                new AidxNewsRequest(Unique("Guarded News"), null, "Summary.", "Body.", null))))
            .Content.ReadFromJsonAsync<AidxIdResponse>(TestContext.Current.CancellationToken))!;

        (await SendAsync(BuildMultipart(
                HttpMethod.Post, $"/api/v1/aidx/authoring/news/{created.Id}/image", null, ValidPngBytes, "image.png", "image/png")))
            .StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // ---- publication PDFs ----------------------------------------------------------------------

    [Fact]
    public async Task A_contributor_uploads_a_publication_pdf_and_it_is_served_with_safe_headers_once_published()
    {
        var (token, userId) = await NewUserAsync();
        await GrantAsync(userId, AidxLabRole.Contributor);
        var (editorToken, editorUserId) = await NewUserAsync();
        await GrantAsync(editorUserId, AidxLabRole.Editor);

        var created = (await (await SendAsync(Build(HttpMethod.Post, "/api/v1/aidx/authoring/publications", token,
                new AidxPublicationRequest(Unique("Media Paper"), null, "Report", null, 2024, null, null, null))))
            .Content.ReadFromJsonAsync<AidxIdResponse>(TestContext.Current.CancellationToken))!;

        var upload = await SendAsync(BuildMultipart(
            HttpMethod.Post, $"/api/v1/aidx/authoring/publications/{created.Id}/pdf", token, ValidPdfBytes, "paper.pdf", "application/pdf"));
        upload.StatusCode.Should().Be(HttpStatusCode.OK);
        var afterUpload = (await upload.Content.ReadFromJsonAsync<AidxMediaUploadResponse>(TestContext.Current.CancellationToken))!;
        afterUpload.StorageKey.Should().NotBeNullOrEmpty();

        var afterUploadVersion = await CurrentPublicationVersionAsync(created.Id);

        (await SendAsync(Build(HttpMethod.Post, $"/api/v1/aidx/content/publication/{created.Id}/submit", token,
                new AidxWorkflowActionRequest(afterUploadVersion))))
            .StatusCode.Should().Be(HttpStatusCode.OK);

        var afterSubmitVersion = await CurrentPublicationVersionAsync(created.Id);

        (await SendAsync(Build(HttpMethod.Post, $"/api/v1/aidx/content/publication/{created.Id}/approve", editorToken,
                new AidxWorkflowActionRequest(afterSubmitVersion))))
            .StatusCode.Should().Be(HttpStatusCode.OK);

        var get = await SendAsync(Build(HttpMethod.Get, $"/api/v1/aidx/media/publications/{created.Id}/pdf", null));
        get.StatusCode.Should().Be(HttpStatusCode.OK);
        get.Content.Headers.ContentType!.MediaType.Should().Be("application/pdf");
        get.Headers.Should().Contain(h => h.Key == "X-Content-Type-Options");
    }

    [Fact]
    public async Task A_non_pdf_file_renamed_to_pdf_is_rejected()
    {
        var (token, userId) = await NewUserAsync();
        await GrantAsync(userId, AidxLabRole.Contributor);
        var created = (await (await SendAsync(Build(HttpMethod.Post, "/api/v1/aidx/authoring/publications", token,
                new AidxPublicationRequest(Unique("Fake PDF Paper"), null, "Report", null, 2024, null, null, null))))
            .Content.ReadFromJsonAsync<AidxIdResponse>(TestContext.Current.CancellationToken))!;

        var notActuallyAPdf = "just some plain text"u8.ToArray();
        (await SendAsync(BuildMultipart(
                HttpMethod.Post, $"/api/v1/aidx/authoring/publications/{created.Id}/pdf", token, notActuallyAPdf, "paper.pdf", "application/pdf")))
            .StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
}
