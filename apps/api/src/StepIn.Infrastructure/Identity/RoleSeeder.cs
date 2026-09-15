using Microsoft.AspNetCore.Identity;

namespace StepIn.Infrastructure.Identity;

/// <summary>Idempotent role creation. Roles are not secret, so this always runs.</summary>
public static class RoleSeeder
{
    public static async Task SeedAsync(RoleManager<IdentityRole<Guid>> roleManager)
    {
        ArgumentNullException.ThrowIfNull(roleManager);

        foreach (var name in RoleNames.All)
        {
            if (!await roleManager.RoleExistsAsync(name))
            {
                await roleManager.CreateAsync(new IdentityRole<Guid>(name) { Id = Guid.CreateVersion7() });
            }
        }
    }
}
