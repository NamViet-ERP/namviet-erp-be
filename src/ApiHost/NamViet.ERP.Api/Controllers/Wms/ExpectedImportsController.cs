using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NamViet.ERP.Api.Application.Dtos.Wms;
using NamViet.ERP.Api.Application.Mappers.Wms;
using NamViet.ERP.Modules.Warehouse.Domain.Entities;
using NamViet.ERP.Modules.Warehouse.Infrastructure.Persistence;

namespace NamViet.ERP.Api.Controllers.Wms;

[ApiController]
[Route("api/expected-imports")]
[Authorize]
public class ExpectedImportsController(WarehouseDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<List<ExpectedImportDto>>> List(CancellationToken ct)
    {
        var items = await db.ExpectedImports.Where(e => e.DeletedAt == null).ToListAsync(ct);
        return Ok(items.Select(e => new ExpectedImportDto(
            e.Id, e.ProjectCode, e.MaterialCode, e.Name, e.Qty, e.Surplus, e.Unit,
            WmsMapper.FormatDate(e.ExpectedDate))).ToList());
    }

    [HttpPut("{id}/surplus")]
    public async Task<IActionResult> UpdateSurplus(string id, [FromBody] decimal surplus, CancellationToken ct)
    {
        var e = await db.ExpectedImports.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (e is null) return NotFound();
        e.Surplus = surplus;
        await db.SaveChangesAsync(ct);
        return NoContent();
    }
}
