using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NamViet.ERP.Modules.Warehouse.Domain;
using NamViet.ERP.Modules.Warehouse.Domain.Entities;
using NamViet.ERP.Modules.Warehouse.Domain.Enums;
using NamViet.ERP.Modules.Warehouse.Domain.Services;
using NamViet.ERP.Modules.Warehouse.Infrastructure;
using NamViet.ERP.Modules.Warehouse.Infrastructure.Helpers;
using NamViet.ERP.Modules.Warehouse.Infrastructure.Persistence;
using NamViet.ERP.Modules.Warehouse.Application.Catalog;

const string SeedMarkerKey = "demo_data_seed_v1";
const string SeedMarkerBulkKey = "demo_data_seed_v2_bulk10";
const string SeedMarkerStatusKey = "demo_data_seed_v3_status_mix";
const string SeedMarkerRejectedKey = "demo_data_seed_v4_rejected";
const string SeedMarkerCatalogKey = "demo_data_seed_v5_material_paths";
const string SeedMarkerStocktakeKey = "demo_data_seed_v6_stocktakes";
const string SeedMarkerStocktakeTestKey = "demo_data_seed_v7_stocktake_test";
const string SeedMarkerKhmhMaterialsKey = "demo_data_seed_v8_khmh_materials";
var demoActorId = Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee");

var force = args.Any(a => string.Equals(a, "--force", StringComparison.OrdinalIgnoreCase));

var apiConfigDir = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "src", "ApiHost", "NamViet.ERP.Api"));
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
    Console.Error.WriteLine("Thiếu ConnectionStrings:Default.");
    Console.Error.WriteLine("Set env ConnectionStrings__Default hoặc user-secrets trên NamViet.ERP.Api.");
    return 1;
}

var services = new ServiceCollection();
services.AddLogging(b => b.AddConsole().SetMinimumLevel(LogLevel.Warning));
services.AddWarehouseModule(configuration);

await using var provider = services.BuildServiceProvider();
await using var scope = provider.CreateAsyncScope();
var db = scope.ServiceProvider.GetRequiredService<WarehouseDbContext>();
var inventory = scope.ServiceProvider.GetRequiredService<IInventoryDomainService>();

await db.Database.MigrateAsync();

var ranV1 = false;
var ranV2 = false;
var ranV3 = false;
var ranV4 = false;
var ranV5 = false;
var ranV6 = false;
var ranV7 = false;
var ranV8 = false;

if (!force && await db.WmsSettings.AnyAsync(s => s.Key == SeedMarkerKey))
{
    Console.WriteLine("Batch v1 đã có — bỏ qua.");
}
else
{
    await using var tx1 = await db.Database.BeginTransactionAsync();
    await EnsureGeneralWarehouseAsync(db);
    var categoryId = await EnsureDemoCategoryAsync(db);
    await EnsureSuppliersAsync(db);
    await EnsureMaterialsAsync(db, categoryId);
    await EnsureProjectsAsync(db);
    await SeedVouchersAsync(db, inventory);
    db.WmsSettings.Add(new WmsSetting { Key = SeedMarkerKey, Value = DateTime.UtcNow.ToString("O") });
    await db.SaveChangesAsync();
    await tx1.CommitAsync();
    ranV1 = true;
}

if (!force && await db.WmsSettings.AnyAsync(s => s.Key == SeedMarkerBulkKey))
{
    Console.WriteLine("Batch v2 (bulk 10) đã có — bỏ qua.");
}
else
{
    await using var tx2 = await db.Database.BeginTransactionAsync();
    await EnsureGeneralWarehouseAsync(db);
    var categoryId = await EnsureDemoCategoryAsync(db);
    await EnsureMaterialUnitsBulkAsync(db);
    await EnsureSuppliersBulkAsync(db);
    await EnsureMaterialsBulkAsync(db, categoryId);
    await EnsureProjectsBulkAsync(db);
    await SeedVouchersBulkAsync(db, inventory);
    db.WmsSettings.Add(new WmsSetting { Key = SeedMarkerBulkKey, Value = DateTime.UtcNow.ToString("O") });
    await db.SaveChangesAsync();
    await tx2.CommitAsync();
    ranV2 = true;
}

if (!force && await db.WmsSettings.AnyAsync(s => s.Key == SeedMarkerStatusKey))
{
    Console.WriteLine("Batch v3 (trạng thái) đã có — bỏ qua.");
}
else
{
    await using var tx3 = await db.Database.BeginTransactionAsync();
    await EnsureGeneralWarehouseAsync(db);
    await SeedVouchersStatusMixAsync(db, inventory);
    db.WmsSettings.Add(new WmsSetting { Key = SeedMarkerStatusKey, Value = DateTime.UtcNow.ToString("O") });
    await db.SaveChangesAsync();
    await tx3.CommitAsync();
    ranV3 = true;
}

if (!force && await db.WmsSettings.AnyAsync(s => s.Key == SeedMarkerRejectedKey))
{
    Console.WriteLine("Batch v4 (từ chối) đã có — bỏ qua.");
}
else
{
    await using var tx4 = await db.Database.BeginTransactionAsync();
    await EnsureGeneralWarehouseAsync(db);
    await SeedVouchersRejectedAsync(db, inventory);
    db.WmsSettings.Add(new WmsSetting { Key = SeedMarkerRejectedKey, Value = DateTime.UtcNow.ToString("O") });
    await db.SaveChangesAsync();
    await tx4.CommitAsync();
    ranV4 = true;
}

if (!force && await db.WmsSettings.AnyAsync(s => s.Key == SeedMarkerCatalogKey))
{
    Console.WriteLine("Batch v5 (cây danh mục vật tư) đã có — bỏ qua.");
}
else
{
    await using var tx5 = await db.Database.BeginTransactionAsync();
    await EnrichDemoMaterialCatalogAsync(db);
    db.WmsSettings.Add(new WmsSetting { Key = SeedMarkerCatalogKey, Value = DateTime.UtcNow.ToString("O") });
    await db.SaveChangesAsync();
    await tx5.CommitAsync();
    ranV5 = true;
}

if (!force && await db.WmsSettings.AnyAsync(s => s.Key == SeedMarkerStocktakeKey))
{
    Console.WriteLine("Batch v6 (kiểm kê) đã có — bỏ qua.");
}
else
{
    await using var tx6 = await db.Database.BeginTransactionAsync();
    await EnsureGeneralWarehouseAsync(db);
    await EnsureProjectWarehousesForDemoAsync(db);
    await SeedStocktakesAsync(db, inventory);
    db.WmsSettings.Add(new WmsSetting { Key = SeedMarkerStocktakeKey, Value = DateTime.UtcNow.ToString("O") });
    await db.SaveChangesAsync();
    await tx6.CommitAsync();
    ranV6 = true;
}

if (!force && await db.WmsSettings.AnyAsync(s => s.Key == SeedMarkerStocktakeTestKey))
{
    Console.WriteLine("Batch v7 (kiểm kê test) đã có — bỏ qua.");
}
else
{
    await using var tx7 = await db.Database.BeginTransactionAsync();
    await EnsureGeneralWarehouseAsync(db);
    await EnsureProjectWarehousesForDemoAsync(db);
    await SeedStocktakesTestAsync(db, inventory);
    db.WmsSettings.Add(new WmsSetting { Key = SeedMarkerStocktakeTestKey, Value = DateTime.UtcNow.ToString("O") });
    await db.SaveChangesAsync();
    await tx7.CommitAsync();
    ranV7 = true;
}

if (!force && await db.WmsSettings.AnyAsync(s => s.Key == SeedMarkerKhmhMaterialsKey))
{
    Console.WriteLine("Batch v8 (50 vật tư KHMH) đã có — bỏ qua.");
}
else
{
    await using var tx8 = await db.Database.BeginTransactionAsync();
    var categoryId = await EnsureDemoCategoryAsync(db);
    await EnsureKhmhCatalogMaterialsAsync(db, categoryId);
    db.WmsSettings.Add(new WmsSetting { Key = SeedMarkerKhmhMaterialsKey, Value = DateTime.UtcNow.ToString("O") });
    await db.SaveChangesAsync();
    await tx8.CommitAsync();
    ranV8 = true;
}

if (!ranV1 && !ranV2 && !ranV3 && !ranV4 && !ranV5 && !ranV6 && !ranV7 && !ranV8)
{
    Console.WriteLine("Không có gì mới để seed. Dùng --force để chạy lại (idempotent theo mã).");
    return 0;
}

Console.WriteLine();
Console.WriteLine("Đã seed dữ liệu demo WMS:");
if (ranV1)
{
    Console.WriteLine("  [v1] 2 kho dự án, 5 vật tư, 2 NCC, phiếu NK/XK mẫu");
}
if (ranV2)
{
    Console.WriteLine("  [v2] +10 đơn vị tính, +10 NCC, +10 vật tư, +10 kho dự án");
    Console.WriteLine("       +10 phiếu nhập, +10 xuất, +10 chuyển kho (đã duyệt)");
}
if (ranV3)
{
    Console.WriteLine("  [v3] +3 nháp +3 chờ duyệt cho NK, XK, KC");
}
if (ranV4)
{
    Console.WriteLine("  [v4] +3 từ chối cho NK, XK, KC");
}
if (ranV5)
{
    Console.WriteLine("  [v5] Gán cây danh mục 5 cấp + quy cách/tiêu chuẩn cho VT-DEMO-*");
}
if (ranV6)
{
    Console.WriteLine("  [v6] +3 phiếu kiểm kê demo (2 chờ duyệt, 1 đã duyệt) + kho dự án trong bảng warehouses");
}
if (ranV7)
{
    Console.WriteLine("  [v7] +phiếu KK test: khôi phục PENDING-01 (nếu đã xóa), PENDING-03/04, CANCELLED-01");
}
if (ranV8)
{
    Console.WriteLine("  [v8] +50 vật tư khớp mẫu import KHMH (VT-THEP-*, VT-ONG-*, …)");
}
Console.WriteLine("Mở lại FE (VITE_API_BASE_URL, VITE_USE_MOCK=false) và refresh trang.");
return 0;

async Task EnsureGeneralWarehouseAsync(WarehouseDbContext db)
{
    if (!await db.WmsWarehouses.AnyAsync(w => w.Id == WmsWarehouseIds.General))
    {
        db.WmsWarehouses.Add(new WmsWarehouse
        {
            Id = WmsWarehouseIds.General,
            Code = "KHO-TONG",
            Name = "Kho tổng",
            Group = "Kho tổng",
            Address = "HCM",
            Branch = "HCM",
            Manager = "Thủ kho",
            Status = "active",
        });
    }

    if (!await db.WmsSettings.AnyAsync(s => s.Key == WmsSettingsKeys.GeneralWarehouseId))
    {
        db.WmsSettings.Add(new WmsSetting
        {
            Key = WmsSettingsKeys.GeneralWarehouseId,
            Value = WmsWarehouseIds.General,
        });
    }

    await db.SaveChangesAsync();
}

async Task<int> EnsureDemoCategoryAsync(WarehouseDbContext db)
{
    const string code = "DEMO-DANH-MUC";
    var existing = await db.MaterialCategories.FirstOrDefaultAsync(c => c.Code == code);
    if (existing is not null) return existing.Id;

    var category = new MaterialCategory
    {
        Code = code,
        Name = "Vật tư demo",
        Level = 1,
        SortOrder = 1,
        IsActive = true,
    };
    db.MaterialCategories.Add(category);
    await db.SaveChangesAsync();
    return category.Id;
}

async Task EnsureSuppliersAsync(WarehouseDbContext db)
{
    var seeds = new[]
    {
        ("NCC-DEMO-01", "Công ty Vật tư Nam Việt Demo"),
        ("NCC-DEMO-02", "Nhà cung cấp Thép & Ống"),
    };

    foreach (var (code, name) in seeds)
    {
        if (await db.Suppliers.AnyAsync(s => s.SupplierCode == code)) continue;
        db.Suppliers.Add(new Supplier
        {
            SupplierCode = code,
            SupplierName = name,
            ContactPhone = "0900000000",
            MaxDebtDays = 30,
            Rating = "A",
        });
    }

    await db.SaveChangesAsync();
}

async Task EnsureMaterialsAsync(WarehouseDbContext db, int categoryId)
{
    var seeds = new (string Code, string Name, string Unit)[]
    {
        ("VT-DEMO-0001", "Thép phi 12", "Kg"),
        ("VT-DEMO-0002", "Xi măng PCB40", "Bao"),
        ("VT-DEMO-0003", "Ống PVC D63", "M"),
        ("VT-DEMO-0004", "Cát vàng", "Khối"),
        ("VT-DEMO-0005", "Sơn nước nội thất", "Thùng"),
    };

    foreach (var (code, name, unit) in seeds)
    {
        if (await db.Materials.AnyAsync(m => m.MaterialCode == code)) continue;
        db.Materials.Add(new Material
        {
            MaterialCode = code,
            MaterialName = name,
            CategoryId = categoryId,
            Unit = unit,
            IsActive = true,
            IsConsumable = true,
        });
    }

    await db.SaveChangesAsync();
}

async Task EnsureKhmhCatalogMaterialsAsync(WarehouseDbContext db, int categoryId)
{
    var added = 0;
    foreach (var row in KhmhDemoMaterialCatalog.All)
    {
        if (await db.Materials.AnyAsync(m => m.MaterialCode == row.MaterialCode))
            continue;

        db.Materials.Add(new Material
        {
            MaterialCode = row.MaterialCode,
            MaterialName = row.ProductName,
            CategoryId = categoryId,
            Unit = row.Unit,
            TechnicalSpec = row.Spec,
            IsActive = true,
            IsConsumable = false,
        });
        added++;
    }

    await db.SaveChangesAsync();
    Console.WriteLine($"  → KHMH materials: thêm {added}/{KhmhDemoMaterialCatalog.All.Count} mã.");
}

async Task EnsureProjectsAsync(WarehouseDbContext db)
{
    var pmId = Guid.Parse("00000000-0000-0000-0000-000000000000");
    var today = DateOnly.FromDateTime(DateTime.UtcNow);
    var seeds = new[]
    {
        ("DA-2026-DEMO-001", "Công trình demo HCM", 5_000_000_000m),
        ("DA-2026-DEMO-002", "Công trình demo Bình Dương", 3_200_000_000m),
    };

    foreach (var (code, name, budget) in seeds)
    {
        if (await db.Projects.AnyAsync(p => p.ProjectCode == code)) continue;
        db.Projects.Add(new Project
        {
            ProjectCode = code,
            ProjectName = name,
            ProjectType = ProjectType.Project,
            PmId = pmId,
            PmName = "PM Demo",
            StartDate = today,
            MaterialBudget = budget,
            Status = ProjectStatus.Active,
        });
    }

    await db.SaveChangesAsync();
}

async Task SeedVouchersAsync(WarehouseDbContext db, IInventoryDomainService inventory)
{
    var supplier = await db.Suppliers.FirstAsync(s => s.SupplierCode == "NCC-DEMO-01");
    var now = DateTime.UtcNow;

    var importPlans = new[]
    {
        ("NK-DEMO-0001", "DA-2026-DEMO-001", now.AddMonths(-5), new[] { ("VT-DEMO-0001", 120m, 18500m), ("VT-DEMO-0002", 40m, 95000m) }),
        ("NK-DEMO-0002", "DA-2026-DEMO-001", now.AddMonths(-2), new[] { ("VT-DEMO-0003", 200m, 42000m) }),
        ("NK-DEMO-0003", "DA-2026-DEMO-002", now.AddMonths(-1), new[] { ("VT-DEMO-0004", 15m, 280000m), ("VT-DEMO-0005", 8m, 650000m) }),
        ("NK-DEMO-0004", "DA-2026-DEMO-002", now.AddDays(-2), new[] { ("VT-DEMO-0001", 50m, 19000m) }),
    };

    foreach (var (ticket, project, createdAt, lines) in importPlans)
    {
        if (await db.WarehouseTransactions.AnyAsync(t => t.TicketCode == ticket)) continue;
        await CreateApprovedImportAsync(db, inventory, supplier, ticket, project, createdAt, lines);
    }

    if (!await db.WarehouseTransactions.AnyAsync(t => t.TicketCode == "NK-DEMO-PENDING"))
    {
        var pending = BuildImportTransaction(
            supplier,
            "NK-DEMO-PENDING",
            "DA-2026-DEMO-001",
            ApprovalStatus.Pending,
            now,
            [("VT-DEMO-0005", 2m, 640000m)]);
        await AttachSnapshotsAsync(db, pending);
        db.WarehouseTransactions.Add(pending);
        await inventory.CreateProjectImportPlannedAsync(pending, CancellationToken.None);
        await db.SaveChangesAsync();
    }

    if (!await db.WarehouseTransactions.AnyAsync(t => t.TicketCode == "XK-DEMO-0001"))
    {
        var export = new WarehouseTransaction
        {
            Id = Guid.NewGuid(),
            TicketCode = "XK-DEMO-0001",
            TransactionType = TransactionType.Export,
            ProjectCode = "DA-2026-DEMO-001",
            BeneficiaryProjectCode = "DA-2026-DEMO-001",
            WarehouseId = WmsWarehouseIds.General,
            ReferenceCode = "SO-DEMO-001",
            SoCode = "SO-DEMO-001",
            Customer = "Công trình nội bộ",
            CreatedById = demoActorId,
            CreatedByName = "PM Demo",
            ApprovalStatus = ApprovalStatus.Approved,
            WarehouseApprovalStatus = WarehouseKeeperApprovalStatus.Approved,
            ApprovedById = demoActorId,
            ApprovedByName = "Thủ kho",
            ApprovedAt = now.AddDays(-1),
            CreatedAt = now.AddDays(-1),
            Items =
            [
                new WarehouseTransactionItem
                {
                    MaterialCode = "VT-DEMO-0001",
                    Quantity = 20m,
                    UnitPrice = 18500m,
                },
            ],
        };
        await AttachSnapshotsAsync(db, export);
        db.WarehouseTransactions.Add(export);
        await inventory.ApplyApprovedExportAsync(export, CancellationToken.None);
        await db.SaveChangesAsync();
    }
}

async Task CreateApprovedImportAsync(
    WarehouseDbContext db,
    IInventoryDomainService inventory,
    Supplier supplier,
    string ticketCode,
    string projectCode,
    DateTime createdAt,
    (string MaterialCode, decimal Qty, decimal UnitPrice)[] lines)
{
    var transaction = BuildImportTransaction(
        supplier,
        ticketCode,
        projectCode,
        ApprovalStatus.Approved,
        createdAt,
        lines);
    await AttachSnapshotsAsync(db, transaction);
    db.WarehouseTransactions.Add(transaction);
    await inventory.ApplyApprovedImportAsync(transaction, CancellationToken.None);
    await db.SaveChangesAsync();
}

WarehouseTransaction BuildImportTransaction(
    Supplier supplier,
    string ticketCode,
    string projectCode,
    ApprovalStatus status,
    DateTime createdAt,
    (string MaterialCode, decimal Qty, decimal UnitPrice)[] lines) =>
    new()
    {
        Id = Guid.NewGuid(),
        TicketCode = ticketCode,
        TransactionType = TransactionType.Import,
        WarehouseId = WmsWarehouseIds.General,
        BeneficiaryProjectCode = projectCode,
        ReferenceCode = $"PO-{ticketCode}",
        SupplierId = supplier.Id,
        SupplierName = supplier.SupplierName,
        CreatedById = demoActorId,
        CreatedByName = "PM Demo",
        ApprovalStatus = status,
        WarehouseApprovalStatus = status == ApprovalStatus.Approved
            ? WarehouseKeeperApprovalStatus.Approved
            : WarehouseKeeperApprovalStatus.Pending,
        ApprovedById = status == ApprovalStatus.Approved ? demoActorId : null,
        ApprovedByName = status == ApprovalStatus.Approved ? "Thủ kho" : null,
        ApprovedAt = status == ApprovalStatus.Approved ? createdAt.AddHours(2) : null,
        CreatedAt = createdAt,
        Items = lines.Select(l => new WarehouseTransactionItem
        {
            MaterialCode = l.MaterialCode,
            Quantity = l.Qty,
            UnitPrice = l.UnitPrice,
            SupplierId = supplier.Id,
            SupplierName = supplier.SupplierName,
        }).ToList(),
    };

async Task AttachSnapshotsAsync(WarehouseDbContext db, WarehouseTransaction transaction)
{
    var materials = await TransactionItemHelper.LoadMaterialsAsync(
        db, transaction.Items.Select(i => i.MaterialCode), CancellationToken.None);
    TransactionItemHelper.EnsureActiveMaterials(transaction.Items.Select(i => i.MaterialCode), materials);
    await TransactionItemHelper.ApplySnapshotsAsync(db, transaction.Items, CancellationToken.None);
}

// ── Batch v2: +10 mỗi loại ─────────────────────────────────────────────

async Task EnsureMaterialUnitsBulkAsync(WarehouseDbContext db)
{
    var names = new[]
    {
        "Cái", "Mét", "Khối", "Kg", "Bao", "Thùng", "Cuộn", "Tấm", "Ly", "Hộp",
    };
    var order = await db.MaterialUnits.CountAsync();
    foreach (var name in names)
    {
        if (await db.MaterialUnits.AnyAsync(u => u.Name == name)) continue;
        order++;
        db.MaterialUnits.Add(new MaterialUnit
        {
            Name = name,
            SortOrder = order,
            IsActive = true,
        });
    }

    await db.SaveChangesAsync();
}

async Task EnsureSuppliersBulkAsync(WarehouseDbContext db)
{
    var ratings = new[] { "A", "A", "B", "B", "C", "A", "B", "A", "C", "B" };
    var debts = new[] { 30, 45, 30, 60, 45, 30, 45, 60, 30, 45 };
    var names = new[]
    {
        "Công ty Thép Miền Nam",
        "Xi măng Châu Thành",
        "Ống nhựa Bình Minh",
        "Vật liệu xây dựng Đại Phát",
        "Sơn Jotun Việt Nam",
        "Điện nước Hoàng Gia",
        "Gạch men Viglacera",
        "Cơ khí Thành Công",
        "Gỗ An Cường",
        "Thiết bị Hòa Phát",
    };

    for (var i = 0; i < 10; i++)
    {
        var code = $"NCC-DEMO-{i + 3:D2}";
        if (await db.Suppliers.AnyAsync(s => s.SupplierCode == code)) continue;
        db.Suppliers.Add(new Supplier
        {
            SupplierCode = code,
            SupplierName = names[i],
            ContactPhone = $"090{1000000 + i}",
            MaxDebtDays = debts[i],
            Rating = ratings[i],
        });
    }

    await db.SaveChangesAsync();
}

async Task EnsureMaterialsBulkAsync(WarehouseDbContext db, int categoryId)
{
    var seeds = new (string Code, string Name, string Unit)[]
    {
        ("VT-DEMO-0006", "Gạch ống 4 lỗ", "Cái"),
        ("VT-DEMO-0007", "Dây điện 2.5mm", "Cuộn"),
        ("VT-DEMO-0008", "Bulong M12", "Cái"),
        ("VT-DEMO-0009", "Tấm thạch cao", "Tấm"),
        ("VT-DEMO-0010", "Keo dán gạch", "Bao"),
        ("VT-DEMO-0011", "Ống đồng 6mm", "Mét"),
        ("VT-DEMO-0012", "Sắt hộp 40x40", "Mét"),
        ("VT-DEMO-0013", "Ván ép phủ film", "Tấm"),
        ("VT-DEMO-0014", "Bình chữa cháy 4kg", "Bộ"),
        ("VT-DEMO-0015", "Đèn LED panel 600", "Cái"),
    };

    foreach (var (code, name, unit) in seeds)
    {
        if (await db.Materials.AnyAsync(m => m.MaterialCode == code)) continue;
        db.Materials.Add(new Material
        {
            MaterialCode = code,
            MaterialName = name,
            CategoryId = categoryId,
            Unit = unit,
            IsActive = true,
            IsConsumable = true,
        });
    }

    await db.SaveChangesAsync();
}

async Task EnsureProjectsBulkAsync(WarehouseDbContext db)
{
    var pmId = Guid.Parse("00000000-0000-0000-0000-000000000000");
    var today = DateOnly.FromDateTime(DateTime.UtcNow);
    var cities = new[]
    {
        "Long An", "Đồng Nai", "Vũng Tàu", "Cần Thơ", "Đà Nẵng",
        "Hà Nội", "Hải Phòng", "Nha Trang", "Huế", "Quảng Ninh",
    };

    for (var i = 0; i < 10; i++)
    {
        var code = $"DA-2026-DEMO-{i + 3:D3}";
        if (await db.Projects.AnyAsync(p => p.ProjectCode == code)) continue;
        db.Projects.Add(new Project
        {
            ProjectCode = code,
            ProjectName = $"Công trình demo {cities[i]}",
            ProjectType = ProjectType.Project,
            PmId = pmId,
            PmName = "PM Demo",
            StartDate = today.AddDays(-30 * (i + 1)),
            MaterialBudget = 2_000_000_000m + i * 500_000_000m,
            Status = ProjectStatus.Active,
        });
    }

    await db.SaveChangesAsync();
}

async Task SeedVouchersBulkAsync(WarehouseDbContext db, IInventoryDomainService inventory)
{
    var suppliers = await db.Suppliers
        .Where(s => s.SupplierCode.StartsWith("NCC-DEMO-"))
        .OrderBy(s => s.SupplierCode)
        .ToListAsync();
    if (suppliers.Count == 0)
        suppliers = [await db.Suppliers.FirstAsync()];

    var projectCodes = await db.Projects
        .Where(p => p.ProjectCode.StartsWith("DA-2026-DEMO-"))
        .OrderBy(p => p.ProjectCode)
        .Select(p => p.ProjectCode)
        .ToListAsync();
    if (projectCodes.Count < 2)
        projectCodes = ["DA-2026-DEMO-001", "DA-2026-DEMO-002"];

    var materialCodes = await db.Materials
        .Where(m => m.MaterialCode.StartsWith("VT-DEMO-"))
        .OrderBy(m => m.MaterialCode)
        .Select(m => m.MaterialCode)
        .ToListAsync();

    var now = DateTime.UtcNow;

    // 10 phiếu nhập đã duyệt — nạp tồn cho xuất/chuyển
    for (var i = 1; i <= 10; i++)
    {
        var ticket = $"NK-DEMO-{100 + i:D3}";
        if (await db.WarehouseTransactions.AnyAsync(t => t.TicketCode == ticket)) continue;

        var supplier = suppliers[i % suppliers.Count];
        var project = projectCodes[i % projectCodes.Count];
        var mat = materialCodes[(i - 1) % materialCodes.Count];
        var qty = 80m + i * 10m;
        var price = 15_000m + i * 2_500m;
        await CreateApprovedImportAsync(
            db, inventory, supplier, ticket, project, now.AddDays(-14 + i),
            [(mat, qty, price)]);
    }

    // 10 phiếu xuất đã duyệt
    for (var i = 1; i <= 10; i++)
    {
        var ticket = $"XK-DEMO-{100 + i:D3}";
        if (await db.WarehouseTransactions.AnyAsync(t => t.TicketCode == ticket)) continue;

        var project = projectCodes[i % projectCodes.Count];
        var mat = materialCodes[(i - 1) % materialCodes.Count];
        var qty = 5m + i;
        var export = new WarehouseTransaction
        {
            Id = Guid.NewGuid(),
            TicketCode = ticket,
            TransactionType = TransactionType.Export,
            ProjectCode = project,
            BeneficiaryProjectCode = project,
            WarehouseId = WmsWarehouseIds.General,
            ReferenceCode = $"SO-DEMO-{100 + i:D3}",
            SoCode = $"SO-DEMO-{100 + i:D3}",
            Customer = "Công trình nội bộ",
            CreatedById = demoActorId,
            CreatedByName = "PM Demo",
            ApprovalStatus = ApprovalStatus.Approved,
            WarehouseApprovalStatus = WarehouseKeeperApprovalStatus.Approved,
            ApprovedById = demoActorId,
            ApprovedByName = "Thủ kho",
            ApprovedAt = now.AddDays(-i),
            CreatedAt = now.AddDays(-i),
            Items =
            [
                new WarehouseTransactionItem
                {
                    MaterialCode = mat,
                    Quantity = qty,
                    UnitPrice = 18_000m,
                },
            ],
        };
        await AttachSnapshotsAsync(db, export);
        db.WarehouseTransactions.Add(export);
        await inventory.ApplyApprovedExportAsync(export, CancellationToken.None);
        await db.SaveChangesAsync();
    }

    // 10 phiếu chuyển kho: xen kẽ chuyển dự án ↔ dự án và hoàn trả kho tổng
    var generalId = WmsWarehouseIds.General;
    for (var i = 1; i <= 10; i++)
    {
        var ticket = $"KC-DEMO-{100 + i:D3}";
        if (await db.WarehouseTransactions.AnyAsync(t => t.TicketCode == ticket)) continue;

        var fromProject = projectCodes[i % projectCodes.Count];
        var mat = materialCodes[(i - 1) % materialCodes.Count];
        var qty = Math.Min(3m + (i % 5), 10m);
        var createdAt = now.AddDays(-7 + i);

        WarehouseTransaction transfer;
        if (i % 2 == 0)
        {
            var toProject = projectCodes[(i + 1) % projectCodes.Count];
            if (string.Equals(fromProject, toProject, StringComparison.OrdinalIgnoreCase))
                toProject = projectCodes[(i + 2) % projectCodes.Count];

            transfer = new WarehouseTransaction
            {
                Id = Guid.NewGuid(),
                TicketCode = ticket,
                TransactionType = TransactionType.Transfer,
                ProjectCode = fromProject,
                BeneficiaryProjectCode = toProject,
                CreatedById = demoActorId,
                CreatedByName = "PM Demo",
                ApprovalStatus = ApprovalStatus.Approved,
                ApprovedById = demoActorId,
                ApprovedByName = "Thủ kho",
                ApprovedAt = createdAt.AddHours(1),
                CreatedAt = createdAt,
                Items =
                [
                    new WarehouseTransactionItem { MaterialCode = mat, Quantity = qty },
                ],
            };
            await AttachSnapshotsAsync(db, transfer);
            db.WarehouseTransactions.Add(transfer);
            await inventory.ApplyCrossProjectTransferAsync(transfer, CancellationToken.None);
        }
        else
        {
            transfer = new WarehouseTransaction
            {
                Id = Guid.NewGuid(),
                TicketCode = ticket,
                TransactionType = TransactionType.Transfer,
                ProjectCode = fromProject,
                ToWarehouseId = generalId,
                CreatedById = demoActorId,
                CreatedByName = "PM Demo",
                ApprovalStatus = ApprovalStatus.Approved,
                ApprovedById = demoActorId,
                ApprovedByName = "Thủ kho",
                ApprovedAt = createdAt.AddHours(1),
                CreatedAt = createdAt,
                Items =
                [
                    new WarehouseTransactionItem { MaterialCode = mat, Quantity = qty },
                ],
            };
            await AttachSnapshotsAsync(db, transfer);
            db.WarehouseTransactions.Add(transfer);
            await inventory.ApplyReturnToGeneralAsync(transfer, CancellationToken.None);
        }

        await db.SaveChangesAsync();
    }
}

// ── Batch v3: đủ trạng thái Nháp / Chờ duyệt ───────────────────────────

async Task SeedVouchersStatusMixAsync(WarehouseDbContext db, IInventoryDomainService inventory)
{
    var supplier = await db.Suppliers
        .OrderBy(s => s.SupplierCode)
        .FirstAsync();

    var projectCodes = await db.Projects
        .Where(p => p.ProjectCode.StartsWith("DA-2026-DEMO-"))
        .OrderBy(p => p.ProjectCode)
        .Select(p => p.ProjectCode)
        .ToListAsync();
    if (projectCodes.Count < 2)
        projectCodes = ["DA-2026-DEMO-001", "DA-2026-DEMO-002"];

    var materialCodes = await db.Materials
        .Where(m => m.MaterialCode.StartsWith("VT-DEMO-"))
        .OrderBy(m => m.MaterialCode)
        .Select(m => m.MaterialCode)
        .ToListAsync();

    var stockProject = projectCodes[0];
    var stockMaterial = materialCodes[0];
    var now = DateTime.UtcNow;

    for (var i = 1; i <= 3; i++)
    {
        var draftTicket = $"NK-STATUS-DRAFT-{i:D2}";
        if (!await db.WarehouseTransactions.AnyAsync(t => t.TicketCode == draftTicket))
        {
            var project = projectCodes[i % projectCodes.Count];
            var mat = materialCodes[(i - 1) % materialCodes.Count];
            var tx = BuildImportTransaction(
                supplier, draftTicket, project, ApprovalStatus.Draft, now.AddHours(-i),
                [(mat, 10m + i, 20_000m + i * 1_000m)]);
            await AttachSnapshotsAsync(db, tx);
            db.WarehouseTransactions.Add(tx);
            await inventory.CreateProjectImportPlannedAsync(tx, CancellationToken.None);
            await db.SaveChangesAsync();
        }

        var pendTicket = $"NK-STATUS-PEND-{i:D2}";
        if (!await db.WarehouseTransactions.AnyAsync(t => t.TicketCode == pendTicket))
        {
            var project = projectCodes[(i + 1) % projectCodes.Count];
            var mat = materialCodes[(i + 2) % materialCodes.Count];
            var tx = BuildImportTransaction(
                supplier, pendTicket, project, ApprovalStatus.Pending, now.AddHours(-i - 3),
                [(mat, 12m + i, 22_000m + i * 1_000m)]);
            await AttachSnapshotsAsync(db, tx);
            db.WarehouseTransactions.Add(tx);
            await inventory.CreateProjectImportPlannedAsync(tx, CancellationToken.None);
            await db.SaveChangesAsync();
        }
    }

    for (var i = 1; i <= 3; i++)
    {
        var draftTicket = $"XK-STATUS-DRAFT-{i:D2}";
        if (!await db.WarehouseTransactions.AnyAsync(t => t.TicketCode == draftTicket))
        {
            var qty = 1m;
            var tx = BuildExportTransaction(
                draftTicket, stockProject, stockMaterial, qty, ApprovalStatus.Draft, now.AddHours(-i));
            await AttachSnapshotsAsync(db, tx);
            db.WarehouseTransactions.Add(tx);
            await inventory.CreateProjectExportPlannedAsync(tx, CancellationToken.None);
            await db.SaveChangesAsync();
        }

        var pendTicket = $"XK-STATUS-PEND-{i:D2}";
        if (!await db.WarehouseTransactions.AnyAsync(t => t.TicketCode == pendTicket))
        {
            var qty = 1m;
            var tx = BuildExportTransaction(
                pendTicket, stockProject, stockMaterial, qty, ApprovalStatus.Pending, now.AddHours(-i - 3));
            await AttachSnapshotsAsync(db, tx);
            db.WarehouseTransactions.Add(tx);
            await inventory.CreateProjectExportPlannedAsync(tx, CancellationToken.None);
            await db.SaveChangesAsync();
        }
    }

    var generalId = WmsWarehouseIds.General;
    for (var i = 1; i <= 3; i++)
    {
        var draftTicket = $"KC-STATUS-DRAFT-{i:D2}";
        if (!await db.WarehouseTransactions.AnyAsync(t => t.TicketCode == draftTicket))
        {
            var qty = 1m;
            var tx = i % 2 == 0
                ? BuildCrossTransferTransaction(
                    draftTicket, stockProject, projectCodes[1], stockMaterial, qty,
                    ApprovalStatus.Draft, now.AddHours(-i))
                : BuildReturnTransferTransaction(
                    draftTicket, stockProject, generalId, stockMaterial, qty,
                    ApprovalStatus.Draft, now.AddHours(-i));
            await AttachSnapshotsAsync(db, tx);
            db.WarehouseTransactions.Add(tx);
            await inventory.CreateProjectExportPlannedAsync(tx, CancellationToken.None);
            await db.SaveChangesAsync();
        }

        var pendTicket = $"KC-STATUS-PEND-{i:D2}";
        if (!await db.WarehouseTransactions.AnyAsync(t => t.TicketCode == pendTicket))
        {
            var qty = 1m;
            var tx = i % 2 == 0
                ? BuildCrossTransferTransaction(
                    pendTicket, stockProject, projectCodes[1], stockMaterial, qty,
                    ApprovalStatus.Pending, now.AddHours(-i - 3))
                : BuildReturnTransferTransaction(
                    pendTicket, stockProject, generalId, stockMaterial, qty,
                    ApprovalStatus.Pending, now.AddHours(-i - 3));
            await AttachSnapshotsAsync(db, tx);
            db.WarehouseTransactions.Add(tx);
            await inventory.CreateProjectExportPlannedAsync(tx, CancellationToken.None);
            await db.SaveChangesAsync();
        }
    }
}

WarehouseTransaction BuildExportTransaction(
    string ticketCode,
    string projectCode,
    string materialCode,
    decimal qty,
    ApprovalStatus status,
    DateTime createdAt) =>
    new()
    {
        Id = Guid.NewGuid(),
        TicketCode = ticketCode,
        TransactionType = TransactionType.Export,
        ProjectCode = projectCode,
        BeneficiaryProjectCode = projectCode,
        WarehouseId = WmsWarehouseIds.General,
        ReferenceCode = $"SO-{ticketCode}",
        SoCode = $"SO-{ticketCode}",
        Customer = "Công trình nội bộ",
        CreatedById = demoActorId,
        CreatedByName = "PM Demo",
        ApprovalStatus = status,
        WarehouseApprovalStatus = WarehouseKeeperApprovalStatus.Pending,
        CreatedAt = createdAt,
        Items =
        [
            new WarehouseTransactionItem
            {
                MaterialCode = materialCode,
                Quantity = qty,
                UnitPrice = 18_000m,
            },
        ],
    };

WarehouseTransaction BuildCrossTransferTransaction(
    string ticketCode,
    string fromProject,
    string toProject,
    string materialCode,
    decimal qty,
    ApprovalStatus status,
    DateTime createdAt) =>
    new()
    {
        Id = Guid.NewGuid(),
        TicketCode = ticketCode,
        TransactionType = TransactionType.Transfer,
        ProjectCode = fromProject,
        BeneficiaryProjectCode = toProject,
        CreatedById = demoActorId,
        CreatedByName = "PM Demo",
        ApprovalStatus = status,
        CreatedAt = createdAt,
        Items = [new WarehouseTransactionItem { MaterialCode = materialCode, Quantity = qty }],
    };

WarehouseTransaction BuildReturnTransferTransaction(
    string ticketCode,
    string fromProject,
    string toWarehouseId,
    string materialCode,
    decimal qty,
    ApprovalStatus status,
    DateTime createdAt) =>
    new()
    {
        Id = Guid.NewGuid(),
        TicketCode = ticketCode,
        TransactionType = TransactionType.Transfer,
        ProjectCode = fromProject,
        ToWarehouseId = toWarehouseId,
        CreatedById = demoActorId,
        CreatedByName = "PM Demo",
        ApprovalStatus = status,
        CreatedAt = createdAt,
        Items = [new WarehouseTransactionItem { MaterialCode = materialCode, Quantity = qty }],
    };

// ── Batch v4: phiếu từ chối (sau pending, đã hủy dự kiến) ─────────────────

async Task SeedVouchersRejectedAsync(WarehouseDbContext db, IInventoryDomainService inventory)
{
    var supplier = await db.Suppliers.OrderBy(s => s.SupplierCode).FirstAsync();
    var projectCodes = await db.Projects
        .Where(p => p.ProjectCode.StartsWith("DA-2026-DEMO-"))
        .OrderBy(p => p.ProjectCode)
        .Select(p => p.ProjectCode)
        .ToListAsync();
    if (projectCodes.Count < 2)
        projectCodes = ["DA-2026-DEMO-001", "DA-2026-DEMO-002"];

    var materialCodes = await db.Materials
        .Where(m => m.MaterialCode.StartsWith("VT-DEMO-"))
        .OrderBy(m => m.MaterialCode)
        .Select(m => m.MaterialCode)
        .ToListAsync();

    var stockProject = projectCodes[0];
    var stockMaterial = materialCodes[0];
    var generalId = WmsWarehouseIds.General;
    var now = DateTime.UtcNow;
    var reasons = new[]
    {
        "Sai quy cách vật tư",
        "Thiếu chứng từ PO",
        "PM yêu cầu hủy",
    };

    for (var i = 1; i <= 3; i++)
    {
        var ticket = $"NK-STATUS-REJ-{i:D2}";
        if (await db.WarehouseTransactions.AnyAsync(t => t.TicketCode == ticket)) continue;

        var project = projectCodes[(i + 3) % projectCodes.Count];
        var mat = materialCodes[(i + 5) % materialCodes.Count];
        var tx = BuildImportTransaction(
            supplier, ticket, project, ApprovalStatus.Pending, now.AddDays(-i - 10),
            [(mat, 8m + i, 25_000m + i * 500m)]);
        await AttachSnapshotsAsync(db, tx);
        db.WarehouseTransactions.Add(tx);
        await inventory.CreateProjectImportPlannedAsync(tx, CancellationToken.None);
        await inventory.CancelProjectImportPlannedAsync(tx, CancellationToken.None);
        MarkRejected(tx, reasons[i - 1]);
        await db.SaveChangesAsync();
    }

    for (var i = 1; i <= 3; i++)
    {
        var ticket = $"XK-STATUS-REJ-{i:D2}";
        if (await db.WarehouseTransactions.AnyAsync(t => t.TicketCode == ticket)) continue;

        var tx = BuildExportTransaction(
            ticket, stockProject, stockMaterial, 1m, ApprovalStatus.Pending, now.AddDays(-i - 10));
        await AttachSnapshotsAsync(db, tx);
        db.WarehouseTransactions.Add(tx);
        await inventory.CreateProjectExportPlannedAsync(tx, CancellationToken.None);
        await inventory.CancelProjectExportPlannedAsync(tx, CancellationToken.None);
        MarkRejected(tx, reasons[i - 1]);
        await db.SaveChangesAsync();
    }

    for (var i = 1; i <= 3; i++)
    {
        var ticket = $"KC-STATUS-REJ-{i:D2}";
        if (await db.WarehouseTransactions.AnyAsync(t => t.TicketCode == ticket)) continue;

        var tx = i % 2 == 0
            ? BuildCrossTransferTransaction(
                ticket, stockProject, projectCodes[1], stockMaterial, 1m,
                ApprovalStatus.Pending, now.AddDays(-i - 10))
            : BuildReturnTransferTransaction(
                ticket, stockProject, generalId, stockMaterial, 1m,
                ApprovalStatus.Pending, now.AddDays(-i - 10));
        await AttachSnapshotsAsync(db, tx);
        db.WarehouseTransactions.Add(tx);
        await inventory.CreateProjectExportPlannedAsync(tx, CancellationToken.None);
        await inventory.CancelProjectExportPlannedAsync(tx, CancellationToken.None);
        MarkRejected(tx, reasons[i - 1]);
        await db.SaveChangesAsync();
    }
}

void MarkRejected(WarehouseTransaction tx, string reason)
{
    tx.ApprovalStatus = ApprovalStatus.Rejected;
    tx.WarehouseApprovalStatus = WarehouseKeeperApprovalStatus.Rejected;
    tx.ApprovedById = demoActorId;
    tx.ApprovedByName = "Thủ kho";
    tx.ApprovedAt = DateTime.UtcNow.AddDays(-1);
    tx.Notes = $"[Từ chối] {reason}";
    tx.UpdatedAt = DateTime.UtcNow;
}

// ── Batch v5: cây danh mục 5 cấp + quy cách cho VT-DEMO-* ───────────────

async Task EnrichDemoMaterialCatalogAsync(WarehouseDbContext db)
{
    var catalog = new (string Code, string[] Path, string Spec, string Standard)[]
    {
        ("VT-DEMO-0001", ["Vật liệu kim loại", "Thép xây dựng", "Thép thanh", "Thép tròn trơn", "Phi 12"], "D12×12m", "TCVN 1651"),
        ("VT-DEMO-0002", ["Vật liệu kết dính", "Xi măng", "Xi măng PCB", "PCB40", "Bao 50kg"], "50kg/bao", "TCVN 6260"),
        ("VT-DEMO-0003", ["Ống & phụ kiện", "Nhựa kỹ thuật", "Ống PVC", "Ống pressure", "D63"], "DN63×4m", "TCVN 8491"),
        ("VT-DEMO-0004", ["Vật liệu xây", "Cát đá", "Cát xây dựng", "Cát vàng", "Hạt trung"], "0–5mm", "TCVN 7570"),
        ("VT-DEMO-0005", ["Sơn & hoàn thiện", "Sơn nước", "Nội thất", "Mịn", "Thùng 18L"], "18L/thùng", "TCVN 8781"),
        ("VT-DEMO-0006", ["Vật liệu xây", "Gạch", "Gạch đất sét", "Gạch ống", "4 lỗ"], "190×190×390mm", "TCVN 1451"),
        ("VT-DEMO-0007", ["Vật tư điện", "Dây cáp", "Dây đồng", "CV", "2.5mm²"], "Cuộn 100m", "TCVN 6610"),
        ("VT-DEMO-0008", ["Vật liệu kim loại", "Bu lông", "Bu lông thép", "M12", "Cấp 8.8"], "M12×80", "TCVN 2520"),
        ("VT-DEMO-0009", ["Vật liệu xây", "Thạch cao", "Tấm thạch cao", "Chống ẩm", "Dày 9.5mm"], "1200×2400×9.5mm", "TCVN 8241"),
        ("VT-DEMO-0010", ["Vật liệu kết dính", "Keo dán", "Keo gạch", "Nội thất", "Bao 25kg"], "25kg/bao", "TCVN 7892"),
        ("VT-DEMO-0011", ["Ống & phụ kiện", "Đồng", "Ống đồng", "Ống mềm", "6mm"], "Φ6×15m", "ASTM B280"),
        ("VT-DEMO-0012", ["Vật liệu kim loại", "Thép hộp", "Hộp vuông", "40×40", "Dày 1.2mm"], "40×40×6m", "JIS G3466"),
        ("VT-DEMO-0013", ["Vật liệu gỗ", "Ván ép", "Phủ film", "18mm", "Tấm chuẩn"], "1220×2440×18mm", "TCVN 1872"),
        ("VT-DEMO-0014", ["PCCC", "Thiết bị chữa cháy", "Bình bột", "ABC", "4kg"], "4kg/bình", "TCVN 3890"),
        ("VT-DEMO-0015", ["Vật tư điện", "Chiếu sáng", "Đèn LED", "Panel", "600×600"], "600×600×9mm", "IEC 60598"),
    };

    foreach (var row in catalog)
    {
        var material = await db.Materials.FirstOrDefaultAsync(m => m.MaterialCode == row.Code);
        if (material is null) continue;

        var leafId = await EnsureCategoryPathAsync(db, row.Code, row.Path);
        material.CategoryId = leafId;
        material.TechnicalSpec = row.Spec;
        material.QualityStandard = row.Standard;
        material.UpdatedAt = DateTime.UtcNow;
    }

    await db.SaveChangesAsync();
}

async Task<int> EnsureCategoryPathAsync(WarehouseDbContext db, string materialCode, string[] levelNames)
{
    int? parentId = null;
    var leafId = 0;
    for (var i = 0; i < levelNames.Length; i++)
    {
        var level = i + 1;
        var code = $"DEMO-PATH-{materialCode}-L{level}";
        var category = await db.MaterialCategories.FirstOrDefaultAsync(c => c.Code == code);
        if (category is null)
        {
            category = new MaterialCategory
            {
                Code = code,
                Name = levelNames[i],
                Level = level,
                ParentId = parentId,
                SortOrder = level,
                IsActive = true,
            };
            db.MaterialCategories.Add(category);
            await db.SaveChangesAsync();
        }

        parentId = category.Id;
        leafId = category.Id;
    }

    return leafId;
}

// ── Batch v6: phiếu kiểm kê demo ─────────────────────────────────────────

async Task EnsureProjectWarehousesForDemoAsync(WarehouseDbContext db)
{
    var projects = await db.Projects
        .Where(p => p.ProjectCode.StartsWith("DA-2026-DEMO-") && p.DeletedAt == null)
        .ToListAsync();

    foreach (var project in projects)
    {
        if (await db.WmsWarehouses.AnyAsync(w => w.Id == project.ProjectCode))
            continue;

        db.WmsWarehouses.Add(new WmsWarehouse
        {
            Id = project.ProjectCode,
            Code = project.ProjectCode,
            Name = project.ProjectName,
            Group = "Kho dự án",
            Address = "Việt Nam",
            Branch = "HCM",
            Manager = string.IsNullOrWhiteSpace(project.PmName) ? "PM Demo" : project.PmName,
            Status = "active",
        });
    }

    await db.SaveChangesAsync();
}

async Task<decimal> GetProjectOnHandAsync(
    WarehouseDbContext db, string projectCode, string materialCode)
{
    var inventory = await db.ProjectInventories
        .AsNoTracking()
        .FirstOrDefaultAsync(p => p.ProjectCode == projectCode && p.MaterialCode == materialCode);
    return inventory?.ActualInventoryQty ?? 0m;
}

Stocktake BuildStocktakeEntity(
    string id,
    string voucherCode,
    Project project,
    DateOnly voucherDate,
    DateTime createdAt,
    string status,
    string condition,
    bool isFrozen,
    string note,
    IEnumerable<(string MaterialCode, string Name, decimal SystemQty, decimal ActualQty)> lines)
{
    var stocktake = new Stocktake
    {
        Id = id,
        VoucherCode = voucherCode,
        VoucherDate = voucherDate,
        CreatedById = demoActorId,
        CreatedByName = "PM Demo",
        Status = status,
        WarehouseId = project.ProjectCode,
        WarehouseName = project.ProjectName,
        Condition = condition,
        IsFrozen = isFrozen,
        Note = note,
        CreatedAt = createdAt,
        UpdatedAt = createdAt,
        Items = lines.Select(line => new StocktakeItem
        {
            MaterialCode = line.MaterialCode,
            Sku = line.MaterialCode,
            Name = line.Name,
            SystemQty = line.SystemQty,
            ActualQty = line.ActualQty,
            Diff = line.ActualQty - line.SystemQty,
        }).ToList(),
    };

    foreach (var item in stocktake.Items)
        item.StocktakeId = stocktake.Id;

    return stocktake;
}

async Task SeedStocktakesAsync(WarehouseDbContext db, IInventoryDomainService inventory)
{
    var project1 = await db.Projects.FirstOrDefaultAsync(p => p.ProjectCode == "DA-2026-DEMO-001");
    var project2 = await db.Projects.FirstOrDefaultAsync(p => p.ProjectCode == "DA-2026-DEMO-002");
    if (project1 is null || project2 is null)
    {
        Console.WriteLine("  [v6] Bỏ qua kiểm kê — thiếu kho dự án demo.");
        return;
    }

    var materials = await db.Materials
        .Where(m => m.MaterialCode.StartsWith("VT-DEMO-"))
        .OrderBy(m => m.MaterialCode)
        .Take(5)
        .ToListAsync();
    if (materials.Count == 0)
    {
        Console.WriteLine("  [v6] Bỏ qua kiểm kê — chưa có vật tư demo.");
        return;
    }

    var now = DateTime.UtcNow;
    var today = DateOnly.FromDateTime(now);

    if (!await db.Stocktakes.AnyAsync(s => s.VoucherCode == "KK-DEMO-PENDING-01"))
    {
        var lines = new List<(string MaterialCode, string Name, decimal SystemQty, decimal ActualQty)>();
        foreach (var material in materials.Take(3))
        {
            var systemQty = await GetProjectOnHandAsync(db, project1.ProjectCode, material.MaterialCode);
            if (systemQty <= 0) systemQty = 10m + lines.Count;
            var actualQty = material.MaterialCode switch
            {
                "VT-DEMO-0001" => systemQty + 2m,
                "VT-DEMO-0002" => systemQty - 1m,
                _ => systemQty,
            };
            if (actualQty < 0) actualQty = 0;
            lines.Add((material.MaterialCode, material.MaterialName, systemQty, actualQty));
        }

        db.Stocktakes.Add(BuildStocktakeEntity(
            "st-demo01",
            "KK-DEMO-PENDING-01",
            project1,
            today.AddDays(-2),
            now.AddDays(-2),
            "pending",
            "counting",
            false,
            "Kiểm kê định kỳ cuối tháng — chờ duyệt",
            lines));
        await db.SaveChangesAsync();
    }

    if (!await db.Stocktakes.AnyAsync(s => s.VoucherCode == "KK-DEMO-PENDING-02"))
    {
        var material = materials.FirstOrDefault(m => m.MaterialCode == "VT-DEMO-0004") ?? materials[0];
        var systemQty = await GetProjectOnHandAsync(db, project2.ProjectCode, material.MaterialCode);
        if (systemQty <= 0) systemQty = 8m;

        db.Stocktakes.Add(BuildStocktakeEntity(
            "st-demo02",
            "KK-DEMO-PENDING-02",
            project2,
            today.AddDays(-1),
            now.AddDays(-1),
            "pending",
            "counting",
            true,
            "Đã khóa kho trong lúc kiểm — chờ duyệt",
            [(material.MaterialCode, material.MaterialName, systemQty, systemQty + 1m)]));
        await db.SaveChangesAsync();
    }

    if (!await db.Stocktakes.AnyAsync(s => s.VoucherCode == "KK-DEMO-APPROVED-01"))
    {
        var material = materials.FirstOrDefault(m => m.MaterialCode == "VT-DEMO-0001") ?? materials[0];
        var systemQty = await GetProjectOnHandAsync(db, project1.ProjectCode, material.MaterialCode);
        if (systemQty <= 0) systemQty = 50m;
        var actualQty = Math.Max(0m, systemQty - 3m);

        var approved = BuildStocktakeEntity(
            "st-demo03",
            "KK-DEMO-APPROVED-01",
            project1,
            today.AddDays(-7),
            now.AddDays(-7),
            "approved",
            "completed",
            false,
            "Kiểm kê đã duyệt — điều chỉnh thiếu 3",
            [(material.MaterialCode, material.MaterialName, systemQty, actualQty)]);

        db.Stocktakes.Add(approved);
        await db.SaveChangesAsync();

        foreach (var item in approved.Items.Where(i => i.Diff != 0))
        {
            await inventory.ApplyStocktakeAdjustmentAsync(
                approved.WarehouseId,
                item.MaterialCode,
                item.Diff,
                CancellationToken.None);
        }

        await db.SaveChangesAsync();
    }
}

async Task RestoreSoftDeletedStocktakeAsync(WarehouseDbContext db, string voucherCode)
{
    var deleted = await db.Stocktakes
        .IgnoreQueryFilters()
        .FirstOrDefaultAsync(s => s.VoucherCode == voucherCode && s.DeletedAt != null);
    if (deleted is null) return;

    deleted.DeletedAt = null;
    deleted.Status = "pending";
    deleted.Condition = "counting";
    deleted.IsFrozen = false;
    deleted.Note = $"{deleted.Note} [khôi phục seed demo]";
    deleted.UpdatedAt = DateTime.UtcNow;
    await db.SaveChangesAsync();
    Console.WriteLine($"  [v7] Khôi phục phiếu đã xóa: {voucherCode}");
}

async Task SeedStocktakesTestAsync(WarehouseDbContext db, IInventoryDomainService inventory)
{
    var project1 = await db.Projects.FirstOrDefaultAsync(p => p.ProjectCode == "DA-2026-DEMO-001");
    var project2 = await db.Projects.FirstOrDefaultAsync(p => p.ProjectCode == "DA-2026-DEMO-002");
    if (project1 is null || project2 is null)
    {
        Console.WriteLine("  [v7] Bỏ qua kiểm kê test — thiếu kho dự án demo.");
        return;
    }

    var materials = await db.Materials
        .Where(m => m.MaterialCode.StartsWith("VT-DEMO-"))
        .OrderBy(m => m.MaterialCode)
        .Take(5)
        .ToListAsync();
    if (materials.Count == 0)
    {
        Console.WriteLine("  [v7] Bỏ qua kiểm kê test — chưa có vật tư demo.");
        return;
    }

    await RestoreSoftDeletedStocktakeAsync(db, "KK-DEMO-PENDING-01");

    var now = DateTime.UtcNow;
    var today = DateOnly.FromDateTime(now);
    var mat1 = materials.FirstOrDefault(m => m.MaterialCode == "VT-DEMO-0001") ?? materials[0];
    var mat2 = materials.FirstOrDefault(m => m.MaterialCode == "VT-DEMO-0003") ?? materials[1];
    var mat3 = materials.FirstOrDefault(m => m.MaterialCode == "VT-DEMO-0004") ?? materials[2];

    if (!await db.Stocktakes.AnyAsync(s => s.VoucherCode == "KK-DEMO-PENDING-01"))
    {
        var systemQty1 = await GetProjectOnHandAsync(db, project1.ProjectCode, mat1.MaterialCode);
        if (systemQty1 <= 0) systemQty1 = 12m;
        db.Stocktakes.Add(BuildStocktakeEntity(
            "st-demo01",
            "KK-DEMO-PENDING-01",
            project1,
            today.AddDays(-2),
            now.AddDays(-2),
            "pending",
            "counting",
            false,
            "Kiểm kê định kỳ — test hủy / đóng băng / duyệt",
            [(mat1.MaterialCode, mat1.MaterialName, systemQty1, systemQty1 + 1m)]));
        await db.SaveChangesAsync();
    }

    if (!await db.Stocktakes.AnyAsync(s => s.VoucherCode == "KK-DEMO-PENDING-03"))
    {
        var systemQty = await GetProjectOnHandAsync(db, project1.ProjectCode, mat2.MaterialCode);
        if (systemQty <= 0) systemQty = 15m;
        db.Stocktakes.Add(BuildStocktakeEntity(
            "st-demo04",
            "KK-DEMO-PENDING-03",
            project1,
            today,
            now.AddHours(-3),
            "pending",
            "counting",
            true,
            "Đang đóng băng — test Mở băng / Duyệt",
            [(mat2.MaterialCode, mat2.MaterialName, systemQty, systemQty)]));
        await db.SaveChangesAsync();
    }

    if (!await db.Stocktakes.AnyAsync(s => s.VoucherCode == "KK-DEMO-PENDING-04"))
    {
        var systemQty = await GetProjectOnHandAsync(db, project2.ProjectCode, mat3.MaterialCode);
        if (systemQty <= 0) systemQty = 6m;
        db.Stocktakes.Add(BuildStocktakeEntity(
            "st-demo05",
            "KK-DEMO-PENDING-04",
            project2,
            today,
            now.AddHours(-1),
            "pending",
            "counting",
            false,
            "Đang mở băng — test Đóng băng / Hủy phiếu",
            [(mat3.MaterialCode, mat3.MaterialName, systemQty, Math.Max(0m, systemQty - 2m))]));
        await db.SaveChangesAsync();
    }

    if (!await db.Stocktakes.AnyAsync(s => s.VoucherCode == "KK-DEMO-CANCELLED-01"))
    {
        var systemQty = await GetProjectOnHandAsync(db, project1.ProjectCode, mat1.MaterialCode);
        if (systemQty <= 0) systemQty = 10m;
        db.Stocktakes.Add(BuildStocktakeEntity(
            "st-demo06",
            "KK-DEMO-CANCELLED-01",
            project1,
            today.AddDays(-3),
            now.AddDays(-3),
            "cancelled",
            "counting",
            false,
            "Phiếu đã hủy — test tab Đã hủy",
            [(mat1.MaterialCode, mat1.MaterialName, systemQty, systemQty)]));
        await db.SaveChangesAsync();
    }
}
