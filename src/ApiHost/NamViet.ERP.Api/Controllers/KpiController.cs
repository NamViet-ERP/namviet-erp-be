using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NamViet.ERP.Api.Infrastructure.Authorization;
using NamViet.ERP.Modules.Hr.Domain.Entities;
using NamViet.ERP.Modules.Hr.Infrastructure.Persistence;

namespace NamViet.ERP.Api.Controllers;

public record CreateKpiDefinitionRequest(string Name, string Group, string? Unit, decimal? Target);

public record UpsertKpiRecordRequest(
    int KpiId, int Month, int Year,
    int? OwnerId, int? DepartmentId,
    decimal TargetValue, decimal ActualValue, string? Note);

[ApiController]
[Route("api/hr/kpi")]
[Authorize]
[RequirePermission("hrm.kpi.view")]
public class KpiController(HrDbContext db) : ControllerBase
{
    // ── Danh sách KPI ──────────────────────────────────────────────────────────
    [HttpGet("definitions")]
    public async Task<IActionResult> GetDefinitions([FromQuery] string? group = null, CancellationToken ct = default)
    {
        var query = db.KpiDefinitions
            .Where(k => k.DeletedAt == null)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(group))
            query = query.Where(k => k.Group == group);

        var items = await query
            .OrderBy(k => k.Group).ThenBy(k => k.Name)
            .Select(k => new { k.Id, k.Name, k.Group, k.Unit, k.Target })
            .ToListAsync(ct);
        return Ok(items);
    }

    [HttpPost("definitions")]
    [RequirePermission("hrm.kpi.manage")]
    public async Task<IActionResult> CreateDefinition([FromBody] CreateKpiDefinitionRequest request, CancellationToken ct)
    {
        var kpi = new KpiDefinition
        {
            Name = request.Name,
            Group = request.Group,
            Unit = request.Unit,
            Target = request.Target
        };
        db.KpiDefinitions.Add(kpi);
        await db.SaveChangesAsync(ct);
        return CreatedAtAction(nameof(GetDefinitions), new { kpi.Id });
    }

    [HttpDelete("definitions/{id:int}")]
    [RequirePermission("hrm.kpi.manage")]
    public async Task<IActionResult> DeleteDefinition(int id, CancellationToken ct)
    {
        var kpi = await db.KpiDefinitions.FindAsync([id], ct);
        if (kpi is null) return NotFound();
        kpi.DeletedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        return NoContent();
    }

    // ── Bản ghi kết quả KPI ────────────────────────────────────────────────────
    [HttpGet("records")]
    public async Task<IActionResult> GetRecords(
        [FromQuery] int? month = null, [FromQuery] int? year = null,
        [FromQuery] int? ownerId = null, [FromQuery] int? departmentId = null,
        [FromQuery] string? group = null,
        CancellationToken ct = default)
    {
        var query = db.KpiRecords
            .Include(r => r.Kpi)
            .AsQueryable();

        if (month.HasValue) query = query.Where(r => r.Month == month.Value);
        if (year.HasValue) query = query.Where(r => r.Year == year.Value);
        if (ownerId.HasValue) query = query.Where(r => r.OwnerId == ownerId.Value);
        if (departmentId.HasValue) query = query.Where(r => r.DepartmentId == departmentId.Value);
        if (!string.IsNullOrWhiteSpace(group)) query = query.Where(r => r.Kpi.Group == group);

        var items = await query
            .OrderByDescending(r => r.Year).ThenByDescending(r => r.Month)
            .Select(r => new
            {
                r.Id, r.KpiId, KpiName = r.Kpi.Name, KpiGroup = r.Kpi.Group,
                r.Kpi.Unit, r.Month, r.Year, r.OwnerId, r.DepartmentId,
                r.TargetValue, r.ActualValue,
                Achievement = r.TargetValue > 0 ? (r.ActualValue / r.TargetValue * 100).ToString("F1") + "%" : "N/A",
                r.Note
            })
            .ToListAsync(ct);
        return Ok(items);
    }

    [HttpPost("records")]
    [RequirePermission("hrm.kpi.manage")]
    public async Task<IActionResult> UpsertRecord([FromBody] UpsertKpiRecordRequest request, CancellationToken ct)
    {
        if (!await db.KpiDefinitions.AnyAsync(k => k.Id == request.KpiId && k.DeletedAt == null, ct))
            return BadRequest($"KPI ID={request.KpiId} không tồn tại hoặc đã bị xoá.");

        var existing = await db.KpiRecords
            .FirstOrDefaultAsync(r =>
                r.KpiId == request.KpiId &&
                r.Month == request.Month &&
                r.Year == request.Year &&
                r.OwnerId == request.OwnerId &&
                r.DepartmentId == request.DepartmentId, ct);

        if (existing is null)
        {
            existing = new KpiRecord
            {
                KpiId = request.KpiId,
                Month = request.Month,
                Year = request.Year,
                OwnerId = request.OwnerId,
                DepartmentId = request.DepartmentId
            };
            db.KpiRecords.Add(existing);
        }

        existing.TargetValue = request.TargetValue;
        existing.ActualValue = request.ActualValue;
        existing.Note = request.Note;
        existing.UpdatedAt = DateTime.UtcNow;

        await db.SaveChangesAsync(ct);
        return Ok(new { existing.Id });
    }

    [HttpDelete("records/{id:int}")]
    [RequirePermission("hrm.kpi.manage")]
    public async Task<IActionResult> DeleteRecord(int id, CancellationToken ct)
    {
        var record = await db.KpiRecords.FindAsync([id], ct);
        if (record is null) return NotFound();
        db.KpiRecords.Remove(record);
        await db.SaveChangesAsync(ct);
        return NoContent();
    }
}
