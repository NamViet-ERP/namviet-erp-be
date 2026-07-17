using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NamViet.ERP.Api.Infrastructure.Authorization;
using NamViet.ERP.Modules.Hr.Domain.Entities;
using NamViet.ERP.Modules.Hr.Infrastructure.Persistence;
using NamViet.ERP.SharedKernel.Common;

namespace NamViet.ERP.Api.Controllers;

public record CreateLeaveTypeRequest(string Name, bool IsPaid = true);

public record CreateLeaveRequest(
    int EmployeeId, int LeaveTypeId,
    string FromDate, string ToDate,
    decimal Days, string? Reason);

public record ApproveLeaveRequest(bool Approve, int? ApproverId);

public record SetLeaveBalanceRequest(int EmployeeId, int Year, decimal TotalDays);

[ApiController]
[Route("api/hr/leaves")]
[Authorize]
public class LeaveController(HrDbContext db) : ControllerBase
{
    // ── Loại nghỉ phép ─────────────────────────────────────────────────────────
    [HttpGet("types")]
    [RequirePermission("hrm.leaves.view")]
    public async Task<IActionResult> GetTypes(CancellationToken ct)
    {
        var items = await db.LeaveTypes
            .Where(t => t.DeletedAt == null)
            .Select(t => new { t.Id, t.Name, t.IsPaid })
            .ToListAsync(ct);
        return Ok(items);
    }

    [HttpPost("types")]
    [RequirePermission("hrm.leaves.manage")]
    public async Task<IActionResult> CreateType([FromBody] CreateLeaveTypeRequest request, CancellationToken ct)
    {
        var lt = new LeaveType { Name = request.Name, IsPaid = request.IsPaid };
        db.LeaveTypes.Add(lt);
        await db.SaveChangesAsync(ct);
        return CreatedAtAction(nameof(GetTypes), new { lt.Id });
    }

    // ── Đơn nghỉ phép ──────────────────────────────────────────────────────────
    [HttpGet]
    [RequirePermission("hrm.leaves.view")]
    public async Task<IActionResult> GetAll(
        [FromQuery] int page = 1, [FromQuery] int pageSize = 20,
        [FromQuery] int? employeeId = null,
        [FromQuery] string? status = null,
        [FromQuery] int? year = null,
        CancellationToken ct = default)
    {
        var query = db.LeaveRequests
            .Include(l => l.Employee)
            .Include(l => l.LeaveType)
            .AsQueryable();

        if (employeeId.HasValue) query = query.Where(l => l.EmployeeId == employeeId.Value);
        if (!string.IsNullOrWhiteSpace(status) && Enum.TryParse<LeaveStatus>(status, out var s))
            query = query.Where(l => l.Status == s);
        if (year.HasValue) query = query.Where(l => l.FromDate.Year == year.Value);

        var total = await query.CountAsync(ct);
        var items = await query
            .OrderByDescending(l => l.FromDate)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .Select(l => new
            {
                l.Id, l.EmployeeId, EmployeeName = l.Employee.FullName,
                l.LeaveTypeId, LeaveTypeName = l.LeaveType.Name,
                l.FromDate, l.ToDate, l.Days, l.Reason,
                Status = l.Status.ToString(), l.ApproverId, l.CreatedAt
            })
            .ToListAsync(ct);

        return Ok(PagedResult<object>.Create(items, total, page, pageSize));
    }

    [HttpPost]
    [RequirePermission("hrm.leaves.create")]
    public async Task<IActionResult> Create([FromBody] CreateLeaveRequest request, CancellationToken ct)
    {
        if (!DateOnly.TryParse(request.FromDate, out var from) ||
            !DateOnly.TryParse(request.ToDate, out var to))
            return BadRequest("Ngày không hợp lệ.");

        var leave = new LeaveRequest
        {
            EmployeeId = request.EmployeeId,
            LeaveTypeId = request.LeaveTypeId,
            FromDate = from,
            ToDate = to,
            Days = request.Days,
            Reason = request.Reason
        };
        db.LeaveRequests.Add(leave);
        await db.SaveChangesAsync(ct);
        return CreatedAtAction(nameof(GetAll), new { employeeId = leave.EmployeeId }, new { leave.Id });
    }

    [HttpPatch("{id:int}/approve")]
    [RequirePermission("hrm.leaves.approve")]
    public async Task<IActionResult> Approve(int id, [FromBody] ApproveLeaveRequest request, CancellationToken ct)
    {
        var leave = await db.LeaveRequests.FindAsync([id], ct);
        if (leave is null) return NotFound();
        if (leave.Status != LeaveStatus.Pending)
            return BadRequest("Đơn không ở trạng thái chờ duyệt.");

        leave.Status = request.Approve ? LeaveStatus.Approved : LeaveStatus.Rejected;
        leave.ApproverId = request.ApproverId;
        leave.UpdatedAt = DateTime.UtcNow;

        if (request.Approve)
        {
            // Trừ số ngày phép còn lại
            var balance = await db.LeaveBalances
                .FirstOrDefaultAsync(b => b.EmployeeId == leave.EmployeeId && b.Year == leave.FromDate.Year, ct);
            if (balance is not null)
            {
                balance.UsedDays += leave.Days;
                balance.UpdatedAt = DateTime.UtcNow;
            }
        }

        await db.SaveChangesAsync(ct);
        return NoContent();
    }

    // ── Số ngày phép còn lại ────────────────────────────────────────────────────
    [HttpGet("balance/{employeeId:int}/{year:int}")]
    [RequirePermission("hrm.leaves.view")]
    public async Task<IActionResult> GetBalance(int employeeId, int year, CancellationToken ct)
    {
        var b = await db.LeaveBalances
            .FirstOrDefaultAsync(x => x.EmployeeId == employeeId && x.Year == year, ct);
        if (b is null) return Ok(new { employeeId, year, totalDays = 0, usedDays = 0, remainingDays = 0 });

        return Ok(new
        {
            b.Id, b.EmployeeId, b.Year,
            b.TotalDays, b.UsedDays, RemainingDays = b.TotalDays - b.UsedDays
        });
    }

    [HttpPost("balance")]
    [RequirePermission("hrm.leaves.manage")]
    public async Task<IActionResult> SetBalance([FromBody] SetLeaveBalanceRequest request, CancellationToken ct)
    {
        var balance = await db.LeaveBalances
            .FirstOrDefaultAsync(b => b.EmployeeId == request.EmployeeId && b.Year == request.Year, ct);

        if (balance is null)
        {
            balance = new LeaveBalance
            {
                EmployeeId = request.EmployeeId,
                Year = request.Year,
                TotalDays = request.TotalDays
            };
            db.LeaveBalances.Add(balance);
        }
        else
        {
            balance.TotalDays = request.TotalDays;
            balance.UpdatedAt = DateTime.UtcNow;
        }

        await db.SaveChangesAsync(ct);
        return Ok(new { balance.Id, balance.EmployeeId, balance.Year, balance.TotalDays });
    }
}
