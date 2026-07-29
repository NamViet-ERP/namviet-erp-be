using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NamViet.ERP.Api.Application.Dtos.Purchasing;
using NamViet.ERP.Api.Application.Services.Purchasing;
using NamViet.ERP.Api.Infrastructure.Authorization;

namespace NamViet.ERP.Api.Controllers.Purchasing;

[ApiController]
[Route("api/warehouse/supplier-settings")]
[Authorize]
public class SupplierModuleSettingsController(ISupplierModuleSettingsService settings) : ControllerBase
{
    [HttpGet]
    [RequirePermission("wms.suppliers.view:all")]
    public async Task<ActionResult<SupplierModuleSettingsDto>> Get(CancellationToken ct) =>
        Ok(await settings.GetAsync(ct));

    [HttpPut]
    [RequirePermission("wms.suppliers.update")]
    public async Task<ActionResult<SupplierModuleSettingsDto>> Update(
        [FromBody] UpdateSupplierModuleSettingsRequest request, CancellationToken ct) =>
        Ok(await settings.UpdateAsync(request, ct));
}
