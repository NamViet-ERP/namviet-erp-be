using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NamViet.ERP.Api.Application.Dtos.Purchasing;
using NamViet.ERP.Api.Application.Services.Purchasing;
using NamViet.ERP.Api.Infrastructure.Authorization;

namespace NamViet.ERP.Api.Controllers.Purchasing;

[ApiController]
[Route("api/purchasing/supplier-payables")]
[Authorize]
public class SupplierPayablesController(ISupplierPayableService payables) : ControllerBase
{
    [HttpGet]
    [RequireAnyPermission("purchasing.suppliers.view", "wms.suppliers.view:all")]
    public async Task<ActionResult<SupplierPayableReportDto>> Get(
        [FromQuery] int? supplierId,
        [FromQuery] string? status,
        [FromQuery] string? search,
        CancellationToken ct) =>
        Ok(await payables.GetReportAsync(supplierId, status, search, ct));

    [HttpGet("snapshot/{supplierId:int}")]
    [RequireAnyPermission("purchasing.suppliers.view", "wms.suppliers.view:all")]
    public async Task<ActionResult<SupplierPayableSnapshotDto>> GetSnapshot(
        int supplierId,
        CancellationToken ct)
    {
        var item = await payables.GetSnapshotAsync(supplierId, ct);
        return item is null ? NotFound() : Ok(item);
    }
}
