using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NamViet.ERP.Api.Infrastructure.Authorization;
using NamViet.ERP.Modules.Warehouse.Application.DTOs;
using NamViet.ERP.Modules.Warehouse.Infrastructure.Persistence;
using NamViet.ERP.Modules.Warehouse.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace NamViet.ERP.Api.Controllers;

[ApiController]
[Route("api/projects/{code}/gantt/snapshots")]
[Route("api/warehouse/projects/{code}/gantt/snapshots")]
[Authorize]
public class ProjectGanttSnapshotsController : ControllerBase
{
    private readonly WarehouseDbContext _dbContext;

    public ProjectGanttSnapshotsController(WarehouseDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    [HttpGet]
    [RequirePermission("wms.projects.view:all")]
    public async Task<ActionResult<List<ProjectGanttSnapshotDto>>> GetSnapshots(
        string code,
        CancellationToken ct = default)
    {
        var snaps = await _dbContext.ProjectGanttSnapshots
            .AsNoTracking()
            .Where(x => x.ProjectCode == code)
            .Include(x => x.Items)
            .OrderByDescending(x => x.CreatedAt)
            .ToListAsync(ct);

        var dtos = snaps.Select(s => new ProjectGanttSnapshotDto(
            s.Id,
            s.ProjectCode,
            s.CreatedAt,
            s.PlanPct,
            s.ActualPct,
            s.ItemCount,
            s.Items.Select(i => new ProjectGanttSnapshotItemDto(
                i.Id,
                i.TaskId,
                i.TaskName,
                i.PlanPct,
                i.ActualPct
            )).ToList()
        )).ToList();

        return Ok(dtos);
    }

    [HttpPost]
    [RequirePermission("wms.projects.update")]
    public async Task<ActionResult<ProjectGanttSnapshotDto>> TakeSnapshot(
        string code,
        [FromBody] CreateProjectGanttSnapshotRequest request,
        CancellationToken ct = default)
    {
        if (request?.Items is null || request.Items.Count > 2000)
            return BadRequest("Số lượng hạng mục snapshot không hợp lệ (tối đa 2000).");

        var project = await _dbContext.Projects.FirstOrDefaultAsync(p => p.ProjectCode == code, ct);
        if (project == null) return NotFound("Project not found");

        var snapshot = new ProjectGanttSnapshot
        {
            ProjectCode = code,
            CreatedAt = DateTimeOffset.UtcNow,
            PlanPct = request.PlanPct,
            ActualPct = request.ActualPct,
            ItemCount = request.Items.Count
        };

        foreach (var item in request.Items)
        {
            snapshot.Items.Add(new ProjectGanttSnapshotItem
            {
                TaskId = item.TaskId,
                TaskName = item.TaskName,
                PlanPct = item.PlanPct,
                ActualPct = item.ActualPct
            });
        }

        _dbContext.ProjectGanttSnapshots.Add(snapshot);
        await _dbContext.SaveChangesAsync(ct);

        var dto = new ProjectGanttSnapshotDto(
            snapshot.Id,
            snapshot.ProjectCode,
            snapshot.CreatedAt,
            snapshot.PlanPct,
            snapshot.ActualPct,
            snapshot.ItemCount,
            snapshot.Items.Select(i => new ProjectGanttSnapshotItemDto(
                i.Id,
                i.TaskId,
                i.TaskName,
                i.PlanPct,
                i.ActualPct
            )).ToList()
        );

        return Ok(dto);
    }
}
