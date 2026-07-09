using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NamViet.ERP.Api.Application.Dtos.Wms;
using NamViet.ERP.Api.Application.Services.Wms;
using NamViet.ERP.Api.Infrastructure.Authorization;

namespace NamViet.ERP.Api.Controllers.Wms;

[ApiController]
[Route("api/warehouse/alerts")]
[Authorize]
public class WmsAlertsController(IWmsLowStockAlertService lowStockAlerts) : ControllerBase
{
    [HttpGet("low-stock")]
    [RequirePermission("wms.stock.view:all")]
    public async Task<ActionResult<IReadOnlyList<WmsLowStockAlertDto>>> LowStock(
        [FromQuery] string? projectCode, [FromQuery] int limit = 30, CancellationToken ct = default)
        => Ok(await lowStockAlerts.GetAlertsAsync(projectCode, limit, ct));
}
