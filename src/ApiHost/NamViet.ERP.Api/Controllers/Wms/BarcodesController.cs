using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NamViet.ERP.Api.Application.Dtos.Wms;
using NamViet.ERP.Api.Application.Mappers.Wms;
using NamViet.ERP.Modules.Warehouse.Infrastructure.Persistence;

namespace NamViet.ERP.Api.Controllers.Wms;

[ApiController]
[Route("api/barcodes")]
[Authorize]
public class BarcodesController(WarehouseDbContext db) : ControllerBase
{
    [HttpGet("scan/{code}")]
    public async Task<ActionResult<ProductDto>> Scan(string code, CancellationToken ct)
    {
        var barcode = await db.ProductBarcodes.Include(b => b.Material)
            .FirstOrDefaultAsync(b => b.NumericSku == code, ct);
        if (barcode is null) return NotFound();
        return Ok(WmsMapper.ToProduct(barcode.Material, 0));
    }

    [HttpPost("print")]
    public IActionResult Print([FromBody] object body) =>
        StatusCode(StatusCodes.Status501NotImplemented, new { message = "In barcode chưa được triển khai." });
}
