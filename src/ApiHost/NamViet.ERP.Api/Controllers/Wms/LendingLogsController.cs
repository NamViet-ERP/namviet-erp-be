using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NamViet.ERP.Api.Application.Dtos.Wms;
using NamViet.ERP.Api.Application.Mappers.Wms;
using NamViet.ERP.Api.Infrastructure.Authorization;
using NamViet.ERP.Modules.Warehouse.Domain.Entities;
using NamViet.ERP.Modules.Warehouse.Domain.Enums;
using NamViet.ERP.Modules.Warehouse.Infrastructure.Persistence;

namespace NamViet.ERP.Api.Controllers.Wms;

[ApiController]
[Route("api/lending-logs")]
[Authorize]
public class LendingLogsController(WarehouseDbContext db) : ControllerBase
{
    [HttpGet]
    [RequirePermission("wms.lending_logs.view:all")]
    public async Task<ActionResult<List<LendingLogDto>>> List(CancellationToken ct)
    {
        var items = await db.ProjectMaterialLoans.Include(l => l.Material).ToListAsync(ct);
        return Ok(items.Select(WmsMapper.ToLendingLog).ToList());
    }

    [HttpPost]
    [RequirePermission("wms.lending_logs.create")]
    public async Task<ActionResult<LendingLogDto>> Create([FromBody] LendingLogDto dto, CancellationToken ct)
    {
        var count = await db.ProjectMaterialLoans.CountAsync(ct);
        var l = new ProjectMaterialLoan
        {
            LoanCode = string.IsNullOrWhiteSpace(dto.VoucherCode) ? $"MT-{count + 1:D6}" : dto.VoucherCode,
            BorrowingProjectCode = dto.BorrowingProjectId,
            LendingProjectCode = dto.LendingProjectId,
            MaterialCode = dto.ProductId,
            LoanQty = dto.Qty,
            LoanReason = dto.Note,
            CreatedByName = dto.CreatedBy,
            Status = LoanStatus.Borrowing
        };
        db.ProjectMaterialLoans.Add(l);
        await db.SaveChangesAsync(ct);
        return Ok(WmsMapper.ToLendingLog(l));
    }

    [HttpPatch("{id}/sign")]
    [RequirePermission("wms.lending_logs.update")]
    public async Task<ActionResult<LendingLogDto>> Sign(string id, [FromBody] object body, CancellationToken ct)
    {
        var l = await db.ProjectMaterialLoans.FirstOrDefaultAsync(x => x.LoanCode == id, ct);
        if (l is null) return NotFound();
        var json = System.Text.Json.JsonSerializer.Serialize(body);
        if (json.Contains("borrow")) l.BorrowPmSigned = true;
        if (json.Contains("lend")) l.LendPmSigned = true;
        await db.SaveChangesAsync(ct);
        return Ok(WmsMapper.ToLendingLog(l));
    }

    [HttpPatch("{id}/return")]
    [RequirePermission("wms.lending_logs.update")]
    public async Task<ActionResult<LendingLogDto>> Return(string id, CancellationToken ct)
    {
        var l = await db.ProjectMaterialLoans.FirstOrDefaultAsync(x => x.LoanCode == id, ct);
        if (l is null) return NotFound();
        l.Status = LoanStatus.Returned;
        await db.SaveChangesAsync(ct);
        return Ok(WmsMapper.ToLendingLog(l));
    }

    [HttpDelete("{id}")]
    [RequirePermission("wms.lending_logs.delete")]
    public async Task<IActionResult> Delete(string id, CancellationToken ct)
    {
        var l = await db.ProjectMaterialLoans.FirstOrDefaultAsync(x => x.LoanCode == id, ct);
        if (l is null) return NotFound();
        db.ProjectMaterialLoans.Remove(l);
        await db.SaveChangesAsync(ct);
        return NoContent();
    }
}
