using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NamViet.ERP.Api.Application.Dtos.Wms;
using NamViet.ERP.Api.Application.Services.Wms;
using NamViet.ERP.Api.Infrastructure.Authorization;

namespace NamViet.ERP.Api.Controllers.Wms;

[ApiController]
[Route("api/transfers")]
[Authorize]
public class TransfersController(
    ITransferApplicationService transfers,
    IWmsVoucherExcelExportService voucherExport,
    IValidator<CreateTransferDto> createValidator) : ControllerBase
{
    [HttpGet]
    [RequirePermission("wms.transfers.view:all")]
    public async Task<ActionResult<List<TransferDto>>> List(
        [FromQuery] DateOnly? dateFrom,
        [FromQuery] DateOnly? dateTo,
        CancellationToken ct) =>
        Ok(await transfers.ListAsync(dateFrom, dateTo, ct));

    [HttpGet("export")]
    [RequirePermission("wms.transfers.view:all")]
    public async Task<IActionResult> ExportExcel(
        [FromQuery] DateOnly? dateFrom,
        [FromQuery] DateOnly? dateTo,
        [FromQuery] string? status = null,
        [FromQuery] string? search = null,
        CancellationToken ct = default)
    {
        var items = WmsVoucherExportFilters.FilterRows(
            await transfers.ListAsync(dateFrom, dateTo, ct),
            status,
            search,
            row => row.Status,
            row => row.VoucherCode,
            row => row.FromWarehouseName,
            row => row.ToWarehouseName,
            row => row.Project,
            row => row.ToProject);

        var bytes = voucherExport.BuildTransferWorkbook(items);
        return File(
            bytes,
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            $"phieu-chuyen-{DateTime.UtcNow:yyyyMMdd-HHmm}.xlsx");
    }

    [HttpGet("{id}")]
    [RequirePermission("wms.transfers.view:all")]
    public async Task<ActionResult<TransferDto>> GetById(string id, CancellationToken ct)
    {
        var item = await transfers.GetByIdAsync(id, ct);
        return item is null ? NotFound() : Ok(item);
    }

    [HttpPost]
    [RequirePermission("wms.transfers.create")]
    public async Task<ActionResult<TransferDto>> Create([FromBody] CreateTransferDto dto, CancellationToken ct)
    {
        var validation = await createValidator.ValidateAsync(dto, ct);
        if (!validation.IsValid)
            return BadRequest(string.Join("; ", validation.Errors.Select(e => e.ErrorMessage)));
        return await WmsVoucherControllerResponses.ExecuteRequiredAsync(() => transfers.CreateAsync(dto, ct), ct);
    }

    [HttpPut("{id}")]
    [RequirePermission("wms.transfers.create")]
    public async Task<ActionResult<TransferDto>> Update(string id, [FromBody] CreateTransferDto dto, CancellationToken ct)
    {
        var validation = await createValidator.ValidateAsync(dto, ct);
        if (!validation.IsValid)
            return BadRequest(string.Join("; ", validation.Errors.Select(e => e.ErrorMessage)));
        return await WmsVoucherControllerResponses.ExecuteAsync(() => transfers.UpdateAsync(id, dto, ct), ct);
    }

    [HttpPost("{id}/approve")]
    [RequirePermission("wms.transfers.approve")]
    public Task<ActionResult<TransferDto>> Approve(string id, CancellationToken ct) =>
        WmsVoucherControllerResponses.ExecuteAsync(() => transfers.ApproveAsync(id, ct), ct);

    [HttpPost("{id}/submit")]
    [RequirePermission("wms.transfers.create")]
    public Task<ActionResult<TransferDto>> Submit(string id, CancellationToken ct) =>
        WmsVoucherControllerResponses.ExecuteAsync(() => transfers.SubmitAsync(id, ct), ct);

    [HttpDelete("{id}")]
    [RequirePermission("wms.transfers.create")]
    public Task<IActionResult> Delete(string id, CancellationToken ct) =>
        WmsVoucherControllerResponses.ExecuteDeleteAsync(() => transfers.DeleteAsync(id, ct), ct);
}
