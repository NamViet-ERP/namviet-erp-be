using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NamViet.ERP.Modules.Hr.Domain.Entities;
using NamViet.ERP.Modules.Hr.Infrastructure;
using NamViet.ERP.Modules.Hr.Infrastructure.Persistence;
using NamViet.ERP.Modules.Identity.Domain.Entities;
using NamViet.ERP.Modules.Identity.Infrastructure;
using NamViet.ERP.Modules.Identity.Infrastructure.Persistence;
using NamViet.ERP.SharedKernel.Authorization;

// Identity: min 8 chars, digit, uppercase.
const string DefaultPassword = "Test@123";

var pilotUsers = new[]
{
    new PilotUser("admin@namviet.vn", "Admin Test", "Admin", "NV-ADMIN"),
    new PilotUser("hr@namviet.vn", "HR Test", "HR", "NV-HR"),
    new PilotUser("staff@namviet.vn", "Staff Test", RolePermissionTemplates.StaffRole, "NV-STAFF"),
};

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
services.AddHrModule(configuration);

await using var provider = services.BuildServiceProvider();
await using var scope = provider.CreateAsyncScope();

var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<ApplicationRole>>();
var identityDb = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
var hrDb = scope.ServiceProvider.GetRequiredService<HrDbContext>();

await identityDb.Database.MigrateAsync();
await hrDb.Database.MigrateAsync();

foreach (var roleName in pilotUsers.Select(u => u.Role).Distinct(StringComparer.Ordinal))
    await EnsureRoleAsync(roleManager, roleName);

var departmentId = await EnsurePilotDepartmentAsync(hrDb);

Console.WriteLine("RBAC pilot test users (dev only):");
Console.WriteLine($"Password for all: {DefaultPassword}");
Console.WriteLine();

foreach (var spec in pilotUsers)
{
    var user = await userManager.FindByEmailAsync(spec.Email);
    if (user is null)
    {
        user = new ApplicationUser
        {
            EmployeeCode = spec.EmployeeCode,
            Name = spec.Name,
            Email = spec.Email,
            UserName = spec.Email,
            EmailConfirmed = true,
            IsActive = true,
        };

        var create = await userManager.CreateAsync(user, DefaultPassword);
        if (!create.Succeeded)
        {
            Console.Error.WriteLine($"Failed to create {spec.Email}: {FormatErrors(create.Errors)}");
            return 1;
        }

        Console.WriteLine($"Created user {spec.Email}");
    }
    else
    {
        Console.WriteLine($"User {spec.Email} already exists — resetting password.");
        var token = await userManager.GeneratePasswordResetTokenAsync(user);
        var reset = await userManager.ResetPasswordAsync(user, token, DefaultPassword);
        if (!reset.Succeeded)
        {
            Console.Error.WriteLine($"Failed to reset password for {spec.Email}: {FormatErrors(reset.Errors)}");
            return 1;
        }
    }

    if (!await userManager.IsInRoleAsync(user, spec.Role))
    {
        await userManager.AddToRoleAsync(user, spec.Role);
        Console.WriteLine($"  → assigned role {spec.Role}");
    }

    if (user.EmployeeCode != spec.EmployeeCode)
    {
        user.EmployeeCode = spec.EmployeeCode;
        user.UpdatedAt = DateTime.UtcNow;
        await userManager.UpdateAsync(user);
    }

    var employee = await hrDb.Employees
        .FirstOrDefaultAsync(e => e.DeletedAt == null && (e.Code == spec.EmployeeCode || e.Email == spec.Email));

    if (employee is null)
    {
        employee = new Employee
        {
            Code = spec.EmployeeCode,
            FullName = spec.Name,
            Email = spec.Email,
            Gender = Gender.Other,
            BirthDate = new DateOnly(1995, 1, 1),
            DepartmentId = departmentId,
            JoinDate = DateOnly.FromDateTime(DateTime.UtcNow),
            Status = EmployeeStatus.Active,
            Education = Education.University,
            UserId = user.Id,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };
        hrDb.Employees.Add(employee);
        await hrDb.SaveChangesAsync();
        Console.WriteLine($"  → created & linked employee {spec.EmployeeCode} (id={employee.Id})");
    }
    else if (employee.UserId != user.Id)
    {
        employee.UserId = user.Id;
        employee.Email ??= spec.Email;
        employee.Code = spec.EmployeeCode;
        employee.UpdatedAt = DateTime.UtcNow;
        await hrDb.SaveChangesAsync();
        Console.WriteLine($"  → linked employee {employee.Code} (id={employee.Id})");
    }
    else
    {
        Console.WriteLine($"  → employee {employee.Code} already linked");
    }

    Console.WriteLine($"  Login: {spec.Email} / {DefaultPassword} / role={spec.Role}");
    Console.WriteLine();
}

return 0;

static async Task EnsureRoleAsync(RoleManager<ApplicationRole> roleManager, string roleName)
{
    if (await roleManager.RoleExistsAsync(roleName))
        return;

    var result = await roleManager.CreateAsync(new ApplicationRole
    {
        Name = roleName,
        Description = roleName,
        Module = "General",
        IsActive = true,
    });

    if (!result.Succeeded)
        throw new InvalidOperationException($"Failed to create role {roleName}: {FormatErrors(result.Errors)}");

    Console.WriteLine($"Created role {roleName}.");
}

static async Task<int> EnsurePilotDepartmentAsync(HrDbContext hrDb)
{
    const string code = "PB-RBAC";
    var dept = await hrDb.Departments
        .FirstOrDefaultAsync(d => d.DeletedAt == null && d.Code == code);

    if (dept is not null)
        return dept.Id;

    dept = new Department
    {
        Code = code,
        Name = "Phòng RBAC Pilot (dev)",
        Block = "HCNS",
        IsActive = true,
        CreatedAt = DateTime.UtcNow,
        UpdatedAt = DateTime.UtcNow,
    };
    hrDb.Departments.Add(dept);
    await hrDb.SaveChangesAsync();
    Console.WriteLine($"Created department {code} (id={dept.Id}).");
    return dept.Id;
}

static string FormatErrors(IEnumerable<IdentityError> errors) =>
    string.Join("; ", errors.Select(e => e.Description));

internal sealed record PilotUser(
    string Email,
    string Name,
    string Role,
    string EmployeeCode);
