using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NamViet.ERP.Api.Application.Dtos.Purchasing;
using NamViet.ERP.Api.Application.Services.Purchasing;
using NamViet.ERP.Api.Infrastructure.Authorization;

namespace NamViet.ERP.Api.Controllers.Purchasing;

[ApiController]
[Route("api/purchasing/plans")]
[Authorize]
public class PurchasePlansController(
    IPurchasePlanService plans,
    IPurchasePlanImportService importService,
    ISupplierMaterialRiskService supplierMaterialRisks) : ControllerBase
{
    [HttpGet]
    [RequirePermission("purchasing.plans.view")]
    public async Task<ActionResult<PurchasePlanPagedResponse>> List(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? code = null,
        [FromQuery] string? projectCode = null,
        [FromQuery] string? status = null,
        [FromQuery] string? search = null,
        [FromQuery] DateOnly? dateFrom = null,
        [FromQuery] DateOnly? dateTo = null,
        [FromQuery] string? kind = null,
        CancellationToken ct = default) =>
        Ok(await plans.ListAsync(page, pageSize, code, projectCode, status, search, dateFrom, dateTo, kind, ct));

    [HttpGet("variance")]
    [RequirePermission("purchasing.plans.view")]
    public async Task<ActionResult<IReadOnlyList<PurchasePlanVarianceDto>>> GetVariance(
        [FromQuery] string? planCode = null,
        [FromQuery] string? projectCode = null,
        CancellationToken ct = default) =>
        Ok(await plans.GetVarianceAsync(planCode, projectCode, ct));

    /// <summary>
    /// Cảnh báo mềm KHMH: NCC + vật tư có ≥5 HL/3 tháng hoặc ≥2 phiếu BH.
    /// Không chặn lưu/duyệt.
    /// </summary>
    [HttpPost("supplier-material-risks")]
    [RequirePermission("purchasing.plans.view")]
    public async Task<ActionResult<IReadOnlyList<SupplierMaterialRiskDto>>> LookupSupplierMaterialRisks(
        [FromBody] SupplierMaterialRiskLookupRequest request,
        CancellationToken ct = default) =>
        Ok(await supplierMaterialRisks.LookupAsync(request, ct));

    [HttpGet("{code}")]
    [RequirePermission("purchasing.plans.view")]
    public async Task<ActionResult<PurchasePlanDetailDto>> GetByCode(string code, CancellationToken ct)
    {
        var item = await plans.GetByCodeAsync(code, ct);
        return item is null ? NotFound() : Ok(item);
    }

    [HttpPost]
    [RequirePermission("purchasing.plans.create")]
    public async Task<ActionResult<PurchasePlanDetailDto>> Create(
        [FromBody] CreatePurchasePlanRequest request, CancellationToken ct)
    {
        var created = await plans.CreateAsync(request, ct);
        return CreatedAtAction(nameof(GetByCode), new { code = created.Code }, created);
    }

    [HttpPut("{code}")]
    [RequirePermission("purchasing.plans.update")]
    public async Task<ActionResult<PurchasePlanDetailDto>> Update(
        string code, [FromBody] UpdatePurchasePlanRequest request, CancellationToken ct)
    {
        var updated = await plans.UpdateAsync(code, request, ct);
        return updated is null ? NotFound() : Ok(updated);
    }

    [HttpDelete("{code}")]
    [RequirePermission("purchasing.plans.update")]
    public async Task<IActionResult> Delete(string code, CancellationToken ct)
    {
        var deleted = await plans.DeleteAsync(code, ct);
        return deleted ? NoContent() : NotFound();
    }

    [HttpPost("{code}/submit")]
    [RequirePermission("purchasing.plans.submit")]
    public async Task<ActionResult<PurchasePlanDetailDto>> Submit(string code, CancellationToken ct)
    {
        var item = await plans.SubmitAsync(code, ct);
        return item is null ? NotFound() : Ok(item);
    }

    [HttpPost("{code}/approve-l1")]
    [RequirePermission("purchasing.plans.approve_l1")]
    public async Task<ActionResult<PurchasePlanDetailDto>> ApproveL1(string code, CancellationToken ct)
    {
        var item = await plans.ApproveL1Async(code, ct);
        return item is null ? NotFound() : Ok(item);
    }

    [HttpPost("{code}/approve-l2")]
    [RequirePermission("purchasing.plans.approve_l2")]
    public async Task<ActionResult<PurchasePlanDetailDto>> ApproveL2(string code, CancellationToken ct)
    {
        var item = await plans.ApproveL2Async(code, ct);
        return item is null ? NotFound() : Ok(item);
    }

    [HttpPost("{code}/reject")]
    [RequirePermission("purchasing.plans.reject")]
    public async Task<ActionResult<PurchasePlanDetailDto>> Reject(
        string code, [FromBody] RejectPurchasePlanRequest? request, CancellationToken ct)
    {
        var item = await plans.RejectAsync(code, request ?? new RejectPurchasePlanRequest(null), ct);
        return item is null ? NotFound() : Ok(item);
    }

    [HttpPost("{code}/remake")]
    [RequirePermission("purchasing.plans.remake")]
    public async Task<ActionResult<PurchasePlanDetailDto>> Remake(string code, CancellationToken ct)
    {
        var created = await plans.RemakeAsync(code, ct);
        return CreatedAtAction(nameof(GetByCode), new { code = created.Code }, created);
    }

    [HttpGet("import/template")]
    [RequirePermission("purchasing.plans.create")]
    public IActionResult DownloadImportTemplate()
    {
        var bytes = importService.BuildImportTemplate();
        return File(
            bytes,
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            "Mau_import_KHMH.xlsx");
    }

    [HttpPost("import/preview")]
    [RequirePermission("purchasing.plans.import")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(20_000_000)]
    public async Task<ActionResult<PurchasePlanImportPreviewDto>> ImportPreview(
        IFormFile file,
        [FromForm] string projectCode,
        CancellationToken ct)
    {
        if (file is null || file.Length == 0)
            return BadRequest("File Excel là bắt buộc.");

        await using var stream = file.OpenReadStream();
        // ClosedXML reads the stream synchronously; copy to memory so async dispose is safe.
        using var ms = new MemoryStream();
        await stream.CopyToAsync(ms, ct);
        ms.Position = 0;
        return Ok(importService.Preview(ms, projectCode));
    }

    [HttpPost("import/confirm")]
    [RequirePermission("purchasing.plans.import")]
    public async Task<ActionResult<PurchasePlanDetailDto>> ImportConfirm(
        [FromBody] ConfirmPurchasePlanImportRequest request, CancellationToken ct)
    {
        var created = await plans.ConfirmImportAsync(request, ct);
        return CreatedAtAction(nameof(GetByCode), new { code = created.Code }, created);
    }
}
