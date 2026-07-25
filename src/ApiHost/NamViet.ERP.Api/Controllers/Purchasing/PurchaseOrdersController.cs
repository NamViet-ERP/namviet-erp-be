using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NamViet.ERP.Api.Application.Dtos.Purchasing;
using NamViet.ERP.Api.Application.Services.Purchasing;
using NamViet.ERP.Api.Infrastructure.Authorization;

namespace NamViet.ERP.Api.Controllers.Purchasing;

[ApiController]
[Route("api/purchasing/orders")]
[Authorize]
public class PurchaseOrdersController(IPurchaseOrderService orders) : ControllerBase
{
    [HttpGet]
    [RequirePermission("purchasing.orders.view")]
    public async Task<ActionResult<PurchaseOrderPagedResponse>> List(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] int? supplierId = null,
        [FromQuery] string? planCode = null,
        [FromQuery] string? projectCode = null,
        [FromQuery] string? orderPlacementStatus = null,
        [FromQuery] string? deliveryStatus = null,
        [FromQuery] string? paymentStatus = null,
        [FromQuery] bool? paymentWarning = null,
        [FromQuery] string? search = null,
        CancellationToken ct = default) =>
        Ok(await orders.ListAsync(
            page, pageSize, supplierId, planCode, projectCode,
            orderPlacementStatus, deliveryStatus, paymentStatus, paymentWarning, search, ct));

    [HttpGet("{code}")]
    [RequirePermission("purchasing.orders.view")]
    public async Task<ActionResult<PurchaseOrderDetailDto>> GetByCode(string code, CancellationToken ct)
    {
        var item = await orders.GetByCodeAsync(code, ct);
        return item is null ? NotFound() : Ok(item);
    }

    [HttpPost]
    [RequirePermission("purchasing.orders.create")]
    public async Task<ActionResult<PurchaseOrderDetailDto>> Create(
        [FromBody] CreatePurchaseOrderRequest request, CancellationToken ct)
    {
        var created = await orders.CreateAsync(request, ct);
        return CreatedAtAction(nameof(GetByCode), new { code = created.Code }, created);
    }

    [HttpPut("{code}")]
    [RequirePermission("purchasing.orders.update")]
    public async Task<ActionResult<PurchaseOrderDetailDto>> Update(
        string code, [FromBody] UpdatePurchaseOrderRequest request, CancellationToken ct)
    {
        var updated = await orders.UpdateAsync(code, request, ct);
        return updated is null ? NotFound() : Ok(updated);
    }

    [HttpPost("{code}/approve")]
    [RequirePermission("purchasing.orders.approve")]
    public async Task<ActionResult<PurchaseOrderDetailDto>> Approve(string code, CancellationToken ct)
    {
        var item = await orders.ApproveAsync(code, ct);
        return item is null ? NotFound() : Ok(item);
    }

    [HttpPost("{code}/unapprove")]
    [RequirePermission("purchasing.orders.unapprove")]
    public async Task<ActionResult<PurchaseOrderDetailDto>> Unapprove(string code, CancellationToken ct)
    {
        var item = await orders.UnapproveAsync(code, ct);
        return item is null ? NotFound() : Ok(item);
    }

    [HttpPost("{code}/place")]
    [RequirePermission("purchasing.orders.place")]
    public async Task<ActionResult<PurchaseOrderDetailDto>> Place(string code, CancellationToken ct)
    {
        var item = await orders.PlaceAsync(code, ct);
        return item is null ? NotFound() : Ok(item);
    }
}
