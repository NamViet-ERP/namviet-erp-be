using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NamViet.ERP.Api.Infrastructure.Authorization;
using NamViet.ERP.Modules.Warehouse.Application.DTOs;
using NamViet.ERP.Modules.Warehouse.Domain.Entities;
using NamViet.ERP.Modules.Warehouse.Domain.Enums;
using NamViet.ERP.Modules.Warehouse.Infrastructure.Persistence;
using NamViet.ERP.SharedKernel.Common;

namespace NamViet.ERP.Api.Controllers;

[ApiController]
[Route("api/warehouse/loans")]
[Authorize]
public class LoansController(WarehouseDbContext db) : ControllerBase
{
    [HttpGet]
    [RequirePermission("wms.loans.view:all")]
    public async Task<IActionResult> GetAll(
        [FromQuery] int page = 1, [FromQuery] int pageSize = 20,
        [FromQuery] string? projectCode = null, [FromQuery] string? status = null,
        CancellationToken ct = default)
    {
        (page, pageSize) = Paging.Normalize(page, pageSize);
        var query = db.ProjectMaterialLoans
            .Include(l => l.Material)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(projectCode))
            query = query.Where(l =>
                l.BorrowingProjectCode == projectCode || l.LendingProjectCode == projectCode);
        if (!string.IsNullOrWhiteSpace(status) && Enum.TryParse<LoanStatus>(status, out var s))
            query = query.Where(l => l.Status == s);

        var total = await query.CountAsync(ct);
        var items = await query
            .OrderByDescending(l => l.LoanDate)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .Select(l => new LoanDto(
                l.LoanCode, l.BorrowingProjectCode, l.LendingProjectCode,
                l.MaterialCode, l.Material.MaterialName,
                l.LoanQty, l.LoanDate, l.DueDate, l.Status.ToString(),
                l.LoanReason, l.ReturnTicketLink))
            .ToListAsync(ct);

        return Ok(PagedResult<LoanDto>.Create(items, total, page, pageSize));
    }

    [HttpGet("{loanCode}")]
    [RequirePermission("wms.loans.view:all")]
    public async Task<IActionResult> GetById(string loanCode, CancellationToken ct)
    {
        var l = await db.ProjectMaterialLoans
            .Include(x => x.Material)
            .FirstOrDefaultAsync(x => x.LoanCode == loanCode, ct);
        if (l is null) return NotFound();
        return Ok(new LoanDto(
            l.LoanCode, l.BorrowingProjectCode, l.LendingProjectCode,
            l.MaterialCode, l.Material.MaterialName,
            l.LoanQty, l.LoanDate, l.DueDate, l.Status.ToString(),
            l.LoanReason, l.ReturnTicketLink));
    }

    [HttpPost]
    [RequirePermission("wms.loans.create")]
    public async Task<IActionResult> Create([FromBody] CreateLoanRequest request, CancellationToken ct)
    {
        var dateStr = DateTime.UtcNow.ToString("yyyyMMdd");
        var count = await db.ProjectMaterialLoans.CountAsync(ct);
        var loanCode = $"MT-{dateStr}-{(count + 1):D4}";

        var loan = new ProjectMaterialLoan
        {
            LoanCode = loanCode,
            BorrowingProjectCode = request.BorrowingProjectCode,
            LendingProjectCode = request.LendingProjectCode,
            MaterialCode = request.MaterialCode,
            LoanQty = request.LoanQty,
            DueDate = request.DueDate,
            LoanReason = request.LoanReason
        };
        db.ProjectMaterialLoans.Add(loan);
        await db.SaveChangesAsync(ct);
        return CreatedAtAction(nameof(GetById), new { loanCode = loan.LoanCode }, new { loan.LoanCode });
    }

    [HttpPatch("{loanCode}/return")]
    [RequirePermission("wms.loans.update")]
    public async Task<IActionResult> MarkReturned(
        string loanCode,
        [FromBody] string? returnTicketLink,
        CancellationToken ct)
    {
        var loan = await db.ProjectMaterialLoans
            .FirstOrDefaultAsync(l => l.LoanCode == loanCode, ct);
        if (loan is null) return NotFound();
        if (loan.Status == LoanStatus.Returned)
            return BadRequest("Phiếu mượn đã được trả.");

        loan.Status = LoanStatus.Returned;
        loan.ReturnTicketLink = returnTicketLink;
        loan.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        return NoContent();
    }
}
