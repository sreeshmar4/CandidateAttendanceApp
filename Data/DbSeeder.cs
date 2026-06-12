using Microsoft.AspNetCore.Identity;

namespace CandidateAttendanceApp.Data;

public static class DbSeeder
{
    public static async Task SeedRolesAsync(IServiceProvider services)
    {
        var roleManager = services.GetRequiredService<RoleManager<IdentityRole>>();
        var userManager = services.GetRequiredService<UserManager<IdentityUser>>();

        string[] roles = { "Admin", "User" };

        foreach (var role in roles)
        {
            if (!await roleManager.RoleExistsAsync(role))
            {
                var createRoleResult = await roleManager.CreateAsync(new IdentityRole(role));
                if (!createRoleResult.Succeeded)
                {
                    var errors = string.Join(" | ", createRoleResult.Errors.Select(e => e.Description));
                    throw new InvalidOperationException($"Failed to create role '{role}': {errors}");
                }
            }
        }

        string adminEmail = "admin@attendance.com";

        var admin = await userManager.FindByEmailAsync(adminEmail);

        if (admin == null)
        {
            admin = new IdentityUser
            {
                UserName = adminEmail,
                Email = adminEmail,
                EmailConfirmed = true
            };

            var result = await userManager.CreateAsync(admin, "Admin@123");

            if (result.Succeeded)
            {
                await userManager.AddToRoleAsync(admin, "Admin");
            }
            else
            {
                foreach (var error in result.Errors)
                {
                    Console.WriteLine(error.Description);
                }

                var errors = string.Join(" | ", result.Errors.Select(e => e.Description));
                throw new InvalidOperationException($"Failed to create admin user '{adminEmail}': {errors}");
            }
        }
        else
        {
            var needsUpdate = false;
            if (!admin.EmailConfirmed)
            {
                admin.EmailConfirmed = true;
                needsUpdate = true;
            }
            if (!string.Equals(admin.UserName, adminEmail, StringComparison.OrdinalIgnoreCase))
            {
                admin.UserName = adminEmail;
                needsUpdate = true;
            }

            if (needsUpdate)
            {
                var updateResult = await userManager.UpdateAsync(admin);
                if (!updateResult.Succeeded)
                {
                    var errors = string.Join(" | ", updateResult.Errors.Select(e => e.Description));
                    throw new InvalidOperationException($"Failed to update admin user '{adminEmail}': {errors}");
                }
            }
        }

        if (!await userManager.IsInRoleAsync(admin, "Admin"))
        {
            var addRoleResult = await userManager.AddToRoleAsync(admin, "Admin");
            if (!addRoleResult.Succeeded)
            {
                var errors = string.Join(" | ", addRoleResult.Errors.Select(e => e.Description));
                throw new InvalidOperationException($"Failed to assign '{adminEmail}' to role 'Admin': {errors}");
            }
        }
        // ------------------------
        // Seed User
        // ------------------------
        string userEmail = "user@attendance.com";

        var user = await userManager.FindByEmailAsync(userEmail);

        if (user == null)
        {
            user = new IdentityUser
            {
                UserName = userEmail,
                Email = userEmail,
                EmailConfirmed = true
            };

            var result1 = await userManager.CreateAsync(user, "User@123");

            if (result1.Succeeded)
            {
                await userManager.AddToRoleAsync(user, "User");
            }
            else
            {
                var errors = string.Join(" | ", result1.Errors.Select(e => e.Description));
                throw new InvalidOperationException($"Failed to create user '{userEmail}': {errors}");
            }
        }
        else
        {
            if (!await userManager.IsInRoleAsync(user, "User"))
            {
                var addRoleResult = await userManager.AddToRoleAsync(user, "User");
                if (!addRoleResult.Succeeded)
                {
                    var errors = string.Join(" | ", addRoleResult.Errors.Select(e => e.Description));
                    throw new InvalidOperationException($"Failed to assign '{userEmail}' to role 'User': {errors}");
                }
            }
        }
    }
}