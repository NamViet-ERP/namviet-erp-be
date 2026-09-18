using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using NamViet.ERP.Api.Application.Services.Purchasing;
using NamViet.ERP.Api.Application.Validators;
using NamViet.ERP.Modules.Warehouse.Domain.Entities;
using NamViet.ERP.Modules.Warehouse.Infrastructure.Persistence;
using NamViet.ERP.Tests.Wms;

namespace NamViet.ERP.Tests.Purchasing;

public class PurchasePlanDetailPoTests : IDisposable
{
    private readonly WarehouseDbContext _db;
    private readonly FakeCurrentUser _user = new();
    private readonly PurchasePlanService _planService;

    public PurchasePlanDetailPoTests()
    {
        var options = new DbContextOptionsBuilder<WarehouseDbContext>()
            .UseInMemoryDatabase($"plandetail-{Guid.NewGuid():N}")
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        _db = new WarehouseDbContext(options);

        _planService = new PurchasePlanService(
            _db,
            new PurchasePlanCodeGenerator(_db),
            _user,
            new FakeActivityEventWriter(),
            new CreatePurchasePlanRequestValidator(),
            new UpdatePurchasePlanRequestValidator());
    }

    [Fact]
    public async Task GetByCodeAsync_WhenPlanHasOrdersAndLineItems_ReturnsRelatedOrdersAndLinePos()
    {
        // Arrange
        _db.Projects.Add(new Project { ProjectCode = "DA-2026-230", ProjectName = "Dự án Liên Hạnh" });

        var plan = new PurchasePlan
        {
            Code = "KHMH-0001",
            ProjectCode = "DA-2026-230",
            Status = PurchasePlanStatuses.Approved,
            Kind = PurchasePlanKinds.Plan,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            Lines =
            [
                new PurchasePlanLine
                {
                    Id = 101,
                    PlanCode = "KHMH-0001",
                    LineNo = 1,
                    ProductName = "Bulong M10",
                    Unit = "cái",
                    Quantity = 10,
                    OrderedQty = 3
                },
                new PurchasePlanLine
                {
                    Id = 102,
                    PlanCode = "KHMH-0001",
                    LineNo = 2,
                    ProductName = "Bulong M12",
                    Unit = "cái",
                    Quantity = 20,
                    OrderedQty = 0
                }
            ]
        };
        _db.PurchasePlans.Add(plan);
        await _db.SaveChangesAsync();

        var line1Id = plan.Lines.First(l => l.LineNo == 1).Id;

        var po = new PurchaseOrder
        {
            Code = "PO-0002",
            PlanCode = "KHMH-0001",
            ProjectCode = "DA-2026-230",
            SupplierNameSnapshot = "HÀ KHÁNH",
            TotalAmount = 1331825.76m,
            ApprovalStatus = PurchaseOrderApprovalStatuses.Approved,
            OrderPlacementStatus = PurchaseOrderPlacementStatuses.Placed,
            DeliveryStatus = PurchaseOrderDeliveryStatuses.None,
            PaymentStatus = PurchaseOrderPaymentStatuses.Partial,
            OrderDate = DateOnly.FromDateTime(DateTime.UtcNow),
            CreatedAt = DateTime.UtcNow,
            Items =
            [
                new PurchaseOrderItem
                {
                    Id = 1,
                    PoCode = "PO-0002",
                    PlanLineId = line1Id,
                    Name = "Bulong M10",
                    Ordered = 3,
                    Received = 0,
                    Unit = "cái"
                }
            ]
        };
        _db.PurchaseOrders.Add(po);
        await _db.SaveChangesAsync();

        // Act
        var detail = await _planService.GetByCodeAsync("KHMH-0001");

        // Assert
        Assert.NotNull(detail);
        Assert.NotNull(detail.RelatedOrders);
        Assert.Single(detail.RelatedOrders);
        var order = detail.RelatedOrders[0];
        Assert.Equal("PO-0002", order.Code);
        Assert.Equal("HÀ KHÁNH", order.SupplierName);
        Assert.Equal(1331825.76m, order.TotalAmount);
        Assert.Equal(PurchaseOrderApprovalStatuses.Approved, order.ApprovalStatus);
        Assert.Equal(PurchaseOrderPlacementStatuses.Placed, order.OrderPlacementStatus);

        var line1 = detail.Lines.First(l => l.Id == line1Id);
        Assert.NotNull(line1.Pos);
        Assert.Single(line1.Pos);
        Assert.Equal("PO-0002", line1.Pos[0].PoCode);
        Assert.Equal(3m, line1.Pos[0].OrderedQty);
        Assert.Equal(0m, line1.Pos[0].ReceivedQty);

        var line2 = detail.Lines.First(l => l.LineNo == 2);
        Assert.Null(line2.Pos);
    }

    public void Dispose()
    {
        _db.Dispose();
        GC.SuppressFinalize(this);
    }
}
