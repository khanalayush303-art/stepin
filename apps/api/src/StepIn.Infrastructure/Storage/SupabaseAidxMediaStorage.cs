using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using StepIn.Application.Common.Interfaces;

namespace StepIn.Infrastructure.Storage;

/// <summary>
/// Persists AIDX media (researcher photos, news/event images, publication PDFs) in its own private
/// Supabase Storage bucket — a separate bucket from resumes (<c>Storage:Supabase:AidxMediaBucket</c>,
/// a new required configuration key when <c>Storage:Provider = "Supabase"</c>; the <c>Url</c>/
/// <c>ServiceKey</c> values are reused, not duplicated). Otherwise identical in mechanics and the
/// same security posture as <see cref="SupabaseResumeStorage"/> — see that class's doc comment for
/// the reasoning (service-role key never logged/returned, bucket must be private, server-generated
/// keys only, defense-in-depth key-shape re-validation).
/// </summary>
public sealed class SupabaseAidxMediaStorage(HttpClient httpClient, ILogger<SupabaseAidxMediaStorage> logger) : IAidxMediaStorage
{
    private static readonly Regex SafeStorageKeyPattern = new(@"^[0-9a-f]{32}\.[A-Za-z0-9]{1,10}$", RegexOptions.Compiled);

    public async Task<string> SaveAsync(Stream content, string fileExtension, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(content);

        var storageKey = $"{Guid.NewGuid():N}{fileExtension}";
        if (!SafeStorageKeyPattern.IsMatch(storageKey))
        {
            throw new InvalidOperationException("Generated an unsafe AIDX media storage key; refusing to upload.");
        }

        using var requestContent = new StreamContent(content);
        requestContent.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");

        using var response = await SendAsync(HttpMethod.Post, storageKey, requestContent, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            logger.LogError("Supabase AIDX media upload failed with status {StatusCode}.", (int)response.StatusCode);
            throw new InvalidOperationException("Could not save the file. Please try again.");
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
            logger.LogError("Supabase AIDX media download failed with status {StatusCode}.", (int)response.StatusCode);
            throw new InvalidOperationException("Media storage is temporarily unavailable.");
        }

        // Images/PDFs are capped at 8 MB by upload validation, so buffering the whole response
        // here is the same deliberate, bounded trade-off SupabaseResumeStorage makes.
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
                logger.LogWarning("Supabase AIDX media deletion failed with status {StatusCode}; the object may be orphaned.", (int)response.StatusCode);
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "Supabase AIDX media deletion threw while cleaning up an object; it may be orphaned.");
        }
    }

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
        using var request = new HttpRequestMessage(method, Uri.EscapeDataString(storageKey)) { Content = content };
        return await httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
    }
}
