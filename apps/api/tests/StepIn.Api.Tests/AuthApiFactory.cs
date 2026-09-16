using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;
using StepIn.Infrastructure.Persistence;
using Testcontainers.PostgreSql;

namespace StepIn.Api.Tests;

/// <summary>
/// Boots the real pipeline against an ephemeral PostgreSQL container, since
/// user-sync and role authorization are persistence-backed behavior. JWT
/// bearer auth is pointed at a locally-signed test key instead of a real
/// Clerk tenant — <see cref="IssueToken"/> mints tokens shaped like Clerk's so
/// the real validation/claims-transformation pipeline still runs end to end.
/// Requires a Docker daemon to be running.
/// </summary>
public sealed class AuthApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    public const string TestIssuer = "https://test.clerk.accounts.dev";
    public const string AuthorizedParty = "http://localhost:3000";

    private static readonly SymmetricSecurityKey SigningKey =
        new(Encoding.UTF8.GetBytes("test-only-signing-key-at-least-32-bytes-long"));

    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder("postgres:17-alpine")
        .WithDatabase("stepin_test")
        .WithUsername("stepin")
        .WithPassword("stepin")
        .Build();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.UseEnvironment("Testing");
        builder.UseSetting("ConnectionStrings:Postgres", _container.GetConnectionString());
        builder.UseSetting("Cors:AllowedOrigins:0", "http://localhost:3000");
        builder.UseSetting("Clerk:Authority", TestIssuer);
        builder.UseSetting("Clerk:AuthorizedParties:0", AuthorizedParty);

        builder.ConfigureTestServices(services =>
        {
            // Skip real OIDC discovery against Clerk and validate against a
            // static, locally-known key/issuer instead — the same substitution
            // Google OAuth needed before real credentials existed.
            services.PostConfigure<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme, options =>
            {
                options.Authority = null;
                options.RequireHttpsMetadata = false;
                options.Configuration = new OpenIdConnectConfiguration();
                options.TokenValidationParameters.ValidateIssuer = true;
                options.TokenValidationParameters.ValidIssuer = TestIssuer;
                options.TokenValidationParameters.ValidateIssuerSigningKey = true;
                options.TokenValidationParameters.IssuerSigningKey = SigningKey;
            });
        });
    }

    public async ValueTask InitializeAsync()
    {
        await _container.StartAsync();

        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        await db.Database.MigrateAsync();
    }

    public new async ValueTask DisposeAsync()
    {
        await _container.DisposeAsync();
        await base.DisposeAsync();
    }

    public static string IssueToken(
        string clerkUserId,
        string email,
        string firstName = "Test",
        string lastName = "User",
        string? azp = AuthorizedParty,
        bool emailVerified = true,
        DateTime? expires = null)
    {
        List<Claim> claims =
        [
            new(JwtRegisteredClaimNames.Sub, clerkUserId),
            new("email", email),
            new("email_verified", emailVerified ? "true" : "false"),
            new("given_name", firstName),
            new("family_name", lastName),
        ];

        if (azp is not null)
        {
            claims.Add(new Claim("azp", azp));
        }

        var token = new JwtSecurityToken(
            issuer: TestIssuer,
            claims: claims,
            expires: expires ?? DateTime.UtcNow.AddMinutes(5),
            signingCredentials: new SigningCredentials(SigningKey, SecurityAlgorithms.HmacSha256));

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
