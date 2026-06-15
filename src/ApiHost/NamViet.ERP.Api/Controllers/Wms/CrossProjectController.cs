using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NamViet.ERP.Modules.Warehouse.Domain.Enums;
using NamViet.ERP.Modules.Warehouse.Infrastructure.Persistence;

namespace NamViet.ERP.Api.Controllers.Wms;

[ApiController]
[Route("api/cross-project")]
[Authorize]
public class CrossProjectController(WarehouseDbContext db) : ControllerBase
{
    [HttpPost("scan")]
    public async Task<ActionResult<object>> Scan([FromBody] object body, CancellationToken ct)
    {
        var expected = await db.ExpectedImports.Where(e => e.Surplus > 0).ToListAsync(ct);
        return Ok(expected.Select(e => new
        {
            sku = e.MaterialCode, name = e.Name, needed = 0m,
            availableSurplus = e.Surplus, allocated = 0m,
            sources = new[] { new { project = e.ProjectCode, surplus = e.Surplus } }
        }));
    }

    [HttpPost("coordinate")]
    public async Task<ActionResult<object>> Coordinate([FromBody] object body, CancellationToken ct)
    {
        var count = await db.WarehouseTransactions.CountAsync(t => t.TransactionType == TransactionType.Transfer, ct);
        return Ok(new { voucherCode = $"CK-{count + 1:D6}", status = "created" });
    }
}
