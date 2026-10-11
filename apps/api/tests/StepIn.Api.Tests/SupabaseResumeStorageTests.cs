using System.Net;
using System.Net.Http.Headers;
using System.Text;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using StepIn.Application.Common.Interfaces;
using StepIn.Infrastructure;
using StepIn.Infrastructure.Storage;

namespace StepIn.Api.Tests;

/// <summary>
/// Offline tests for <see cref="SupabaseResumeStorage"/>: everything runs against a fake
/// <see cref="HttpMessageHandler"/>, so none of this needs a real Supabase project or
/// credentials. It proves the request shape (method, URL, headers, body) and the failure
/// handling, which is exactly what can't be proven by reading the code alone.
/// </summary>
public sealed class SupabaseResumeStorageTests
{
    private const string ApiKey = "sb_service_role_test_key_do_not_log_this";
    private static readonly Uri BucketBaseAddress = new("https://example.supabase.co/storage/v1/object/resumes/");

    /// <summary>Captures the last request's shape and body, then returns a scripted response.</summary>
    private sealed class CapturingHandler : HttpMessageHandler
    {
        public Func<HttpRequestMessage, HttpResponseMessage> Respond { get; set; } = _ => new HttpResponseMessage(HttpStatusCode.OK);

        public HttpMethod? Method { get; private set; }
        public Uri? RequestUri { get; private set; }
        public string? ApiKeyHeader { get; private set; }
        public string? AuthorizationHeader { get; private set; }
        public byte[]? Body { get; private set; }
        public int CallCount { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            CallCount++;
            Method = request.Method;
            RequestUri = request.RequestUri;
            ApiKeyHeader = request.Headers.TryGetValues("apikey", out var values) ? values.FirstOrDefault() : null;
            AuthorizationHeader = request.Headers.Authorization?.ToString();
            Body = request.Content is null ? null : await request.Content.ReadAsByteArrayAsync(cancellationToken);

            return Respond(request);
        }
    }

    /// <summary>Captures every formatted log line, so a test can assert a secret never appears in one.</summary>
    private sealed class CapturingLogger : ILogger<SupabaseResumeStorage>
    {
        public List<string> Messages { get; } = [];

        public IDisposable BeginScope<TState>(TState state) where TState : notnull => NullScope.Instance;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) =>
            Messages.Add(formatter(state, exception));

        private sealed class NullScope : IDisposable
        {
            public static readonly NullScope Instance = new();
            public void Dispose() { }
        }
    }

    private static (SupabaseResumeStorage Storage, CapturingHandler Handler, CapturingLogger Logger) CreateStorage()
    {
        var handler = new CapturingHandler();
        var client = new HttpClient(handler) { BaseAddress = BucketBaseAddress };
        // Mirrors exactly what AddResumeStorage configures on the typed client in production —
        // this test builds the client directly instead of through DI, so it must set the same
        // default headers itself, or every header assertion below would be testing nothing.
        client.DefaultRequestHeaders.Add("apikey", ApiKey);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", ApiKey);
        var logger = new CapturingLogger();
        return (new SupabaseResumeStorage(client, logger), handler, logger);
    }

    [Fact]
    public async Task SaveAsync_uploads_with_a_server_generated_key_and_the_service_role_headers()
    {
        var (storage, handler, _) = CreateStorage();
        var bytes = "resume bytes"u8.ToArray();
        handler.Respond = _ => new HttpResponseMessage(HttpStatusCode.OK);

        string key;
        using (var content = new MemoryStream(bytes))
        {
            key = await storage.SaveAsync(content, ".pdf", TestContext.Current.CancellationToken);
        }

        key.Should().MatchRegex("^[0-9a-f]{32}\\.pdf$");
        handler.Method.Should().Be(HttpMethod.Post);
        handler.RequestUri!.ToString().Should().Be($"{BucketBaseAddress}{key}");
        handler.Body.Should().Equal(bytes);
    }

    [Fact]
    public async Task SaveAsync_throws_without_leaking_details_when_upload_fails()
    {
        var (storage, handler, logger) = CreateStorage();
        handler.Respond = _ => new HttpResponseMessage(HttpStatusCode.InternalServerError);

        var act = async () =>
        {
            using var content = new MemoryStream([1, 2, 3]);
            await storage.SaveAsync(content, ".pdf", TestContext.Current.CancellationToken);
        };

        (await act.Should().ThrowAsync<InvalidOperationException>()).Which.Message.Should().NotContain(ApiKey);
        logger.Messages.Should().Contain(m => m.Contains("500") || m.Contains("upload failed"));
    }

    [Fact]
    public async Task OpenReadAsync_downloads_and_returns_the_exact_bytes()
    {
        var (storage, handler, _) = CreateStorage();
        var bytes = "stored resume"u8.ToArray();
        handler.Respond = _ => new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(bytes) };

        var key = "0123456789abcdef0123456789abcdef.pdf";
        var stream = await storage.OpenReadAsync(key, TestContext.Current.CancellationToken);

        stream.Should().NotBeNull();
        handler.Method.Should().Be(HttpMethod.Get);
        handler.RequestUri!.ToString().Should().Be($"{BucketBaseAddress}{key}");
        using var buffer = new MemoryStream();
        await stream!.CopyToAsync(buffer, TestContext.Current.CancellationToken);
        buffer.ToArray().Should().Equal(bytes);
    }

    [Fact]
    public async Task OpenReadAsync_returns_null_for_a_missing_object_without_throwing()
    {
        var (storage, handler, _) = CreateStorage();
        handler.Respond = _ => new HttpResponseMessage(HttpStatusCode.NotFound);

        var result = await storage.OpenReadAsync("0123456789abcdef0123456789abcdef.pdf", TestContext.Current.CancellationToken);

        result.Should().BeNull();
    }

    [Fact]
    public async Task OpenReadAsync_rejects_a_key_that_does_not_match_the_generated_shape_without_calling_out()
    {
        // No client in this codebase ever supplies a key, but this proves the defense-in-depth
        // check actually works: a path-traversal-shaped key is refused before any HTTP call.
        var (storage, handler, _) = CreateStorage();

        var result = await storage.OpenReadAsync("../../etc/passwd", TestContext.Current.CancellationToken);

        result.Should().BeNull();
        handler.CallCount.Should().Be(0);
    }

    [Fact]
    public async Task OpenReadAsync_throws_on_a_genuine_server_error_rather_than_treating_it_as_missing()
    {
        var (storage, handler, _) = CreateStorage();
        handler.Respond = _ => new HttpResponseMessage(HttpStatusCode.InternalServerError);

        var act = () => storage.OpenReadAsync("0123456789abcdef0123456789abcdef.pdf", TestContext.Current.CancellationToken);

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task DeleteAsync_sends_a_delete_request_for_the_object()
    {
        var (storage, handler, _) = CreateStorage();
        handler.Respond = _ => new HttpResponseMessage(HttpStatusCode.OK);

        var key = "0123456789abcdef0123456789abcdef.pdf";
        await storage.DeleteAsync(key, TestContext.Current.CancellationToken);

        handler.Method.Should().Be(HttpMethod.Delete);
        handler.RequestUri!.ToString().Should().Be($"{BucketBaseAddress}{key}");
    }

    [Fact]
    public async Task DeleteAsync_never_throws_on_failure_so_it_cannot_mask_the_original_error()
    {
        var (storage, handler, logger) = CreateStorage();
        handler.Respond = _ => new HttpResponseMessage(HttpStatusCode.InternalServerError);

        var act = async () => await storage.DeleteAsync("0123456789abcdef0123456789abcdef.pdf", TestContext.Current.CancellationToken);

        await act.Should().NotThrowAsync();
        logger.Messages.Should().Contain(m => m.Contains("orphaned"));
    }

    [Fact]
    public async Task DeleteAsync_swallows_a_network_failure_too()
    {
        var (storage, handler, logger) = CreateStorage();
        handler.Respond = _ => throw new HttpRequestException("simulated network failure");

        var act = async () => await storage.DeleteAsync("0123456789abcdef0123456789abcdef.pdf", TestContext.Current.CancellationToken);

        await act.Should().NotThrowAsync();
        logger.Messages.Should().Contain(m => m.Contains("orphaned"));
    }

    [Fact]
    public async Task SaveAsync_propagates_cancellation_rather_than_treating_it_as_a_failure()
    {
        var (storage, _, _) = CreateStorage();
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        var act = async () =>
        {
            using var content = new MemoryStream([1]);
            await storage.SaveAsync(content, ".pdf", cts.Token);
        };

        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task Every_request_carries_the_service_role_key_and_the_key_is_never_logged()
    {
        var (storage, handler, logger) = CreateStorage();
        handler.Respond = _ => new HttpResponseMessage(HttpStatusCode.InternalServerError);

        try
        {
            using var content = new MemoryStream([1]);
            await storage.SaveAsync(content, ".pdf", TestContext.Current.CancellationToken);
        }
        catch (InvalidOperationException)
        {
            // Expected — this test only cares about headers sent and what was logged.
        }

        handler.ApiKeyHeader.Should().Be(ApiKey);
        handler.AuthorizationHeader.Should().Be(new AuthenticationHeaderValue("Bearer", ApiKey).ToString());
        logger.Messages.Should().NotContain(m => m.Contains(ApiKey));
    }

    [Fact]
    public void AddInfrastructure_throws_at_startup_when_Supabase_is_selected_but_not_fully_configured()
    {
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:Postgres"] = "Host=localhost;Database=unused;Username=unused;Password=unused",
                ["Storage:Provider"] = "Supabase",
                // Url is set, ServiceKey and Bucket are deliberately missing.
                ["Storage:Supabase:Url"] = "https://example.supabase.co",
            })
            .Build();

        var act = () => services.AddInfrastructure(configuration);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*Storage:Supabase*");
    }

    [Fact]
    public void AddInfrastructure_registers_local_disk_storage_by_default()
    {
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:Postgres"] = "Host=localhost;Database=unused;Username=unused;Password=unused",
            })
            .Build();

        // The real app host (WebApplicationBuilder) registers IConfiguration into the container
        // automatically; this bare ServiceCollection does not, so LocalDiskResumeStorage's own
        // IConfiguration dependency must be registered explicitly here to resolve at all.
        services.AddSingleton<IConfiguration>(configuration);
        services.AddInfrastructure(configuration);
        using var provider = services.BuildServiceProvider();

        provider.GetRequiredService<IResumeStorage>().Should().BeOfType<LocalDiskResumeStorage>();
    }

    [Fact]
    public void AddInfrastructure_registers_Supabase_storage_when_fully_configured()
    {
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:Postgres"] = "Host=localhost;Database=unused;Username=unused;Password=unused",
                ["Storage:Provider"] = "Supabase",
                ["Storage:Supabase:Url"] = "https://example.supabase.co",
                ["Storage:Supabase:ServiceKey"] = ApiKey,
                ["Storage:Supabase:Bucket"] = "resumes",
            })
            .Build();

        services.AddSingleton<IConfiguration>(configuration);
        services.AddInfrastructure(configuration);
        using var provider = services.BuildServiceProvider();

        provider.GetRequiredService<IResumeStorage>().Should().BeOfType<SupabaseResumeStorage>();
    }
}
