using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NamViet.ERP.Modules.Warehouse.Domain.Entities;
using NamViet.ERP.Modules.Warehouse.Domain.Enums;
using NamViet.ERP.Modules.Warehouse.Infrastructure.Persistence;

namespace NamViet.ERP.Modules.Warehouse.Infrastructure.Services;

public static class WmsSeeder
{
    public static async Task SeedAsync(WarehouseDbContext db, ILogger logger, CancellationToken ct = default)
    {
        if (await db.WmsWarehouses.AnyAsync(ct))
        {
            logger.LogInformation("WMS seed skipped – data already exists.");
            return;
        }

        logger.LogInformation("Seeding WMS demo data...");

        await SeedMaterialsAsync(db, ct);
        await SeedWarehousesAsync(db, ct);
        await SeedInventoryAsync(db, ct);
        await SeedTransactionsAsync(db, ct);
        await SeedStocktakesAsync(db, ct);
        await SeedAdjustmentsAsync(db, ct);

        await db.SaveChangesAsync(ct);
        logger.LogInformation("WMS demo data seeded.");
    }

    private static async Task SeedMaterialsAsync(WarehouseDbContext db, CancellationToken ct)
    {
        var supplier = new Supplier
        {
            SupplierCode = "NCC-DEMO",
            SupplierName = "Nhà cung cấp demo",
            Rating = "A"
        };
        db.Suppliers.Add(supplier);
        await db.SaveChangesAsync(ct);

        var products = new (string Sku, string Name, string Category, string Unit, decimal Price)[]
        {
            ("MNK-SC-7.5", "Máy nén khí trục vít 7.5HP", "Máy nén khí trục vít", "Cái", 45000000),
            ("MNK-SC-15", "Máy nén khí trục vít 15HP", "Máy nén khí trục vít", "Cái", 78000000),
            ("MNK-SC-20", "Máy nén khí trục vít 20HP", "Máy nén khí trục vít", "Cái", 95000000),
            ("MNK-PT-3", "Máy nén khí piston 3HP", "Máy nén khí piston", "Cái", 12000000),
            ("MNK-PT-5", "Máy nén khí piston 5HP", "Máy nén khí piston", "Cái", 18500000),
            ("BLK-500", "Bình chứa khí nén 500L", "Bình chứa", "Cái", 8500000),
            ("BLK-1000", "Bình chứa khí nén 1000L", "Bình chứa", "Cái", 15000000),
            ("MBK-R22", "Máy sấy khí lạnh R22", "Máy sấy khí", "Cái", 25000000),
            ("LLK-10", "Lọc khí công nghiệp 10μm", "Lọc khí", "Cái", 3500000),
            ("DK-OIL-20L", "Dầu máy nén khí 20L", "Phụ kiện", "Thùng", 2800000),
            ("MNK-SC-30", "Máy nén khí trục vít 30HP", "Máy nén khí trục vít", "Cái", 135000000),
            ("MNK-SC-50", "Máy nén khí trục vít 50HP", "Máy nén khí trục vít", "Cái", 210000000),
            ("QH-KT-421", "Que hàn điện Kim Tín KT-421", "Vật tư tiêu hao", "Hộp", 120000),
            ("GT-BH-DA", "Găng tay bảo hộ hàn da bò", "Vật tư tiêu hao", "Đôi", 45000),
            ("DC-HD-100", "Đá cắt sắt Hải Dương 100mm", "Vật tư tiêu hao", "Viên", 15000),
        };

        foreach (var p in products)
        {
            db.Materials.Add(new Material
            {
                MaterialCode = p.Sku,
                MaterialName = p.Name,
                MaterialCategory = p.Category,
                Unit = p.Unit
            });
            db.PriceBooks.Add(new PriceBook
            {
                MaterialCode = p.Sku,
                SupplierId = supplier.Id,
                UnitPrice = p.Price,
                PriceDate = DateTime.UtcNow
            });
        }
        await db.SaveChangesAsync(ct);
    }

    private static async Task SeedWarehousesAsync(WarehouseDbContext db, CancellationToken ct)
    {
        var warehouses = new WmsWarehouse[]
        {
            new() { Id = "wh-001", Code = "KHO-HCM", Name = "Kho Hồ Chí Minh", Group = "Kho chính", Address = "123 Nguyễn Văn Linh, Q.7, TP.HCM", Branch = "HCM", Manager = "Phan Văn Vỹ", Status = "active" },
            new() { Id = "wh-002", Code = "KHO-BD", Name = "Kho Bình Dương", Group = "Kho chính", Address = "456 Đại Lộ Bình Dương, Thủ Dầu Một", Branch = "Bình Dương", Manager = "Trần Huy Hoàng", Status = "active" },
            new() { Id = "wh-003", Code = "KHO-HN", Name = "Kho Hà Nội", Group = "Kho chính", Address = "789 Phạm Hùng, Nam Từ Liêm, Hà Nội", Branch = "Hà Nội", Manager = "Nguyễn Minh Tuấn", Status = "active" },
            new() { Id = "wh-004", Code = "KHO-CT", Name = "Kho Cần Thơ", Group = "Kho phụ", Address = "321 Trần Hưng Đạo, Ninh Kiều, Cần Thơ", Branch = "Cần Thơ", Manager = "Lê Thị Hồng", Status = "active" },
            new() { Id = "wh-005", Code = "KHO-DN", Name = "Kho Đà Nẵng", Group = "Kho phụ", Address = "654 Nguyễn Văn Linh, Hải Châu, Đà Nẵng", Branch = "Đà Nẵng", Manager = "Võ Anh Khoa", Status = "active" },
            new() { Id = "wh-006", Code = "KHO-HL", Name = "Kho hư lỗi HCM", Group = "Kho hư lỗi", Address = "123 Nguyễn Văn Linh, Q.7, TP.HCM", Branch = "HCM", Manager = "Phan Văn Vỹ", Status = "active" },
            new() { Id = "wh-007", Code = "KHO-BH", Name = "Kho bảo hành HCM", Group = "Kho bảo hành", Address = "123 Nguyễn Văn Linh, Q.7, TP.HCM", Branch = "HCM", Manager = "Mỹ Chi", Status = "active" },
        };
        db.WmsWarehouses.AddRange(warehouses);
        await db.SaveChangesAsync(ct);
    }

    private static async Task SeedInventoryAsync(WarehouseDbContext db, CancellationToken ct)
    {
        var entries = new (string WarehouseId, string Sku, decimal Qty)[]
        {
            ("wh-001", "MNK-SC-7.5", 45), ("wh-001", "MNK-SC-15", 22), ("wh-001", "LLK-10", 120),
            ("wh-001", "DK-OIL-20L", 84), ("wh-002", "MNK-PT-3", 30), ("wh-002", "BLK-500", 50),
            ("wh-003", "MNK-PT-5", 15), ("wh-003", "BLK-500", 20),
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
                new WarehouseTransactionItem { MaterialCode = "BLK-500", Quantity = 10, UnitPrice = 0 }
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
                new StocktakeItem { StocktakeId = "st-001", MaterialCode = "MNK-PT-3", Sku = "MNK-PT-3", Name = "Máy nén khí piston 3HP", SystemQty = 30, ActualQty = 30, Diff = 0 }
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
                    MaterialCode = "BLK-500",
                    Sku = "BLK-500",
                    Name = "Bình chứa khí nén 500L",
                    AdjustQty = 2,
                    Reason = "Thừa khi kiểm kê"
                }
            ]
        });

        await db.SaveChangesAsync(ct);
    }
}
