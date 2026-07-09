using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NamViet.ERP.Api.Application.Dtos.Wms;
using NamViet.ERP.Api.Application.Services.Wms;
using NamViet.ERP.Api.Infrastructure.Authorization;

namespace NamViet.ERP.Api.Controllers.Wms;

[ApiController]
[Route("api/imports")]
[Authorize]
public class ImportsController(
    IImportApplicationService imports,
    IImportExcelService importExcel,
    IWmsVoucherExcelExportService voucherExport,
    IValidator<CreateImportDto> createValidator) : ControllerBase
{
    [HttpGet]
    [RequirePermission("wms.imports.view:all")]
    public async Task<ActionResult<List<ImportDto>>> List(
        [FromQuery] DateOnly? dateFrom,
        [FromQuery] DateOnly? dateTo,
        CancellationToken ct) =>
        Ok(await imports.ListAsync(dateFrom, dateTo, ct));

    [HttpGet("{id}")]
    [RequirePermission("wms.imports.view:all")]
    public async Task<ActionResult<ImportDto>> GetById(string id, CancellationToken ct)
    {
        var item = await imports.GetByIdAsync(id, ct);
        return item is null ? NotFound() : Ok(item);
    }

    [HttpPost]
    [RequirePermission("wms.imports.create")]
    public async Task<ActionResult<ImportDto>> Create([FromBody] CreateImportDto dto, CancellationToken ct)
    {
        var validation = await createValidator.ValidateAsync(dto, ct);
        if (!validation.IsValid)
            return BadRequest(string.Join("; ", validation.Errors.Select(e => e.ErrorMessage)));
        return Ok(await imports.CreateAsync(dto, ct));
    }

    [HttpPut("{id}")]
    [RequirePermission("wms.imports.update")]
    public async Task<ActionResult<ImportDto>> Update(string id, [FromBody] CreateImportDto dto, CancellationToken ct)
    {
        var validation = await createValidator.ValidateAsync(dto, ct);
        if (!validation.IsValid)
            return BadRequest(string.Join("; ", validation.Errors.Select(e => e.ErrorMessage)));
        return await WmsVoucherControllerResponses.ExecuteAsync(() => imports.UpdateAsync(id, dto, ct), ct);
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
            await imports.ListAsync(dateFrom, dateTo, ct),
            status,
            search,
            row => row.Status,
            row => row.VoucherCode,
            row => row.Supplier,
            row => row.ReferenceCode,
            row => row.Project);

        var bytes = voucherExport.BuildImportWorkbook(items);
        var fileName = $"phieu-nhap-{DateTime.UtcNow:yyyyMMdd-HHmm}.xlsx";
        return File(
            bytes,
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            fileName);
    }

    [HttpPost("{id}/approve")]
    [RequirePermission("wms.imports.approve")]
    public async Task<ActionResult<ImportDto>> Approve(
        string id,
        [FromBody] ApproveImportRequest? request,
        CancellationToken ct)
    {
        var confirmOverBudget = request?.ConfirmOverBudget ?? false;
        try
        {
            var item = await imports.ApproveAsync(id, confirmOverBudget, ct);
            return item is null ? NotFound() : Ok(item);
        }
        catch (ImportBudgetExceededException ex)
        {
            return Conflict(new ImportBudgetExceededResponse(
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
        catch (Exception)
        {
            return StatusCode(500, "Không thể duyệt phiếu nhập. Vui lòng thử lại hoặc liên hệ quản trị viên.");
        }
    }

    [HttpPost("{id}/submit")]
    [RequirePermission("wms.imports.update")]
    public Task<ActionResult<ImportDto>> Submit(string id, CancellationToken ct) =>
        WmsVoucherControllerResponses.ExecuteAsync(() => imports.SubmitAsync(id, ct), ct);

    [HttpDelete("{id}")]
    [RequirePermission("wms.imports.update")]
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
