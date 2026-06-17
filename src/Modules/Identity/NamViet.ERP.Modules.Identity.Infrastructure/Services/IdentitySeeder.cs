using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using NamViet.ERP.Modules.Hr.Domain.Entities;
using NamViet.ERP.Modules.Hr.Infrastructure.Persistence;
using NamViet.ERP.Modules.Identity.Domain.Entities;

namespace NamViet.ERP.Modules.Identity.Infrastructure.Services;

public static class IdentitySeeder
{
    private static readonly (string Name, string Module, string Description)[] RoleDefs =
    [
        ("Admin", "Admin", "Quản trị hệ thống"),
        ("BOD", "Admin", "Ban giám đốc"),
        ("Procurement", "WMS", "Phòng mua hàng"),
        ("PM", "WMS", "Quản lý dự án"),
        ("Accountant", "WMS", "Kế toán trưởng"),
        ("CSKH", "HR", "Chăm sóc khách hàng"),
        ("Production", "WMS", "Sản xuất"),
        ("Warehouse_Keeper", "WMS", "Thủ kho"),
        ("QA_QC", "WMS", "Kiểm soát chất lượng"),
        ("HR", "HR", "Nhân sự"),
        ("Employee", "General", "Nhân viên")
    ];

    public static async Task SeedAsync(
        RoleManager<ApplicationRole> roleManager,
        UserManager<ApplicationUser> userManager,
        IConfiguration configuration,
        ILogger logger)
    {
        foreach (var (name, module, description) in RoleDefs)
        {
            var existing = await roleManager.FindByNameAsync(name);
            if (existing is null)
            {
                await roleManager.CreateAsync(new ApplicationRole
                {
                    Name = name,
                    Module = module,
                    Description = description,
                    IsActive = true
                });
                logger.LogInformation("Created role: {Role}", name);
            }
            else if (string.IsNullOrEmpty(existing.Module) || existing.Module == "General")
            {
                existing.Module = module;
                existing.Description = description;
                await roleManager.UpdateAsync(existing);
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

    public static async Task SeedDemoMembersAsync(
        UserManager<ApplicationUser> userManager,
        HrDbContext hrDb,
        ILogger logger)
    {
        var defaultDept = await EnsureDefaultDepartmentAsync(hrDb);

        var members = new (string Code, string Name, string Role, string Email, string Phone, string JoinedDate, string Password)[]
        {
            ("NV-0021", "Phan Văn Vỹ", "Warehouse_Keeper", "vy.phan@namviet.vn", "0901.234.567", "12/03/2022", "Demo@12345"),
            ("NV-0043", "Nguyễn Văn Hùng", "PM", "hung.nguyen@namviet.vn", "0988.765.432", "15/07/2021", "Demo@12345"),
            ("NV-0012", "Lê Thị Hồng", "Accountant", "hong.le@namviet.vn", "0912.345.678", "01/01/2020", "Demo@12345"),
            ("NV-0034", "Mỹ Chi", "Procurement", "chi.my@namviet.vn", "0934.567.890", "10/11/2023", "Demo@12345"),
            ("NV-0089", "Trần Huy Hoàng", "Warehouse_Keeper", "hoang.tran@namviet.vn", "0909.888.999", "05/05/2024", "Demo@12345"),
            ("NV-0005", "Nguyễn Minh Tuấn", "Admin", "tuan.nguyen@namviet.vn", "0902.999.888", "01/06/2019", "Demo@12345"),
        };

        foreach (var m in members)
        {
            if (await userManager.FindByEmailAsync(m.Email) is not null) continue;

            var user = new ApplicationUser
            {
                EmployeeCode = m.Code,
                Name = m.Name,
                Email = m.Email,
                UserName = m.Email,
                PhoneNumber = m.Phone,
                EmailConfirmed = true,
                IsActive = true
            };
            var result = await userManager.CreateAsync(user, m.Password);
            if (result.Succeeded)
            {
                await userManager.AddToRoleAsync(user, m.Role);
                await UpsertEmployeeAsync(hrDb, user, m.Phone, m.JoinedDate, defaultDept.Id);
                logger.LogInformation("Created demo member: {Email}", m.Email);
            }
        }
    }

    private static readonly (string Code, string Name, string Email, string Phone, string JoinedDate)[] HrStaffMembers =
    [
        ("NV-0101", "Thùy Dương", "thuy.duong@namviet.vn", "0901000001", "01/01/2024"),
        ("NV-0102", "Phương Thảo", "phuong.thao@namviet.vn", "0901000002", "01/02/2024"),
        ("NV-0103", "Vinh Việt", "vinh.viet@namviet.vn", "0901000003", "01/03/2024"),
        ("NV-0104", "Thủy Tiên", "thuy.tien@namviet.vn", "0901000004", "01/04/2024"),
        ("NV-0105", "Kim Ngân", "kim.ngan@namviet.vn", "0901000005", "01/05/2024"),
        ("NV-0106", "Kim Huệ", "kim.hue@namviet.vn", "0901000006", "01/06/2024"),
        ("NV-0107", "Son Ny", "son.ny@namviet.vn", "0901000007", "01/07/2024"),
        ("NV-0108", "Thảo Phát", "thao.phat@namviet.vn", "0901000008", "01/08/2024"),
    ];

    public static async Task SeedHrStaffAsync(
        UserManager<ApplicationUser> userManager,
        HrDbContext hrDb,
        ILogger logger)
    {
        const string password = "Demo@12345";
        var hrDept = await EnsureHrDepartmentAsync(hrDb);
        var canonicalEmails = HrStaffMembers
            .Select(m => m.Email)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var user in await userManager.GetUsersInRoleAsync("HR"))
        {
            if (string.IsNullOrWhiteSpace(user.Email)) continue;
            if (canonicalEmails.Contains(user.Email)) continue;
            user.IsActive = false;
            await userManager.UpdateAsync(user);
            logger.LogInformation("Deactivated legacy HR user: {Email}", user.Email);
        }

        foreach (var m in HrStaffMembers)
        {
            var existing = await userManager.FindByEmailAsync(m.Email);
            if (existing is not null)
            {
                if (!existing.IsActive)
                {
                    existing.IsActive = true;
                    await userManager.UpdateAsync(existing);
                }

                if (!await userManager.IsInRoleAsync(existing, "HR"))
                    await userManager.AddToRoleAsync(existing, "HR");

                await UpsertEmployeeAsync(hrDb, existing, m.Phone, m.JoinedDate, hrDept.Id);
                continue;
            }

            var user = new ApplicationUser
            {
                EmployeeCode = m.Code,
                Name = m.Name,
                Email = m.Email,
                UserName = m.Email,
                PhoneNumber = m.Phone,
                EmailConfirmed = true,
                IsActive = true,
            };
            var result = await userManager.CreateAsync(user, password);
            if (result.Succeeded)
            {
                await userManager.AddToRoleAsync(user, "HR");
                await UpsertEmployeeAsync(hrDb, user, m.Phone, m.JoinedDate, hrDept.Id);
                logger.LogInformation("Created HR staff: {Name} ({Email})", m.Name, m.Email);
            }
            else
            {
                logger.LogError("Failed to create HR staff {Email}: {Errors}",
                    m.Email, string.Join(", ", result.Errors.Select(e => e.Description)));
            }
        }
    }

    public static async Task<Department> EnsureHrDepartmentAsync(HrDbContext hrDb)
    {
        var dept = await hrDb.Departments.FirstOrDefaultAsync(d => d.Code == "HR");
        if (dept is not null) return dept;

        dept = new Department { Code = "HR", Name = "Nhân sự" };
        hrDb.Departments.Add(dept);
        await hrDb.SaveChangesAsync();
        return dept;
    }

    public static async Task<Department> EnsureDefaultDepartmentAsync(HrDbContext hrDb)
    {
        var dept = await hrDb.Departments.FirstOrDefaultAsync(d => d.Code == "DEFAULT");
        if (dept is not null) return dept;

        dept = new Department { Code = "DEFAULT", Name = "Mặc định" };
        hrDb.Departments.Add(dept);
        await hrDb.SaveChangesAsync();
        return dept;
    }

    public static async Task UpsertEmployeeAsync(
        HrDbContext hrDb, ApplicationUser user, string? phone, string? joinedDate, int departmentId)
    {
        var employee = await hrDb.Employees
            .FirstOrDefaultAsync(e => e.UserId == user.Id || e.Code == user.EmployeeCode);

        var joinDate = DateOnly.TryParseExact(joinedDate, "dd/MM/yyyy", out var parsedJoin)
            ? parsedJoin
            : DateOnly.FromDateTime(DateTime.UtcNow);

        if (employee is null)
        {
            hrDb.Employees.Add(new Employee
            {
                Code = user.EmployeeCode,
                UserId = user.Id,
                FullName = user.Name,
                Email = user.Email,
                Phone = phone ?? user.PhoneNumber,
                DepartmentId = departmentId,
                JoinDate = joinDate,
                BirthDate = new DateOnly(1990, 1, 1),
                Gender = Gender.Other,
                Education = Education.Other,
                Status = EmployeeStatus.Active
            });
        }
        else
        {
            employee.UserId = user.Id;
            employee.FullName = user.Name;
            employee.Email = user.Email;
            employee.Phone = phone ?? user.PhoneNumber;
            employee.JoinDate = joinDate;
            employee.UpdatedAt = DateTime.UtcNow;
        }

        await hrDb.SaveChangesAsync();
    }
}
