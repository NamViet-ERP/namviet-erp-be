using Microsoft.EntityFrameworkCore;
using NamViet.ERP.Modules.Warehouse.Domain;
using NamViet.ERP.Modules.Warehouse.Domain.Entities;
using NamViet.ERP.Modules.Warehouse.Domain.Enums;
using NamViet.ERP.Modules.Warehouse.Infrastructure.Persistence;
using NamViet.ERP.Modules.Warehouse.Infrastructure.Services;

namespace NamViet.ERP.Tests.Warehouse;

public class InventoryDomainServiceTests : IDisposable
{
    private readonly WarehouseDbContext _db;
    private readonly InventoryDomainService _service;

    public InventoryDomainServiceTests()
    {
        var options = new DbContextOptionsBuilder<WarehouseDbContext>()
            .UseInMemoryDatabase($"wms-test-{Guid.NewGuid():N}")
            .Options;
        _db = new WarehouseDbContext(options);
        _service = new InventoryDomainService(_db);
        SeedBaseline();
    }

    [Fact]
    public async Task CreateReservationAsync_reserves_up_to_available_qty()
    {
        var result = await _service.CreateReservationAsync(
            WmsWarehouseIds.General, "DA-TEST", "MAT-001", 100m, "NK-DRAFT-001");
        await _db.SaveChangesAsync();

        Assert.Equal(80m, result.ReservedFromStock);
        Assert.Equal(20m, result.Shortfall);

        var reservation = await _db.InventoryReservations.SingleAsync();
        Assert.Equal(ReservationStatus.Holding, reservation.Status);
        Assert.Equal(80m, reservation.ReservedQty);
        Assert.Equal(20m, reservation.ExternalQty);

        var projectInventory = await _db.ProjectInventories.SingleAsync();
        Assert.Equal(100m, projectInventory.PlannedImportQty);
        Assert.Equal(80m, projectInventory.ReservedQty);
    }

    [Fact]
    public async Task FulfillReservationsAsync_moves_stock_to_project_actual()
    {
        await _service.CreateReservationAsync(
            WmsWarehouseIds.General, "DA-TEST", "MAT-001", 50m, "NK-DRAFT-002");
        await _db.SaveChangesAsync();
        await _service.FulfillReservationsAsync("NK-DRAFT-002");
        await _db.SaveChangesAsync();

        var warehouseQty = await _db.WarehouseInventories
            .Where(i => i.WarehouseId == WmsWarehouseIds.General && i.MaterialCode == "MAT-001")
            .Select(i => i.Qty)
            .SingleAsync();
        Assert.Equal(30m, warehouseQty);

        var projectInventory = await _db.ProjectInventories.SingleAsync();
        Assert.Equal(50m, projectInventory.ActualImportQty);
        Assert.Equal(0m, projectInventory.PlannedImportQty);
        Assert.Equal(0m, projectInventory.ReservedQty);

        var reservation = await _db.InventoryReservations.SingleAsync();
        Assert.Equal(ReservationStatus.Fulfilled, reservation.Status);
    }

    [Fact]
    public async Task CancelReservationsAsync_releases_planned_and_reserved_cache()
    {
        await _service.CreateReservationAsync(
            WmsWarehouseIds.General, "DA-TEST", "MAT-001", 40m, "NK-DRAFT-003");
        await _db.SaveChangesAsync();
        await _service.CancelReservationsAsync("NK-DRAFT-003");
        await _db.SaveChangesAsync();

        var projectInventory = await _db.ProjectInventories.SingleAsync();
        Assert.Equal(0m, projectInventory.PlannedImportQty);
        Assert.Equal(0m, projectInventory.ReservedQty);

        var reservation = await _db.InventoryReservations.SingleAsync();
        Assert.Equal(ReservationStatus.Cancelled, reservation.Status);

        var available = await _service.GetAvailableQtyAsync(WmsWarehouseIds.General, "MAT-001");
        Assert.Equal(80m, available);
    }

    [Fact]
    public async Task ApplyApprovedImport_without_prior_planned_does_not_go_negative()
    {
        var import = new WarehouseTransaction
        {
            TicketCode = "NK-APPROVED-001",
            TransactionType = TransactionType.Import,
            BeneficiaryProjectCode = "DA-TEST",
            ApprovalStatus = ApprovalStatus.Approved,
            Items =
            [
                new WarehouseTransactionItem
                {
                    MaterialCode = "MAT-001",
                    Quantity = 25m,
                    UnitPrice = 1000m
                }
            ]
        };
        _db.WarehouseTransactions.Add(import);
        await _db.SaveChangesAsync();

        await _service.ApplyApprovedImportAsync(import);
        await _db.SaveChangesAsync();

        var projectInventory = await _db.ProjectInventories.SingleAsync();
        Assert.Equal(25m, projectInventory.ActualImportQty);
        Assert.Equal(0m, projectInventory.PlannedImportQty);
    }

    [Fact]
    public async Task ApplyDirectIssueAsync_clears_pending_planned_import()
    {
        var import = new WarehouseTransaction
        {
            TicketCode = "NK-DI-001",
            TransactionType = TransactionType.Import,
            IsDirectProcurement = true,
            BeneficiaryProjectCode = "DA-TEST",
            ApprovalStatus = ApprovalStatus.Approved,
            Items =
            [
                new WarehouseTransactionItem
                {
                    MaterialCode = "MAT-001",
                    Quantity = 10m,
                    UnitPrice = 1000m
                }
            ]
        };
        _db.WarehouseTransactions.Add(import);
        await _db.SaveChangesAsync();

        await _service.CreateProjectImportPlannedAsync(import);
        await _db.SaveChangesAsync();

        await _service.ApplyDirectIssueAsync(import);
        await _db.SaveChangesAsync();

        var projectInventory = await _db.ProjectInventories.SingleAsync();
        Assert.Equal(10m, projectInventory.ActualImportQty);
        Assert.Equal(10m, projectInventory.ActualExportQty);
        Assert.Equal(0m, projectInventory.ActualImportQty - projectInventory.ActualExportQty);
        Assert.Equal(0m, projectInventory.PlannedImportQty);
        Assert.NotNull(import.LinkedTicketCode);
    }

    [Fact]
    public async Task ApplyDirectIssueAsync_keeps_project_net_inventory_at_zero()
    {
        var import = new WarehouseTransaction
        {
            TicketCode = "NK-DI-001",
            TransactionType = TransactionType.Import,
            IsDirectProcurement = true,
            BeneficiaryProjectCode = "DA-TEST",
            ApprovalStatus = ApprovalStatus.Approved,
            Items =
            [
                new WarehouseTransactionItem
                {
                    MaterialCode = "MAT-001",
                    Quantity = 10m,
                    UnitPrice = 1000m
                }
            ]
        };
        _db.WarehouseTransactions.Add(import);
        await _db.SaveChangesAsync();

        await _service.ApplyDirectIssueAsync(import);
        await _db.SaveChangesAsync();

        var projectInventory = await _db.ProjectInventories.SingleAsync();
        Assert.Equal(10m, projectInventory.ActualImportQty);
        Assert.Equal(10m, projectInventory.ActualExportQty);
        Assert.Equal(0m, projectInventory.ActualImportQty - projectInventory.ActualExportQty);
        Assert.NotNull(import.LinkedTicketCode);
    }

    private void SeedBaseline()
    {
        _db.WmsWarehouses.Add(new WmsWarehouse
        {
            Id = WmsWarehouseIds.General,
            Code = "KHO-TONG",
            Name = "Kho tổng",
            Group = "Kho tổng",
            Address = "A",
            Branch = "HCM",
            Manager = "Keeper",
            Status = "active"
        });
        _db.WmsSettings.Add(new WmsSetting
        {
            Key = WmsSettingsKeys.GeneralWarehouseId,
            Value = WmsWarehouseIds.General
        });
        _db.Projects.Add(new Project
        {
            ProjectCode = "DA-TEST",
            ProjectName = "Dự án test",
            PmName = "PM",
            StartDate = DateOnly.FromDateTime(DateTime.UtcNow)
        });
        _db.Materials.Add(new Material
        {
            MaterialCode = "MAT-001",
            MaterialName = "Vật tư test",
            CategoryId = 1,
            Unit = "Cái"
        });
        _db.WarehouseInventories.Add(new WarehouseInventory
        {
            WarehouseId = WmsWarehouseIds.General,
            MaterialCode = "MAT-001",
            Qty = 80m
        });
        _db.SaveChanges();
    }

    public void Dispose() => _db.Dispose();
}
