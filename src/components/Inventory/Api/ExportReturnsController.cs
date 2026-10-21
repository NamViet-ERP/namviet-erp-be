using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NamViet.ERP.Api.Application.Dtos.Wms;
using NamViet.ERP.Api.Application.Services.Wms;
using NamViet.ERP.Api.Infrastructure.Authorization;

namespace NamViet.ERP.Api.Controllers.Wms;

[ApiController]
[Route("api/export-returns")]
[Authorize]
public class ExportReturnsController(
    IExportReturnApplicationService exportReturns,
    IValidator<CreateExportReturnDto> createValidator,
    NamViet.ERP.Api.Services.Storage.IFileStorage fileStorage,
    ProjectWarehouseAccessService projectAccess) : ControllerBase
{
    [HttpGet]
    [RequirePermission("wms.export_returns.view:all")]
    public async Task<ActionResult<List<ExportReturnDto>>> List(
        [FromQuery] DateOnly? dateFrom,
        [FromQuery] DateOnly? dateTo,
        CancellationToken ct) =>
        Ok(await exportReturns.ListAsync(dateFrom, dateTo, ct));

    [HttpGet("returnable-exports")]
    [RequirePermission("wms.export_returns.view:all")]
    public async Task<ActionResult<List<ExportReturnableExportDto>>> ListReturnableExports(
        [FromQuery] string projectCode,
        [FromQuery] string? excludeReturnId,
        CancellationToken ct) =>
        Ok(await exportReturns.ListReturnableExportsAsync(projectCode, excludeReturnId, ct));

    [HttpGet("returnable-lines")]
    [RequirePermission("wms.export_returns.view:all")]
    public async Task<ActionResult<List<ExportReturnableLineDto>>> GetReturnableLines(
        [FromQuery] string sourceExportId,
        [FromQuery] string? excludeReturnId,
        CancellationToken ct) =>
        Ok(await exportReturns.GetReturnableLinesAsync(sourceExportId, excludeReturnId, ct));

    [HttpGet("{id}")]
    [RequirePermission("wms.export_returns.view:all")]
    public async Task<ActionResult<ExportReturnDto>> GetById(string id, CancellationToken ct)
    {
        var item = await exportReturns.GetByIdAsync(id, ct);
        return item is null ? NotFound() : Ok(item);
    }

    [HttpPost]
    [RequirePermission("wms.export_returns.create")]
    public async Task<ActionResult<ExportReturnDto>> Create(
        [FromBody] CreateExportReturnDto dto,
        CancellationToken ct)
    {
        var validation = await createValidator.ValidateAsync(dto, ct);
        if (!validation.IsValid)
            return BadRequest(string.Join("; ", validation.Errors.Select(e => e.ErrorMessage)));
        try
        {
            if (!string.IsNullOrWhiteSpace(dto.Project))
                await projectAccess.EnsureCanOperateOnProjectAsync(dto.Project, ct);
            return await WmsVoucherControllerResponses.ExecuteRequiredAsync(
                () => exportReturns.CreateAsync(dto, ct), ct);
        }
        catch (UnauthorizedAccessException ex)
        {
            return StatusCode(StatusCodes.Status403Forbidden, ex.Message);
        }
    }

    [HttpPut("{id}")]
    [RequirePermission("wms.export_returns.create")]
    public async Task<ActionResult<ExportReturnDto>> Update(
        string id,
        [FromBody] CreateExportReturnDto dto,
        CancellationToken ct)
    {
        var validation = await createValidator.ValidateAsync(dto, ct);
        if (!validation.IsValid)
            return BadRequest(string.Join("; ", validation.Errors.Select(e => e.ErrorMessage)));
        try
        {
            if (!string.IsNullOrWhiteSpace(dto.Project))
                await projectAccess.EnsureCanOperateOnProjectAsync(dto.Project, ct);
            return await WmsVoucherControllerResponses.ExecuteAsync(
                () => exportReturns.UpdateAsync(id, dto, ct), ct);
        }
        catch (UnauthorizedAccessException ex)
        {
            return StatusCode(StatusCodes.Status403Forbidden, ex.Message);
        }
    }

    [HttpPost("{id}/approve")]
    [RequirePermission("wms.export_returns.approve")]
    public async Task<ActionResult<ExportReturnDto>> Approve(string id, CancellationToken ct)
    {
        var item = await exportReturns.GetByIdAsync(id, ct);
        if (item is null) return NotFound();
        try
        {
            if (!string.IsNullOrWhiteSpace(item.Project))
                await projectAccess.EnsureCanOperateOnProjectAsync(item.Project, ct);
        }
        catch (UnauthorizedAccessException ex)
        {
            return StatusCode(StatusCodes.Status403Forbidden, ex.Message);
        }
        return await WmsVoucherControllerResponses.ExecuteAsync(() => exportReturns.ApproveAsync(id, ct), ct);
    }

    [HttpPost("{id}/submit")]
    [RequirePermission("wms.export_returns.create")]
    public Task<ActionResult<ExportReturnDto>> Submit(string id, CancellationToken ct) =>
        WmsVoucherControllerResponses.ExecuteAsync(() => exportReturns.SubmitAsync(id, ct), ct);

    [HttpDelete("{id}")]
    [RequirePermission("wms.export_returns.create")]
    public async Task<IActionResult> Delete(string id, CancellationToken ct)
    {
        var item = await exportReturns.GetByIdAsync(id, ct);
        if (item is null) return NotFound();
        try
        {
            if (!string.IsNullOrWhiteSpace(item.Project))
                await projectAccess.EnsureCanOperateOnProjectAsync(item.Project, ct);
        }
        catch (UnauthorizedAccessException ex)
        {
            return StatusCode(StatusCodes.Status403Forbidden, ex.Message);
        }
        return await WmsVoucherControllerResponses.ExecuteDeleteAsync(() => exportReturns.DeleteAsync(id, ct), ct);
    }

    [HttpPost("{id}/attachments")]
    [RequirePermission("wms.export_returns.create")]
    [RequestSizeLimit(10 * 1024 * 1024 + 1024)]
    public async Task<ActionResult<ImportAttachmentDto>> UploadAttachment(
        string id,
        IFormFile file,
        [FromForm] string? kind,
        CancellationToken ct)
    {
        if (file is null || file.Length == 0)
            return BadRequest("Vui lòng chọn file.");
        if (file.Length > 10 * 1024 * 1024)
            return BadRequest("File vượt quá 10MB.");

        try
        {
            await using var stream = file.OpenReadStream();
            var created = await exportReturns.UploadAttachmentAsync(
                id,
                kind ?? "photo",
                file.FileName,
                file.ContentType ?? "application/octet-stream",
                stream,
                ct);
            return created is null ? NotFound() : Ok(created);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
    }

    [HttpDelete("{id}/attachments/{attachmentId:guid}")]
    [RequirePermission("wms.export_returns.create")]
    public async Task<IActionResult> DeleteAttachment(string id, Guid attachmentId, CancellationToken ct)
    {
        try
        {
            var ok = await exportReturns.DeleteAttachmentAsync(id, attachmentId, ct);
            return ok ? NoContent() : NotFound();
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
    }

    [HttpGet("{id}/attachments/{attachmentId:guid}/download")]
    [RequirePermission("wms.export_returns.view:all")]
    public async Task<IActionResult> DownloadAttachment(string id, Guid attachmentId, CancellationToken ct)
    {
        var dto = await exportReturns.GetAttachmentAsync(id, attachmentId, ct);
        if (dto is null) return NotFound();
        try
        {
            var stored = await fileStorage.OpenReadAsync(dto.StorageKey, ct);
            if (stored is null) return NotFound();
            return File(stored.Stream, stored.ResolveContentType(dto.FileName), dto.FileName);
        }
        catch (Exception)
        {
            return NotFound();
        }
    }
}
