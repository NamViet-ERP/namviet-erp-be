using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NamViet.ERP.Modules.Warehouse.Domain;
using NamViet.ERP.Modules.Warehouse.Domain.Entities;
using NamViet.ERP.Modules.Warehouse.Domain.Enums;
using NamViet.ERP.Modules.Warehouse.Infrastructure.Persistence;

namespace NamViet.ERP.Modules.Warehouse.Infrastructure.Services;

public static class WmsSeeder
{
    public static async Task SeedAsync(WarehouseDbContext db, ILogger logger, CancellationToken ct = default)
    {
        await MaterialCategoryTreeLoader.EnsureTreeSeededAsync(db, logger, ct);
        await MaterialDemoSeeder.EnsureDemoMaterialsAsync(db, logger, ct);

        if (await db.WmsWarehouses.AnyAsync(ct))
        {
            logger.LogInformation("WMS seed skipped – warehouse data already exists.");
            return;
        }

        logger.LogInformation("Seeding WMS demo data...");

        await SeedWarehousesAsync(db, ct);
        await SeedSettingsAsync(db, ct);
        await SeedInventoryAsync(db, ct);
        await SeedTransactionsAsync(db, ct);
        await SeedStocktakesAsync(db, ct);
        await SeedAdjustmentsAsync(db, ct);

        await db.SaveChangesAsync(ct);
        logger.LogInformation("WMS demo data seeded.");
    }

    private static async Task SeedWarehousesAsync(WarehouseDbContext db, CancellationToken ct)
    {
        var warehouses = new WmsWarehouse[]
        {
            new() { Id = WmsWarehouseIds.General, Code = "KHO-TONG", Name = "Kho tổng Nam Việt", Group = "Kho tổng", Address = "Kho tổng tập trung", Branch = "HCM", Manager = "Phan Văn Vỹ", Status = "active" },
            new() { Id = "wh-001", Code = "KHO-HCM", Name = "Kho Hồ Chí Minh", Group = "Kho chính", Address = "123 Nguyễn Văn Linh, Q.7, TP.HCM", Branch = "HCM", Manager = "Phan Văn Vỹ", Status = "active" },            new() { Id = "wh-002", Code = "KHO-BD", Name = "Kho Bình Dương", Group = "Kho chính", Address = "456 Đại Lộ Bình Dương, Thủ Dầu Một", Branch = "Bình Dương", Manager = "Trần Huy Hoàng", Status = "active" },
            new() { Id = "wh-003", Code = "KHO-HN", Name = "Kho Hà Nội", Group = "Kho chính", Address = "789 Phạm Hùng, Nam Từ Liêm, Hà Nội", Branch = "Hà Nội", Manager = "Nguyễn Minh Tuấn", Status = "active" },
            new() { Id = "wh-004", Code = "KHO-CT", Name = "Kho Cần Thơ", Group = "Kho phụ", Address = "321 Trần Hưng Đạo, Ninh Kiều, Cần Thơ", Branch = "Cần Thơ", Manager = "Lê Thị Hồng", Status = "active" },
            new() { Id = "wh-005", Code = "KHO-DN", Name = "Kho Đà Nẵng", Group = "Kho phụ", Address = "654 Nguyễn Văn Linh, Hải Châu, Đà Nẵng", Branch = "Đà Nẵng", Manager = "Võ Anh Khoa", Status = "active" },
            new() { Id = "wh-006", Code = "KHO-HL", Name = "Kho hư lỗi HCM", Group = "Kho hư lỗi", Address = "123 Nguyễn Văn Linh, Q.7, TP.HCM", Branch = "HCM", Manager = "Phan Văn Vỹ", Status = "active" },
            new() { Id = "wh-007", Code = "KHO-BH", Name = "Kho bảo hành HCM", Group = "Kho bảo hành", Address = "123 Nguyễn Văn Linh, Q.7, TP.HCM", Branch = "HCM", Manager = "Mỹ Chi", Status = "active" },
        };
        db.WmsWarehouses.AddRange(warehouses);
        await db.SaveChangesAsync(ct);
    }

    private static async Task SeedSettingsAsync(WarehouseDbContext db, CancellationToken ct)
    {
        db.WmsSettings.Add(new WmsSetting
        {
            Key = WmsSettingsKeys.GeneralWarehouseId,
            Value = WmsWarehouseIds.General
        });
        await db.SaveChangesAsync(ct);
    }

    private static async Task SeedInventoryAsync(WarehouseDbContext db, CancellationToken ct)
    {
        var entries = new (string WarehouseId, string Sku, decimal Qty)[]
        {
            (WmsWarehouseIds.General, "MNK-SC-7.5", 120), (WmsWarehouseIds.General, "MNK-SC-15", 80),
            (WmsWarehouseIds.General, "LLK-10", 200), (WmsWarehouseIds.General, "DK-OIL-20L", 150),
            (WmsWarehouseIds.General, "TE-PLT-012-SA516", 45),
            ("wh-001", "MNK-SC-7.5", 45), ("wh-001", "MNK-SC-15", 22), ("wh-001", "LLK-10", 120),
            ("wh-001", "DK-OIL-20L", 84), ("wh-001", "BM-CN-15KW", 8), ("wh-001", "BLK-M12x50-88", 500),
            ("wh-002", "BM-CN-15KW", 5), ("wh-002", "BLK-M12x50-88", 200),
            ("wh-003", "VN-CAU-MB-DN100", 12), ("wh-003", "IX-304-2B-3T", 30),
        };
        foreach (var e in entries)
        {
            db.WarehouseInventories.Add(new WarehouseInventory
            {
                WarehouseId = e.WarehouseId,
                MaterialCode = e.Sku,
                Qty = e.Qty
            });
        }
        await db.SaveChangesAsync(ct);
    }

    private static async Task SeedTransactionsAsync(WarehouseDbContext db, CancellationToken ct)
    {
        var adminId = Guid.Empty;

        db.WarehouseTransactions.Add(new WarehouseTransaction
        {
            Id = Guid.NewGuid(),
            TicketCode = "NK-002146",
            TransactionType = TransactionType.Import,
            WarehouseId = "wh-001",
            ReferenceCode = "PO-002411",
            SupplierName = "CÔNG TY TNHH THIẾT BỊ CÔNG NGHIỆP BẢO KHÁNH",
            WarehouseApprovalStatus = WarehouseKeeperApprovalStatus.Approved,
            ApprovedByName = "Lê Thị Hồng",
            WarehouseKeeper = "Lê Thị Hồng",
            CreatedById = adminId,
            CreatedByName = "Mỹ Chi",
            ApprovalStatus = ApprovalStatus.Approved,
            Items = [new WarehouseTransactionItem { MaterialCode = "LLK-10", Quantity = 24, UnitPrice = 3500000 }]
        });

        db.WarehouseTransactions.Add(new WarehouseTransaction
        {
            Id = Guid.NewGuid(),
            TicketCode = "NK-002144",
            TransactionType = TransactionType.Import,
            WarehouseId = "wh-001",
            ReferenceCode = "PO-002410",
            SupplierName = "CÔNG TY TNHH ĐĂNG THÀNH PHÁT",
            WarehouseApprovalStatus = WarehouseKeeperApprovalStatus.Approved,
            ApprovedByName = "Mỹ Chi",
            WarehouseKeeper = "Phan Văn Vỹ",
            CreatedById = adminId,
            CreatedByName = "Mỹ Chi",
            ApprovalStatus = ApprovalStatus.Approved,
            Items = [new WarehouseTransactionItem { MaterialCode = "MNK-SC-7.5", Quantity = 2, UnitPrice = 42606000 }]
        });

        db.WarehouseTransactions.Add(new WarehouseTransaction
        {
            Id = Guid.NewGuid(),
            TicketCode = "XK-001892",
            TransactionType = TransactionType.Export,
            WarehouseId = "wh-001",
            SoCode = "SO-003456",
            Customer = "CÔNG TY TNHH CƠ KHÍ THÀNH ĐẠT",
            CreatedById = adminId,
            CreatedByName = "Phan Văn Vỹ",
            ApprovalStatus = ApprovalStatus.Approved,
            Notes = "Giao hàng theo SO",
            Items = [new WarehouseTransactionItem { MaterialCode = "MNK-SC-7.5", Quantity = 2, UnitPrice = 0 }]
        });

        db.WarehouseTransactions.Add(new WarehouseTransaction
        {
            Id = Guid.NewGuid(),
            TicketCode = "CK-000124",
            TransactionType = TransactionType.Transfer,
            FromWarehouseId = "wh-001",
            ToWarehouseId = "wh-003",
            ApprovedByName = "Mỹ Chi",
            CreatedById = adminId,
            CreatedByName = "Phan Văn Vỹ",
            ApprovalStatus = ApprovalStatus.Approved,
            Notes = "Chuyển hàng bổ sung cho kho HN",
            Items =
            [
                new WarehouseTransactionItem { MaterialCode = "MNK-SC-7.5", Quantity = 5, UnitPrice = 0 },
                new WarehouseTransactionItem { MaterialCode = "BLK-M12x50-88", Quantity = 10, UnitPrice = 0 }
            ]
        });

        await db.SaveChangesAsync(ct);
    }

    private static async Task SeedStocktakesAsync(WarehouseDbContext db, CancellationToken ct)
    {
        var adminId = Guid.Empty;
        var st = new Stocktake
        {
            Id = "st-001",
            VoucherCode = "KK-001529",
            VoucherDate = new DateOnly(2026, 5, 27),
            CreatedById = adminId,
            CreatedByName = "Trần Huy Hoàng",
            Status = "pending",
            WarehouseId = "wh-002",
            WarehouseName = "BÌNH DƯƠNG",
            Condition = "counting",
            IsFrozen = true,
            Note = "Kiểm kê cuối tháng 5",
            Items =
            [
                new StocktakeItem { StocktakeId = "st-001", MaterialCode = "MNK-SC-7.5", Sku = "MNK-SC-7.5", Name = "Máy nén khí trục vít 7.5HP", SystemQty = 15, ActualQty = 14, Diff = -1 },
                new StocktakeItem { StocktakeId = "st-001", MaterialCode = "BM-CN-15KW", Sku = "BM-CN-15KW", Name = "Bơm ly tâm trục đứng 15kW", SystemQty = 8, ActualQty = 8, Diff = 0 }
            ]
        };
        db.Stocktakes.Add(st);

        db.Stocktakes.Add(new Stocktake
        {
            Id = "st-002",
            VoucherCode = "KK-001528",
            VoucherDate = new DateOnly(2026, 5, 25),
            CreatedById = adminId,
            CreatedByName = "Ái Du",
            Status = "approved",
            WarehouseId = "wh-001",
            WarehouseName = "HỒ CHÍ MINH",
            Condition = "completed",
            IsFrozen = false,
            Note = ""
        });

        await db.SaveChangesAsync(ct);
    }

    private static async Task SeedAdjustmentsAsync(WarehouseDbContext db, CancellationToken ct)
    {
        var adminId = Guid.Empty;
        db.Adjustments.Add(new Adjustment
        {
            Id = "adj-001",
            VoucherCode = "ĐCT-001452",
            VoucherDate = new DateOnly(2026, 5, 27),
            StocktakeCode = "KK-001529",
            Type = "increase",
            CreatedById = adminId,
            CreatedByName = "Trần Huy Hoàng",
            Condition = "exported",
            WarehouseId = "wh-002",
            WarehouseName = "BÌNH DƯƠNG",
            Note = "",
            IsNew = true,
            Items =
            [
                new AdjustmentItem
                {
                    AdjustmentId = "adj-001",
                    MaterialCode = "BLK-M12x50-88",
                    Sku = "BLK-M12x50-88",
                    Name = "Bulong M12×50 cấp 8.8",
                    AdjustQty = 200,
                    Reason = "Thừa khi kiểm kê"
                }
            ]
        });

        await db.SaveChangesAsync(ct);
    }
}
