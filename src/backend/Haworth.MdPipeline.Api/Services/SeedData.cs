using Haworth.MdPipeline.Data.Identity;
using Microsoft.AspNetCore.Identity;

namespace Haworth.MdPipeline.Api.Services;

public static class SeedData
{
    public const string DevPassword = "ChangeMe123!";

    public static async Task SeedIdentityAsync(IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();

        foreach (var role in Policies.AllRoles)
        {
            if (!await roleManager.RoleExistsAsync(role))
            {
                await roleManager.CreateAsync(new IdentityRole(role));
            }
        }

        await EnsureUserAsync(userManager, "admin@mdpipeline.local", "Admin User", [Policies.Admin, Policies.Approver, Policies.Analyst, Policies.Viewer]);
        await EnsureUserAsync(userManager, "approver@mdpipeline.local", "Approver User", [Policies.Approver, Policies.Analyst, Policies.Viewer]);
        await EnsureUserAsync(userManager, "analyst@mdpipeline.local", "Analyst User", [Policies.Analyst, Policies.Viewer]);
        await EnsureUserAsync(userManager, "viewer@mdpipeline.local", "Viewer User", [Policies.Viewer]);
    }

    private static async Task EnsureUserAsync(UserManager<AppUser> userManager, string email, string displayName, IReadOnlyCollection<string> roles)
    {
        var user = await userManager.FindByEmailAsync(email);
        if (user is null)
        {
            user = new AppUser { UserName = email, Email = email, EmailConfirmed = true, DisplayName = displayName };
            await userManager.CreateAsync(user, DevPassword);
        }

        foreach (var role in roles)
        {
            if (!await userManager.IsInRoleAsync(user, role))
            {
                await userManager.AddToRoleAsync(user, role);
            }
        }
    }
}
