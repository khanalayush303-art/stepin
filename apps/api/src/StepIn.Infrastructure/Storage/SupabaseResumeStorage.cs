using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using StepIn.Application.Common.Interfaces;

namespace StepIn.Infrastructure.Storage;

/// <summary>
/// Persists resume files in a private Supabase Storage bucket over its plain HTTP object API
/// (<c>POST/GET/DELETE {Url}/storage/v1/object/{Bucket}/{key}</c>), so resumes survive a
/// container redeploy or replacement — unlike <see cref="LocalDiskResumeStorage"/>. Selected by
/// <c>Storage:Provider = "Supabase"</c> (see <c>DependencyInjection.AddInfrastructure</c>, which
/// also validates the required configuration before this class is ever constructed).
///
/// Security notes:
/// - The service-role key lives only in <see cref="HttpClient.DefaultRequestHeaders"/>, set once
///   when the typed client is configured. It is never logged, never returned to a caller, and
///   never reaches the frontend — every upload/download/delete goes through this server-side type.
/// - The bucket must be private. This class never requests or constructs a public or signed URL.
/// - Storage keys are always server-generated (<see cref="SaveAsync"/>), but <see cref="OpenReadAsync"/>
///   and <see cref="DeleteAsync"/> re-validate the key shape before using it in a request path, the
///   same defense-in-depth <see cref="LocalDiskResumeStorage"/> applies with <c>Path.GetFileName</c>.
///   This rejects path traversal (e.g. "../../etc/passwd") even though nothing in this codebase
///   ever supplies a client-chosen key.
/// </summary>
public sealed class SupabaseResumeStorage(HttpClient httpClient, ILogger<SupabaseResumeStorage> logger) : IResumeStorage
{
    // {32 hex chars}.{1-10 alphanumeric chars} — exactly the shape SaveAsync generates below.
    // Anything else (slashes, "..", empty, unexpected length) is treated as not-ours and refused
    // before it is ever sent to Supabase.
    private static readonly Regex SafeStorageKeyPattern = new(@"^[0-9a-f]{32}\.[A-Za-z0-9]{1,10}$", RegexOptions.Compiled);

    public async Task<string> SaveAsync(Stream content, string fileExtension, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(content);

        var storageKey = $"{Guid.NewGuid():N}{fileExtension}";
        if (!SafeStorageKeyPattern.IsMatch(storageKey))
        {
            // The extension came from upload validation (an allow-list of .pdf/.doc/.docx), so
            // this should be unreachable. Refusing outright is safer than silently relying on
            // Supabase's own path handling for a value this class is about to trust.
            throw new InvalidOperationException("Generated an unsafe resume storage key; refusing to upload.");
        }

        // Streamed directly from the incoming upload — never buffered here. Supabase reports back
        // whatever Content-Type is sent on upload, but nothing reads that back: the endpoint
        // always serves downloads with the Content-Type already recorded on the JobApplication
        // row, so the exact value sent here is not security- or behaviour-relevant.
        using var requestContent = new StreamContent(content);
        requestContent.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");

        using var response = await SendAsync(HttpMethod.Post, storageKey, requestContent, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            logger.LogError("Supabase resume upload failed with status {StatusCode}.", (int)response.StatusCode);
            throw new InvalidOperationException("Could not save the resume. Please try again.");
        }

        return storageKey;
    }

    public async Task<Stream?> OpenReadAsync(string storageKey, CancellationToken cancellationToken)
    {
        if (!IsSafeStorageKey(storageKey))
        {
            return null;
        }

        using var response = await SendAsync(HttpMethod.Get, storageKey, content: null, cancellationToken);

        if (response.StatusCode == HttpStatusCode.NotFound || await IsObjectNotFoundAsync(response, cancellationToken))
        {
            return null;
        }

        if (!response.IsSuccessStatusCode)
        {
            logger.LogError("Supabase resume download failed with status {StatusCode}.", (int)response.StatusCode);
            throw new InvalidOperationException("Resume storage is temporarily unavailable.");
        }

        // Resumes are capped at 5 MB by upload validation (see ApplicationEndpoints), so buffering
        // the whole response here is a deliberate, bounded trade-off: it lets this method dispose
        // the HttpResponseMessage (and its connection) immediately, instead of keeping it alive
        // for as long as the caller takes to stream the download — which is the usual reason a
        // typed HttpClient's response ends up leaked across a method boundary.
        var buffer = new MemoryStream();
        await response.Content.CopyToAsync(buffer, cancellationToken);
        buffer.Position = 0;
        return buffer;
    }

    public async Task DeleteAsync(string storageKey, CancellationToken cancellationToken)
    {
        if (!IsSafeStorageKey(storageKey))
        {
            return;
        }

        try
        {
            using var response = await SendAsync(HttpMethod.Delete, storageKey, content: null, cancellationToken);

            if (!response.IsSuccessStatusCode && response.StatusCode != HttpStatusCode.NotFound && !await IsObjectNotFoundAsync(response, cancellationToken))
            {
                // This is called only to clean up the losing side of a create-time race (see
                // ApplicationEndpoints) or an equivalent cleanup path. Never throw from here: a
                // failed delete must not mask whatever failure triggered the cleanup. It is
                // logged so an orphaned object can be found and reconciled later.
                logger.LogWarning("Supabase resume deletion failed with status {StatusCode}; the object may be orphaned.", (int)response.StatusCode);
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "Supabase resume deletion threw while cleaning up an object; it may be orphaned.");
        }
    }

    /// <summary>
    /// Supabase Storage's object GET and DELETE endpoints report a missing object as HTTP 400
    /// with a body shaped like <c>{"statusCode":"404","error":"not_found","code":"NoSuchKey"}</c> —
    /// not a literal HTTP 404 status. Discovered during live verification against a real project
    /// (an offline mock, built from the documented shape rather than the real response, missed
    /// this). Only this specific, small, structured body is inspected; any other 4xx/5xx — including
    /// a 400 for a different reason — is still treated as a genuine failure.
    /// </summary>
    private static async Task<bool> IsObjectNotFoundAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        if (response.StatusCode != HttpStatusCode.BadRequest)
        {
            return false;
        }

        try
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            using var json = JsonDocument.Parse(body);
            return json.RootElement.TryGetProperty("error", out var error)
                && string.Equals(error.GetString(), "not_found", StringComparison.OrdinalIgnoreCase);
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static bool IsSafeStorageKey(string storageKey) =>
        !string.IsNullOrEmpty(storageKey) && SafeStorageKeyPattern.IsMatch(storageKey);

    private async Task<HttpResponseMessage> SendAsync(HttpMethod method, string storageKey, HttpContent? content, CancellationToken cancellationToken)
    {
        // The base address (configured in DependencyInjection) already ends in the bucket path,
        // so this is the only variable segment. Escaped even though the key shape above already
        // excludes '/' and '..' — a second, cheap guarantee against path traversal.
        //
        // This must actually await before `request` is disposed: without `async`/`await` here,
        // the `using` would dispose the request as soon as this method returns the in-flight
        // Task, not after it completes.
        using var request = new HttpRequestMessage(method, Uri.EscapeDataString(storageKey)) { Content = content };
        return await httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
    }
}
