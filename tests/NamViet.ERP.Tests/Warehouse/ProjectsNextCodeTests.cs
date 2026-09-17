using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using FluentValidation;
using NamViet.ERP.Api.Controllers;
using NamViet.ERP.Modules.Warehouse.Application.DTOs;
using NamViet.ERP.Modules.Warehouse.Domain.Entities;
using NamViet.ERP.Modules.Warehouse.Infrastructure.Persistence;

namespace NamViet.ERP.Tests.Warehouse;

public class ProjectsNextCodeTests : IDisposable
{
    private readonly WarehouseDbContext _db;
    private readonly ProjectsController _controller;

    public ProjectsNextCodeTests()
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
        _db.Database.EnsureDeleted();
        _db.Dispose();
    }

    [Fact]
    public async Task GetNextCode_returns_001_when_no_projects_in_year()
    {
        var actionResult = await _controller.GetNextCode(2026);
        var okResult = Assert.IsType<OkObjectResult>(actionResult.Result);
        var response = Assert.IsType<NextProjectCodeResponse>(okResult.Value);

        Assert.Equal("DA-2026-001", response.ProjectCode);
    }

    [Fact]
    public async Task GetNextCode_increments_sequentially()
    {
        _db.Projects.Add(new Project { ProjectCode = "DA-2026-001", ProjectName = "Dự án 1" });
        _db.Projects.Add(new Project { ProjectCode = "DA-2026-002", ProjectName = "Dự án 2" });
        await _db.SaveChangesAsync();

        var actionResult = await _controller.GetNextCode(2026);
        var okResult = Assert.IsType<OkObjectResult>(actionResult.Result);
        var response = Assert.IsType<NextProjectCodeResponse>(okResult.Value);

        Assert.Equal("DA-2026-003", response.ProjectCode);
    }

    [Fact]
    public async Task GetNextCode_fills_first_available_number_ignoring_random_large_code()
    {
        // Có mã DA-2026-230 do trước đây random số to
        _db.Projects.Add(new Project { ProjectCode = "DA-2026-001", ProjectName = "Dự án 1" });
        _db.Projects.Add(new Project { ProjectCode = "DA-2026-230", ProjectName = "Liên Hạnh" });
        await _db.SaveChangesAsync();

        var actionResult = await _controller.GetNextCode(2026);
        var okResult = Assert.IsType<OkObjectResult>(actionResult.Result);
        var response = Assert.IsType<NextProjectCodeResponse>(okResult.Value);

        // Vẫn phải lấy DA-2026-002 chứ không được nhảy cóc lên DA-2026-231
        Assert.Equal("DA-2026-002", response.ProjectCode);
    }

    [Fact]
    public async Task GetNextCode_resets_to_001_for_next_year()
    {
        _db.Projects.Add(new Project { ProjectCode = "DA-2026-001", ProjectName = "Dự án 2026" });
        _db.Projects.Add(new Project { ProjectCode = "DA-2026-002", ProjectName = "Dự án 2026" });
        await _db.SaveChangesAsync();

        var actionResult = await _controller.GetNextCode(2027);
        var okResult = Assert.IsType<OkObjectResult>(actionResult.Result);
        var response = Assert.IsType<NextProjectCodeResponse>(okResult.Value);

        Assert.Equal("DA-2027-001", response.ProjectCode);
    }
}
