using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using NamViet.ERP.Api.Application.Dtos.Purchasing;
using NamViet.ERP.Api.Application.Services.Purchasing;
using NamViet.ERP.Api.Application.Validators;
using NamViet.ERP.Modules.Warehouse.Domain.Entities;
using NamViet.ERP.Modules.Warehouse.Infrastructure.Persistence;
using NamViet.ERP.Tests.Wms;

namespace NamViet.ERP.Tests.Purchasing;

public class PurchasePaymentPlanTests : IDisposable
{
    private readonly WarehouseDbContext _db;
    private readonly FakeCurrentUser _user = new();
    private readonly PurchasePaymentService _paymentService;
    private readonly PurchasePaymentPlanService _planService;
    private readonly PurchaseOrderService _orderService;

    public PurchasePaymentPlanTests()
    {
        var options = new DbContextOptionsBuilder<WarehouseDbContext>()
            .UseInMemoryDatabase($"payplan-{Guid.NewGuid():N}")
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        _db = new WarehouseDbContext(options);

        _paymentService = new PurchasePaymentService(
            _db,
            new PurchasePaymentCodeGenerator(_db),
            _user,
            new FakeActivityEventWriter(),
            new CreatePurchasePaymentRequestValidator());

        _planService = new PurchasePaymentPlanService(_db);

        var planService = new PurchasePlanService(
            _db, new PurchasePlanCodeGenerator(_db), _user, new FakeActivityEventWriter(),
            new CreatePurchasePlanRequestValidator(), new UpdatePurchasePlanRequestValidator());

        _orderService = new PurchaseOrderService(
            _db, new PurchaseOrderCodeGenerator(_db), planService, _user, new FakeActivityEventWriter(),
            new FakeFileStorage(),
            new CreatePurchaseOrderRequestValidator(), new UpdatePurchaseOrderRequestValidator());
    }

    [Fact]
    public async Task MarkPaidAsync_WhenAllStagesPaid_UpdatesPoStatusToPaid()
    {
        // Arrange: PO with 2 installments (50% each)
        var po = new PurchaseOrder
        {
            Code = "PO-HK-001",
            PlanCode = "KHMH-001",
            ProjectCode = "DA-HK",
            OrderDate = DateOnly.FromDateTime(DateTime.UtcNow),
            ApprovalStatus = PurchaseOrderApprovalStatuses.Approved,
            TotalAmount = 1000000m,
            PaymentStatus = PurchaseOrderPaymentStatuses.Unpaid,
            Installments =
            [
                new PurchaseOrderInstallment
                {
                    Id = 1,
                    PoCode = "PO-HK-001",
                    StageNumber = 1,
                    Percent = 50,
                    Status = PurchaseOrderInstallmentStatuses.Open,
                },
                new PurchaseOrderInstallment
                {
                    Id = 2,
                    PoCode = "PO-HK-001",
                    StageNumber = 2,
                    Percent = 50,
                    Status = PurchaseOrderInstallmentStatuses.Open,
                }
            ]
        };
        _db.PurchaseOrders.Add(po);

        // Payment 1
        var p1 = new PurchasePayment
        {
            Code = "DNTT-001",
            PoCode = "PO-HK-001",
            StageNumber = 1,
            Amount = 500000m,
            Status = PurchasePaymentStatuses.Submitted,
            DueDate = DateOnly.FromDateTime(DateTime.UtcNow),
        };
        _db.PurchasePayments.Add(p1);

        // Payment 2
        var p2 = new PurchasePayment
        {
            Code = "DNTT-002",
            PoCode = "PO-HK-001",
            StageNumber = 2,
            Amount = 500000m,
            Status = PurchasePaymentStatuses.Submitted,
            DueDate = DateOnly.FromDateTime(DateTime.UtcNow),
        };
        _db.PurchasePayments.Add(p2);
        await _db.SaveChangesAsync();

        // Act 1: Mark payment 1 as paid
        await _paymentService.MarkPaidAsync("DNTT-001");

        // Verify intermediate status is partial
        var orderAfterP1 = await _db.PurchaseOrders.AsNoTracking().FirstAsync(o => o.Code == "PO-HK-001");
        Assert.Equal(PurchaseOrderPaymentStatuses.Partial, orderAfterP1.PaymentStatus);

        // Act 2: Mark payment 2 as paid
        await _paymentService.MarkPaidAsync("DNTT-002");

        // Assert: PO must transition to Paid
        var orderAfterP2 = await _db.PurchaseOrders.AsNoTracking().FirstAsync(o => o.Code == "PO-HK-001");
        Assert.Equal(PurchaseOrderPaymentStatuses.Paid, orderAfterP2.PaymentStatus);
    }

    [Fact]
    public async Task PaymentPlanService_ListAsync_FiltersByProjectAndStatus()
    {
        // PO 1: DA-01, unpaid
        var po1 = new PurchaseOrder
        {
            Code = "PO-001",
            PlanCode = "KHMH-001",
            ProjectCode = "DA-01",
            ApprovalStatus = PurchaseOrderApprovalStatuses.Approved,
            TotalAmount = 1000000m,
            PaymentStatus = PurchaseOrderPaymentStatuses.Unpaid,
            Installments = [new PurchaseOrderInstallment { Id = 10, PoCode = "PO-001", StageNumber = 1, Percent = 100 }]
        };
        // PO 2: DA-02, paid
        var po2 = new PurchaseOrder
        {
            Code = "PO-002",
            PlanCode = "KHMH-002",
            ProjectCode = "DA-02",
            ApprovalStatus = PurchaseOrderApprovalStatuses.Approved,
            TotalAmount = 2000000m,
            PaymentStatus = PurchaseOrderPaymentStatuses.Paid,
            Installments = [new PurchaseOrderInstallment { Id = 20, PoCode = "PO-002", StageNumber = 1, Percent = 100 }]
        };
        _db.PurchaseOrders.AddRange(po1, po2);
        await _db.SaveChangesAsync();

        // 1. Filter by project DA-01
        var resProject = await _planService.ListAsync(1, 20, null, "DA-01", null);
        Assert.Single(resProject.Items);
        Assert.Equal("PO-001", resProject.Items[0].PoCode);

        // 2. Filter uncompleted status (any unpaid stages)
        var resUncompleted = await _planService.ListAsync(1, 20, null, null, "uncompleted");
        Assert.Single(resUncompleted.Items);
        Assert.Equal("PO-001", resUncompleted.Items[0].PoCode);

        // 3. Filter paid status
        var resPaid = await _planService.ListAsync(1, 20, null, null, "paid");
        Assert.Single(resPaid.Items);
        Assert.Equal("PO-002", resPaid.Items[0].PoCode);
    }

    public void Dispose()
    {
        _db.Dispose();
        GC.SuppressFinalize(this);
    }
}
