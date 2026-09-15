using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using StepIn.Domain.Common;

namespace StepIn.Infrastructure.Identity;

/// <summary>
/// Creates one local admin account for local development only, and only when
/// explicitly opted in. Never runs outside Development; the password always
/// comes from user-secrets or the environment, never from source control.
/// </summary>
public static class DevAdminSeeder
{
    public static async Task SeedAsync(
        IHostEnvironment environment,
        IConfiguration configuration,
        UserManager<ApplicationUser> userManager,
        ILogger logger)
    {
        ArgumentNullException.ThrowIfNull(environment);
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(userManager);
        ArgumentNullException.ThrowIfNull(logger);

        if (!environment.IsDevelopment())
        {
            return;
        }

        var section = configuration.GetSection("Seed:DevAdmin");

        if (!section.GetValue("Enabled", false))
        {
            return;
        }

        var email = section["Email"];
        var password = section["Password"];

        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
        {
            logger.LogWarning("Seed:DevAdmin:Enabled is true but Email/Password is missing; skipping dev admin seed");
            return;
        }

        if (await userManager.FindByEmailAsync(email) is not null)
        {
            return;
        }

        var admin = new ApplicationUser
        {
            Id = Guid.CreateVersion7(),
            UserName = email,
            Email = email,
            EmailConfirmed = true,
            FirstName = "Dev",
            LastName = "Admin",
            AccountStatus = AccountStatus.Active,
        };

        var result = await userManager.CreateAsync(admin, password);

        if (result.Succeeded)
        {
            await userManager.AddToRoleAsync(admin, RoleNames.Admin);
            logger.LogInformation("Seeded development admin account {Email}", email);
        }
        else
        {
            logger.LogWarning(
                "Failed to seed development admin account: {Errors}",
                string.Join("; ", result.Errors.Select(e => e.Description)));
        }
    }
}
