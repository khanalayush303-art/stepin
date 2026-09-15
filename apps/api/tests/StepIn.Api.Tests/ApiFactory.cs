using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;

namespace StepIn.Api.Tests;

/// <summary>
/// Boots the real pipeline in memory.
///
/// The connection string points at a database that does not need to exist: every
/// test here is about wiring (routing, OpenAPI, CORS, problem details), and the
/// readiness probe is expected to report the database as down. Tests that need a
/// live PostgreSQL arrive with the first real repository, using Testcontainers.
/// </summary>
public sealed class ApiFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.UseEnvironment("Testing");

        builder.UseSetting(
            "ConnectionStrings:Postgres",
            "Host=127.0.0.1;Port=59999;Database=stepin_test;Username=stepin;Password=stepin;Timeout=1;Command Timeout=1");
        builder.UseSetting("Cors:AllowedOrigins:0", "http://localhost:3000");
    }
}
