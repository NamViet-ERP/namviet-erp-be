using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NamViet.ERP.Api.Application.Dtos.Purchasing;
using NamViet.ERP.Api.Application.Services.Purchasing;
using NamViet.ERP.Api.Infrastructure.Authorization;

namespace NamViet.ERP.Api.Controllers.Purchasing;

[ApiController]
[Route("api/warehouse/supplier-evaluations")]
[Authorize]
public class SupplierEvaluationsController(ISupplierEvaluationService evaluations) : ControllerBase
{
    [HttpGet]
    [RequirePermission("wms.suppliers.view:all")]
    public async Task<ActionResult<SupplierEvaluationPagedResponse>> List(
        [FromQuery] int? supplierId = null,
        [FromQuery] string? period = null,
        [FromQuery] string? search = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        CancellationToken ct = default) =>
        Ok(await evaluations.ListAsync(supplierId, period, search, page, pageSize, ct));

    [HttpGet("{id:int}")]
    [RequirePermission("wms.suppliers.view:all")]
    public async Task<ActionResult<SupplierEvaluationDto>> GetById(int id, CancellationToken ct) =>
        Ok(await evaluations.GetByIdAsync(id, ct));

    [HttpPost]
    [RequirePermission("wms.suppliers.create")]
    public async Task<ActionResult<SupplierEvaluationDto>> Create(
        [FromBody] UpsertSupplierEvaluationRequest request, CancellationToken ct) =>
        Ok(await evaluations.CreateAsync(request, ct));

    [HttpPut("{id:int}")]
    [RequirePermission("wms.suppliers.update")]
    public async Task<ActionResult<SupplierEvaluationDto>> Update(
        int id, [FromBody] UpsertSupplierEvaluationRequest request, CancellationToken ct) =>
        Ok(await evaluations.UpdateAsync(id, request, ct));

    [HttpDelete("{id:int}")]
    [RequirePermission("wms.suppliers.delete")]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        await evaluations.DeleteAsync(id, ct);
        return NoContent();
    }
}
