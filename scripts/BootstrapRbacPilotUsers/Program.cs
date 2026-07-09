using System.Text.RegularExpressions;
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
const string EmployeeCodePrefix = "NV-";
const int EmployeeCodeDigits = 4;

var pilotUsers = new[]
{
    new PilotUser(
        "admin@namviet.vn", "Admin Test", "Admin", "NV-0001",
        "Vận hành", "Hà Nội", "Quản trị hệ thống", Gender.Male),
    new PilotUser(
        "hr@namviet.vn", "HR Test", "HR", "NV-0002",
        "HCNS", "Hà Nội", "Chuyên viên nhân sự", Gender.Male),
    new PilotUser(
        "staff@namviet.vn", "Staff Test", RolePermissionTemplates.StaffRole, "NV-0003",
        "Xây dựng", "Hải Phòng", "Nhân viên kho", Gender.Male),
};

var apiConfigDir = Path.GetFullPath(Path.Combine(
    AppContext.BaseDirectory, "..", "..", "..", "..", "..", "src", "ApiHost", "NamViet.ERP.Api"));

var configuration = new ConfigurationBuilder()
    .SetBasePath(apiConfigDir)
    .AddJsonFile("appsettings.json", optional: true)
    .AddJsonFile("appsettings.Development.json", optional: true)
    .AddUserSecrets("namviet-erp-api")
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
var positionIds = await EnsurePilotPositionsAsync(hrDb, departmentId, pilotUsers);

Console.WriteLine("RBAC pilot test users (dev only):");
Console.WriteLine($"Password for all: {DefaultPassword}");
Console.WriteLine($"Mã NV chuẩn: {EmployeeCodePrefix}XXXX (VD: NV-0001)");
Console.WriteLine();

foreach (var spec in pilotUsers)
{
    var positionId = positionIds[spec.PositionName];
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

    await FreeEmployeeCodeAsync(hrDb, identityDb, userManager, spec.EmployeeCode, spec.Email);

    var employee = await hrDb.Employees
        .FirstOrDefaultAsync(e => e.DeletedAt == null && (e.Email == spec.Email || e.Code == spec.EmployeeCode));

    if (employee is null)
    {
        employee = new Employee
        {
            Code = spec.EmployeeCode,
            FullName = spec.Name,
            Email = spec.Email,
            Gender = spec.Gender,
            BirthDate = new DateOnly(1995, 1, 1),
            DepartmentId = departmentId,
            PositionId = positionId,
            Segment = spec.Segment,
            Branch = spec.Branch,
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
    else
    {
        var changed = false;
        if (!string.Equals(employee.Code, spec.EmployeeCode, StringComparison.Ordinal))
        {
            employee.Code = spec.EmployeeCode;
            changed = true;
        }
        if (employee.UserId != user.Id)
        {
            employee.UserId = user.Id;
            changed = true;
        }
        if (employee.Email != spec.Email)
        {
            employee.Email = spec.Email;
            changed = true;
        }
        if (employee.DepartmentId != departmentId)
        {
            employee.DepartmentId = departmentId;
            changed = true;
        }
        if (employee.PositionId != positionId)
        {
            employee.PositionId = positionId;
            changed = true;
        }
        if (employee.Segment != spec.Segment)
        {
            employee.Segment = spec.Segment;
            changed = true;
        }
        if (employee.Branch != spec.Branch)
        {
            employee.Branch = spec.Branch;
            changed = true;
        }
        if (employee.Gender != spec.Gender)
        {
            employee.Gender = spec.Gender;
            changed = true;
        }
        if (changed)
        {
            employee.UpdatedAt = DateTime.UtcNow;
            await hrDb.SaveChangesAsync();
            Console.WriteLine($"  → updated employee {employee.Code} (id={employee.Id})");
        }
        else
        {
            Console.WriteLine($"  → employee {employee.Code} already up to date");
        }
    }

    if (!string.Equals(user.EmployeeCode, spec.EmployeeCode, StringComparison.Ordinal))
    {
        user.EmployeeCode = spec.EmployeeCode;
        user.UpdatedAt = DateTime.UtcNow;
        await userManager.UpdateAsync(user);
        Console.WriteLine($"  → updated identity code → {spec.EmployeeCode}");
    }

    Console.WriteLine($"  Login: {spec.Email} / {DefaultPassword} / role={spec.Role}");
    Console.WriteLine();
}

await RepairLegacyPilotEmployeesAsync(hrDb, userManager, departmentId);

return 0;

static async Task FreeEmployeeCodeAsync(
    HrDbContext hrDb,
    IdentityDbContext identityDb,
    UserManager<ApplicationUser> userManager,
    string targetCode,
    string keepEmail)
{
    var hrConflicts = await hrDb.Employees
        .Where(e => e.DeletedAt == null && e.Code == targetCode && e.Email != keepEmail)
        .ToListAsync();
    foreach (var emp in hrConflicts)
    {
        var temp = $"LEGACY-HR-{emp.Id}";
        Console.WriteLine($"  → free HR code {targetCode}: employee id={emp.Id} → {temp}");
        emp.Code = temp;
        emp.UpdatedAt = DateTime.UtcNow;
    }
    if (hrConflicts.Count > 0)
        await hrDb.SaveChangesAsync();

    var keepUser = await userManager.FindByEmailAsync(keepEmail);
    var keepUserId = keepUser?.Id;

    var identityConflicts = await identityDb.Users
        .Where(u => u.DeletedAt == null && u.EmployeeCode == targetCode && (keepUserId == null || u.Id != keepUserId))
        .ToListAsync();
    foreach (var u in identityConflicts)
    {
        var temp = $"LEGACY-U-{u.Id.ToString()[..8]}";
        Console.WriteLine($"  → free identity code {targetCode}: {u.Email} → {temp}");
        u.EmployeeCode = temp;
        u.UpdatedAt = DateTime.UtcNow;
        await userManager.UpdateAsync(u);
    }
}

static async Task RepairLegacyPilotEmployeesAsync(
    HrDbContext hrDb,
    UserManager<ApplicationUser> userManager,
    int departmentId)
{
    var pilotEmails = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "admin@namviet.vn",
        "hr@namviet.vn",
        "staff@namviet.vn",
    };

    var legacy = await hrDb.Employees
        .Where(e => e.DeletedAt == null && e.DepartmentId == departmentId)
        .Where(e => !pilotEmails.Contains(e.Email ?? string.Empty))
        .OrderBy(e => e.Id)
        .ToListAsync();

    if (legacy.Count == 0)
        return;

    var usedCodes = new HashSet<string>(
        (await hrDb.Employees
            .Where(e => e.DeletedAt == null)
            .Select(e => e.Code)
            .ToListAsync())
            .Where(IsValidEmployeeCode),
        StringComparer.OrdinalIgnoreCase);

    Console.WriteLine($"Repairing {legacy.Count} legacy pilot employee(s) with non-standard codes…");

    foreach (var employee in legacy)
    {
        if (IsValidEmployeeCode(employee.Code))
            continue;

        var newCode = AllocateNextCode(usedCodes);
        var oldCode = employee.Code;
        employee.Code = newCode;
        employee.Segment ??= "Xây dựng";
        employee.Branch ??= "Hà Nội";
        employee.PositionId ??= await EnsureDefaultPositionAsync(hrDb, departmentId);
        employee.UpdatedAt = DateTime.UtcNow;

        if (employee.UserId is Guid userId)
        {
            var user = await userManager.FindByIdAsync(userId.ToString());
            if (user is not null && !string.Equals(user.EmployeeCode, newCode, StringComparison.Ordinal))
            {
                user.EmployeeCode = newCode;
                user.UpdatedAt = DateTime.UtcNow;
                await userManager.UpdateAsync(user);
            }
        }

        await hrDb.SaveChangesAsync();
        Console.WriteLine($"  → {oldCode} → {newCode} ({employee.FullName})");
    }
}

static async Task<int> EnsureDefaultPositionAsync(HrDbContext hrDb, int departmentId)
{
    const string name = "Nhân viên";
    var existing = await hrDb.Positions
        .FirstOrDefaultAsync(p => p.DeletedAt == null && p.DepartmentId == departmentId && p.Name == name);
    if (existing is not null)
        return existing.Id;

    var position = new Position
    {
        Name = name,
        DepartmentId = departmentId,
        Level = "Nhân viên",
        CreatedAt = DateTime.UtcNow,
        UpdatedAt = DateTime.UtcNow,
    };
    hrDb.Positions.Add(position);
    await hrDb.SaveChangesAsync();
    return position.Id;
}

static async Task<Dictionary<string, int>> EnsurePilotPositionsAsync(
    HrDbContext hrDb,
    int departmentId,
    IReadOnlyList<PilotUser> users)
{
    var map = new Dictionary<string, int>(StringComparer.Ordinal);
    foreach (var name in users.Select(u => u.PositionName).Distinct(StringComparer.Ordinal))
    {
        var existing = await hrDb.Positions
            .FirstOrDefaultAsync(p => p.DeletedAt == null && p.DepartmentId == departmentId && p.Name == name);
        if (existing is not null)
        {
            map[name] = existing.Id;
            continue;
        }

        var position = new Position
        {
            Name = name,
            DepartmentId = departmentId,
            Level = name.Contains("Quản trị", StringComparison.Ordinal) ? "Quản lý" : "Nhân viên",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };
        hrDb.Positions.Add(position);
        await hrDb.SaveChangesAsync();
        map[name] = position.Id;
        Console.WriteLine($"Created position \"{name}\" (id={position.Id}).");
    }

    return map;
}

static bool IsValidEmployeeCode(string? code) =>
    !string.IsNullOrWhiteSpace(code)
    && Regex.IsMatch(code.Trim(), @"^NV-\d{4}$", RegexOptions.CultureInvariant);

static string AllocateNextCode(HashSet<string> usedCodes)
{
    var max = 0;
    foreach (var code in usedCodes)
    {
        var match = Regex.Match(code, @"(\d+)$");
        if (match.Success && int.TryParse(match.Groups[1].Value, out var n) && n > max)
            max = n;
    }

    while (true)
    {
        max++;
        var candidate = $"{EmployeeCodePrefix}{max.ToString().PadLeft(EmployeeCodeDigits, '0')}";
        if (usedCodes.Add(candidate))
            return candidate;
    }
}

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
    string EmployeeCode,
    string Segment,
    string Branch,
    string PositionName,
    Gender Gender);
