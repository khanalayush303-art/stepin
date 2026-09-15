using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using StepIn.Application.Common.Interfaces;
using StepIn.Infrastructure.Identity;
using StepIn.Infrastructure.Persistence;
using Testcontainers.PostgreSql;

namespace StepIn.Api.Tests;

/// <summary>
/// Boots the real pipeline against an ephemeral PostgreSQL container, since
/// Identity's register/login/lockout/token behavior needs a genuine database.
/// Requires a Docker daemon to be running.
/// </summary>
public sealed class AuthApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
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

        builder.ConfigureTestServices(services =>
        {
            services.AddSingleton<IEmailSender, RecordingEmailSender>();
        });
    }

    public RecordingEmailSender EmailSender => (RecordingEmailSender)Services.GetRequiredService<IEmailSender>();

    public async ValueTask InitializeAsync()
    {
        await _container.StartAsync();

        using var scope = Services.CreateScope();

        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        await db.Database.MigrateAsync();

        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole<Guid>>>();
        await RoleSeeder.SeedAsync(roleManager);
    }

    public new async ValueTask DisposeAsync()
    {
        await _container.DisposeAsync();
        await base.DisposeAsync();
    }
}
