using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NamViet.ERP.Api.Application.Dtos.Wms;
using NamViet.ERP.Api.Controllers.Wms;
using NamViet.ERP.Modules.Warehouse.Domain;

namespace NamViet.ERP.Tests.Wms;

public sealed class ImportApprovalControllerTests
{
    [Fact]
    public async Task MissingEvidence_ReturnsBadRequest_AndDoesNotReceiveStock()
    {
        using var ctx = new WmsVoucherTestContext();
        var created = await ctx.Imports.CreateAsync(PendingImport());
        var controller = new ImportsController(ctx.Imports, null!, null!, null!, null!, null!);

        var result = await controller.Approve(created.Id, null, default);

        var error = Assert.IsType<BadRequestObjectResult>(result.Result);
        Assert.False(string.IsNullOrWhiteSpace(Assert.IsType<string>(error.Value)));
        Assert.Equal("pending", (await ctx.Imports.GetByIdAsync(created.Id))!.Status);
        Assert.Equal(0m, (await ctx.Db.ProjectInventories.SingleAsync()).ActualInventoryQty);
    }

    [Fact]
    public async Task RepeatedApproval_ReturnsBadRequest_AndDoesNotReceiveStockTwice()
    {
        using var ctx = new WmsVoucherTestContext();
        var created = await ctx.Imports.CreateAsync(PendingImport());
        await ctx.SeedImportApprovalEvidenceAsync(created.Id);
        await ctx.Imports.ApproveAsync(created.Id);
        var controller = new ImportsController(ctx.Imports, null!, null!, null!, null!, null!);

        var result = await controller.Approve(created.Id, null, default);

        var error = Assert.IsType<BadRequestObjectResult>(result.Result);
        Assert.Contains("đã duyệt", Assert.IsType<string>(error.Value));
        Assert.Equal(5m, (await ctx.Db.ProjectInventories.SingleAsync()).ActualInventoryQty);
    }

    [Fact]
    public async Task FrozenProject_ReturnsConflict_AndDoesNotReceiveStock()
    {
        using var ctx = new WmsVoucherTestContext();
        var created = await ctx.Imports.CreateAsync(PendingImport());
        await ctx.SeedImportApprovalEvidenceAsync(created.Id);
        ctx.SeedFrozenStocktake();
        var controller = new ImportsController(ctx.Imports, null!, null!, null!, null!, null!);

        var result = await controller.Approve(created.Id, null, default);

        Assert.IsType<ConflictObjectResult>(result.Result);
        Assert.Equal(0m, (await ctx.Db.ProjectInventories.SingleAsync()).ActualInventoryQty);
    }

    private static CreateImportDto PendingImport() =>
        new("", "27/10/2026", "NCC Test", "REF-TEST", 5000m, "pending", "pending", "", "Keeper",
            [WmsVoucherTestContext.ImportLine(5)], "Test", "", WmsWarehouseIds.General, false, "DA-TEST");
}
