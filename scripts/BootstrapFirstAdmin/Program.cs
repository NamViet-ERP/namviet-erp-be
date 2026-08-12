using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NamViet.ERP.Modules.Identity.Domain.Entities;
using NamViet.ERP.Modules.Identity.Infrastructure;
using NamViet.ERP.Modules.Identity.Infrastructure.Persistence;

static string? ArgValue(string[] args, string name)
{
    for (var i = 0; i < args.Length - 1; i++)
    {
        if (string.Equals(args[i], name, StringComparison.OrdinalIgnoreCase))
            return args[i + 1];
    }
    return null;
}

var email = ArgValue(args, "--email")?.Trim().ToLowerInvariant();
var password = ArgValue(args, "--password");
var name = ArgValue(args, "--name")?.Trim();
var employeeCode = ArgValue(args, "--employee-code")?.Trim() ?? "SYS-ADMIN";

if (string.IsNullOrEmpty(email) || string.IsNullOrEmpty(password) || string.IsNullOrEmpty(name))
{
    Console.Error.WriteLine(
        "Usage: dotnet run -- --email <email> --name <full name> --password <password> [--employee-code SYS-ADMIN]");
    Console.Error.WriteLine("Requires env ConnectionStrings__Default (PostgreSQL).");
    return 1;
}

var configuration = new ConfigurationBuilder()
    .AddEnvironmentVariables()
    .Build();

var connectionString = configuration.GetConnectionString("Default");
if (string.IsNullOrWhiteSpace(connectionString))
{
    Console.Error.WriteLine("Missing ConnectionStrings__Default environment variable.");
    return 1;
}

var services = new ServiceCollection();
services.AddLogging(b => b.AddConsole().SetMinimumLevel(LogLevel.Warning));
services.AddIdentityModule(configuration);

await using var provider = services.BuildServiceProvider();
await using var scope = provider.CreateAsyncScope();
var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<ApplicationRole>>();
var db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();

await db.Database.MigrateAsync();

const string adminRole = "Admin";
if (!await roleManager.RoleExistsAsync(adminRole))
{
    var createRole = await roleManager.CreateAsync(new ApplicationRole
    {
        Name = adminRole,
        Description = "Quản trị viên",
        Module = "General",
        IsActive = true,
    });
    if (!createRole.Succeeded)
    {
        Console.Error.WriteLine("Failed to create Admin role: " +
            string.Join("; ", createRole.Errors.Select(e => e.Description)));
        return 1;
    }
    Console.WriteLine("Created role Admin.");
}

var forceReset = args.Any(a => string.Equals(a, "--force-reset", StringComparison.OrdinalIgnoreCase));
var existing = await userManager.FindByEmailAsync(email);
if (existing is not null)
{
    if (!forceReset)
    {
        Console.WriteLine($"User '{email}' already exists — skipping (idempotent). Pass --force-reset to update password.");
        return 0;
    }

    existing.Name = name;
    existing.IsActive = true;
    existing.EmailConfirmed = true;
    existing.DeletedAt = null;
    await userManager.UpdateAsync(existing);
    var token = await userManager.GeneratePasswordResetTokenAsync(existing);
    var reset = await userManager.ResetPasswordAsync(existing, token, password);
    if (!reset.Succeeded)
    {
        Console.Error.WriteLine("Failed to reset password: " +
            string.Join("; ", reset.Errors.Select(e => e.Description)));
        return 1;
    }
    if (!await userManager.IsInRoleAsync(existing, adminRole))
        await userManager.AddToRoleAsync(existing, adminRole);
    Console.WriteLine($"Reset password for admin user '{email}'.");
    return 0;
}

var user = new ApplicationUser
{
    EmployeeCode = employeeCode,
    Name = name,
    Email = email,
    UserName = email,
    EmailConfirmed = true,
    IsActive = true,
};

var createUser = await userManager.CreateAsync(user, password);
if (!createUser.Succeeded)
{
    Console.Error.WriteLine("Failed to create user: " +
        string.Join("; ", createUser.Errors.Select(e => e.Description)));
    return 1;
}

await userManager.AddToRoleAsync(user, adminRole);
Console.WriteLine($"Created admin user '{email}' (Id={user.Id}).");
return 0;
