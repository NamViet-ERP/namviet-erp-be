using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NamViet.ERP.Api.Application.Dtos.Purchasing;
using NamViet.ERP.Api.Application.Services.Purchasing;
using NamViet.ERP.Api.Infrastructure.Authorization;

namespace NamViet.ERP.Api.Controllers.Purchasing;

[ApiController]
[Route("api/warehouse/suppliers/dashboard")]
[Authorize]
[RequirePermission("wms.suppliers.view:all")]
public class SuppliersDashboardController(ISuppliersDashboardService dashboard) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<SuppliersDashboardDto>> Get(
        [FromQuery] string? period,
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        [FromQuery] int? supplierId,
        CancellationToken ct)
    {
        try
        {
            return Ok(await dashboard.GetAsync(period, from, to, supplierId, ct));
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }
}
