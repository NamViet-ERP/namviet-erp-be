using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using NamViet.ERP.Modules.Identity.Domain.Entities;

namespace NamViet.ERP.Modules.Identity.Infrastructure.Services;

public static class IdentitySeeder
{
    // Các role khớp với user_role_enum trong DBML + role HRM
    public static readonly string[] Roles =
    [
        "Admin", "BOD", "Procurement", "PM", "Accountant",
        "CSKH", "Production", "Warehouse_Keeper", "QA_QC",
        "HR", "Employee"
    ];

    public static async Task SeedAsync(
        RoleManager<IdentityRole<Guid>> roleManager,
        UserManager<ApplicationUser> userManager,
        IConfiguration configuration,
        ILogger logger)
    {
        foreach (var role in Roles)
        {
            if (!await roleManager.RoleExistsAsync(role))
            {
                await roleManager.CreateAsync(new IdentityRole<Guid>(role));
                logger.LogInformation("Created role: {Role}", role);
            }
        }

        var adminEmail = configuration["Seed:AdminEmail"] ?? "admin@namviet.vip";
        var adminPassword = configuration["Seed:AdminPassword"] ?? "Admin@12345";
        var adminCode = configuration["Seed:AdminEmployeeCode"] ?? "NV-0001";

        var existingAdmin = await userManager.FindByEmailAsync(adminEmail);
        if (existingAdmin is null)
        {
            var admin = new ApplicationUser
            {
                EmployeeCode = adminCode,
                Name = "System Admin",
                Email = adminEmail,
                UserName = adminEmail,
                EmailConfirmed = true
            };
            var result = await userManager.CreateAsync(admin, adminPassword);
            if (result.Succeeded)
            {
                await userManager.AddToRoleAsync(admin, "Admin");
                logger.LogInformation("Created default admin user: {Email}", adminEmail);
            }
            else
            {
                logger.LogError("Failed to create admin user: {Errors}",
                    string.Join(", ", result.Errors.Select(e => e.Description)));
            }
        }
    }
}
