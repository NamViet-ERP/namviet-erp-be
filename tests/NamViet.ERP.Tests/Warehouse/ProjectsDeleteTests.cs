using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using FluentValidation;
using NamViet.ERP.Api.Controllers;
using NamViet.ERP.Modules.Warehouse.Application.DTOs;
using NamViet.ERP.Modules.Warehouse.Domain.Entities;
using NamViet.ERP.Modules.Warehouse.Domain.Enums;
using NamViet.ERP.Modules.Warehouse.Infrastructure.Persistence;

namespace NamViet.ERP.Tests.Warehouse;

public class ProjectsDeleteTests : IDisposable
{
    private readonly WarehouseDbContext _db;
    private readonly ProjectsController _controller;

    public ProjectsDeleteTests()
    {
        var options = new DbContextOptionsBuilder<WarehouseDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _db = new WarehouseDbContext(options);

        _controller = new ProjectsController(
            _db,
            null!,
            null!,
            new InlineValidator<CreateProjectRequest>(),
            new InlineValidator<UpdateProjectRequest>());
    }

    public void Dispose()
    {
        GC.SuppressFinalize(this);
        _db.Database.EnsureDeleted();
        _db.Dispose();
    }

    [Fact]
    public async Task Delete_returns_NotFound_when_project_does_not_exist()
    {
        var result = await _controller.Delete("DA-NONEXISTENT", CancellationToken.None);
        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task Delete_returns_BadRequest_when_project_has_positive_stock()
    {
        var code = "DA-STOCK";
        _db.Projects.Add(new Project { ProjectCode = code, ProjectName = "Project with stock" });
        _db.ProjectInventories.Add(new ProjectInventory
        {
            ProjectCode = code,
            MaterialCode = "MAT-01",
            ActualImportQty = 10,
            ActualExportQty = 0,
            PlannedImportQty = 0,
            PlannedExportQty = 0,
            ReservedQty = 0,
        });
        await _db.SaveChangesAsync();

        var result = await _controller.Delete(code, CancellationToken.None);
        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Contains("tồn kho", badRequest.Value?.ToString() ?? "", StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Delete_returns_BadRequest_when_project_has_transactions()
    {
        var code = "DA-TX";
        _db.Projects.Add(new Project { ProjectCode = code, ProjectName = "Project with tx" });
        _db.WarehouseTransactions.Add(new WarehouseTransaction
        {
            TicketCode = "XK-TEST-001",
            TransactionType = TransactionType.Export,
            ProjectCode = code,
            Notes = "Test tx",
        });
        await _db.SaveChangesAsync();

        var result = await _controller.Delete(code, CancellationToken.None);
        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Contains("chứng từ", badRequest.Value?.ToString() ?? "", StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Delete_succeeds_when_empty()
    {
        var code = "DA-EMPTY";
        var project = new Project { ProjectCode = code, ProjectName = "Empty Project" };
        _db.Projects.Add(project);
        _db.ProjectWarehouseKeepers.Add(new ProjectWarehouseKeeper
        {
            ProjectCode = code,
            UserId = Guid.NewGuid(),
            DisplayName = "Thủ kho A",
        });
        _db.ProjectInventories.Add(new ProjectInventory
        {
            ProjectCode = code,
            MaterialCode = "MAT-00",
            ActualImportQty = 0,
            ActualExportQty = 0,
            PlannedImportQty = 0,
            PlannedExportQty = 0,
            ReservedQty = 0,
        });
        await _db.SaveChangesAsync();

        var result = await _controller.Delete(code, CancellationToken.None);
        Assert.IsType<NoContentResult>(result);

        // Verify project is soft deleted
        var inDb = await _db.Projects.IgnoreQueryFilters().FirstOrDefaultAsync(p => p.ProjectCode == code);
        Assert.NotNull(inDb);
        Assert.NotNull(inDb.DeletedAt);

        // Verify keeper mappings and empty inventories are removed
        var keepers = await _db.ProjectWarehouseKeepers.Where(k => k.ProjectCode == code).ToListAsync();
        Assert.Empty(keepers);

        var inventories = await _db.ProjectInventories.Where(i => i.ProjectCode == code).ToListAsync();
        Assert.Empty(inventories);
    }
}
