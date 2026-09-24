using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NamViet.ERP.Api.Application.Dtos.Purchasing;
using NamViet.ERP.Api.Application.Services.Purchasing;
using NamViet.ERP.Api.Infrastructure.Authorization;
using NamViet.ERP.Api.Services.Storage;

namespace NamViet.ERP.Api.Controllers.Wms;

[ApiController]
[Route("api/defects")]
[Authorize]
public class DefectsController(
    IPurchaseDefectApplicationService defects,
    IFileStorage fileStorage) : ControllerBase
{
    [HttpGet]
    [RequirePermission("wms.defects.view:all")]
    public async Task<ActionResult<List<PurchaseDefectDto>>> List(CancellationToken ct) =>
        Ok(await defects.ListAsync(ct));

    [HttpGet("eligible-purchase-orders")]
    [RequirePermission("wms.defects.create")]
    public async Task<ActionResult<IReadOnlyList<PoDefectEligiblePurchaseOrderDto>>> ListEligiblePurchaseOrders(
        CancellationToken ct) =>
        Ok(await defects.ListEligiblePurchaseOrdersAsync(ct));

    [HttpGet("{id}")]
    [RequirePermission("wms.defects.view:all")]
    public async Task<ActionResult<PurchaseDefectDto>> GetById(string id, CancellationToken ct)
    {
        var item = await defects.GetByIdAsync(id, ct);
        return item is null ? NotFound() : Ok(item);
    }

    [HttpGet("from-po/{poCode}/preview")]
    [RequirePermission("wms.defects.create")]
    public async Task<ActionResult<PoDefectPreviewDto>> PreviewFromPo(string poCode, CancellationToken ct) =>
        Ok(await defects.PreviewFromPoAsync(poCode, ct));

    [HttpPost("from-po/{poCode}")]
    [RequirePermission("wms.defects.create")]
    public async Task<ActionResult<PurchaseDefectDto>> CreateFromPo(
        string poCode,
        [FromBody] CreateDefectFromPoRequest request,
        CancellationToken ct) =>
        Ok(await defects.CreateFromPoAsync(poCode, request, ct));

    [HttpGet("from-stock/{projectCode}/preview")]
    [RequirePermission("wms.defects.create")]
    public async Task<ActionResult<StockDefectPreviewDto>> PreviewFromStock(string projectCode, CancellationToken ct) =>
        Ok(await defects.PreviewFromStockAsync(projectCode, ct));

    [HttpPost("from-stock")]
    [RequirePermission("wms.defects.create")]
    public async Task<ActionResult<PurchaseDefectDto>> CreateFromStock(
        [FromBody] CreateDefectFromStockRequest request,
        CancellationToken ct) =>
        Ok(await defects.CreateFromStockAsync(request, ct));

    [HttpPost("{id}/submit")]
    [RequirePermission("wms.defects.create")]
    public async Task<ActionResult<PurchaseDefectDto>> Submit(string id, CancellationToken ct)
    {
        var item = await defects.SubmitAsync(id, ct);
        return item is null ? NotFound() : Ok(item);
    }

    [HttpPost("{id}/approve")]
    [RequirePermission("wms.defects.approve")]
    public async Task<ActionResult<PurchaseDefectDto>> Approve(string id, CancellationToken ct)
    {
        var item = await defects.ApproveAsync(id, ct);
        return item is null ? NotFound() : Ok(item);
    }

    [HttpPut("{id}/reason")]
    [RequirePermission("wms.defects.create")]
    public async Task<ActionResult<PurchaseDefectDto>> UpdateReason(
        string id,
        [FromBody] UpdateDefectReasonRequest request,
        CancellationToken ct)
    {
        var item = await defects.UpdateReasonAsync(id, request.Reason, ct);
        return item is null ? NotFound() : Ok(item);
    }

    [HttpPut("{id}/items")]
    [RequirePermission("wms.defects.create")]
    public async Task<ActionResult<PurchaseDefectDto>> UpdateItems(
        string id,
        [FromBody] UpdateDefectItemsRequest request,
        CancellationToken ct)
    {
        var item = await defects.UpdateItemsAsync(id, request, ct);
        return item is null ? NotFound() : Ok(item);
    }

    [HttpPost("{id}/cancel")]
    [RequirePermission("wms.defects.create")]
    public async Task<ActionResult<PurchaseDefectDto>> Cancel(string id, CancellationToken ct)
    {
        var item = await defects.CancelAsync(id, ct);
        return item is null ? NotFound() : Ok(item);
    }

    [HttpPut("{id}/processing")]
    [RequirePermission("wms.defects.approve")]
    public async Task<ActionResult<PurchaseDefectDto>> UpdateProcessing(
        string id,
        [FromBody] UpdateDefectProcessingRequest request,
        CancellationToken ct)
    {
        var item = await defects.UpdateProcessingAsync(id, request, ct);
        return item is null ? NotFound() : Ok(item);
    }

    [HttpPost("{id}/attachments")]
    [RequirePermission("wms.defects.create")]
    [RequestSizeLimit(20 * 1024 * 1024)]
    public async Task<ActionResult<PurchaseDefectAttachmentDto>> UploadAttachment(
        string id,
        IFormFile file,
        [FromForm] string kind = "photo",
        CancellationToken ct = default)
    {
        if (file is null || file.Length == 0)
            return BadRequest("File trống.");
        await using var stream = file.OpenReadStream();
        var dto = await defects.UploadAttachmentAsync(
            id, kind, file.FileName, file.ContentType ?? "application/octet-stream", stream, ct);
        return dto is null ? NotFound() : Ok(dto);
    }

    [HttpGet("{id}/attachments/{attachmentId:guid}")]
    [RequirePermission("wms.defects.view:all")]
    public async Task<IActionResult> DownloadAttachment(string id, Guid attachmentId, CancellationToken ct)
    {
        var dto = await defects.GetAttachmentAsync(id, attachmentId, ct);
        if (dto is null) return NotFound();
        var stored = await fileStorage.OpenReadAsync(dto.StorageKey, ct);
        if (stored is null) return NotFound();
        return File(stored.Stream, stored.ResolveContentType(dto.FileName), dto.FileName);
    }

    [HttpDelete("{id}/attachments/{attachmentId:guid}")]
    [RequirePermission("wms.defects.create")]
    public async Task<IActionResult> DeleteAttachment(string id, Guid attachmentId, CancellationToken ct)
    {
        var ok = await defects.DeleteAttachmentAsync(id, attachmentId, ct);
        return ok ? NoContent() : NotFound();
    }

    [HttpDelete("{id}")]
    [RequirePermission("wms.defects.create")]
    public async Task<IActionResult> Delete(string id, CancellationToken ct)
    {
        var ok = await defects.DeleteAsync(id, ct);
        return ok ? NoContent() : NotFound();
    }
}
