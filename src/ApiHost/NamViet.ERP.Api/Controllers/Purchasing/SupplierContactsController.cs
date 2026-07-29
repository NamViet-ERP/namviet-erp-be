using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NamViet.ERP.Api.Application.Dtos.Purchasing;
using NamViet.ERP.Api.Application.Services.Purchasing;
using NamViet.ERP.Api.Infrastructure.Authorization;

namespace NamViet.ERP.Api.Controllers.Purchasing;

[ApiController]
[Route("api/warehouse/supplier-contacts")]
[Authorize]
public class SupplierContactsController(ISupplierContactService contacts) : ControllerBase
{
    [HttpGet]
    [RequirePermission("wms.suppliers.view:all")]
    public async Task<ActionResult<SupplierContactPagedResponse>> List(
        [FromQuery] int? supplierId = null,
        [FromQuery] string? search = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        CancellationToken ct = default) =>
        Ok(await contacts.ListAsync(supplierId, search, page, pageSize, ct));

    [HttpPost]
    [RequirePermission("wms.suppliers.create")]
    public async Task<ActionResult<SupplierContactDto>> Create(
        [FromBody] UpsertSupplierContactRequest request, CancellationToken ct) =>
        Ok(await contacts.CreateAsync(request, ct));

    [HttpPut("{id:int}")]
    [RequirePermission("wms.suppliers.update")]
    public async Task<ActionResult<SupplierContactDto>> Update(
        int id, [FromBody] UpsertSupplierContactRequest request, CancellationToken ct) =>
        Ok(await contacts.UpdateAsync(id, request, ct));

    [HttpDelete("{id:int}")]
    [RequirePermission("wms.suppliers.delete")]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        await contacts.DeleteAsync(id, ct);
        return NoContent();
    }
}
