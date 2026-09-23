using FluentValidation;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NamViet.ERP.Api.Application.Services.Wms;
using NamViet.ERP.Api.Application.Validators;
using NamViet.ERP.Api.Controllers;
using NamViet.ERP.Modules.Warehouse.Application.DTOs;
using NamViet.ERP.Modules.Warehouse.Domain.Enums;
using NamViet.ERP.Modules.Warehouse.Infrastructure.Persistence;

namespace NamViet.ERP.Tests.Warehouse;

public class ProjectsCreateTests : IDisposable
{
    private readonly WarehouseDbContext _db;
    private readonly ProjectsController _controller;

    public ProjectsCreateTests()
    {
        var options = new DbContextOptionsBuilder<WarehouseDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _db = new WarehouseDbContext(options);

        _controller = new ProjectsController(
            _db,
            null!,
            new ProjectBudgetService(_db),
            new CreateProjectValidator(),
            new InlineValidator<UpdateProjectRequest>());
    }

    public void Dispose()
    {
        GC.SuppressFinalize(this);
        _db.Database.EnsureDeleted();
        _db.Dispose();
    }

    [Fact]
    public async Task Create_successful_persists_project_keepers_and_mirror_warehouse()
    {
        var keeperId = Guid.NewGuid();
        var request = new CreateProjectRequest(
            ProjectCode: "DA-2026-002",
            ProjectName: "Triệu phú lộc",
            ProjectType: ProjectType.Project,
            PmId: Guid.NewGuid(),
            PmName: "Admin NamViet",
            StartDate: new DateOnly(2026, 9, 23),
            EndDate: null,
            MaterialBudget: 100_000_000m,
            WarehouseKeepers: [new ProjectWarehouseKeeperDto(keeperId, "Admin NamViet")]);

        var result = await _controller.Create(request, CancellationToken.None);

        var createdResult = Assert.IsType<CreatedAtActionResult>(result);
        var dto = Assert.IsType<ProjectDto>(createdResult.Value);
        Assert.Equal("DA-2026-002", dto.ProjectCode);
        Assert.Equal("Triệu phú lộc", dto.ProjectName);
        Assert.Single(dto.WarehouseKeepers);
        Assert.Equal(keeperId, dto.WarehouseKeepers[0].UserId);

        // Verify DB persistence
        var project = await _db.Projects.FirstOrDefaultAsync(p => p.ProjectCode == "DA-2026-002");
        Assert.NotNull(project);
        Assert.Equal("Triệu phú lộc", project.ProjectName);

        var keepers = await _db.ProjectWarehouseKeepers.Where(k => k.ProjectCode == "DA-2026-002").ToListAsync();
        Assert.Single(keepers);
        Assert.Equal(keeperId, keepers[0].UserId);
        Assert.Equal("Admin NamViet", keepers[0].DisplayName);

        var warehouse = await _db.WmsWarehouses.FirstOrDefaultAsync(w => w.Id == "DA-2026-002");
        Assert.NotNull(warehouse);
        Assert.Equal("DA-2026-002", warehouse.Code);
        Assert.Equal("Triệu phú lộc", warehouse.Name);
        Assert.Equal("Kho dự án", warehouse.Group);
    }

    [Fact]
    public async Task Create_returns_conflict_when_project_code_already_exists()
    {
        _db.Projects.Add(new() { ProjectCode = "DA-2026-001", ProjectName = "Existing" });
        await _db.SaveChangesAsync();

        var request = new CreateProjectRequest(
            ProjectCode: "DA-2026-001",
            ProjectName: "Duplicate",
            ProjectType: ProjectType.Project,
            PmId: Guid.NewGuid(),
            PmName: "Admin",
            StartDate: new DateOnly(2026, 9, 23),
            EndDate: null,
            MaterialBudget: 50_000_000m);

        var result = await _controller.Create(request, CancellationToken.None);
        var conflict = Assert.IsType<ConflictObjectResult>(result);
        Assert.Contains("đã tồn tại", conflict.Value?.ToString() ?? "");
    }
}
