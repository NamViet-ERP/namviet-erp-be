using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NamViet.ERP.Api.Application.Dtos.Purchasing;
using NamViet.ERP.Api.Application.Services.Purchasing;
using NamViet.ERP.Api.Infrastructure.Authorization;
using NamViet.ERP.Api.Services.Storage;

namespace NamViet.ERP.Api.Controllers.Purchasing;

[ApiController]
[Route("api/purchasing/orders")]
[Authorize]
public class PurchaseOrdersController(
    IPurchaseOrderService orders,
    IFileStorage fileStorage) : ControllerBase
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

    [HttpGet("{code}/attachments")]
    [RequirePermission("purchasing.orders.view")]
    public async Task<ActionResult<IReadOnlyList<PurchaseOrderAttachmentDto>>> ListAttachments(
        string code, CancellationToken ct)
    {
        var items = await orders.ListAttachmentsAsync(code, ct);
        return items is null ? NotFound() : Ok(items);
    }

    [HttpPost("{code}/attachments")]
    [RequirePermission("purchasing.orders.update")]
    [RequestSizeLimit(20 * 1024 * 1024)]
    public async Task<ActionResult<PurchaseOrderAttachmentDto>> UploadAttachment(
        string code,
        IFormFile file,
        [FromForm] string docType = "other",
        CancellationToken ct = default)
    {
        if (file is null || file.Length == 0)
            return BadRequest("File trống.");

        await using var stream = file.OpenReadStream();
        var dto = await orders.UploadAttachmentAsync(
            code,
            docType,
            file.FileName,
            file.ContentType ?? "application/octet-stream",
            stream,
            ct);
        return dto is null ? NotFound() : Ok(dto);
    }

    [HttpGet("{code}/attachments/{id:guid}/download")]
    [RequirePermission("purchasing.orders.view")]
    public async Task<IActionResult> DownloadAttachment(string code, Guid id, CancellationToken ct)
    {
        var dto = await orders.GetAttachmentAsync(code, id, ct);
        if (dto is null) return NotFound();

        var stored = fileStorage.Get(dto.StorageKey);
        if (stored is null) return NotFound();

        return PhysicalFile(stored.FullPath, GuessContentType(dto.FileName), dto.FileName);
    }

    [HttpDelete("{code}/attachments/{id:guid}")]
    [RequirePermission("purchasing.orders.update")]
    public async Task<IActionResult> DeleteAttachment(string code, Guid id, CancellationToken ct)
    {
        var ok = await orders.DeleteAttachmentAsync(code, id, ct);
        return ok ? NoContent() : NotFound();
    }

    private static string GuessContentType(string fileName)
    {
        var ext = Path.GetExtension(fileName).ToLowerInvariant();
        return ext switch
        {
            ".jpg" or ".jpeg" => "image/jpeg",
            ".png" => "image/png",
            ".webp" => "image/webp",
            ".gif" => "image/gif",
            ".pdf" => "application/pdf",
            ".doc" => "application/msword",
            ".docx" => "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
            ".rtf" => "application/rtf",
            ".xls" => "application/vnd.ms-excel",
            ".xlsx" => "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            ".xlsm" => "application/vnd.ms-excel.sheet.macroEnabled.12",
            ".xlsb" => "application/vnd.ms-excel.sheet.binary.macroEnabled.12",
            ".csv" => "text/csv",
            ".ppt" => "application/vnd.ms-powerpoint",
            ".pptx" => "application/vnd.openxmlformats-officedocument.presentationml.presentation",
            ".txt" => "text/plain",
            _ => "application/octet-stream",
        };
    }
}
