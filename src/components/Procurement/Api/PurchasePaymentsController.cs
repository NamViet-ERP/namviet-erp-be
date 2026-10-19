using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NamViet.ERP.Api.Application.Dtos.Purchasing;
using NamViet.ERP.Api.Application.Services.Purchasing;
using NamViet.ERP.Api.Infrastructure.Authorization;

namespace NamViet.ERP.Api.Controllers.Purchasing;

[ApiController]
[Route("api/purchasing/payments")]
[Authorize]
public class PurchasePaymentsController(IPurchasePaymentService payments) : ControllerBase
{
    [HttpGet]
    [RequirePermission("purchasing.payments.view")]
    public async Task<ActionResult<PurchasePaymentPagedResponse>> List(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? poCode = null,
        [FromQuery] string? status = null,
        [FromQuery] string? search = null,
        CancellationToken ct = default) =>
        Ok(await payments.ListAsync(page, pageSize, poCode, status, search, ct));

    /// <summary>Đợt tiếp theo cần tạo DNTT (tuần tự, không chọn đợt).</summary>
    [HttpGet("next-stage")]
    [RequirePermission("purchasing.payments.view")]
    public async Task<ActionResult<PurchasePaymentNextStageDto>> GetNextStage(
        [FromQuery] string poCode,
        CancellationToken ct)
    {
        var item = await payments.GetNextStageAsync(poCode, ct);
        return item is null ? NotFound() : Ok(item);
    }

    [HttpGet("{code}")]
    [RequirePermission("purchasing.payments.view")]
    public async Task<ActionResult<PurchasePaymentDto>> GetByCode(string code, CancellationToken ct)
    {
        var item = await payments.GetByCodeAsync(code, ct);
        return item is null ? NotFound() : Ok(item);
    }

    [HttpPost]
    [RequirePermission("purchasing.payments.create")]
    public async Task<ActionResult<PurchasePaymentDto>> Create(
        [FromBody] CreatePurchasePaymentRequest request, CancellationToken ct)
    {
        var created = await payments.CreateFromPoAsync(request, ct);
        return CreatedAtAction(nameof(GetByCode), new { code = created.Code }, created);
    }

    [HttpPost("{code}/submit")]
    [RequirePermission("purchasing.payments.submit")]
    public async Task<ActionResult<PurchasePaymentDto>> Submit(string code, CancellationToken ct) =>
        Ok(await payments.SubmitAsync(code, ct));

    [HttpPost("{code}/pay")]
    [RequirePermission("purchasing.payments.pay")]
    public async Task<ActionResult<PurchasePaymentDto>> MarkPaid(string code, CancellationToken ct) =>
        Ok(await payments.MarkPaidAsync(code, ct));

    [HttpPost("{code}/cancel")]
    [RequirePermission("purchasing.payments.cancel")]
    public async Task<ActionResult<PurchasePaymentDto>> Cancel(string code, CancellationToken ct) =>
        Ok(await payments.CancelAsync(code, ct));
}
