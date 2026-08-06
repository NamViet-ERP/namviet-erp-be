using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NamViet.ERP.Api.Application.Dtos.Purchasing;
using NamViet.ERP.Api.Application.Services.Purchasing;
using NamViet.ERP.Api.Infrastructure.Authorization;

namespace NamViet.ERP.Api.Controllers.Purchasing;

[ApiController]
[Route("api/purchasing/payment-plans")]
[Authorize]
public class PurchasePaymentPlansController(IPurchasePaymentPlanService plans) : ControllerBase
{
    [HttpGet]
    [RequirePermission("purchasing.payments.view")]
    public async Task<ActionResult<PurchasePaymentPlanPagedResponse>> List(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? search = null,
        [FromQuery] string? projectCode = null,
        CancellationToken ct = default) =>
        Ok(await plans.ListAsync(page, pageSize, search, projectCode, ct));
}
