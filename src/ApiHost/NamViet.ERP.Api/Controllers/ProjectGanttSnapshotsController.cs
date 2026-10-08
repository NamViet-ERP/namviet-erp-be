using Microsoft.AspNetCore.Mvc;
using NamViet.ERP.Modules.Warehouse.Application.DTOs;
using NamViet.ERP.Modules.Warehouse.Infrastructure.Persistence;
using NamViet.ERP.Modules.Warehouse.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace NamViet.ERP.Api.Controllers;

[ApiController]
[Route("api/projects/{code}/gantt/snapshots")]
public class ProjectGanttSnapshotsController : ControllerBase
{
    private readonly WarehouseDbContext _dbContext;

    public ProjectGanttSnapshotsController(WarehouseDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    [HttpGet]
    public async Task<ActionResult<List<ProjectGanttSnapshotDto>>> GetSnapshots(string code)
    {
        var snaps = await _dbContext.ProjectGanttSnapshots
            .Where(x => x.ProjectCode == code)
            .Include(x => x.Items)
            .OrderByDescending(x => x.CreatedAt)
            .ToListAsync();

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
    public async Task<ActionResult<ProjectGanttSnapshotDto>> TakeSnapshot(string code, [FromBody] CreateProjectGanttSnapshotRequest request)
    {
        var project = await _dbContext.Projects.FirstOrDefaultAsync(p => p.ProjectCode == code);
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
        await _dbContext.SaveChangesAsync();

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
