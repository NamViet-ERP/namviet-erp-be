using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using NamViet.ERP.Api.Application.Dtos.Purchasing;
using NamViet.ERP.Api.Application.Services.Purchasing;
using NamViet.ERP.Modules.Warehouse.Domain.Entities;
using NamViet.ERP.Modules.Warehouse.Domain.Enums;
using NamViet.ERP.Modules.Warehouse.Infrastructure.Persistence;
using NamViet.ERP.Tests.Wms;

namespace NamViet.ERP.Tests.Purchasing;

public class PurchaseOrderReceiptServiceTests : IDisposable
{
    private readonly WarehouseDbContext _db;
    private readonly FakeCurrentUser _user = new();
    private readonly PurchasePlanService _plans;
    private readonly PurchaseOrderService _orders;
    private readonly PurchaseOrderReceiptService _receipt;

    public PurchaseOrderReceiptServiceTests()
    {
        var options = new DbContextOptionsBuilder<WarehouseDbContext>()
            .UseInMemoryDatabase($"po-receipt-{Guid.NewGuid():N}")
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        _db = new WarehouseDbContext(options);
        Seed();

        _plans = new PurchasePlanService(_db, new PurchasePlanCodeGenerator(_db), _user);
        _orders = new PurchaseOrderService(
            _db, new PurchaseOrderCodeGenerator(_db), _plans, _user);
        _receipt = new PurchaseOrderReceiptService(_db);
    }

    [Fact]
    public async Task BuildImportDraft_RespectsPendingNkReservation()
    {
        var order = await SeedPlacedOrderAsync(qty: 10, orderQty: 10);
        var poItemId = (await _db.PurchaseOrderItems.AsNoTracking()
            .FirstAsync(i => i.PoCode == order.Code)).Id;

        _db.WarehouseTransactions.Add(new WarehouseTransaction
        {
            Id = Guid.NewGuid(),
            TicketCode = "NK-PEND-1",
            TransactionType = TransactionType.Import,
            ReferenceCode = order.Code,
            BeneficiaryProjectCode = "DA-TEST",
            ApprovalStatus = ApprovalStatus.Pending,
            Items =
            [
                new WarehouseTransactionItem
                {
                    MaterialCode = "VT-ONG",
                    MaterialNameSnapshot = "Ống thép",
                    Quantity = 7,
                    UnitPrice = 1000,
                    Notes = $"{PurchaseOrderReceiptService.PoItemNotePrefix}{poItemId}",
                },
            ],
        });
        await _db.SaveChangesAsync();

        var preview = await _receipt.PreviewImportFromPoAsync(order.Code);
        Assert.Equal(3m, preview.Lines.Single().Available);

        var dto = await _receipt.BuildImportDraftFromPoAsync(order.Code, [
            new CreateImportFromPoLineRequest(poItemId, 3),
        ]);
        Assert.Equal(3m, dto.Items[0].Qty);
        Assert.Equal(order.PlanCode, dto.PlanCode);

        var ex = await Assert.ThrowsAsync<PurchasingConflictException>(() =>
            _receipt.BuildImportDraftFromPoAsync(order.Code, [
                new CreateImportFromPoLineRequest(poItemId, 4),
            ]));
        Assert.Contains("chỉ còn", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task BuildImportDraft_FromPlacedPo_MapsRemainingQty()
    {
        var order = await SeedPlacedOrderAsync(qty: 10, orderQty: 6);
        var dto = await _receipt.BuildImportDraftFromPoAsync(order.Code);

        Assert.Equal(order.Code, dto.ReferenceCode);
        Assert.Equal("DA-TEST", dto.Project);
        Assert.Single(dto.Items);
        Assert.Equal(6m, dto.Items[0].Qty);
        Assert.Equal("VT-ONG", dto.Items[0].Sku);
        Assert.StartsWith(PurchaseOrderReceiptService.PoItemNotePrefix, dto.Items[0].Notes);
    }

    [Fact]
    public async Task BuildImportDraft_RejectsWhenNotPlaced()
    {
        var plan = await SeedPlanAsync();
        var order = await _orders.CreateAsync(new CreatePurchaseOrderRequest(
            plan.Code, 1, null, null,
            [new CreatePurchaseOrderLineRequest(plan.Lines[0].Id, 2, 1000, null, null)]),
            CancellationToken.None);

        var ex = await Assert.ThrowsAsync<PurchasingConflictException>(() =>
            _receipt.BuildImportDraftFromPoAsync(order.Code));
        Assert.Contains("placed", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ApplyApprovedImport_UpdatesReceivedAndDeliveryStatus()
    {
        var order = await SeedPlacedOrderAsync(qty: 10, orderQty: 8);
        var poItemId = (await _db.PurchaseOrderItems.AsNoTracking()
            .FirstAsync(i => i.PoCode == order.Code)).Id;

        var import = new WarehouseTransaction
        {
            Id = Guid.NewGuid(),
            TicketCode = "NK-TEST-1",
            TransactionType = TransactionType.Import,
            ReferenceCode = order.Code,
            BeneficiaryProjectCode = "DA-TEST",
            ApprovalStatus = ApprovalStatus.Approved,
            Items =
            [
                new WarehouseTransactionItem
                {
                    MaterialCode = "VT-ONG",
                    MaterialNameSnapshot = "Ống thép",
                    Quantity = 5,
                    UnitPrice = 1000,
                    Notes = $"{PurchaseOrderReceiptService.PoItemNotePrefix}{poItemId}",
                },
            ],
        };

        await _receipt.ApplyApprovedImportReceiptAsync(import);
        await _db.SaveChangesAsync();

        var refreshed = await _db.PurchaseOrders
            .Include(o => o.Items)
            .FirstAsync(o => o.Code == order.Code);
        Assert.Equal(5m, refreshed.Items.Single().Received);
        Assert.Equal(PurchaseOrderDeliveryStatuses.Partial, refreshed.DeliveryStatus);

        var planLine = await _db.PurchasePlanLines.FirstAsync(l => l.Id == refreshed.Items.Single().PlanLineId);
        Assert.Equal(5m, planLine.ReceivedQty);
        Assert.Equal(PurchaseLineStatuses.PartialDelivery, planLine.PurchaseStatus);
    }

    [Fact]
    public async Task ApplyApprovedImport_CompletesWhenFullyReceived()
    {
        var order = await SeedPlacedOrderAsync(qty: 10, orderQty: 4);
        var poItemId = (await _db.PurchaseOrderItems.AsNoTracking()
            .FirstAsync(i => i.PoCode == order.Code)).Id;

        var import = new WarehouseTransaction
        {
            Id = Guid.NewGuid(),
            TicketCode = "NK-TEST-2",
            TransactionType = TransactionType.Import,
            ReferenceCode = order.Code,
            BeneficiaryProjectCode = "DA-TEST",
            ApprovalStatus = ApprovalStatus.Approved,
            Items =
            [
                new WarehouseTransactionItem
                {
                    MaterialCode = "VT-ONG",
                    Quantity = 4,
                    UnitPrice = 1000,
                    Notes = $"{PurchaseOrderReceiptService.PoItemNotePrefix}{poItemId}",
                },
            ],
        };

        await _receipt.ApplyApprovedImportReceiptAsync(import);
        await _db.SaveChangesAsync();

        var refreshed = await _db.PurchaseOrders
            .Include(o => o.Items)
            .FirstAsync(o => o.Code == order.Code);
        Assert.Equal(4m, refreshed.Items.Single().Received);
        Assert.Equal(PurchaseOrderDeliveryStatuses.Complete, refreshed.DeliveryStatus);
    }

    private async Task<PurchaseOrderDetailDto> SeedPlacedOrderAsync(decimal qty, decimal orderQty)
    {
        var plan = await SeedPlanAsync(qty);
        var order = await _orders.CreateAsync(new CreatePurchaseOrderRequest(
            plan.Code, 1, null, null,
            [new CreatePurchaseOrderLineRequest(plan.Lines[0].Id, orderQty, 1000, null, null)]),
            CancellationToken.None);
        await _orders.ApproveAsync(order.Code, CancellationToken.None);
        return (await _orders.PlaceAsync(order.Code, CancellationToken.None))!;
    }

    private async Task<PurchasePlanDetailDto> SeedPlanAsync(decimal qty = 10)
    {
        var created = await _plans.CreateAsync(new CreatePurchasePlanRequest("DA-TEST", "test"), CancellationToken.None);
        var plan = await _db.PurchasePlans.Include(p => p.Lines).FirstAsync(p => p.Code == created.Code);
        plan.Lines.Add(new PurchasePlanLine
        {
            PlanCode = plan.Code,
            LineNo = 1,
            MaterialCode = "VT-ONG",
            ProductName = "Ống thép",
            Unit = "Cái",
            Quantity = qty,
            ProposalSupplierName = "NCC Alpha",
            SelectedSupplierName = "NCC Alpha",
            PurchaseStatus = PurchaseLineStatuses.NotPurchased,
            BudgetUnitPrice = 1000,
            BudgetAmount = 1000 * qty,
        });
        _plans.RecalcTotals(plan);
        await _db.SaveChangesAsync();
        return (await _plans.GetByCodeAsync(plan.Code))!;
    }

    private void Seed()
    {
        _db.Projects.Add(new Project
        {
            ProjectCode = "DA-TEST",
            ProjectName = "Dự án test",
            PmName = "PM",
            StartDate = DateOnly.FromDateTime(DateTime.UtcNow),
            Status = ProjectStatus.Active,
        });
        _db.Suppliers.Add(new Supplier
        {
            Id = 1,
            SupplierCode = "NCC-A",
            SupplierName = "NCC Alpha",
            MaxDebtDays = 45,
        });
        _db.Materials.Add(new Material
        {
            MaterialCode = "VT-ONG",
            MaterialName = "Ống thép",
            CategoryId = 1,
            Unit = "Cái",
            IsActive = true,
        });
        _db.SaveChanges();
    }

    public void Dispose() => _db.Dispose();
}
