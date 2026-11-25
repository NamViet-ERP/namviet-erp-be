using Microsoft.EntityFrameworkCore;
using NamViet.ERP.Api.Application.Services.Purchasing;
using NamViet.ERP.Modules.Warehouse.Domain.Entities;
using NamViet.ERP.Modules.Warehouse.Domain.Enums;
using NamViet.ERP.Modules.Warehouse.Infrastructure.Persistence;
using NamViet.ERP.Tests.Wms;

namespace NamViet.ERP.Tests.Purchasing;

public class PurchaseWarrantyServiceTests : IDisposable
{
    private readonly WarehouseDbContext _db;
    private readonly FakeCurrentUser _user = new();
    private readonly PurchaseWarrantyService _service;

    public PurchaseWarrantyServiceTests()
    {
        var options = new DbContextOptionsBuilder<WarehouseDbContext>()
            .UseInMemoryDatabase($"warranty-test-{Guid.NewGuid():N}")
            .Options;
        _db = new WarehouseDbContext(options);
        _service = new PurchaseWarrantyService(_db, _user);
    }

    public void Dispose()
    {
        _db.Dispose();
        GC.SuppressFinalize(this);
    }

    [Fact]
    public async Task UpsertFromApprovedImportAsync_WithMultipleWarrantyItems_GeneratesDistinctSequentialCodes()
    {
        var po = new PurchaseOrder
        {
            Code = "PO-BH-001",
            ProjectCode = "DA-BH",
            PlanCode = "KHMH-BH-001",
            SupplierId = 1,
            SupplierNameSnapshot = "Công ty TNHH Thiết Bị Nhiệt",
            OrderDate = new DateOnly(2026, 11, 20),
            ApprovalStatus = PurchaseOrderApprovalStatuses.Approved,
            OrderPlacementStatus = PurchaseOrderPlacementStatuses.Placed,
            Items =
            [
                new PurchaseOrderItem
                {
                    Id = 101,
                    PoCode = "PO-BH-001",
                    MaterialCode = "BOM-001",
                    Sku = "BOM-001",
                    Name = "Bơm nước cấp lò hơi Grundfos CR15",
                    Unit = "Cái",
                    Ordered = 2,
                    Received = 2,
                    WarrantyMonths = 12,
                    WarrantyAnchorType = PurchaseWarrantyAnchorTypes.Delivery,
                },
                new PurchaseOrderItem
                {
                    Id = 102,
                    PoCode = "PO-BH-001",
                    MaterialCode = "VAN-002",
                    Sku = "VAN-002",
                    Name = "Van an toàn gang nối bích DN50",
                    Unit = "Cái",
                    Ordered = 4,
                    Received = 4,
                    WarrantyMonths = 24,
                    WarrantyAnchorType = PurchaseWarrantyAnchorTypes.Delivery,
                },
                new PurchaseOrderItem
                {
                    Id = 103,
                    PoCode = "PO-BH-001",
                    MaterialCode = "GIOANG-003",
                    Sku = "GIOANG-003",
                    Name = "Gioăng làm kín mặt bích",
                    Unit = "Cái",
                    Ordered = 10,
                    Received = 10,
                    WarrantyMonths = 0,
                },
            ],
        };
        _db.PurchaseOrders.Add(po);
        await _db.SaveChangesAsync();

        var import = new WarehouseTransaction
        {
            TicketCode = "NK-BH-001",
            TransactionType = TransactionType.Import,
            ReferenceCode = "PO-BH-001",
            BeneficiaryProjectCode = "DA-BH",
            ApprovalStatus = ApprovalStatus.Approved,
            Items =
            [
                new WarehouseTransactionItem
                {
                    TicketCode = "NK-BH-001",
                    MaterialCode = "BOM-001",
                    MaterialNameSnapshot = "Bơm nước cấp lò hơi Grundfos CR15",
                    Quantity = 2,
                    Notes = $"{PurchaseOrderReceiptService.PoItemNotePrefix}101",
                },
                new WarehouseTransactionItem
                {
                    TicketCode = "NK-BH-001",
                    MaterialCode = "VAN-002",
                    MaterialNameSnapshot = "Van an toàn gang nối bích DN50",
                    Quantity = 4,
                    Notes = $"{PurchaseOrderReceiptService.PoItemNotePrefix}102",
                },
                new WarehouseTransactionItem
                {
                    TicketCode = "NK-BH-001",
                    MaterialCode = "GIOANG-003",
                    MaterialNameSnapshot = "Gioăng làm kín mặt bích",
                    Quantity = 10,
                    Notes = $"{PurchaseOrderReceiptService.PoItemNotePrefix}103",
                },
            ],
        };

        await _service.UpsertFromApprovedImportAsync(import);
        await _db.SaveChangesAsync();

        var warranties = await _db.PurchaseWarranties
            .OrderBy(w => w.Code)
            .ToListAsync();

        Assert.Equal(2, warranties.Count);
        Assert.Equal(["BH-0001", "BH-0002"], warranties.Select(w => w.Code).ToArray());
        Assert.Equal(2, warranties.Select(w => w.PoItemId).Distinct().Count());
    }

    [Fact]
    public async Task UpsertFromApprovedImportAsync_WithDuplicateImportLinesForSamePoItem_CreatesSingleWarranty()
    {
        var po = new PurchaseOrder
        {
            Code = "PO-BH-DUP",
            ProjectCode = "DA-BH",
            PlanCode = "KHMH-BH-DUP",
            SupplierId = 2,
            SupplierNameSnapshot = "Công ty Cơ Khí An Bình",
            OrderDate = new DateOnly(2026, 11, 21),
            ApprovalStatus = PurchaseOrderApprovalStatuses.Approved,
            OrderPlacementStatus = PurchaseOrderPlacementStatuses.Placed,
            Items =
            [
                new PurchaseOrderItem
                {
                    Id = 201,
                    PoCode = "PO-BH-DUP",
                    MaterialCode = "QUAT-HT-01",
                    Sku = "QUAT-HT-01",
                    Name = "Quạt hút khói ly tâm 15kW",
                    Unit = "Bộ",
                    Ordered = 5,
                    Received = 5,
                    WarrantyMonths = 18,
                    WarrantyAnchorType = PurchaseWarrantyAnchorTypes.Delivery,
                },
            ],
        };
        _db.PurchaseOrders.Add(po);
        await _db.SaveChangesAsync();

        var import = new WarehouseTransaction
        {
            TicketCode = "NK-BH-DUP",
            TransactionType = TransactionType.Import,
            ReferenceCode = "PO-BH-DUP",
            BeneficiaryProjectCode = "DA-BH",
            ApprovalStatus = ApprovalStatus.Approved,
            Items =
            [
                new WarehouseTransactionItem
                {
                    TicketCode = "NK-BH-DUP",
                    MaterialCode = "QUAT-HT-01",
                    MaterialNameSnapshot = "Quạt hút khói ly tâm 15kW",
                    Quantity = 2,
                    Notes = $"{PurchaseOrderReceiptService.PoItemNotePrefix}201",
                },
                new WarehouseTransactionItem
                {
                    TicketCode = "NK-BH-DUP",
                    MaterialCode = "QUAT-HT-01",
                    MaterialNameSnapshot = "Quạt hút khói ly tâm 15kW",
                    Quantity = 3,
                    Notes = $"{PurchaseOrderReceiptService.PoItemNotePrefix}201",
                },
            ],
        };

        await _service.UpsertFromApprovedImportAsync(import);
        await _db.SaveChangesAsync();

        var warranty = await _db.PurchaseWarranties.SingleAsync(w => w.PoItemId == 201);
        Assert.Equal("BH-0001", warranty.Code);
        Assert.Equal(5m, warranty.Quantity);
        Assert.Equal(18, warranty.WarrantyMonths);
    }

    [Fact]
    public async Task SyncFromReceivedPoLinesAsync_WithMultiplePoItemsAndUtcLateNightTimestamp_ConvertsToVietnamDate()
    {
        var po = new PurchaseOrder
        {
            Code = "PO-SYNC-001",
            ProjectCode = "DA-SYNC",
            PlanCode = "KHMH-SYNC-001",
            SupplierId = 3,
            SupplierNameSnapshot = "Nhà cung cấp Tự Động Hóa",
            OrderDate = new DateOnly(2026, 11, 24),
            PlacedAt = new DateTime(2026, 11, 24, 20, 0, 0, DateTimeKind.Utc),
            CreatedAt = new DateTime(2026, 11, 24, 19, 0, 0, DateTimeKind.Utc),
            ApprovalStatus = PurchaseOrderApprovalStatuses.Approved,
            OrderPlacementStatus = PurchaseOrderPlacementStatuses.Placed,
            Items =
            [
                new PurchaseOrderItem
                {
                    Id = 301,
                    PoCode = "PO-SYNC-001",
                    MaterialCode = "PLC-S7-1200",
                    Sku = "PLC-S7-1200",
                    Name = "Bộ điều khiển PLC Siemens S7-1200",
                    Unit = "Bộ",
                    Ordered = 2,
                    Received = 2,
                    WarrantyMonths = 12,
                    WarrantyAnchorType = PurchaseWarrantyAnchorTypes.Delivery,
                },
                new PurchaseOrderItem
                {
                    Id = 302,
                    PoCode = "PO-SYNC-001",
                    MaterialCode = "BIEN-TAN-11KW",
                    Sku = "BIEN-TAN-11KW",
                    Name = "Biến tần ABB 11kW",
                    Unit = "Bộ",
                    Ordered = 3,
                    Received = 3,
                    WarrantyMonths = 24,
                    WarrantyAnchorType = PurchaseWarrantyAnchorTypes.Delivery,
                },
            ],
        };
        _db.PurchaseOrders.Add(po);
        await _db.SaveChangesAsync();

        var result = await _service.SyncFromReceivedPoLinesAsync();

        Assert.Equal(2, result.Created);
        Assert.Equal(0, result.Updated);

        var warranties = await _db.PurchaseWarranties
            .OrderBy(w => w.Code)
            .ToListAsync();

        Assert.Equal(2, warranties.Count);
        Assert.Equal(["BH-0001", "BH-0002"], warranties.Select(w => w.Code).ToArray());
        // 2026-11-24T20:00:00Z == 2026-11-25 03:00:00 in Asia/Ho_Chi_Minh (UTC+7)
        Assert.All(warranties, w => Assert.Equal(new DateOnly(2026, 11, 25), w.DeliveryDate));
    }
}
