using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NamViet.ERP.Api.Application.Dtos.Purchasing;
using NamViet.ERP.Api.Application.Services.Purchasing;
using NamViet.ERP.Api.Infrastructure.Authorization;
using NamViet.ERP.Api.Services.Storage;

namespace NamViet.ERP.Api.Controllers.Purchasing;

[ApiController]
[Route("api/purchasing/contracts")]
[Authorize]
public class PurchaseContractsController(
    IPurchaseContractService contracts,
    IFileStorage fileStorage) : ControllerBase
{
    [HttpGet]
    [RequirePermission("purchasing.contracts.view")]
    public async Task<ActionResult<PurchaseContractPagedResponse>> List(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? projectCode = null,
        [FromQuery] int? supplierId = null,
        [FromQuery] string? poCode = null,
        [FromQuery] string? status = null,
        [FromQuery] string? search = null,
        CancellationToken ct = default) =>
        Ok(await contracts.ListAsync(page, pageSize, projectCode, supplierId, poCode, status, search, ct));

    [HttpPost("import-from-po-attachments")]
    [RequirePermission("purchasing.contracts.create")]
    public async Task<ActionResult<ImportPurchaseContractsFromPoResult>> ImportFromPoAttachments(
        CancellationToken ct = default) =>
        Ok(await contracts.ImportFromPoAttachmentsAsync(ct));

    [HttpGet("{id:guid}")]
    [RequirePermission("purchasing.contracts.view")]
    public async Task<ActionResult<PurchaseContractDto>> GetById(Guid id, CancellationToken ct)
    {
        var row = await contracts.GetByIdAsync(id, ct);
        return row is null ? NotFound() : Ok(row);
    }

    [HttpPost]
    [RequirePermission("purchasing.contracts.create")]
    public async Task<ActionResult<PurchaseContractDto>> Create(
        [FromBody] CreatePurchaseContractRequest request,
        CancellationToken ct)
    {
        try
        {
            var created = await contracts.CreateAsync(request, ct);
            return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
        }
        catch (PurchasingConflictException ex)
        {
            return Conflict(ex.Message);
        }
    }

    [HttpPut("{id:guid}")]
    [RequirePermission("purchasing.contracts.update")]
    public async Task<ActionResult<PurchaseContractDto>> Update(
        Guid id,
        [FromBody] UpdatePurchaseContractRequest request,
        CancellationToken ct)
    {
        try
        {
            var row = await contracts.UpdateAsync(id, request, ct);
            return row is null ? NotFound() : Ok(row);
        }
        catch (PurchasingConflictException ex)
        {
            return Conflict(ex.Message);
        }
    }

    [HttpPost("{id:guid}/upload")]
    [RequirePermission("purchasing.contracts.update")]
    [RequestSizeLimit(20 * 1024 * 1024)]
    public async Task<ActionResult<PurchaseContractDto>> Upload(
        Guid id,
        IFormFile file,
        CancellationToken ct = default)
    {
        if (file is null || file.Length == 0)
            return BadRequest("File trống.");

        try
        {
            await using var stream = file.OpenReadStream();
            var row = await contracts.UploadAsync(
                id,
                file.FileName,
                file.ContentType ?? "application/octet-stream",
                stream,
                ct);
            return row is null ? NotFound() : Ok(row);
        }
        catch (PurchasingConflictException ex)
        {
            return Conflict(ex.Message);
        }
    }

    [HttpGet("{id:guid}/download")]
    [RequirePermission("purchasing.contracts.view")]
    public async Task<IActionResult> Download(Guid id, CancellationToken ct)
    {
        var result = await contracts.GetForDownloadAsync(id, ct);
        if (result is null)
            return NotFound("Không tìm thấy hợp đồng hoặc hợp đồng chưa có file đính kèm.");

        var stored = await fileStorage.OpenReadAsync(result.Value.StorageKey, ct);
        if (stored is null)
            return NotFound("Tệp đính kèm không còn tồn tại trên máy chủ lưu trữ (có thể do máy chủ khởi động lại làm mất file lưu tạm). Vui lòng tải lại file lên hợp đồng.");

        return File(stored.Stream, stored.ResolveContentType(result.Value.Dto.FileName), result.Value.Dto.FileName);
    }

    [HttpDelete("{id:guid}")]
    [RequirePermission("purchasing.contracts.update")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        var ok = await contracts.SoftDeleteAsync(id, ct);
        return ok ? NoContent() : NotFound();
    }
}
