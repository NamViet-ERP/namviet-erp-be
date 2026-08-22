using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using NamViet.ERP.Api.Application.Dtos.Purchasing;
using NamViet.ERP.Api.Application.Services.Purchasing;
using NamViet.ERP.Modules.Warehouse.Domain.Entities;
using NamViet.ERP.Modules.Warehouse.Domain.Enums;
using NamViet.ERP.Modules.Warehouse.Infrastructure.Persistence;
using NamViet.ERP.Modules.Warehouse.Infrastructure.Services;
using NamViet.ERP.Tests.Wms;

namespace NamViet.ERP.Tests.Purchasing;

public class PurchaseDefectApplicationServiceTests : IDisposable
{
    private readonly WarehouseDbContext _db;
    private readonly PurchaseDefectApplicationService _defects;

    public PurchaseDefectApplicationServiceTests()
    {
        var options = new DbContextOptionsBuilder<WarehouseDbContext>()
            .UseInMemoryDatabase($"defects-{Guid.NewGuid():N}")
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        _db = new WarehouseDbContext(options);
        SeedBaseline();
        _defects = new PurchaseDefectApplicationService(
            _db, new FakeCurrentUser(), new FakeFileStorage(), new InventoryDomainService(_db));
    }

    [Fact]
    public async Task UpdateItems_pending_stock_reduces_qty_within_available()
    {
        var item = SeedPendingStockDefect(qty: 53m, onHand: 52m);

        var updated = await _defects.UpdateItemsAsync(
            "HL-0001",
            new UpdateDefectItemsRequest([new UpdateDefectItemRequest(item.Id, 52m)]),
            CancellationToken.None);

        Assert.NotNull(updated);
        Assert.Equal(52m, updated!.Items[0].Quantity);
        Assert.Equal(PurchaseDefectStatuses.Pending, updated.Status);
    }

    [Fact]
    public async Task UpdateItems_pending_stock_rejects_qty_above_available()
    {
        var item = SeedPendingStockDefect(qty: 53m, onHand: 52m);

        var ex = await Assert.ThrowsAsync<PurchasingConflictException>(() =>
            _defects.UpdateItemsAsync(
                "HL-0001",
                new UpdateDefectItemsRequest([new UpdateDefectItemRequest(item.Id, 53m)]),
                CancellationToken.None));

        Assert.Contains("khả dụng", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task UpdateItems_approved_is_rejected()
    {
        var item = SeedPendingStockDefect(qty: 10m, onHand: 20m);
        var ticket = await _db.PurchaseDefects.SingleAsync();
        ticket.Status = PurchaseDefectStatuses.Approved;
        await _db.SaveChangesAsync();

        var ex = await Assert.ThrowsAsync<PurchasingConflictException>(() =>
            _defects.UpdateItemsAsync(
                "HL-0001",
                new UpdateDefectItemsRequest([new UpdateDefectItemRequest(item.Id, 8m)]),
                CancellationToken.None));

        Assert.Contains("nháp hoặc chờ duyệt", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Cancel_pending_sets_rejected()
    {
        SeedPendingStockDefect(qty: 53m, onHand: 52m);

        var cancelled = await _defects.CancelAsync("HL-0001", CancellationToken.None);

        Assert.NotNull(cancelled);
        Assert.Equal(PurchaseDefectStatuses.Rejected, cancelled!.Status);
        Assert.Equal(PurchaseDefectStatuses.Rejected, cancelled.ProcessingPhase);
    }

    [Fact]
    public async Task Cancel_draft_is_rejected()
    {
        SeedPendingStockDefect(qty: 10m, onHand: 20m);
        var ticket = await _db.PurchaseDefects.SingleAsync();
        ticket.Status = PurchaseDefectStatuses.Draft;
        await _db.SaveChangesAsync();

        var ex = await Assert.ThrowsAsync<PurchasingConflictException>(() =>
            _defects.CancelAsync("HL-0001", CancellationToken.None));

        Assert.Contains("chờ duyệt", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    private PurchaseDefectItem SeedPendingStockDefect(decimal qty, decimal onHand)
    {
        _db.ProjectInventories.Add(new ProjectInventory
        {
            ProjectCode = "DA-TEST",
            MaterialCode = "MAT-001",
            ActualImportQty = onHand,
        });

        var item = new PurchaseDefectItem
        {
            TicketCode = "HL-0001",
            PoItemId = 0,
            MaterialCode = "MAT-001",
            Name = "Máy bơm chìm 5.5kW",
            Unit = "Cái",
            Quantity = qty,
            LineReason = "Hỏng",
        };
        _db.PurchaseDefects.Add(new PurchaseDefect
        {
            TicketCode = "HL-0001",
            SourceType = PurchaseDefectSourceTypes.Stock,
            PlanCode = "",
            ProjectCode = "DA-TEST",
            Status = PurchaseDefectStatuses.Pending,
            CreatedById = Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee"),
            CreatedByName = "Test Keeper",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            Items = [item],
        });
        _db.SaveChanges();
        return item;
    }

    private void SeedBaseline()
    {
        _db.Projects.Add(new Project
        {
            ProjectCode = "DA-TEST",
            ProjectName = "Dự án test",
            PmName = "PM",
            StartDate = DateOnly.FromDateTime(DateTime.UtcNow),
            Status = ProjectStatus.Active,
        });
        _db.Materials.Add(new Material
        {
            MaterialCode = "MAT-001",
            MaterialName = "Máy bơm chìm 5.5kW",
            CategoryId = 1,
            Unit = "Cái",
        });
        _db.SaveChanges();
    }

    public void Dispose() => _db.Dispose();
}
