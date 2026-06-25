using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NamViet.ERP.Api.Application.Dtos.Wms;
using NamViet.ERP.Api.Application.Services.Wms;

namespace NamViet.ERP.Api.Controllers.Wms;

[ApiController]
[Route("api/imports")]
[Authorize]
public class ImportsController(
    IImportApplicationService imports,
    IValidator<CreateImportDto> createValidator) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<List<ImportDto>>> List(CancellationToken ct) =>
        Ok(await imports.ListAsync(ct));

    [HttpGet("{id}")]
    public async Task<ActionResult<ImportDto>> GetById(string id, CancellationToken ct)
    {
        var item = await imports.GetByIdAsync(id, ct);
        return item is null ? NotFound() : Ok(item);
    }

    [HttpPost]
    public async Task<ActionResult<ImportDto>> Create([FromBody] CreateImportDto dto, CancellationToken ct)
    {
        var validation = await createValidator.ValidateAsync(dto, ct);
        if (!validation.IsValid)
            return BadRequest(string.Join("; ", validation.Errors.Select(e => e.ErrorMessage)));
        return Ok(await imports.CreateAsync(dto, ct));
    }

    [HttpPut("{id}")]
    public async Task<ActionResult<ImportDto>> Update(string id, [FromBody] CreateImportDto dto, CancellationToken ct)
    {
        var validation = await createValidator.ValidateAsync(dto, ct);
        if (!validation.IsValid)
            return BadRequest(string.Join("; ", validation.Errors.Select(e => e.ErrorMessage)));
        return await WmsVoucherControllerResponses.ExecuteAsync(() => imports.UpdateAsync(id, dto, ct), ct);
    }

    [HttpPost("{id}/approve")]
    [Authorize(Roles = "Admin,Warehouse_Keeper,Storekeeper")]
    public Task<ActionResult<ImportDto>> Approve(string id, CancellationToken ct) =>
        WmsVoucherControllerResponses.ExecuteAsync(() => imports.ApproveAsync(id, ct), ct);

    [HttpPost("{id}/submit")]
    [Authorize(Roles = "Admin,PM,Warehouse_Keeper,Storekeeper")]
    public Task<ActionResult<ImportDto>> Submit(string id, CancellationToken ct) =>
        WmsVoucherControllerResponses.ExecuteAsync(() => imports.SubmitAsync(id, ct), ct);

    [HttpDelete("{id}")]
    public Task<IActionResult> Delete(string id, CancellationToken ct) =>
        WmsVoucherControllerResponses.ExecuteDeleteAsync(() => imports.DeleteAsync(id, ct), ct);
}
