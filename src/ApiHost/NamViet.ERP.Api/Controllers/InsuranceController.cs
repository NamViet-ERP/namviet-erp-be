using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NamViet.ERP.Modules.Hr.Domain.Entities;
using NamViet.ERP.Modules.Hr.Infrastructure.Persistence;
using NamViet.ERP.SharedKernel.Common;

namespace NamViet.ERP.Api.Controllers;

public record CreateInsuranceRecordRequest(
    int EmployeeId, int Month, int Year,
    decimal SalaryBase, decimal EmployeeAmount, decimal CompanyAmount);

public record CreateInsuranceTaskRequest(
    string Type, int EmployeeId, string? DueDate, string? Assignee, string? Note);

[ApiController]
[Route("api/hr/insurance")]
[Authorize(Roles = "Admin,HR")]
public class InsuranceController(HrDbContext db) : ControllerBase
{
    // ── Bản ghi đóng BHXH theo tháng ──────────────────────────────────────────
    [HttpGet("records")]
    public async Task<IActionResult> GetRecords(
        [FromQuery] int? employeeId = null,
        [FromQuery] int? month = null, [FromQuery] int? year = null,
        [FromQuery] string? status = null,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 50,
        CancellationToken ct = default)
    {
        var query = db.SocialInsuranceRecords
            .Include(r => r.Employee)
            .AsQueryable();

        if (employeeId.HasValue) query = query.Where(r => r.EmployeeId == employeeId.Value);
        if (month.HasValue) query = query.Where(r => r.Month == month.Value);
        if (year.HasValue) query = query.Where(r => r.Year == year.Value);
        if (!string.IsNullOrWhiteSpace(status)) query = query.Where(r => r.Status == status);

        var total = await query.CountAsync(ct);
        var items = await query
            .OrderByDescending(r => r.Year).ThenByDescending(r => r.Month)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .Select(r => new
            {
                r.Id, r.EmployeeId, EmployeeName = r.Employee.FullName,
                r.Month, r.Year, r.SalaryBase,
                r.EmployeeAmount, r.CompanyAmount,
                Total = r.EmployeeAmount + r.CompanyAmount,
                r.Status
            })
            .ToListAsync(ct);

        return Ok(PagedResult<object>.Create(items, total, page, pageSize));
    }

    [HttpPost("records")]
    public async Task<IActionResult> CreateRecord([FromBody] CreateInsuranceRecordRequest request, CancellationToken ct)
    {
        var existing = await db.SocialInsuranceRecords
            .FirstOrDefaultAsync(r =>
                r.EmployeeId == request.EmployeeId &&
                r.Month == request.Month &&
                r.Year == request.Year, ct);

        if (existing is not null)
            return Conflict($"Đã có bản ghi BHXH tháng {request.Month}/{request.Year} cho nhân viên này.");

        var record = new SocialInsuranceRecord
        {
            EmployeeId = request.EmployeeId,
            Month = request.Month,
            Year = request.Year,
            SalaryBase = request.SalaryBase,
            EmployeeAmount = request.EmployeeAmount,
            CompanyAmount = request.CompanyAmount
        };
        db.SocialInsuranceRecords.Add(record);
        await db.SaveChangesAsync(ct);
        return CreatedAtAction(nameof(GetRecords), new { employeeId = record.EmployeeId }, new { record.Id });
    }

    [HttpPatch("records/{id:int}/status")]
    public async Task<IActionResult> UpdateRecordStatus(int id, [FromBody] string status, CancellationToken ct)
    {
        var record = await db.SocialInsuranceRecords.FindAsync([id], ct);
        if (record is null) return NotFound();
        record.Status = status;
        record.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        return NoContent();
    }

    // ── Công việc BHXH cần xử lý ──────────────────────────────────────────────
    [HttpGet("tasks")]
    public async Task<IActionResult> GetTasks(
        [FromQuery] string? status = null,
        [FromQuery] string? type = null,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        var query = db.SocialInsuranceTasks
            .Include(t => t.Employee)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(status)) query = query.Where(t => t.Status == status);
        if (!string.IsNullOrWhiteSpace(type)) query = query.Where(t => t.Type == type);

        var total = await query.CountAsync(ct);
        var items = await query
            .OrderBy(t => t.DueDate)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .Select(t => new
            {
                t.Id, t.Type, t.EmployeeId, EmployeeName = t.Employee.FullName,
                t.Status, t.DueDate, t.Assignee, t.Note
            })
            .ToListAsync(ct);

        return Ok(PagedResult<object>.Create(items, total, page, pageSize));
    }

    [HttpPost("tasks")]
    public async Task<IActionResult> CreateTask([FromBody] CreateInsuranceTaskRequest request, CancellationToken ct)
    {
        if (!await db.Employees.AnyAsync(e => e.Id == request.EmployeeId, ct))
            return BadRequest($"Nhân viên ID={request.EmployeeId} không tồn tại.");

        var task = new SocialInsuranceTask
        {
            Type = request.Type,
            EmployeeId = request.EmployeeId,
            DueDate = DateOnly.TryParse(request.DueDate, out var d) ? d : null,
            Assignee = request.Assignee,
            Note = request.Note
        };
        db.SocialInsuranceTasks.Add(task);
        await db.SaveChangesAsync(ct);
        return CreatedAtAction(nameof(GetTasks), new { task.Id });
    }

    [HttpPatch("tasks/{id:int}/status")]
    public async Task<IActionResult> UpdateTaskStatus(int id, [FromBody] string status, CancellationToken ct)
    {
        var task = await db.SocialInsuranceTasks.FindAsync([id], ct);
        if (task is null) return NotFound();
        task.Status = status;
        task.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        return NoContent();
    }
}
