using System.Net;
using System.Text.Json;
using FluentAssertions;

namespace StepIn.Api.Tests;

public sealed class ApiContractTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _factory = factory;

    [Fact]
    public async Task OpenApi_document_is_served_and_is_valid_json()
    {
        using var client = _factory.CreateClient();

        var response = await client.GetAsync(new Uri("/openapi/v1.json", UriKind.Relative), TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        using var document = JsonDocument.Parse(body);
        document.RootElement.TryGetProperty("openapi", out _).Should().BeTrue();
        document.RootElement.GetProperty("paths").GetRawText().Should().Contain("/api/v1/meta");
    }

    [Fact]
    public async Task Meta_endpoint_reports_the_phase_and_environment()
    {
        using var client = _factory.CreateClient();

        var response = await client.GetAsync(new Uri("/api/v1/meta", UriKind.Relative), TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        using var document = JsonDocument.Parse(body);
        document.RootElement.GetProperty("phase").GetInt32().Should().Be(0);
        document.RootElement.GetProperty("environment").GetString().Should().Be("Testing");
    }

    [Fact]
    public async Task Cors_preflight_is_allowed_for_the_configured_frontend_origin()
    {
        using var client = _factory.CreateClient();

        using var request = new HttpRequestMessage(HttpMethod.Options, new Uri("/api/v1/meta", UriKind.Relative));
        request.Headers.Add("Origin", "http://localhost:3000");
        request.Headers.Add("Access-Control-Request-Method", "GET");

        var response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        response.Headers.TryGetValues("Access-Control-Allow-Origin", out var allowed).Should().BeTrue();
        allowed.Should().Contain("http://localhost:3000");
    }

    [Fact]
    public async Task Cors_preflight_is_refused_for_an_unknown_origin()
    {
        using var client = _factory.CreateClient();

        using var request = new HttpRequestMessage(HttpMethod.Options, new Uri("/api/v1/meta", UriKind.Relative));
        request.Headers.Add("Origin", "https://not-our-frontend.example");
        request.Headers.Add("Access-Control-Request-Method", "GET");

        var response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        response.Headers.Contains("Access-Control-Allow-Origin").Should().BeFalse();
    }

    [Fact]
    public async Task Unknown_route_returns_a_problem_document_not_an_html_page()
    {
        using var client = _factory.CreateClient();

        var response = await client.GetAsync(new Uri("/api/v1/does-not-exist", UriKind.Relative), TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        response.Content.Headers.ContentType?.MediaType.Should().Be("application/problem+json");
    }
}
