using Microsoft.EntityFrameworkCore;
using NamViet.ERP.Api.Application.Dtos.Wms;
using NamViet.ERP.Modules.Warehouse.Domain;

namespace NamViet.ERP.Tests.Wms;

public class VoucherWorkflowTests : IDisposable
{
    private readonly WmsVoucherTestContext _ctx = new();

    [Fact]
    public async Task Import_pending_then_approve_moves_stock_to_project()
    {
        var created = await _ctx.Imports.CreateAsync(NewPendingImport(25m));
        Assert.Equal("pending", created.Status);

        var approved = await _ctx.Imports.ApproveAsync(created.Id);
        Assert.NotNull(approved);
        Assert.Equal("approved", approved.Status);

        var projectInventory = await _ctx.Db.ProjectInventories
            .SingleAsync(p => p.ProjectCode == "DA-TEST" && p.MaterialCode == "MAT-001");
        Assert.Equal(25m, projectInventory.ActualImportQty);
        Assert.Equal(0m, projectInventory.PlannedImportQty);
    }

    [Fact]
    public async Task Export_pending_then_approve_reduces_project_stock()
    {
        _ctx.SeedProjectStock("MAT-001", 40m);

        var created = await _ctx.Exports.CreateAsync(NewPendingExport(15m));
        Assert.Equal("pending", created.Status);

        var approved = await _ctx.Exports.ApproveAsync(created.Id);
        Assert.NotNull(approved);
        Assert.Equal("approved", approved.Status);

        var projectInventory = await _ctx.Db.ProjectInventories
            .SingleAsync(p => p.ProjectCode == "DA-TEST" && p.MaterialCode == "MAT-001");
        Assert.Equal(40m, projectInventory.ActualImportQty);
        Assert.Equal(15m, projectInventory.ActualExportQty);
    }

    [Fact]
    public async Task Transfer_project_return_pending_then_approve_moves_to_general()
    {
        _ctx.SeedProjectStock("MAT-001", 30m);

        var created = await _ctx.Transfers.CreateAsync(NewPendingProjectReturn(10m));
        Assert.Equal("pending", created.Status);

        var approved = await _ctx.Transfers.ApproveAsync(created.Id);
        Assert.NotNull(approved);
        Assert.Equal("approved", approved.Status);

        var projectInventory = await _ctx.Db.ProjectInventories
            .SingleAsync(p => p.ProjectCode == "DA-TEST" && p.MaterialCode == "MAT-001");
        Assert.Equal(10m, projectInventory.ActualExportQty);

        var generalQty = await _ctx.Db.WarehouseInventories
            .Where(i => i.WarehouseId == WmsWarehouseIds.General && i.MaterialCode == "MAT-001")
            .Select(i => i.Qty)
            .SingleAsync();
        Assert.Equal(90m, generalQty);
    }

    [Fact]
    public async Task Import_approve_rejects_when_not_pending()
    {
        var created = await _ctx.Imports.CreateAsync(NewPendingImport(5m) with { Status = "draft" });
        await Assert.ThrowsAsync<InvalidOperationException>(() => _ctx.Imports.ApproveAsync(created.Id));
    }

    private static CreateImportDto NewPendingImport(decimal qty) =>
        new(
            VoucherCode: "",
            Date: "01/07/2026",
            Supplier: "NCC Test",
            ReferenceCode: "REF-001",
            TotalValue: qty * 1000m,
            Status: "pending",
            WarehouseApproval: "pending",
            ApprovedBy: "",
            WarehouseKeeper: "Keeper",
            Items: [WmsVoucherTestContext.ImportLine(qty)],
            CreatedBy: "PM Test",
            Note: "",
            WarehouseId: WmsWarehouseIds.General,
            IsDirectProcurement: false,
            Project: "DA-TEST");

    private static CreateExportDto NewPendingExport(decimal qty) =>
        new(
            VoucherCode: "",
            Date: "01/07/2026",
            SoCode: "SO-001",
            Customer: "Khách test",
            WarehouseId: WmsWarehouseIds.General,
            Items: [WmsVoucherTestContext.ExportLine(qty)],
            Status: "pending",
            CreatedBy: "PM Test",
            Note: "",
            Project: "DA-TEST");

    private static CreateTransferDto NewPendingProjectReturn(decimal qty) =>
        new(
            VoucherCode: "",
            Date: "01/07/2026",
            FromWarehouseId: WmsWarehouseIds.General,
            ToWarehouseId: WmsWarehouseIds.General,
            Items: [WmsVoucherTestContext.TransferLine(qty)],
            Status: "pending",
            CreatedBy: "PM Test",
            Note: "",
            Project: "DA-TEST",
            ToProject: null);

    public void Dispose() => _ctx.Dispose();
}
