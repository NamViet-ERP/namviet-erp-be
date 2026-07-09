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
    IValidator<CreateExportReturnDto> createValidator) : ControllerBase
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
        return await WmsVoucherControllerResponses.ExecuteRequiredAsync(
            () => exportReturns.CreateAsync(dto, ct), ct);
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
        return await WmsVoucherControllerResponses.ExecuteAsync(
            () => exportReturns.UpdateAsync(id, dto, ct), ct);
    }

    [HttpPost("{id}/approve")]
    [RequirePermission("wms.export_returns.approve")]
    public Task<ActionResult<ExportReturnDto>> Approve(string id, CancellationToken ct) =>
        WmsVoucherControllerResponses.ExecuteAsync(() => exportReturns.ApproveAsync(id, ct), ct);

    [HttpPost("{id}/submit")]
    [RequirePermission("wms.export_returns.create")]
    public Task<ActionResult<ExportReturnDto>> Submit(string id, CancellationToken ct) =>
        WmsVoucherControllerResponses.ExecuteAsync(() => exportReturns.SubmitAsync(id, ct), ct);

    [HttpDelete("{id}")]
    [RequirePermission("wms.export_returns.create")]
    public Task<IActionResult> Delete(string id, CancellationToken ct) =>
        WmsVoucherControllerResponses.ExecuteDeleteAsync(() => exportReturns.DeleteAsync(id, ct), ct);
}
