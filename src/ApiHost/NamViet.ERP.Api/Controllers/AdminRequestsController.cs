using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NamViet.ERP.Modules.Hr.Domain.Entities;
using NamViet.ERP.Modules.Hr.Domain.Enums;
using NamViet.ERP.Modules.Hr.Infrastructure.Persistence;
using NamViet.ERP.SharedKernel.Common;

namespace NamViet.ERP.Api.Controllers;

public record CreateAdminRequestRequest(
    string Type, int EmployeeId, string Title,
    string? Content, decimal? Quantity);

public record ApproveAdminRequestRequest(bool Approve, int? ApproverId);

[ApiController]
[Route("api/hr/admin-requests")]
[Authorize]
public class AdminRequestsController(HrDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetAll(
        [FromQuery] int page = 1, [FromQuery] int pageSize = 20,
        [FromQuery] string? type = null, [FromQuery] string? status = null,
        [FromQuery] int? employeeId = null,
        CancellationToken ct = default)
    {
        var query = db.AdminRequests
            .Include(r => r.Employee)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(type))
        {
            var parsedType = HrEnumMapper.ParseAdminRequestType(type);
            query = query.Where(r => r.Type == parsedType);
        }
        if (!string.IsNullOrWhiteSpace(status)) query = query.Where(r => r.Status == status);
        if (employeeId.HasValue) query = query.Where(r => r.EmployeeId == employeeId.Value);

        var total = await query.CountAsync(ct);
        var items = await query
            .OrderByDescending(r => r.CreatedAt)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .Select(r => new
            {
                r.Id, Type = HrEnumMapper.ToDisplayString(r.Type), r.EmployeeId, EmployeeName = r.Employee.FullName,
                r.Title, r.Content, r.Quantity, r.Status, r.ApproverId, r.CreatedAt
            })
            .ToListAsync(ct);

        return Ok(PagedResult<object>.Create(items, total, page, pageSize));
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(int id, CancellationToken ct)
    {
        var r = await db.AdminRequests
            .Include(x => x.Employee)
            .FirstOrDefaultAsync(x => x.Id == id, ct);
        if (r is null) return NotFound();
        return Ok(new
        {
            r.Id, Type = HrEnumMapper.ToDisplayString(r.Type), r.EmployeeId, EmployeeName = r.Employee.FullName,
            r.Title, r.Content, r.Quantity, r.Status, r.ApproverId, r.CreatedAt
        });
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateAdminRequestRequest request, CancellationToken ct)
    {
        var req = new AdminRequest
        {
            Type = HrEnumMapper.ParseAdminRequestType(request.Type),
            EmployeeId = request.EmployeeId,
            Title = request.Title,
            Content = request.Content,
            Quantity = request.Quantity
        };
        db.AdminRequests.Add(req);
        await db.SaveChangesAsync(ct);
        return CreatedAtAction(nameof(GetById), new { id = req.Id }, new { req.Id });
    }

    [HttpPatch("{id:int}/approve")]
    [Authorize(Roles = "Admin,HR,BOD")]
    public async Task<IActionResult> Approve(int id, [FromBody] ApproveAdminRequestRequest request, CancellationToken ct)
    {
        var req = await db.AdminRequests.FindAsync([id], ct);
        if (req is null) return NotFound();
        if (req.Status != "Pending") return BadRequest("Yêu cầu không ở trạng thái chờ duyệt.");

        req.Status = request.Approve ? "Approved" : "Rejected";
        req.ApproverId = request.ApproverId;
        req.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        return NoContent();
    }
}
