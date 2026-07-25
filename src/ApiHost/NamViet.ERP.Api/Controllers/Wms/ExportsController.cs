using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NamViet.ERP.Api.Application.Dtos.Wms;
using NamViet.ERP.Api.Application.Services.Wms;
using NamViet.ERP.Api.Infrastructure.Authorization;

namespace NamViet.ERP.Api.Controllers.Wms;

[ApiController]
[Route("api/exports")]
[Authorize]
public class ExportsController(
    IExportApplicationService exports,
    IWmsVoucherExcelExportService voucherExport,
    IValidator<CreateExportDto> createValidator,
    ProjectWarehouseAccessService projectAccess) : ControllerBase
{
    [HttpGet]
    [RequirePermission("wms.exports.view:all")]
    public async Task<ActionResult<List<ExportDto>>> List(
        [FromQuery] DateOnly? dateFrom,
        [FromQuery] DateOnly? dateTo,
        CancellationToken ct) =>
        Ok(await exports.ListAsync(dateFrom, dateTo, ct));

    [HttpGet("export")]
    [RequirePermission("wms.exports.view:all")]
    public async Task<IActionResult> ExportExcel(
        [FromQuery] DateOnly? dateFrom,
        [FromQuery] DateOnly? dateTo,
        [FromQuery] string? status = null,
        [FromQuery] string? search = null,
        CancellationToken ct = default)
    {
        var items = WmsVoucherExportFilters.FilterRows(
            await exports.ListAsync(dateFrom, dateTo, ct),
            status,
            search,
            row => row.Status,
            row => row.VoucherCode,
            row => row.Customer,
            row => row.SoCode,
            row => row.Project);

        var bytes = voucherExport.BuildExportWorkbook(items);
        return File(
            bytes,
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            $"phieu-xuat-{DateTime.UtcNow:yyyyMMdd-HHmm}.xlsx");
    }

    [HttpGet("{id}")]
    [RequirePermission("wms.exports.view:all")]
    public async Task<ActionResult<ExportDto>> GetById(string id, CancellationToken ct)
    {
        var item = await exports.GetByIdAsync(id, ct);
        return item is null ? NotFound() : Ok(item);
    }

    [HttpPost]
    [RequirePermission("wms.exports.create")]
    public async Task<ActionResult<ExportDto>> Create([FromBody] CreateExportDto dto, CancellationToken ct)
    {
        var validation = await createValidator.ValidateAsync(dto, ct);
        if (!validation.IsValid)
            return BadRequest(string.Join("; ", validation.Errors.Select(e => e.ErrorMessage)));
        try
        {
            if (!string.IsNullOrWhiteSpace(dto.Project))
                await projectAccess.EnsureCanOperateOnProjectAsync(dto.Project, ct);
            return Ok(await exports.CreateAsync(dto, ct));
        }
        catch (UnauthorizedAccessException ex)
        {
            return StatusCode(StatusCodes.Status403Forbidden, ex.Message);
        }
    }

    [HttpPut("{id}")]
    [RequirePermission("wms.exports.update")]
    public async Task<ActionResult<ExportDto>> Update(string id, [FromBody] CreateExportDto dto, CancellationToken ct)
    {
        var validation = await createValidator.ValidateAsync(dto, ct);
        if (!validation.IsValid)
            return BadRequest(string.Join("; ", validation.Errors.Select(e => e.ErrorMessage)));
        try
        {
            if (!string.IsNullOrWhiteSpace(dto.Project))
                await projectAccess.EnsureCanOperateOnProjectAsync(dto.Project, ct);
            return await WmsVoucherControllerResponses.ExecuteAsync(() => exports.UpdateAsync(id, dto, ct), ct);
        }
        catch (UnauthorizedAccessException ex)
        {
            return StatusCode(StatusCodes.Status403Forbidden, ex.Message);
        }
    }

    [HttpPost("{id}/approve")]
    [RequirePermission("wms.exports.approve")]
    public Task<ActionResult<ExportDto>> Approve(string id, CancellationToken ct) =>
        WmsVoucherControllerResponses.ExecuteAsync(() => exports.ApproveAsync(id, ct), ct);

    [HttpPost("{id}/submit")]
    [RequirePermission("wms.exports.update")]
    public Task<ActionResult<ExportDto>> Submit(string id, CancellationToken ct) =>
        WmsVoucherControllerResponses.ExecuteAsync(() => exports.SubmitAsync(id, ct), ct);

    [HttpDelete("{id}")]
    [RequirePermission("wms.exports.update")]
    public Task<IActionResult> Delete(string id, CancellationToken ct) =>
        WmsVoucherControllerResponses.ExecuteDeleteAsync(() => exports.DeleteAsync(id, ct), ct);
}
