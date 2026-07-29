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
using NamViet.ERP.Modules.Warehouse.Domain.Entities;
using NamViet.ERP.Modules.Warehouse.Infrastructure;
using NamViet.ERP.Modules.Warehouse.Infrastructure.Persistence;
using NamViet.ERP.SharedKernel.Authorization;

// Identity: min 8 chars, digit, uppercase — password MUST come from CLI (no hardcoded default).
const string EmployeeCodePrefix = "NV-";
const int EmployeeCodeDigits = 4;

static void PrintUsage()
{
    Console.Error.WriteLine("""
        BootstrapRbacPilotUsers — DEMO / local only. Does not run at API startup.

        Required:
          env ALLOW_DEMO_BOOTSTRAP=1
          --password <Pwd>     Shared password for newly created pilot users (Identity complexity rules)

        Optional:
          --force-reset        Also reset password for existing pilot emails (default: skip reset)

        Example:
          $env:ALLOW_DEMO_BOOTSTRAP='1'
          $env:ConnectionStrings__Default='Host=localhost;...'
          dotnet run -- --password 'YourStrong1Pass'
        """);
}

var argsList = args.ToList();
if (argsList.Exists(a => a is "-h" or "--help"))
{
    PrintUsage();
    return 0;
}

var allowBootstrap = string.Equals(
    Environment.GetEnvironmentVariable("ALLOW_DEMO_BOOTSTRAP"),
    "1",
    StringComparison.Ordinal);
if (!allowBootstrap)
{
    Console.Error.WriteLine("Refusing to run: set ALLOW_DEMO_BOOTSTRAP=1 (demo/local only).");
    PrintUsage();
    return 1;
}

string? password = null;
var forceReset = false;
for (var i = 0; i < argsList.Count; i++)
{
    if (argsList[i] == "--password" && i + 1 < argsList.Count)
    {
        password = argsList[++i];
        continue;
    }
    if (argsList[i] == "--force-reset")
    {
        forceReset = true;
        continue;
    }
}

if (string.IsNullOrWhiteSpace(password) || password.Length < 8)
{
    Console.Error.WriteLine("Missing or weak --password (min 8 chars; must meet Identity rules).");
    PrintUsage();
    return 1;
}

// Admin ×2; mỗi vai trò khác ×10. Tên thật để test RBAC / UI chính xác.
var pilotUsers = new[]
{
    // Admin (2)
    new PilotUser("admin@namviet.vn", "Nguyễn Văn Khoa", RolePermissionTemplates.AdminRole, "NV-0001", "Vận hành", "Hà Nội", "Quản trị hệ thống", Gender.Male),
    new PilotUser("admin2@namviet.vn", "Trần Thị Mai Anh", RolePermissionTemplates.AdminRole, "NV-0002", "Vận hành", "Hà Nội", "Quản trị hệ thống", Gender.Female),

    // HR (10)
    new PilotUser("hr01@namviet.vn", "Lê Thị Thu Hà", RolePermissionTemplates.HrRole, "NV-0011", "HCNS", "Hà Nội", "Chuyên viên nhân sự", Gender.Female),
    new PilotUser("hr02@namviet.vn", "Phạm Minh Tuấn", RolePermissionTemplates.HrRole, "NV-0012", "HCNS", "Hà Nội", "Chuyên viên nhân sự", Gender.Male),
    new PilotUser("hr03@namviet.vn", "Hoàng Thị Lan", RolePermissionTemplates.HrRole, "NV-0013", "HCNS", "Hải Phòng", "Chuyên viên nhân sự", Gender.Female),
    new PilotUser("hr04@namviet.vn", "Vũ Đức Anh", RolePermissionTemplates.HrRole, "NV-0014", "HCNS", "Hà Nội", "Chuyên viên nhân sự", Gender.Male),
    new PilotUser("hr05@namviet.vn", "Đặng Thị Hương", RolePermissionTemplates.HrRole, "NV-0015", "HCNS", "Đà Nẵng", "Chuyên viên nhân sự", Gender.Female),
    new PilotUser("hr06@namviet.vn", "Bùi Quang Huy", RolePermissionTemplates.HrRole, "NV-0016", "HCNS", "Hà Nội", "Chuyên viên nhân sự", Gender.Male),
    new PilotUser("hr07@namviet.vn", "Ngô Thị Thanh", RolePermissionTemplates.HrRole, "NV-0017", "HCNS", "TP.HCM", "Chuyên viên nhân sự", Gender.Female),
    new PilotUser("hr08@namviet.vn", "Đỗ Văn Long", RolePermissionTemplates.HrRole, "NV-0018", "HCNS", "Hà Nội", "Chuyên viên nhân sự", Gender.Male),
    new PilotUser("hr09@namviet.vn", "Lý Thị Ngọc", RolePermissionTemplates.HrRole, "NV-0019", "HCNS", "Bình Dương", "Chuyên viên nhân sự", Gender.Female),
    new PilotUser("hr10@namviet.vn", "Trịnh Quốc Bảo", RolePermissionTemplates.HrRole, "NV-0020", "HCNS", "Hà Nội", "Chuyên viên nhân sự", Gender.Male),

    // Staff (10)
    new PilotUser("staff01@namviet.vn", "Nguyễn Văn Đức", RolePermissionTemplates.StaffRole, "NV-0031", "Xây dựng", "Hải Phòng", "Nhân viên kho", Gender.Male),
    new PilotUser("staff02@namviet.vn", "Trần Thị Kim Anh", RolePermissionTemplates.StaffRole, "NV-0032", "Xây dựng", "Hà Nội", "Nhân viên kho", Gender.Female),
    new PilotUser("staff03@namviet.vn", "Phan Văn Hùng", RolePermissionTemplates.StaffRole, "NV-0033", "Xây dựng", "Đà Nẵng", "Nhân viên kho", Gender.Male),
    new PilotUser("staff04@namviet.vn", "Lưu Thị Mỹ Linh", RolePermissionTemplates.StaffRole, "NV-0034", "Xây dựng", "TP.HCM", "Nhân viên kho", Gender.Female),
    new PilotUser("staff05@namviet.vn", "Cao Minh Nhật", RolePermissionTemplates.StaffRole, "NV-0035", "Xây dựng", "Hà Nội", "Nhân viên kho", Gender.Male),
    new PilotUser("staff06@namviet.vn", "Đinh Thị Phương", RolePermissionTemplates.StaffRole, "NV-0036", "Xây dựng", "Bình Dương", "Nhân viên kho", Gender.Female),
    new PilotUser("staff07@namviet.vn", "Mai Văn Thành", RolePermissionTemplates.StaffRole, "NV-0037", "Xây dựng", "Hải Phòng", "Nhân viên kho", Gender.Male),
    new PilotUser("staff08@namviet.vn", "Tạ Thị Hồng", RolePermissionTemplates.StaffRole, "NV-0038", "Xây dựng", "Hà Nội", "Nhân viên kho", Gender.Female),
    new PilotUser("staff09@namviet.vn", "Hồ Quang Vinh", RolePermissionTemplates.StaffRole, "NV-0039", "Xây dựng", "TP.HCM", "Nhân viên kho", Gender.Male),
    new PilotUser("staff10@namviet.vn", "Chu Thị Diệu", RolePermissionTemplates.StaffRole, "NV-0040", "Xây dựng", "Đà Nẵng", "Nhân viên kho", Gender.Female),

    // PM (10)
    new PilotUser("pm01@namviet.vn", "Nguyễn Hoàng Nam", RolePermissionTemplates.PmRole, "NV-0051", "Xây dựng", "Hà Nội", "Quản lý dự án", Gender.Male),
    new PilotUser("pm02@namviet.vn", "Phạm Thị Thu Trang", RolePermissionTemplates.PmRole, "NV-0052", "Xây dựng", "TP.HCM", "Quản lý dự án", Gender.Female),
    new PilotUser("pm03@namviet.vn", "Lê Quốc Việt", RolePermissionTemplates.PmRole, "NV-0053", "Xây dựng", "Đà Nẵng", "Quản lý dự án", Gender.Male),
    new PilotUser("pm04@namviet.vn", "Võ Thị Bích Ngọc", RolePermissionTemplates.PmRole, "NV-0054", "Xây dựng", "Hà Nội", "Quản lý dự án", Gender.Female),
    new PilotUser("pm05@namviet.vn", "Trương Minh Khôi", RolePermissionTemplates.PmRole, "NV-0055", "Xây dựng", "Bình Dương", "Quản lý dự án", Gender.Male),
    new PilotUser("pm06@namviet.vn", "Đào Thị Thanh Tâm", RolePermissionTemplates.PmRole, "NV-0056", "Xây dựng", "Hải Phòng", "Quản lý dự án", Gender.Female),
    new PilotUser("pm07@namviet.vn", "Huỳnh Văn Sơn", RolePermissionTemplates.PmRole, "NV-0057", "Xây dựng", "TP.HCM", "Quản lý dự án", Gender.Male),
    new PilotUser("pm08@namviet.vn", "Nguyễn Thị Ánh", RolePermissionTemplates.PmRole, "NV-0058", "Xây dựng", "Hà Nội", "Quản lý dự án", Gender.Female),
    new PilotUser("pm09@namviet.vn", "Phan Thanh Bình", RolePermissionTemplates.PmRole, "NV-0059", "Xây dựng", "Đà Nẵng", "Quản lý dự án", Gender.Male),
    new PilotUser("pm10@namviet.vn", "Lâm Thị Quỳnh", RolePermissionTemplates.PmRole, "NV-0060", "Xây dựng", "TP.HCM", "Quản lý dự án", Gender.Female),

    // Warehouse_Keeper (10)
    new PilotUser("keeper01@namviet.vn", "Nguyễn Văn Hậu", RolePermissionTemplates.WarehouseRole, "NV-0071", "Kho", "Hà Nội", "Thủ kho", Gender.Male),
    new PilotUser("keeper02@namviet.vn", "Trần Thị Thuý", RolePermissionTemplates.WarehouseRole, "NV-0072", "Kho", "Bình Dương", "Thủ kho", Gender.Female),
    new PilotUser("keeper03@namviet.vn", "Lê Minh Cường", RolePermissionTemplates.WarehouseRole, "NV-0073", "Kho", "Hải Phòng", "Thủ kho", Gender.Male),
    new PilotUser("keeper04@namviet.vn", "Phạm Thị Yến", RolePermissionTemplates.WarehouseRole, "NV-0074", "Kho", "Đà Nẵng", "Thủ kho", Gender.Female),
    new PilotUser("keeper05@namviet.vn", "Hoàng Văn Tùng", RolePermissionTemplates.WarehouseRole, "NV-0075", "Kho", "TP.HCM", "Thủ kho", Gender.Male),
    new PilotUser("keeper06@namviet.vn", "Vũ Thị Mai", RolePermissionTemplates.WarehouseRole, "NV-0076", "Kho", "Hà Nội", "Thủ kho", Gender.Female),
    new PilotUser("keeper07@namviet.vn", "Đặng Quốc Dũng", RolePermissionTemplates.WarehouseRole, "NV-0077", "Kho", "Bình Dương", "Thủ kho", Gender.Male),
    new PilotUser("keeper08@namviet.vn", "Bùi Thị Hằng", RolePermissionTemplates.WarehouseRole, "NV-0078", "Kho", "Hải Phòng", "Thủ kho", Gender.Female),
    new PilotUser("keeper09@namviet.vn", "Ngô Văn Phong", RolePermissionTemplates.WarehouseRole, "NV-0079", "Kho", "Đà Nẵng", "Thủ kho", Gender.Male),
    new PilotUser("keeper10@namviet.vn", "Đỗ Thị Lan Anh", RolePermissionTemplates.WarehouseRole, "NV-0080", "Kho", "TP.HCM", "Thủ kho", Gender.Female),

    // Accountant (10)
    new PilotUser("acc01@namviet.vn", "Nguyễn Thị Hồng Nhung", RolePermissionTemplates.AccountantRole, "NV-0091", "Tài chính", "Hà Nội", "Kế toán", Gender.Female),
    new PilotUser("acc02@namviet.vn", "Trần Văn Khoa", RolePermissionTemplates.AccountantRole, "NV-0092", "Tài chính", "Hà Nội", "Kế toán", Gender.Male),
    new PilotUser("acc03@namviet.vn", "Lê Thị Phương Thảo", RolePermissionTemplates.AccountantRole, "NV-0093", "Tài chính", "TP.HCM", "Kế toán", Gender.Female),
    new PilotUser("acc04@namviet.vn", "Phạm Đức Thắng", RolePermissionTemplates.AccountantRole, "NV-0094", "Tài chính", "Đà Nẵng", "Kế toán", Gender.Male),
    new PilotUser("acc05@namviet.vn", "Hoàng Thị Kim Oanh", RolePermissionTemplates.AccountantRole, "NV-0095", "Tài chính", "Hải Phòng", "Kế toán", Gender.Female),
    new PilotUser("acc06@namviet.vn", "Vũ Minh Tuấn", RolePermissionTemplates.AccountantRole, "NV-0096", "Tài chính", "Hà Nội", "Kế toán", Gender.Male),
    new PilotUser("acc07@namviet.vn", "Đặng Thị Thuỷ", RolePermissionTemplates.AccountantRole, "NV-0097", "Tài chính", "Bình Dương", "Kế toán", Gender.Female),
    new PilotUser("acc08@namviet.vn", "Bùi Quang Trung", RolePermissionTemplates.AccountantRole, "NV-0098", "Tài chính", "TP.HCM", "Kế toán", Gender.Male),
    new PilotUser("acc09@namviet.vn", "Ngô Thị Hạnh", RolePermissionTemplates.AccountantRole, "NV-0099", "Tài chính", "Hà Nội", "Kế toán", Gender.Female),
    new PilotUser("acc10@namviet.vn", "Đỗ Văn Hải", RolePermissionTemplates.AccountantRole, "NV-0100", "Tài chính", "Đà Nẵng", "Kế toán", Gender.Male),

    // Procurement (10)
    new PilotUser("proc01@namviet.vn", "Nguyễn Minh Châu", RolePermissionTemplates.ProcurementRole, "NV-0111", "Mua hàng", "Hà Nội", "Nhân viên cung ứng", Gender.Female),
    new PilotUser("proc02@namviet.vn", "Trần Quốc Hưng", RolePermissionTemplates.ProcurementRole, "NV-0112", "Mua hàng", "Hà Nội", "Nhân viên cung ứng", Gender.Male),
    new PilotUser("proc03@namviet.vn", "Lê Thị Mỹ Dung", RolePermissionTemplates.ProcurementRole, "NV-0113", "Mua hàng", "TP.HCM", "Nhân viên cung ứng", Gender.Female),
    new PilotUser("proc04@namviet.vn", "Phạm Văn Đạt", RolePermissionTemplates.ProcurementRole, "NV-0114", "Mua hàng", "Đà Nẵng", "Nhân viên cung ứng", Gender.Male),
    new PilotUser("proc05@namviet.vn", "Hoàng Thị Ngọc Hà", RolePermissionTemplates.ProcurementRole, "NV-0115", "Mua hàng", "Hải Phòng", "Nhân viên cung ứng", Gender.Female),
    new PilotUser("proc06@namviet.vn", "Vũ Đức Mạnh", RolePermissionTemplates.ProcurementRole, "NV-0116", "Mua hàng", "Bình Dương", "Nhân viên cung ứng", Gender.Male),
    new PilotUser("proc07@namviet.vn", "Đặng Thị Thuỳ Dương", RolePermissionTemplates.ProcurementRole, "NV-0117", "Mua hàng", "Hà Nội", "Nhân viên cung ứng", Gender.Female),
    new PilotUser("proc08@namviet.vn", "Bùi Văn Khoa", RolePermissionTemplates.ProcurementRole, "NV-0118", "Mua hàng", "TP.HCM", "Nhân viên cung ứng", Gender.Male),
    new PilotUser("proc09@namviet.vn", "Ngô Thị Kim Chi", RolePermissionTemplates.ProcurementRole, "NV-0119", "Mua hàng", "Đà Nẵng", "Nhân viên cung ứng", Gender.Female),
    new PilotUser("proc10@namviet.vn", "Đỗ Minh Triết", RolePermissionTemplates.ProcurementRole, "NV-0120", "Mua hàng", "Hà Nội", "Nhân viên cung ứng", Gender.Male),

    // BOD (10)
    new PilotUser("bod01@namviet.vn", "Nguyễn Văn Thành", RolePermissionTemplates.BodRole, "NV-0131", "Ban lãnh đạo", "Hà Nội", "Ban giám đốc", Gender.Male),
    new PilotUser("bod02@namviet.vn", "Trần Thị Lan Hương", RolePermissionTemplates.BodRole, "NV-0132", "Ban lãnh đạo", "Hà Nội", "Ban giám đốc", Gender.Female),
    new PilotUser("bod03@namviet.vn", "Lê Hoàng Phúc", RolePermissionTemplates.BodRole, "NV-0133", "Ban lãnh đạo", "TP.HCM", "Ban giám đốc", Gender.Male),
    new PilotUser("bod04@namviet.vn", "Phạm Thị Thuý Hằng", RolePermissionTemplates.BodRole, "NV-0134", "Ban lãnh đạo", "Đà Nẵng", "Ban giám đốc", Gender.Female),
    new PilotUser("bod05@namviet.vn", "Hoàng Quốc Bảo", RolePermissionTemplates.BodRole, "NV-0135", "Ban lãnh đạo", "Hải Phòng", "Ban giám đốc", Gender.Male),
    new PilotUser("bod06@namviet.vn", "Vũ Thị Ngọc Bích", RolePermissionTemplates.BodRole, "NV-0136", "Ban lãnh đạo", "Hà Nội", "Ban giám đốc", Gender.Female),
    new PilotUser("bod07@namviet.vn", "Đặng Minh Tuấn", RolePermissionTemplates.BodRole, "NV-0137", "Ban lãnh đạo", "TP.HCM", "Ban giám đốc", Gender.Male),
    new PilotUser("bod08@namviet.vn", "Bùi Thị Thanh Vân", RolePermissionTemplates.BodRole, "NV-0138", "Ban lãnh đạo", "Bình Dương", "Ban giám đốc", Gender.Female),
    new PilotUser("bod09@namviet.vn", "Ngô Văn Kiên", RolePermissionTemplates.BodRole, "NV-0139", "Ban lãnh đạo", "Đà Nẵng", "Ban giám đốc", Gender.Male),
    new PilotUser("bod10@namviet.vn", "Đỗ Thị Kim Loan", RolePermissionTemplates.BodRole, "NV-0140", "Ban lãnh đạo", "Hà Nội", "Ban giám đốc", Gender.Female),
};

/// <summary>Gán thủ kho → dự án demo (chỉ khi project đã tồn tại sau SeedWms / SeedPurchasing).</summary>
var keeperProjectAssignments = new (string Email, string[] ProjectCodes)[]
{
    ("keeper01@namviet.vn", ["DA-2026-DEMO-001", "DEMO-PUR-PROJ"]),
    ("keeper02@namviet.vn", ["DA-2026-DEMO-002", "DEMO-PUR-PROJ"]),
    ("keeper03@namviet.vn", ["DA-2026-DEMO-001", "DA-2026-DEMO-002"]),
    ("keeper04@namviet.vn", ["DEMO-PUR-PROJ"]),
    ("keeper05@namviet.vn", ["DA-2026-DEMO-001"]),
    ("keeper06@namviet.vn", ["DA-2026-DEMO-002"]),
    ("keeper07@namviet.vn", ["DEMO-PUR-PROJ", "DA-2026-DEMO-001"]),
    ("keeper08@namviet.vn", ["DA-2026-DEMO-002", "DEMO-PUR-PROJ"]),
    ("keeper09@namviet.vn", ["DA-2026-DEMO-001", "DEMO-PUR-PROJ"]),
    ("keeper10@namviet.vn", ["DA-2026-DEMO-002", "DEMO-PUR-PROJ"]),
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
services.AddWarehouseModule(configuration);

await using var provider = services.BuildServiceProvider();
await using var scope = provider.CreateAsyncScope();

var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<ApplicationRole>>();
var identityDb = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
var hrDb = scope.ServiceProvider.GetRequiredService<HrDbContext>();
var warehouseDb = scope.ServiceProvider.GetRequiredService<WarehouseDbContext>();

await identityDb.Database.MigrateAsync();
await hrDb.Database.MigrateAsync();
await warehouseDb.Database.MigrateAsync();

foreach (var roleName in RolePermissionTemplates.ByIdentityRoleName.Keys
             .Concat(pilotUsers.Select(u => u.Role))
             .Distinct(StringComparer.Ordinal))
    await EnsureRoleAsync(roleManager, roleName);

var departmentId = await EnsurePilotDepartmentAsync(hrDb);
var positionIds = await EnsurePilotPositionsAsync(hrDb, departmentId, pilotUsers);

Console.WriteLine("RBAC pilot test users (demo/local only):");
Console.WriteLine($"Password: (from --password; not echoed)");
Console.WriteLine($"Force reset existing: {forceReset}");
Console.WriteLine($"Mã NV chuẩn: {EmployeeCodePrefix}XXXX (VD: NV-0001)");
Console.WriteLine();

foreach (var spec in pilotUsers)
{
    var positionId = positionIds[spec.PositionName];
    await FreeEmployeeCodeAsync(hrDb, identityDb, userManager, spec.EmployeeCode, spec.Email);

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

        var create = await userManager.CreateAsync(user, password);
        if (!create.Succeeded)
        {
            Console.Error.WriteLine($"Failed to create {spec.Email}: {FormatErrors(create.Errors)}");
            return 1;
        }

        Console.WriteLine($"Created user {spec.Email}");
    }
    else
    {
        Console.WriteLine($"User {spec.Email} already exists.");
        if (forceReset)
        {
            Console.WriteLine($"  → --force-reset: resetting password.");
            var token = await userManager.GeneratePasswordResetTokenAsync(user);
            var reset = await userManager.ResetPasswordAsync(user, token, password);
            if (!reset.Succeeded)
            {
                Console.Error.WriteLine($"Failed to reset password for {spec.Email}: {FormatErrors(reset.Errors)}");
                return 1;
            }
        }
        else
        {
            Console.WriteLine("  → skipping password reset (pass --force-reset to override).");
        }

        if (!string.Equals(user.Name, spec.Name, StringComparison.Ordinal))
        {
            user.Name = spec.Name;
            user.UpdatedAt = DateTime.UtcNow;
            await userManager.UpdateAsync(user);
            Console.WriteLine($"  → updated display name → {spec.Name}");
        }
    }

    if (!await userManager.IsInRoleAsync(user, spec.Role))
    {
        await userManager.AddToRoleAsync(user, spec.Role);
        Console.WriteLine($"  → assigned role {spec.Role}");
    }

    var employee = await hrDb.Employees
        .IgnoreQueryFilters()
        .FirstOrDefaultAsync(e =>
            e.DeletedAt == null
            && (e.Email == spec.Email || e.Code == spec.EmployeeCode));

    // Soft-deleted row still occupying the unique code — rename then create.
    if (employee is null)
    {
        var softConflict = await hrDb.Employees
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(e => e.DeletedAt != null && e.Code == spec.EmployeeCode);
        if (softConflict is not null)
        {
            softConflict.Code = $"LEGACY-HR-{softConflict.Id}";
            softConflict.UpdatedAt = DateTime.UtcNow;
            await hrDb.SaveChangesAsync();
            Console.WriteLine($"  → free soft-deleted HR code {spec.EmployeeCode} → {softConflict.Code}");
        }
    }

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
        if (!string.Equals(employee.FullName, spec.Name, StringComparison.Ordinal))
        {
            employee.FullName = spec.Name;
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

    Console.WriteLine($"  Login: {spec.Email} / (password from --password) / role={spec.Role}");
    Console.WriteLine();
}

await RepairLegacyPilotEmployeesAsync(
    hrDb,
    userManager,
    departmentId,
    pilotUsers.Select(u => u.Email).ToHashSet(StringComparer.OrdinalIgnoreCase));

await AssignWarehouseKeepersAsync(warehouseDb, userManager, keeperProjectAssignments);

return 0;

static async Task AssignWarehouseKeepersAsync(
    WarehouseDbContext warehouseDb,
    UserManager<ApplicationUser> userManager,
    IReadOnlyList<(string Email, string[] ProjectCodes)> assignments)
{
    Console.WriteLine("Assigning warehouse keepers to demo projects…");

    var existingProjectCodes = (await warehouseDb.Projects.AsNoTracking()
            .Select(p => p.ProjectCode)
            .ToListAsync())
        .ToHashSet(StringComparer.OrdinalIgnoreCase);

    if (existingProjectCodes.Count == 0)
    {
        Console.WriteLine("  (skip) Chưa có dự án — chạy SeedWmsDemoData / SeedPurchasingDemoData trước rồi chạy lại script này.");
        return;
    }

    var added = 0;
    var skippedMissingProject = 0;

    foreach (var (email, projectCodes) in assignments)
    {
        var user = await userManager.FindByEmailAsync(email);
        if (user is null)
        {
            Console.WriteLine($"  (skip) User {email} chưa tạo.");
            continue;
        }

        var displayName = string.IsNullOrWhiteSpace(user.Name) ? email : user.Name!;

        foreach (var projectCode in projectCodes)
        {
            if (!existingProjectCodes.Contains(projectCode))
            {
                skippedMissingProject++;
                Console.WriteLine($"  (skip) Dự án {projectCode} chưa có — bỏ gán {email}.");
                continue;
            }

            var exists = await warehouseDb.ProjectWarehouseKeepers.AnyAsync(k =>
                k.ProjectCode == projectCode && k.UserId == user.Id);
            if (exists)
            {
                Console.WriteLine($"  → {email} đã gán {projectCode}");
                continue;
            }

            warehouseDb.ProjectWarehouseKeepers.Add(new ProjectWarehouseKeeper
            {
                ProjectCode = projectCode,
                UserId = user.Id,
                DisplayName = displayName,
            });
            added++;
            Console.WriteLine($"  → gán {email} → {projectCode}");
        }
    }

    if (added > 0)
        await warehouseDb.SaveChangesAsync();

    Console.WriteLine($"Keeper assignments done (added={added}, missingProject={skippedMissingProject}).");
    Console.WriteLine();
}

static async Task FreeEmployeeCodeAsync(
    HrDbContext hrDb,
    IdentityDbContext identityDb,
    UserManager<ApplicationUser> userManager,
    string targetCode,
    string keepEmail)
{
    // Unique index includes soft-deleted rows — must IgnoreQueryFilters.
    var hrConflicts = await hrDb.Employees
        .IgnoreQueryFilters()
        .Where(e => e.Code == targetCode && (e.Email == null || e.Email != keepEmail))
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
        .IgnoreQueryFilters()
        .Where(u => u.EmployeeCode == targetCode && (keepUserId == null || u.Id != keepUserId))
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
    int departmentId,
    HashSet<string> pilotEmails)
{
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
            Level = name.Contains("Quản trị", StringComparison.Ordinal)
                || name.Contains("Quản lý", StringComparison.Ordinal)
                ? "Quản lý"
                : "Nhân viên",
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
