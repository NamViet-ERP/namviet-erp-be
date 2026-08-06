using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NamViet.ERP.Api.Application.Dtos.Purchasing;
using NamViet.ERP.Api.Application.Services.Purchasing;
using NamViet.ERP.Api.Infrastructure.Authorization;

namespace NamViet.ERP.Api.Controllers.Purchasing;

[ApiController]
[Route("api/warehouse/supplier-delivery-evaluations")]
[Authorize]
public class SupplierDeliveryEvaluationsController(
    ISupplierDeliveryEvaluationService evaluations) : ControllerBase
{
    [HttpGet]
    [RequirePermission("wms.suppliers.view:all")]
    public async Task<ActionResult<SupplierDeliveryEvaluationPagedResponse>> List(
        [FromQuery] int? supplierId = null,
        [FromQuery] string? poCode = null,
        [FromQuery] string? status = null,
        [FromQuery] string? search = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        CancellationToken ct = default) =>
        Ok(await evaluations.ListAsync(supplierId, poCode, status, search, page, pageSize, ct));

    [HttpPut("{id:int}/complete")]
    [RequirePermission("wms.suppliers.update")]
    public async Task<ActionResult<SupplierDeliveryEvaluationDto>> Complete(
        int id,
        [FromBody] CompleteSupplierDeliveryEvaluationRequest request,
        CancellationToken ct) =>
        Ok(await evaluations.CompleteAsync(id, request, ct));
}
