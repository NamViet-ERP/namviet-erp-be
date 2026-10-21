using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NamViet.ERP.Api.Application.Dtos.Purchasing;
using NamViet.ERP.Api.Application.Dtos.Wms;
using NamViet.ERP.Api.Application.Services.Purchasing;
using NamViet.ERP.Api.Application.Services.Wms;
using NamViet.ERP.Api.Infrastructure.Authorization;
using NamViet.ERP.Api.Services.Storage;
using NamViet.ERP.SharedKernel.Authorization;

namespace NamViet.ERP.Api.Controllers.Wms;

[ApiController]
[Route("api/imports")]
[Authorize]
public class ImportsController(
    IImportApplicationService imports,
    IImportExcelService importExcel,
    IWmsVoucherExcelExportService voucherExport,
    IValidator<CreateImportDto> createValidator,
    IFileStorage fileStorage,
    ProjectWarehouseAccessService projectAccess) : ControllerBase
{
    [HttpGet]
    [RequirePermission("wms.imports.view:all")]
    public async Task<ActionResult<List<ImportDto>>> List(
        [FromQuery] DateOnly? dateFrom,
        [FromQuery] DateOnly? dateTo,
        CancellationToken ct)
    {
        var rows = await imports.ListAsync(dateFrom, dateTo, ct);
        return Ok(rows.Select(MaskImportPrices).ToList());
    }

    [HttpGet("{id}")]
    [RequirePermission("wms.imports.view:all")]
    public async Task<ActionResult<ImportDto>> GetById(string id, CancellationToken ct)
    {
        var item = await imports.GetByIdAsync(id, ct);
        return item is null ? NotFound() : Ok(MaskImportPrices(item));
    }

    [HttpPost]
    [RequirePermission("wms.imports.create")]
    public async Task<ActionResult<ImportDto>> Create([FromBody] CreateImportDto dto, CancellationToken ct)
    {
        var validation = await createValidator.ValidateAsync(dto, ct);
        if (!validation.IsValid)
            return BadRequest(string.Join("; ", validation.Errors.Select(e => e.ErrorMessage)));
        try
        {
            if (!string.IsNullOrWhiteSpace(dto.Project))
                await projectAccess.EnsureCanOperateOnProjectAsync(dto.Project, ct);
            return Ok(MaskImportPrices(await imports.CreateAsync(dto, ct)));
        }
        catch (UnauthorizedAccessException ex)
        {
            return StatusCode(StatusCodes.Status403Forbidden, ex.Message);
        }
    }

    [HttpPut("{id}")]
    [RequirePermission("wms.imports.update")]
    public async Task<ActionResult<ImportDto>> Update(string id, [FromBody] CreateImportDto dto, CancellationToken ct)
    {
        var validation = await createValidator.ValidateAsync(dto, ct);
        if (!validation.IsValid)
            return BadRequest(string.Join("; ", validation.Errors.Select(e => e.ErrorMessage)));
        try
        {
            if (!string.IsNullOrWhiteSpace(dto.Project))
                await projectAccess.EnsureCanOperateOnProjectAsync(dto.Project, ct);
            return await WmsVoucherControllerResponses.ExecuteAsync(
                async () =>
                {
                    var updated = await imports.UpdateAsync(id, dto, ct);
                    return updated is null ? null : MaskImportPrices(updated);
                },
                ct);
        }
        catch (UnauthorizedAccessException ex)
        {
            return StatusCode(StatusCodes.Status403Forbidden, ex.Message);
        }
    }

    [HttpGet("export")]
    [RequirePermission("wms.imports.view:all")]
    public async Task<IActionResult> ExportExcel(
        [FromQuery] DateOnly? dateFrom,
        [FromQuery] DateOnly? dateTo,
        [FromQuery] string? status = null,
        [FromQuery] string? search = null,
        CancellationToken ct = default)
    {
        var items = WmsVoucherExportFilters.FilterRows(
            (await imports.ListAsync(dateFrom, dateTo, ct)).Select(MaskImportPrices).ToList(),
            status,
            search,
            row => row.Status,
            row => row.VoucherCode,
            row => row.Supplier,
            row => row.ReferenceCode,
            row => row.Project);

        var bytes = voucherExport.BuildImportWorkbook(items, includePrices: CanViewPrices);
        var fileName = $"phieu-nhap-{DateTime.UtcNow:yyyyMMdd-HHmm}.xlsx";
        return File(
            bytes,
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            fileName);
    }

    [HttpGet("from-po/{poCode}/preview")]
    [RequirePermission("wms.imports.create")]
    public async Task<ActionResult<PoImportPreviewDto>> PreviewFromPurchaseOrder(
        string poCode,
        [FromQuery] string? excludeTicketCode,
        CancellationToken ct)
    {
        try
        {
            var preview = await imports.PreviewImportFromPurchaseOrderAsync(poCode, excludeTicketCode, ct);
            await projectAccess.EnsureCanOperateOnProjectAsync(preview.ProjectCode, ct);
            return Ok(MaskPoImportPrices(preview));
        }
        catch (UnauthorizedAccessException ex)
        {
            return StatusCode(StatusCodes.Status403Forbidden, ex.Message);
        }
        catch (PurchasingConflictException ex)
        {
            return Conflict(ex.Message);
        }
    }

    [HttpPost("from-po/{poCode}")]
    [RequirePermission("wms.imports.create")]
    public async Task<ActionResult<ImportDto>> CreateFromPurchaseOrder(
        string poCode,
        [FromBody] CreateImportFromPoRequest? request,
        [FromQuery] bool isDirectProcurement = false,
        CancellationToken ct = default)
    {
        try
        {
            var preview = await imports.PreviewImportFromPurchaseOrderAsync(poCode, ct: ct);
            await projectAccess.EnsureCanOperateOnProjectAsync(preview.ProjectCode, ct);
            var effective = (request ?? new CreateImportFromPoRequest()) with
            {
                IsDirectProcurement = isDirectProcurement || (request?.IsDirectProcurement ?? false),
                IssueFromGeneralWarehouse = request?.IssueFromGeneralWarehouse ?? false,
            };
            var created = await imports.CreateDraftFromPurchaseOrderAsync(poCode, effective, ct);
            return Ok(MaskImportPrices(created));
        }
        catch (UnauthorizedAccessException ex)
        {
            return StatusCode(StatusCodes.Status403Forbidden, ex.Message);
        }
        catch (PurchasingConflictException ex)
        {
            return Conflict(ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
    }

    private bool CanViewPrices => PriceVisibility.CanViewPrices(User);

    private PoImportPreviewDto MaskPoImportPrices(PoImportPreviewDto preview)
    {
        // Prices stripped on FE too; BE hides when claim missing (Admin has all via catalog).
        if (CanViewPrices)
            return preview;

        return preview with
        {
            Lines = preview.Lines
                .Select(l => l with { UnitPrice = 0m })
                .ToList(),
        };
    }

    private ImportDto MaskImportPrices(ImportDto dto)
    {
        if (CanViewPrices)
            return dto;

        var maskedItems = dto.Items
            .Select(i => i with { UnitPrice = 0m })
            .ToList();
        return dto with
        {
            TotalValue = 0m,
            Items = maskedItems,
        };
    }

    [HttpPost("{id}/approve")]
    [RequirePermission("wms.imports.approve")]
    [ProducesResponseType(typeof(ImportDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ImportBudgetExceededDto), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ImportDto>> Approve(
        string id,
        [FromBody] ApproveImportRequest? request,
        CancellationToken ct)
    {
        try
        {
            var item = await imports.ApproveAsync(id, request, ct);
            return item is null ? NotFound() : Ok(MaskImportPrices(item));
        }
        catch (ImportBudgetExceededException ex)
        {
            return Conflict(new ImportBudgetExceededDto(
                "IMPORT_BUDGET_EXCEEDED",
                ex.Message,
                ex.ProjectCode,
                ex.MaterialBudget,
                ex.ApprovedImportValue,
                ex.ImportValue,
                ex.OverflowAmount));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (PurchasingConflictException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (Exception)
        {
            return StatusCode(500, "Không thể duyệt phiếu nhập. Vui lòng thử lại hoặc liên hệ quản trị viên.");
        }
    }

    [HttpPut("{id}/receipt-evaluation")]
    [RequirePermission("wms.imports.update")]
    public async Task<ActionResult<ImportDto>> UpdateReceiptEvaluation(
        string id,
        [FromBody] UpdateImportReceiptEvaluationRequest request,
        CancellationToken ct)
    {
        try
        {
            var item = await imports.UpdateReceiptEvaluationAsync(id, request.Evaluation, ct);
            return item is null ? NotFound() : Ok(MaskImportPrices(item));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
    }

    [HttpPost("{id}/attachments")]
    [RequirePermission("wms.imports.update")]
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
            var created = await imports.UploadAttachmentAsync(
                id,
                kind ?? "document",
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
    [RequirePermission("wms.imports.update")]
    public async Task<IActionResult> DeleteAttachment(
        string id,
        Guid attachmentId,
        CancellationToken ct)
    {
        try
        {
            var ok = await imports.DeleteAttachmentAsync(id, attachmentId, ct);
            return ok ? NoContent() : NotFound();
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
    }

    [HttpGet("{id}/attachments/{attachmentId:guid}/download")]
    [RequirePermission("wms.imports.view:all")]
    public async Task<IActionResult> DownloadAttachment(
        string id,
        Guid attachmentId,
        CancellationToken ct)
    {
        var dto = await imports.GetAttachmentAsync(id, attachmentId, ct);
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

    [HttpPost("{id}/submit")]
    [RequirePermission("wms.imports.create")]
    public Task<ActionResult<ImportDto>> Submit(string id, CancellationToken ct) =>
        WmsVoucherControllerResponses.ExecuteAsync(
            async () =>
            {
                var submitted = await imports.SubmitAsync(id, ct);
                return submitted is null ? null : MaskImportPrices(submitted);
            },
            ct);

    [HttpDelete("{id}")]
    [RequirePermission("wms.imports.create")]
    public Task<IActionResult> Delete(string id, CancellationToken ct) =>
        WmsVoucherControllerResponses.ExecuteDeleteAsync(() => imports.DeleteAsync(id, ct), ct);

    [HttpGet("import-template")]
    [RequirePermission("wms.imports.create")]
    public IActionResult DownloadImportTemplate()
    {
        var bytes = importExcel.GenerateTemplate();
        return File(
            bytes,
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            "phieu-nhap-vat-tu-mau.xlsx");
    }

    [HttpPost("parse-import-file")]
    [RequirePermission("wms.imports.create")]
    [RequestSizeLimit(2 * 1024 * 1024 + 1024)]
    public async Task<ActionResult<ImportParseResultDto>> ParseImportFile(IFormFile file, CancellationToken ct)
    {
        if (file.Length == 0)
            return BadRequest("Vui lòng chọn file import.");
        if (file.Length > 2 * 1024 * 1024)
            return BadRequest("File vượt quá 2MB.");

        var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (ext is not (".xlsx" or ".xls" or ".csv"))
            return BadRequest("Chỉ chấp nhận file .xlsx, .xls hoặc .csv.");

        try
        {
            await using var stream = file.OpenReadStream();
            var result = await importExcel.ParseImportFileAsync(stream, file.FileName, ct);
            return Ok(result);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
    }
}
