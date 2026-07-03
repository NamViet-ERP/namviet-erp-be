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
    IValidator<CreateExportDto> createValidator) : ControllerBase
{
    [HttpGet]
    [RequirePermission("wms.exports.view:all")]
    public async Task<ActionResult<List<ExportDto>>> List(
        [FromQuery] DateOnly? dateFrom,
        [FromQuery] DateOnly? dateTo,
        CancellationToken ct) =>
        Ok(await exports.ListAsync(dateFrom, dateTo, ct));

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
        return Ok(await exports.CreateAsync(dto, ct));
    }

    [HttpPut("{id}")]
    [RequirePermission("wms.exports.update")]
    public async Task<ActionResult<ExportDto>> Update(string id, [FromBody] CreateExportDto dto, CancellationToken ct)
    {
        var validation = await createValidator.ValidateAsync(dto, ct);
        if (!validation.IsValid)
            return BadRequest(string.Join("; ", validation.Errors.Select(e => e.ErrorMessage)));
        return await WmsVoucherControllerResponses.ExecuteAsync(() => exports.UpdateAsync(id, dto, ct), ct);
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
