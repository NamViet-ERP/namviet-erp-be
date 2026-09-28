using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NamViet.ERP.Api.Infrastructure.Authorization;
using NamViet.ERP.Modules.Hr.Domain.Entities;
using NamViet.ERP.Modules.Hr.Infrastructure.Persistence;
using NamViet.ERP.Api.Application.Services.Hr;
using NamViet.ERP.SharedKernel.Common;
using NamViet.ERP.SharedKernel.Interfaces;

namespace NamViet.ERP.Api.Controllers;

public record UpsertAttendanceRequest(
    int EmployeeId, string Date,
    string? CheckIn, string? CheckOut,
    decimal WorkHours, string Status, string? Source);

public record UpsertAttendanceSummaryRequest(
    int EmployeeId, int Month, int Year,
    decimal WorkDays, decimal LeaveDays, decimal OtHours);

[ApiController]
[Route("api/hr/attendance")]
[Authorize]
[RequirePermission("hrm.attendance.view")]
public class AttendanceController(
    HrDbContext db,
    ICurrentUser currentUser,
    IEmployeeAccessService employeeAccess) : ControllerBase
{
    // ── Chấm công ngày ─────────────────────────────────────────────────────────
    [HttpGet]
    public async Task<IActionResult> GetAll(
        [FromQuery] int? employeeId = null,
        [FromQuery] string? fromDate = null,
        [FromQuery] string? toDate = null,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 50,
        CancellationToken ct = default)
    {
        (page, pageSize) = Paging.Normalize(page, pageSize);
        var query = db.Attendances
            .Include(a => a.Employee)
            .AsQueryable();

        if (employeeId.HasValue)
        {
            if (!await employeeAccess.CanViewEmployeeAsync(currentUser, employeeId.Value, ct))
                return Forbid();
            query = query.Where(a => a.EmployeeId == employeeId.Value);
        }
        if (DateOnly.TryParse(fromDate, out var fd)) query = query.Where(a => a.Date >= fd);
        if (DateOnly.TryParse(toDate, out var td)) query = query.Where(a => a.Date <= td);

        var total = await query.CountAsync(ct);
        var items = await query
            .OrderByDescending(a => a.Date)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .Select(a => new
            {
                a.Id, a.EmployeeId, EmployeeName = a.Employee.FullName,
                a.Date, a.CheckIn, a.CheckOut, a.WorkHours, a.Status, a.Source
            })
            .ToListAsync(ct);

        return Ok(PagedResult<object>.Create(items, total, page, pageSize));
    }

    [HttpPost]
    [RequirePermission("hrm.attendance.manage")]
    public async Task<IActionResult> Upsert([FromBody] UpsertAttendanceRequest request, CancellationToken ct)
    {
        if (!DateOnly.TryParse(request.Date, out var date))
            return BadRequest("Ngày không hợp lệ.");

        var existing = await db.Attendances
            .FirstOrDefaultAsync(a => a.EmployeeId == request.EmployeeId && a.Date == date, ct);

        if (existing is null)
        {
            existing = new Attendance { EmployeeId = request.EmployeeId, Date = date };
            db.Attendances.Add(existing);
        }

        existing.CheckIn = TimeOnly.TryParse(request.CheckIn, out var ci) ? ci : null;
        existing.CheckOut = TimeOnly.TryParse(request.CheckOut, out var co) ? co : null;
        existing.WorkHours = request.WorkHours;
        existing.Status = request.Status;
        existing.Source = request.Source;
        existing.UpdatedAt = DateTime.UtcNow;

        await db.SaveChangesAsync(ct);
        return Ok(new { existing.Id });
    }

    // ── Tổng hợp chấm công theo tháng ─────────────────────────────────────────
    [HttpGet("summary")]
    public async Task<IActionResult> GetSummary(
        [FromQuery] int? employeeId = null,
        [FromQuery] int? month = null,
        [FromQuery] int? year = null,
        [FromQuery] int? departmentId = null,
        CancellationToken ct = default)
    {
        var query = db.AttendanceSummaries
            .Include(a => a.Employee)
            .AsQueryable();

        if (employeeId.HasValue) query = query.Where(a => a.EmployeeId == employeeId.Value);
        if (month.HasValue) query = query.Where(a => a.Month == month.Value);
        if (year.HasValue) query = query.Where(a => a.Year == year.Value);
        if (departmentId.HasValue)
            query = query.Where(a => a.Employee.DepartmentId == departmentId.Value);

        var items = await query
            .OrderByDescending(a => a.Year).ThenByDescending(a => a.Month)
            .Select(a => new
            {
                a.Id, a.EmployeeId, EmployeeName = a.Employee.FullName,
                a.Month, a.Year, a.WorkDays, a.LeaveDays, a.OtHours
            })
            .ToListAsync(ct);

        return Ok(items);
    }

    [HttpPost("summary")]
    [RequirePermission("hrm.attendance.manage")]
    public async Task<IActionResult> UpsertSummary([FromBody] UpsertAttendanceSummaryRequest request, CancellationToken ct)
    {
        var existing = await db.AttendanceSummaries
            .FirstOrDefaultAsync(a =>
                a.EmployeeId == request.EmployeeId &&
                a.Month == request.Month &&
                a.Year == request.Year, ct);

        if (existing is null)
        {
            existing = new AttendanceSummary
            {
                EmployeeId = request.EmployeeId,
                Month = request.Month,
                Year = request.Year
            };
            db.AttendanceSummaries.Add(existing);
        }

        existing.WorkDays = request.WorkDays;
        existing.LeaveDays = request.LeaveDays;
        existing.OtHours = request.OtHours;
        existing.UpdatedAt = DateTime.UtcNow;

        await db.SaveChangesAsync(ct);
        return Ok(new { existing.Id });
    }
}
