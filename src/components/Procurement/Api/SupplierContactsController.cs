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
        [FromBody] UpsertSupplierContactRequest request, CancellationToken ct)
    {
        try
        {
            return Ok(await contacts.CreateAsync(request, ct));
        }
        catch (PurchasingConflictException ex)
        {
            return ContactFieldError(ex.Message);
        }
    }

    [HttpPut("{id:int}")]
    [RequirePermission("wms.suppliers.update")]
    public async Task<ActionResult<SupplierContactDto>> Update(
        int id, [FromBody] UpsertSupplierContactRequest request, CancellationToken ct)
    {
        try
        {
            return Ok(await contacts.UpdateAsync(id, request, ct));
        }
        catch (PurchasingConflictException ex)
        {
            return ContactFieldError(ex.Message);
        }
    }

    [HttpDelete("{id:int}")]
    [RequirePermission("wms.suppliers.delete")]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        await contacts.DeleteAsync(id, ct);
        return NoContent();
    }

    private static BadRequestObjectResult ContactFieldError(string message)
    {
        var field = "fullName";
        if (message.Contains("điện thoại", StringComparison.OrdinalIgnoreCase)
            || message.Contains("SĐT", StringComparison.OrdinalIgnoreCase))
            field = "phone";
        else if (message.Contains("Email", StringComparison.OrdinalIgnoreCase))
            field = "email";
        else if (message.Contains("Chức vụ", StringComparison.OrdinalIgnoreCase))
            field = "position";

        return new BadRequestObjectResult(new
        {
            errors = new Dictionary<string, string[]> { [field] = [message] },
        });
    }
}
