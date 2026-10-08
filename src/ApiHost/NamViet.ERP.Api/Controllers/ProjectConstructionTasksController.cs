using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NamViet.ERP.Api.Infrastructure.Authorization;
using NamViet.ERP.Modules.Warehouse.Application.DTOs;
using NamViet.ERP.Modules.Warehouse.Domain.Entities;
using NamViet.ERP.Modules.Warehouse.Infrastructure.Persistence;

namespace NamViet.ERP.Api.Controllers;

[ApiController]
[Route("api/warehouse/projects")]
[Authorize]
public class ProjectConstructionTasksController(WarehouseDbContext db) : ControllerBase
{
    [HttpGet("construction-tasks")]
    [RequirePermission("wms.projects.view:all")]
    public async Task<ActionResult<List<ProjectConstructionTaskDto>>> List(
        [FromQuery] string? projectCode,
        CancellationToken ct)
    {
        var query = db.ProjectConstructionTasks.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(projectCode))
            query = query.Where(t => t.ProjectCode == projectCode.Trim());

        var rows = await (
            from t in query
            join p in db.Projects.AsNoTracking() on t.ProjectCode equals p.ProjectCode
            orderby t.ProjectCode, t.SortOrder, t.PlannedStart
            select new { t, p.ProjectName }
        ).ToListAsync(ct);

        var today = TodayVn();
        return Ok(rows.Select(r => ToDto(r.t, r.ProjectName, today)).ToList());
    }

    [HttpPost("{code}/construction-tasks")]
    [RequirePermission("wms.projects.update")]
    public async Task<ActionResult<ProjectConstructionTaskDto>> Create(
        string code,
        [FromBody] CreateProjectConstructionTaskRequest request,
        CancellationToken ct)
    {
        var project = await db.Projects.FirstOrDefaultAsync(p => p.ProjectCode == code, ct);
        if (project is null) return NotFound();

        var error = Validate(request.Name, request.PlannedStart, request.PlannedEnd, request.PercentComplete);
        if (error is not null) return BadRequest(error);

        var maxOrder = await db.ProjectConstructionTasks
            .Where(t => t.ProjectCode == code)
            .Select(t => (int?)t.SortOrder)
            .MaxAsync(ct) ?? 0;

        var row = new ProjectConstructionTask
        {
            ProjectCode = code,
            Name = request.Name.Trim(),
            PlannedStart = request.PlannedStart,
            PlannedEnd = request.PlannedEnd,
            ActualEnd = request.ActualEnd,
            PercentComplete = request.PercentComplete,
            SortOrder = maxOrder + 1,
            WbsCode = request.WbsCode ?? string.Empty,
            Level = request.Level,
            ParentId = request.ParentId,
            IsGroup = request.IsGroup,
            Department = request.Department,
            StatusLabel = request.StatusLabel,
            StatusTone = request.StatusTone,
            Notes = request.Notes,
        };
        db.ProjectConstructionTasks.Add(row);
        await db.SaveChangesAsync(ct);
        return CreatedAtAction(
            nameof(List),
            new { projectCode = code },
            ToDto(row, project.ProjectName, TodayVn()));
    }

    [HttpPut("{code}/construction-tasks/{id:guid}")]
    [RequirePermission("wms.projects.update")]
    public async Task<ActionResult<ProjectConstructionTaskDto>> Update(
        string code,
        Guid id,
        [FromBody] UpdateProjectConstructionTaskRequest request,
        CancellationToken ct)
    {
        var project = await db.Projects.FirstOrDefaultAsync(p => p.ProjectCode == code, ct);
        if (project is null) return NotFound();

        var error = Validate(request.Name, request.PlannedStart, request.PlannedEnd, request.PercentComplete);
        if (error is not null) return BadRequest(error);

        var row = await db.ProjectConstructionTasks
            .FirstOrDefaultAsync(t => t.Id == id && t.ProjectCode == code, ct);
        if (row is null) return NotFound();

        row.Name = request.Name.Trim();
        row.PlannedStart = request.PlannedStart;
        row.PlannedEnd = request.PlannedEnd;
        row.ActualEnd = request.ActualEnd;
        row.PercentComplete = request.PercentComplete;
        row.SortOrder = request.SortOrder;
        if (request.WbsCode is not null) row.WbsCode = request.WbsCode;
        row.Level = request.Level;
        row.ParentId = request.ParentId;
        row.IsGroup = request.IsGroup;
        if (request.Department is not null) row.Department = request.Department;
        if (request.StatusLabel is not null) row.StatusLabel = request.StatusLabel;
        if (request.StatusTone is not null) row.StatusTone = request.StatusTone;
        if (request.Notes is not null) row.Notes = request.Notes;
        row.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        await RecalculateWbsProgressAsync(code, ct);
        return Ok(ToDto(row, project.ProjectName, TodayVn()));
    }

    [HttpPost("{code}/construction-tasks/sync-wbs")]
    [RequirePermission("wms.projects.update")]
    public async Task<ActionResult<List<ProjectConstructionTaskDto>>> SyncWbs(
        string code,
        [FromBody] SyncProjectConstructionTasksRequest request,
        CancellationToken ct)
    {
        var project = await db.Projects.FirstOrDefaultAsync(p => p.ProjectCode == code, ct);
        if (project is null) return NotFound();

        var existing = await db.ProjectConstructionTasks
            .Where(t => t.ProjectCode == code)
            .ToListAsync(ct);

        db.ProjectConstructionTasks.RemoveRange(existing);

        var rows = new List<ProjectConstructionTask>();
        for (var i = 0; i < request.Tasks.Count; i++)
        {
            var item = request.Tasks[i];
            rows.Add(new ProjectConstructionTask
            {
                Id = item.Id ?? Guid.NewGuid(),
                ProjectCode = code,
                Name = item.Name.Trim(),
                PlannedStart = item.PlannedStart,
                PlannedEnd = item.PlannedEnd,
                PercentComplete = item.PercentComplete,
                SortOrder = item.SortOrder > 0 ? item.SortOrder : i + 1,
                WbsCode = item.WbsCode ?? string.Empty,
                Level = item.Level,
                ParentId = item.ParentId,
                IsGroup = item.IsGroup,
                Department = item.Department,
                StatusLabel = item.StatusLabel,
                StatusTone = item.StatusTone,
                Notes = item.Notes,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
            });
        }

        db.ProjectConstructionTasks.AddRange(rows);
        await db.SaveChangesAsync(ct);

        var today = TodayVn();
        return Ok(rows.OrderBy(r => r.SortOrder).Select(r => ToDto(r, project.ProjectName, today)).ToList());
    }

    [HttpDelete("{code}/construction-tasks/{id:guid}")]
    [RequirePermission("wms.projects.update")]
    public async Task<IActionResult> Delete(string code, Guid id, CancellationToken ct)
    {
        var row = await db.ProjectConstructionTasks
            .FirstOrDefaultAsync(t => t.Id == id && t.ProjectCode == code, ct);
        if (row is null) return NotFound();
        row.DeletedAt = DateTime.UtcNow;
        row.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        await RecalculateWbsProgressAsync(code, ct);
        return NoContent();
    }

    
    private async Task RecalculateWbsProgressAsync(string projectCode, CancellationToken ct)
    {
        var allTasks = await db.ProjectConstructionTasks
            .Where(t => t.ProjectCode == projectCode && t.DeletedAt == null)
            .ToListAsync(ct);

        var groups = allTasks.Where(t => t.IsGroup).OrderByDescending(t => t.Level).ToList();

        foreach (var group in groups)
        {
            var children = allTasks.Where(t => t.ParentId == group.Id).ToList();
            if (!children.Any()) continue;

            var minStart = children.Min(c => c.PlannedStart);
            var maxEnd = children.Max(c => c.PlannedEnd);

            double totalWeight = 0;
            double weightedProgress = 0;

            foreach (var child in children)
            {
                var days = child.PlannedEnd.DayNumber - child.PlannedStart.DayNumber + 1;
                if (days < 1) days = 1;
                totalWeight += days;
                weightedProgress += child.PercentComplete * days;
            }

            group.PlannedStart = minStart;
            group.PlannedEnd = maxEnd;
            group.PercentComplete = totalWeight > 0 ? (int)Math.Round(weightedProgress / totalWeight) : 0;
            group.UpdatedAt = DateTime.UtcNow;
        }

        await db.SaveChangesAsync(ct);
    }

    private static DateOnly TodayVn()
    {
        var tz = TimeZoneInfo.FindSystemTimeZoneById(
            OperatingSystem.IsWindows() ? "SE Asia Standard Time" : "Asia/Ho_Chi_Minh");
        return DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, tz));
    }

    private static string? Validate(string name, DateOnly start, DateOnly end, int percent)
    {
        if (string.IsNullOrWhiteSpace(name)) return "Tên hạng mục không được trống.";
        if (end < start) return "Ngày kết thúc kế hoạch phải sau hoặc bằng ngày bắt đầu.";
        if (percent is < 0 or > 100) return "Tiến độ phải từ 0 đến 100.";
        return null;
    }

    private static ProjectConstructionTaskDto ToDto(ProjectConstructionTask t, string projectName, DateOnly today)
    {
        var completed = t.PercentComplete >= 100 || t.ActualEnd is not null;
        string status;
        if (completed) status = "completed";
        else if (today > t.PlannedEnd) status = "late";
        else if (today >= t.PlannedStart) status = "in_progress";
        else status = "planned";

        return new ProjectConstructionTaskDto(
            t.Id, t.ProjectCode, projectName, t.Name,
            t.PlannedStart, t.PlannedEnd, t.ActualEnd,
            t.PercentComplete, t.SortOrder, status,
            t.WbsCode, t.Level, t.ParentId, t.IsGroup,
            t.Department, t.StatusLabel, t.StatusTone, t.Notes);
    }
}
