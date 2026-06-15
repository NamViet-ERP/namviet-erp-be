using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NamViet.ERP.Api.Application.Dtos.Wms;
using NamViet.ERP.Api.Application.Mappers.Wms;
using NamViet.ERP.Modules.Warehouse.Infrastructure.Persistence;

namespace NamViet.ERP.Api.Controllers.Wms;

[ApiController]
[Route("api/reconciliation")]
[Authorize]
public class ReconciliationController(WarehouseDbContext db) : ControllerBase
{
    [HttpGet("po-grn")]
    public async Task<ActionResult<List<PoRecordDto>>> PoGrnList(CancellationToken ct)
    {
        var pos = await db.PurchaseOrders
            .Include(p => p.Items)
            .Include(p => p.Project)
            .Include(p => p.Supplier)
            .Where(p => p.DeletedAt == null).ToListAsync(ct);
        return Ok(pos.Select(p => new PoRecordDto(p.Code,
            p.Project?.ProjectName ?? p.ProjectCode ?? "",
            p.Supplier?.SupplierName ?? "",
            WmsMapper.FormatDate(p.OrderDate),
            p.Items.Select(i => new ReconciliationItemDto(
                i.Sku, i.Name, i.EstPrice, i.PoPrice, i.Ordered, i.Received, i.Unit)).ToList())).ToList());
    }

    [HttpGet("po-grn/{code}")]
    public async Task<ActionResult<PoRecordDto>> PoGrnDetail(string code, CancellationToken ct)
    {
        var p = await db.PurchaseOrders
            .Include(x => x.Items)
            .Include(x => x.Project)
            .Include(x => x.Supplier)
            .FirstOrDefaultAsync(x => x.Code == code, ct);
        if (p is null) return NotFound();
        return Ok(new PoRecordDto(p.Code,
            p.Project?.ProjectName ?? p.ProjectCode ?? "",
            p.Supplier?.SupplierName ?? "",
            WmsMapper.FormatDate(p.OrderDate),
            p.Items.Select(i => new ReconciliationItemDto(
                i.Sku, i.Name, i.EstPrice, i.PoPrice, i.Ordered, i.Received, i.Unit)).ToList()));
    }

    [HttpPost("po-grn/{code}/approve-override")]
    public Task<IActionResult> ApproveOverride(string code, CancellationToken ct) => Task.FromResult<IActionResult>(NoContent());
}
