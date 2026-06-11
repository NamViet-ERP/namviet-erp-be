using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NamViet.ERP.Modules.Warehouse.Application.DTOs;
using NamViet.ERP.Modules.Warehouse.Domain.Entities;
using NamViet.ERP.Modules.Warehouse.Domain.Enums;
using NamViet.ERP.Modules.Warehouse.Infrastructure.Persistence;
using NamViet.ERP.SharedKernel.Common;

namespace NamViet.ERP.Api.Controllers;

[ApiController]
[Route("api/warehouse/projects")]
[Authorize]
public class ProjectsController(WarehouseDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetAll(
        [FromQuery] int page = 1, [FromQuery] int pageSize = 20,
        [FromQuery] string? search = null, [FromQuery] string? status = null,
        CancellationToken ct = default)
    {
        var query = db.Projects.AsQueryable();
        if (!string.IsNullOrWhiteSpace(search))
            query = query.Where(p => p.ProjectName.Contains(search) || p.ProjectCode.Contains(search));
        if (!string.IsNullOrWhiteSpace(status) && Enum.TryParse<ProjectStatus>(status, out var s))
            query = query.Where(p => p.Status == s);

        var total = await query.CountAsync(ct);
        var items = await query
            .OrderByDescending(p => p.CreatedAt)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .Select(p => new ProjectDto(p.ProjectCode, p.ProjectName, p.ProjectType.ToString(),
                p.PmId, p.PmName, p.StartDate, p.EndDate, p.MaterialBudget, p.Status.ToString()))
            .ToListAsync(ct);

        return Ok(PagedResult<ProjectDto>.Create(items, total, page, pageSize));
    }

    [HttpGet("{code}")]
    public async Task<IActionResult> GetById(string code, CancellationToken ct)
    {
        var p = await db.Projects.FirstOrDefaultAsync(x => x.ProjectCode == code, ct);
        if (p is null) return NotFound();
        return Ok(new ProjectDto(p.ProjectCode, p.ProjectName, p.ProjectType.ToString(),
            p.PmId, p.PmName, p.StartDate, p.EndDate, p.MaterialBudget, p.Status.ToString()));
    }

    [HttpGet("{code}/inventory")]
    public async Task<IActionResult> GetInventory(string code, CancellationToken ct)
    {
        var items = await db.ProjectInventories
            .Include(pi => pi.Material)
            .Where(pi => pi.ProjectCode == code)
            .Select(pi => new ProjectInventoryDto(
                pi.ProjectCode, pi.MaterialCode, pi.Material.MaterialName, pi.Material.Unit,
                pi.PlannedImportQty, pi.PlannedExportQty,
                pi.ActualImportQty, pi.ActualExportQty,
                pi.ActualImportQty - pi.ActualExportQty,
                pi.PlannedImportQty - pi.PlannedExportQty))
            .ToListAsync(ct);
        return Ok(items);
    }

    [HttpPost]
    [Authorize(Roles = "Admin,PM")]
    public async Task<IActionResult> Create([FromBody] CreateProjectRequest request, CancellationToken ct)
    {
        if (await db.Projects.AnyAsync(p => p.ProjectCode == request.ProjectCode, ct))
            return Conflict($"Mã dự án '{request.ProjectCode}' đã tồn tại.");

        var project = new Project
        {
            ProjectCode = request.ProjectCode,
            ProjectName = request.ProjectName,
            ProjectType = request.ProjectType,
            PmId = request.PmId,
            PmName = request.PmName,
            StartDate = request.StartDate,
            EndDate = request.EndDate,
            MaterialBudget = request.MaterialBudget
        };
        db.Projects.Add(project);
        await db.SaveChangesAsync(ct);
        return CreatedAtAction(nameof(GetById), new { code = project.ProjectCode }, null);
    }

    [HttpPut("{code}")]
    [Authorize(Roles = "Admin,PM")]
    public async Task<IActionResult> Update(string code, [FromBody] UpdateProjectRequest request, CancellationToken ct)
    {
        var p = await db.Projects.FirstOrDefaultAsync(x => x.ProjectCode == code, ct);
        if (p is null) return NotFound();
        p.ProjectName = request.ProjectName;
        p.PmId = request.PmId;
        p.PmName = request.PmName;
        p.EndDate = request.EndDate;
        p.MaterialBudget = request.MaterialBudget;
        p.Status = request.Status;
        p.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        return NoContent();
    }
}
