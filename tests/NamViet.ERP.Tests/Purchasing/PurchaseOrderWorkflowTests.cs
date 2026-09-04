using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using NamViet.ERP.Api.Application.Dtos.Purchasing;
using NamViet.ERP.Api.Application.Services.Purchasing;
using NamViet.ERP.Api.Application.Validators;
using NamViet.ERP.Modules.Warehouse.Domain.Entities;
using NamViet.ERP.Modules.Warehouse.Domain.Enums;
using NamViet.ERP.Modules.Warehouse.Infrastructure.Persistence;
using NamViet.ERP.Tests.Wms;

namespace NamViet.ERP.Tests.Purchasing;

public class PurchaseOrderWorkflowTests : IDisposable
{
    private readonly WarehouseDbContext _db;
    private readonly FakeCurrentUser _user = new();
    private readonly PurchasePlanService _plans;
    private readonly PurchaseOrderService _orders;

    public PurchaseOrderWorkflowTests()
    {
        var options = new DbContextOptionsBuilder<WarehouseDbContext>()
            .UseInMemoryDatabase($"purchasing-{Guid.NewGuid():N}")
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        _db = new WarehouseDbContext(options);
        Seed();

        _plans = new PurchasePlanService(
            _db, new PurchasePlanCodeGenerator(_db), _user, new FakeActivityEventWriter(),
            new CreatePurchasePlanRequestValidator(), new UpdatePurchasePlanRequestValidator());
        _orders = new PurchaseOrderService(
            _db, new PurchaseOrderCodeGenerator(_db), _plans, _user, new FakeActivityEventWriter(),
            new FakeFileStorage(),
            new CreatePurchaseOrderRequestValidator(), new UpdatePurchaseOrderRequestValidator());
    }

    [Fact]
    public async Task Create_RejectsWrongSupplier()
    {
        var plan = await SeedPlanWithLineAsync(supplierName: "NCC Alpha", qty: 10);

        var ex = await Assert.ThrowsAsync<PurchasingConflictException>(() =>
            _orders.CreateAsync(new CreatePurchaseOrderRequest(
                plan.Code,
                SupplierId: 2, // NCC Beta
                OrderDate: null,
                Note: null,
                Lines:
                [
                    new CreatePurchaseOrderLineRequest(plan.Lines[0].Id, 5, 1000, null, null),
                ],
                Installments: OneInstallment()), CancellationToken.None));

        Assert.Contains("không khớp", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Create_RejectsQtyOverRemaining()
    {
        var plan = await SeedPlanWithLineAsync(supplierName: "NCC Alpha", qty: 10);

        var ex = await Assert.ThrowsAsync<PurchasingConflictException>(() =>
            _orders.CreateAsync(new CreatePurchaseOrderRequest(
                plan.Code,
                SupplierId: 1,
                null,
                null,
                [
                    new CreatePurchaseOrderLineRequest(plan.Lines[0].Id, 15, 1000, null, null),
                ],
                Installments: OneInstallment()), CancellationToken.None));

        Assert.Contains("vượt phần còn lại", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Place_UpdatesPlanOrderedQtyAndStatus()
    {
        var plan = await SeedPlanWithLineAsync(supplierName: "NCC Alpha", qty: 10);
        var order = await _orders.CreateAsync(new CreatePurchaseOrderRequest(
            plan.Code, 1, null, null,
            [new CreatePurchaseOrderLineRequest(plan.Lines[0].Id, 4, 1200, null, null)],
            Installments: OneInstallment()),
            CancellationToken.None);

        await _orders.ApproveAsync(order.Code, CancellationToken.None);
        var placed = await _orders.PlaceAsync(order.Code, CancellationToken.None);

        Assert.Equal(PurchaseOrderPlacementStatuses.Placed, placed!.OrderPlacementStatus);

        var line = await _db.PurchasePlanLines.AsNoTracking()
            .FirstAsync(l => l.Id == plan.Lines[0].Id);
        Assert.Equal(4m, line.OrderedQty);
        Assert.Equal(6m, line.RemainingOrderableQty);
        Assert.Equal(PurchaseLineStatuses.Ordered, line.PurchaseStatus);
    }

    [Fact]
    public async Task Place_WhenPlanSoftDeleted_StillPlaces()
    {
        var plan = await SeedPlanWithLineAsync(supplierName: "NCC Alpha", qty: 10);
        var order = await _orders.CreateAsync(new CreatePurchaseOrderRequest(
            plan.Code, 1, null, null,
            [new CreatePurchaseOrderLineRequest(plan.Lines[0].Id, 4, 1200, null, null)],
            Installments: OneInstallment()),
            CancellationToken.None);
        await _orders.ApproveAsync(order.Code, CancellationToken.None);

        var stored = await _db.PurchasePlans.FirstAsync(p => p.Code == plan.Code);
        stored.DeletedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        var placed = await _orders.PlaceAsync(order.Code, CancellationToken.None);

        Assert.Equal(PurchaseOrderPlacementStatuses.Placed, placed!.OrderPlacementStatus);
        var line = await _db.PurchasePlanLines.AsNoTracking()
            .FirstAsync(l => l.Id == plan.Lines[0].Id);
        Assert.Equal(4m, line.OrderedQty);
    }

    [Fact]
    public async Task Unapprove_BlockedAfterPlace()
    {
        var plan = await SeedPlanWithLineAsync(supplierName: "NCC Alpha", qty: 10);
        var order = await _orders.CreateAsync(new CreatePurchaseOrderRequest(
            plan.Code, 1, null, null,
            [new CreatePurchaseOrderLineRequest(plan.Lines[0].Id, 2, 1000, null, null)],
            Installments: OneInstallment()),
            CancellationToken.None);
        await _orders.ApproveAsync(order.Code, CancellationToken.None);
        await _orders.PlaceAsync(order.Code, CancellationToken.None);

        var ex = await Assert.ThrowsAsync<PurchasingConflictException>(() =>
            _orders.UnapproveAsync(order.Code, CancellationToken.None));
        Assert.Contains("đã đặt hàng", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Update_BlockedWhenApproved()
    {
        var plan = await SeedPlanWithLineAsync(supplierName: "NCC Alpha", qty: 10);
        var order = await _orders.CreateAsync(new CreatePurchaseOrderRequest(
            plan.Code, 1, null, null,
            [new CreatePurchaseOrderLineRequest(plan.Lines[0].Id, 2, 1000, null, null)],
            Installments: OneInstallment()),
            CancellationToken.None);
        await _orders.ApproveAsync(order.Code, CancellationToken.None);

        var ex = await Assert.ThrowsAsync<PurchasingConflictException>(() =>
            _orders.UpdateAsync(order.Code, new UpdatePurchaseOrderRequest(
                "note", null,
                [new CreatePurchaseOrderLineRequest(plan.Lines[0].Id, 1, 1000, null, null)]),
                CancellationToken.None));
        Assert.Contains("draft", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Create_PersistsInstallmentsAndValidatesTotalPercent()
    {
        var plan = await SeedPlanWithLineAsync(supplierName: "NCC Alpha", qty: 10);
        var lines = new List<CreatePurchaseOrderLineRequest>
        {
            new(plan.Lines[0].Id, 2, 1000, null, null),
        };

        var ex = await Assert.ThrowsAsync<PurchasingConflictException>(() =>
            _orders.CreateAsync(new CreatePurchaseOrderRequest(
                plan.Code, 1, null, null, lines,
                Installments:
                [
                    new(1, 60, ["CO"]),
                    new(2, 30, ["CQ"]),
                ])));
        Assert.Contains("100", ex.Message);

        var order = await _orders.CreateAsync(new CreatePurchaseOrderRequest(
            plan.Code, 1, null, null, lines,
            Installments:
            [
                new(1, 60, ["CO"]),
                new(2, 40, ["CQ"]),
            ]));

        Assert.Equal(2, order.Installments.Count);
        Assert.Equal(PurchaseOrderInstallmentStatuses.Open, order.Installments[0].Status);
        Assert.Equal(PurchaseOrderInstallmentStatuses.Locked, order.Installments[1].Status);
    }

    [Fact]
    public async Task CompleteInstallment_RequiresDocumentsAndUnlocksNextStage()
    {
        var plan = await SeedPlanWithLineAsync(supplierName: "NCC Alpha", qty: 10);
        var order = await _orders.CreateAsync(new CreatePurchaseOrderRequest(
            plan.Code, 1, null, null,
            [new CreatePurchaseOrderLineRequest(plan.Lines[0].Id, 2, 1000, null, null)],
            Installments:
            [
                new(1, 50, ["CO"]),
                new(2, 50, ["CQ"]),
            ]));

        await Assert.ThrowsAsync<PurchasingConflictException>(() =>
            _orders.CompleteInstallmentAsync(order.Code, 1));

        await using var content = new MemoryStream([1, 2, 3]);
        await _orders.UploadInstallmentDocumentAsync(
            order.Code, 1, "co", "certificate.pdf", "application/pdf", content);
        var completed = await _orders.CompleteInstallmentAsync(order.Code, 1);

        Assert.Equal(PurchaseOrderInstallmentStatuses.Completed, completed!.Status);
        var refreshed = await _orders.GetByCodeAsync(order.Code);
        Assert.Equal(PurchaseOrderInstallmentStatuses.Open, refreshed!.Installments[1].Status);
    }

    private static List<PurchaseOrderInstallmentRequest> OneInstallment() =>
        [new(1, 100, ["OTHER"])];

    private async Task<PurchasePlanDetailDto> SeedPlanWithLineAsync(string supplierName, decimal qty)
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
            ProposalSupplierName = supplierName,
            SelectedSupplierName = supplierName,
            PurchaseStatus = PurchaseLineStatuses.NotPurchased,
            BudgetUnitPrice = 1000,
            BudgetAmount = 1000 * qty,
        });
        _plans.RecalcTotals(plan);
        await _db.SaveChangesAsync();
        await _plans.SubmitAsync(plan.Code, CancellationToken.None);
        await _plans.ApproveL1Async(plan.Code, CancellationToken.None);
        await _plans.ApproveL2Async(plan.Code, CancellationToken.None);
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
        _db.Suppliers.AddRange(
            new Supplier { Id = 1, SupplierCode = "NCC-A", SupplierName = "NCC Alpha", MaxDebtDays = 45, ApprovalStatus = SupplierApprovalStatuses.Approved },
            new Supplier { Id = 2, SupplierCode = "NCC-B", SupplierName = "NCC Beta", MaxDebtDays = 30, ApprovalStatus = SupplierApprovalStatuses.Approved });
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
