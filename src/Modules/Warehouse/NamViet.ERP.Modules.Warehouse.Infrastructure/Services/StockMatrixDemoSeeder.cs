using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NamViet.ERP.Modules.Warehouse.Domain;
using NamViet.ERP.Modules.Warehouse.Domain.Entities;
using NamViet.ERP.Modules.Warehouse.Domain.Enums;
using NamViet.ERP.Modules.Warehouse.Infrastructure.Persistence;

namespace NamViet.ERP.Modules.Warehouse.Infrastructure.Services;

/// <summary>
/// Xóa và tạo lại dữ liệu demo cho ma trận tồn kho dự án (~20 công trình, đủ trạng thái giữ kho / dự kiến / thực tế).
/// Bật qua <c>Seed:ResetStockMatrixDemo</c> trong appsettings Development.
/// </summary>
public static class StockMatrixDemoSeeder
{
    private static readonly string[] DemoProjectNames =
    [
        "Nhà máy Heineken Vũng Tàu",
        "Nhà máy Sữa Vinamilk Bình Dương",
        "Nhà máy Bia Sabeco Long An",
        "Dự án Lọc dầu Dung Quất mở rộng",
        "Nhà máy Thép Hòa Phát Dung Quất",
        "Dự án Vinhomes Grand Park giai đoạn 2",
        "Nhà máy Xi măng FICO Tây Ninh",
        "Dự án Điện gió Trà Vinh",
        "Nhà máy Bột giấy SCG Bình Dương",
        "Dự án Cảng container Cái Mép",
        "Nhà máy Sơn Kansai Đồng Nai",
        "Dự án Nhiệt điện Long Phú 1",
        "Nhà máy Bia Tiger Bình Dương",
        "Dự án Khu công nghiệp Amata",
        "Nhà máy Thép Pomina Bà Rịa",
        "Dự án Mở rộng đường cao tốc Bến Lức – Long Thành",
        "Nhà máy Dược Hậu Giang",
        "Dự án Nhà máy điện mặt trời Ninh Thuận",
        "Nhà máy Bia Carlsberg Hải Phòng",
        "Dự án Hóa chất Long Sơn giai đoạn 3",
    ];

    public static async Task ResetAndSeedAsync(
        WarehouseDbContext db,
        ILogger logger,
        CancellationToken ct = default)
    {
        logger.LogWarning("Resetting stock matrix demo data...");

        await MaterialCategoryTreeLoader.EnsureTreeSeededAsync(db, logger, ct);
        await MaterialDemoSeeder.EnsureDemoMaterialsAsync(db, logger, ct);
        await EnsureGeneralWarehouseAsync(db, ct);

        var reservations = await db.InventoryReservations.ToListAsync(ct);
        db.InventoryReservations.RemoveRange(reservations);

        var oldDemoTickets = await db.WarehouseTransactions
            .Where(t => t.TicketCode.StartsWith("NK-DEMO-"))
            .ToListAsync(ct);
        db.WarehouseTransactions.RemoveRange(oldDemoTickets);

        var projectInventories = await db.ProjectInventories.ToListAsync(ct);
        db.ProjectInventories.RemoveRange(projectInventories);

        var constructionProjects = await db.Projects
            .Where(p => p.ProjectType == ProjectType.Project)
            .ToListAsync(ct);
        var projectCodes = constructionProjects.Select(p => p.ProjectCode).ToHashSet(StringComparer.Ordinal);

        if (projectCodes.Count > 0)
        {
            var loans = await db.ProjectMaterialLoans
                .Where(l => projectCodes.Contains(l.BorrowingProjectCode) || projectCodes.Contains(l.LendingProjectCode))
                .ToListAsync(ct);
            db.ProjectMaterialLoans.RemoveRange(loans);

            var quotas = await db.ConsumableQuotas.Where(q => projectCodes.Contains(q.ProjectCode)).ToListAsync(ct);
            db.ConsumableQuotas.RemoveRange(quotas);

            var boms = await db.ProjectBoms.Where(b => projectCodes.Contains(b.ProjectCode)).ToListAsync(ct);
            db.ProjectBoms.RemoveRange(boms);

            var expectedImports = await db.ExpectedImports.Where(e => projectCodes.Contains(e.ProjectCode)).ToListAsync(ct);
            db.ExpectedImports.RemoveRange(expectedImports);

            var purchaseOrders = await db.PurchaseOrders
                .Where(p => p.ProjectCode != null && projectCodes.Contains(p.ProjectCode))
                .ToListAsync(ct);
            foreach (var po in purchaseOrders)
                po.ProjectCode = null;

            var linkedTransactions = await db.WarehouseTransactions
                .Where(t =>
                    (t.ProjectCode != null && projectCodes.Contains(t.ProjectCode)) ||
                    (t.BeneficiaryProjectCode != null && projectCodes.Contains(t.BeneficiaryProjectCode)))
                .ToListAsync(ct);
            foreach (var tx in linkedTransactions)
            {
                if (tx.ProjectCode != null && projectCodes.Contains(tx.ProjectCode))
                    tx.ProjectCode = null;
                if (tx.BeneficiaryProjectCode != null && projectCodes.Contains(tx.BeneficiaryProjectCode))
                    tx.BeneficiaryProjectCode = null;
            }
        }

        db.Projects.RemoveRange(constructionProjects);
        await db.SaveChangesAsync(ct);

        var materials = await db.Materials
            .AsNoTracking()
            .Where(m => m.DeletedAt == null && m.IsActive)
            .OrderBy(m => m.MaterialCode)
            .Select(m => m.MaterialCode)
            .ToListAsync(ct);

        if (materials.Count == 0)
        {
            logger.LogWarning("No materials found — skip stock matrix demo seed.");
            return;
        }

        var pmId = Guid.Empty;
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var projects = new List<Project>();

        for (var i = 0; i < DemoProjectNames.Length; i++)
        {
            var code = $"DA-DEMO-{(i + 1):D2}";
            projects.Add(new Project
            {
                ProjectCode = code,
                ClientProjectId = $"proj-demo-{i + 1:D2}",
                ProjectName = DemoProjectNames[i],
                ProjectType = ProjectType.Project,
                PmId = pmId,
                PmName = "PM Demo",
                StartDate = today.AddMonths(-6 + (i % 4)),
                EndDate = i % 5 == 0 ? today.AddMonths(12) : null,
                MaterialBudget = 500_000_000m + i * 75_000_000m,
                Status = i % 7 == 0 ? ProjectStatus.Transferring : ProjectStatus.Active,
                Stage = (i % 3) switch { 0 => "Thi công", 1 => "Lắp đặt", _ => "Bàn giao" },
            });
        }

        db.Projects.AddRange(projects);
        await db.SaveChangesAsync(ct);

        var generalId = WmsWarehouseIds.General;
        var generalInventories = await db.WarehouseInventories
            .Where(i => i.WarehouseId == generalId)
            .ToListAsync(ct);
        db.WarehouseInventories.RemoveRange(generalInventories);

        var draftTransactions = new List<WarehouseTransaction>();
        var holdingReservations = new List<InventoryReservation>();
        var projectInventoryRows = new List<ProjectInventory>();

        for (var mi = 0; mi < materials.Count; mi++)
        {
            var materialCode = materials[mi];
            var generalQty = 40m + (mi + 1) * 12m + (mi % 3) * 25m;
            db.WarehouseInventories.Add(new WarehouseInventory
            {
                WarehouseId = generalId,
                MaterialCode = materialCode,
                Qty = generalQty,
            });

            var totalHoldingForMaterial = 0m;

            for (var pi = 0; pi < projects.Count; pi++)
            {
                var project = projects[pi];
                var seed = mi * 17 + pi * 31;
                var row = new ProjectInventory
                {
                    ProjectCode = project.ProjectCode,
                    MaterialCode = materialCode,
                };

                // Đã cấp phát thực tế (nhập − xuất)
                if (seed % 4 != 3)
                {
                    var imported = 10m + (seed % 90);
                    var exported = seed % 5 == 0 ? imported * 0.3m : seed % 7 == 0 ? imported * 0.6m : 0m;
                    row.ActualImportQty = imported;
                    row.ActualExportQty = Math.Round(exported, 0);
                }

                // Phiếu nhập draft — đang giữ kho / chờ duyệt
                if (seed % 6 == 1 || seed % 11 == 4)
                {
                    var planned = 15m + (seed % 40);
                    var reservedFromStock = Math.Min(planned, Math.Max(0, generalQty - totalHoldingForMaterial));
                    reservedFromStock = Math.Min(reservedFromStock, planned * 0.7m);
                    var shortfall = planned - reservedFromStock;

                    row.PlannedImportQty = planned;
                    row.ReservedQty = reservedFromStock;
                    totalHoldingForMaterial += reservedFromStock;

                    var ticketCode = $"NK-DEMO-{mi + 1:D2}-{pi + 1:D2}";
                    draftTransactions.Add(new WarehouseTransaction
                        {
                            Id = Guid.NewGuid(),
                            TicketCode = ticketCode,
                            TransactionType = TransactionType.Import,
                            ProjectCode = project.ProjectCode,
                            BeneficiaryProjectCode = project.ProjectCode,
                            WarehouseId = generalId,
                            ApprovalStatus = ApprovalStatus.Draft,
                            WarehouseApprovalStatus = WarehouseKeeperApprovalStatus.Pending,
                            CreatedById = pmId,
                            CreatedByName = "Kế hoạch vật tư",
                            SupplierName = "NCC Demo",
                            Notes = "Phiếu nhập dự kiến — demo giữ kho",
                            Items =
                            [
                                new WarehouseTransactionItem
                                {
                                    MaterialCode = materialCode,
                                    Quantity = planned,
                                    UnitPrice = 1_000_000m,
                                }
                            ],
                        });

                    holdingReservations.Add(new InventoryReservation
                    {
                        WarehouseId = generalId,
                        ProjectCode = project.ProjectCode,
                        MaterialCode = materialCode,
                        SourceTicketCode = ticketCode,
                        ReservedQty = reservedFromStock,
                        ExternalQty = shortfall,
                        Status = ReservationStatus.Holding,
                    });
                }

                // Xuất dự kiến
                if (seed % 9 == 2 && row.ActualInventoryQty > 0)
                    row.PlannedExportQty = Math.Min(row.ActualInventoryQty * 0.25m, 20m);

                if (row.ActualImportQty > 0 || row.PlannedImportQty > 0 || row.PlannedExportQty > 0 || row.ReservedQty > 0)
                    projectInventoryRows.Add(row);
            }
        }

        db.WarehouseTransactions.AddRange(draftTransactions);
        await db.SaveChangesAsync(ct);

        db.ProjectInventories.AddRange(projectInventoryRows);
        db.InventoryReservations.AddRange(holdingReservations);
        await db.SaveChangesAsync(ct);

        await SeedDemoVouchersAsync(db, projects, materials, generalId, ct);

        logger.LogInformation(
            "Stock matrix demo seeded: {ProjectCount} projects, {InventoryRows} project_inventory rows, {HoldingCount} holding reservations.",
            projects.Count, projectInventoryRows.Count, holdingReservations.Count);
    }

    private static async Task EnsureGeneralWarehouseAsync(WarehouseDbContext db, CancellationToken ct)
    {
        if (!await db.WmsWarehouses.AnyAsync(w => w.Id == WmsWarehouseIds.General, ct))
        {
            db.WmsWarehouses.Add(new WmsWarehouse
            {
                Id = WmsWarehouseIds.General,
                Code = "KHO-TONG",
                Name = "Kho tổng Nam Việt",
                Group = "Kho tổng",
                Address = "Kho tổng tập trung",
                Branch = "HCM",
                Manager = "Phan Văn Vỹ",
                Status = "active",
            });
        }

        if (!await db.WmsSettings.AnyAsync(s => s.Key == WmsSettingsKeys.GeneralWarehouseId, ct))
        {
            db.WmsSettings.Add(new WmsSetting
            {
                Key = WmsSettingsKeys.GeneralWarehouseId,
                Value = WmsWarehouseIds.General,
            });
        }

        await db.SaveChangesAsync(ct);
    }

    private static async Task SeedDemoVouchersAsync(
        WarehouseDbContext db,
        IReadOnlyList<Project> projects,
        IReadOnlyList<string> materialCodes,
        string generalWarehouseId,
        CancellationToken ct)
    {
        var pmId = Guid.Empty;
        var suppliers = new[] { "CÔNG TY TNHH THIẾT BỊ BẢO KHÁNH", "ĐĂNG THÀNH PHÁT", "MINH PHÚ STEEL", "Hòa Phát Distribution" };
        var keepers = new[] { "Phan Văn Vỹ", "Lê Thị Hồng", "Nguyễn Minh Tuấn" };

        var oldSeedVouchers = await db.WarehouseTransactions
            .Where(t => t.TicketCode.StartsWith("NK-SEED-") || t.TicketCode.StartsWith("XK-SEED-"))
            .ToListAsync(ct);
        db.WarehouseTransactions.RemoveRange(oldSeedVouchers);

        var importStatuses = new[]
        {
            ApprovalStatus.Draft,
            ApprovalStatus.Draft,
            ApprovalStatus.Pending,
            ApprovalStatus.Pending,
            ApprovalStatus.Approved,
            ApprovalStatus.Approved,
        };

        for (var i = 0; i < importStatuses.Length; i++)
        {
            var project = projects[i % projects.Count];
            var mat = materialCodes[i % materialCodes.Count];
            var qty = 10m + i * 7;
            db.WarehouseTransactions.Add(new WarehouseTransaction
            {
                Id = Guid.NewGuid(),
                TicketCode = $"NK-SEED-{i + 1:D3}",
                TransactionType = TransactionType.Import,
                ProjectCode = project.ProjectCode,
                BeneficiaryProjectCode = project.ProjectCode,
                WarehouseId = generalWarehouseId,
                ReferenceCode = $"PO-DEMO-{2026}{i + 1:D4}",
                SupplierName = suppliers[i % suppliers.Length],
                ApprovalStatus = importStatuses[i],
                WarehouseApprovalStatus = importStatuses[i] == ApprovalStatus.Approved
                    ? WarehouseKeeperApprovalStatus.Approved
                    : WarehouseKeeperApprovalStatus.Pending,
                ApprovedByName = importStatuses[i] == ApprovalStatus.Approved ? keepers[i % keepers.Length] : null,
                WarehouseKeeper = keepers[i % keepers.Length],
                CreatedById = pmId,
                CreatedByName = "Kế hoạch vật tư",
                Notes = importStatuses[i] switch
                {
                    ApprovalStatus.Draft => "Phiếu nháp — đã giữ kho cho dự án",
                    ApprovalStatus.Pending => "Chờ thủ kho duyệt nhập",
                    _ => "Đã duyệt nhập kho tổng → cấp dự án",
                },
                Items =
                [
                    new WarehouseTransactionItem
                    {
                        MaterialCode = mat,
                        Quantity = qty,
                        UnitPrice = 1_500_000m + i * 250_000m,
                    }
                ],
            });
        }

        var exportStatuses = new[]
        {
            ApprovalStatus.Pending,
            ApprovalStatus.Pending,
            ApprovalStatus.Approved,
            ApprovalStatus.Approved,
            ApprovalStatus.Draft,
        };

        for (var i = 0; i < exportStatuses.Length; i++)
        {
            var project = projects[(i + 3) % projects.Count];
            var mat = materialCodes[(i + 2) % materialCodes.Count];
            var qty = 5m + i * 3;
            db.WarehouseTransactions.Add(new WarehouseTransaction
            {
                Id = Guid.NewGuid(),
                TicketCode = $"XK-SEED-{i + 1:D3}",
                TransactionType = TransactionType.Export,
                ProjectCode = project.ProjectCode,
                WarehouseId = generalWarehouseId,
                SoCode = $"SO-DEMO-{i + 1:D4}",
                Customer = project.ProjectName,
                ApprovalStatus = exportStatuses[i],
                CreatedById = pmId,
                CreatedByName = keepers[i % keepers.Length],
                Notes = exportStatuses[i] switch
                {
                    ApprovalStatus.Draft => "Nháp xuất vật tư công trình",
                    ApprovalStatus.Pending => "Chờ xác nhận xuất kho",
                    _ => "Đã xuất cho tổ thi công",
                },
                Items =
                [
                    new WarehouseTransactionItem
                    {
                        MaterialCode = mat,
                        Quantity = qty,
                        UnitPrice = 0,
                    }
                ],
            });
        }

        await db.SaveChangesAsync(ct);
    }
}
