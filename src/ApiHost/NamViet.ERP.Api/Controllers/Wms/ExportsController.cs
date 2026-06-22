using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NamViet.ERP.Api.Application.Dtos.Wms;
using NamViet.ERP.Api.Application.Services.Wms;

namespace NamViet.ERP.Api.Controllers.Wms;

[ApiController]
[Route("api/exports")]
[Authorize]
public class ExportsController(
    IExportApplicationService exports,
    IValidator<CreateExportDto> createValidator) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<List<ExportDto>>> List(CancellationToken ct) =>
        Ok(await exports.ListAsync(ct));

    [HttpGet("{id}")]
    public async Task<ActionResult<ExportDto>> GetById(string id, CancellationToken ct)
    {
        var item = await exports.GetByIdAsync(id, ct);
        return item is null ? NotFound() : Ok(item);
    }

    [HttpPost]
    public async Task<ActionResult<ExportDto>> Create([FromBody] CreateExportDto dto, CancellationToken ct)
    {
        var validation = await createValidator.ValidateAsync(dto, ct);
        if (!validation.IsValid)
            return BadRequest(string.Join("; ", validation.Errors.Select(e => e.ErrorMessage)));
        return Ok(await exports.CreateAsync(dto, ct));
    }

    [HttpPut("{id}")]
    public async Task<ActionResult<ExportDto>> Update(string id, [FromBody] CreateExportDto dto, CancellationToken ct)
    {
        var validation = await createValidator.ValidateAsync(dto, ct);
        if (!validation.IsValid)
            return BadRequest(string.Join("; ", validation.Errors.Select(e => e.ErrorMessage)));
        try
        {
            var item = await exports.UpdateAsync(id, dto, ct);
            return item is null ? NotFound() : Ok(item);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
    }

    [HttpPost("{id}/approve")]
    [Authorize(Roles = "Admin,Warehouse_Keeper,Storekeeper")]
    public async Task<ActionResult<ExportDto>> Approve(string id, CancellationToken ct)
    {
        try
        {
            var item = await exports.ApproveAsync(id, ct);
            return item is null ? NotFound() : Ok(item);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
    }

    [HttpPost("{id}/submit")]
    [Authorize(Roles = "Admin,PM,Warehouse_Keeper,Storekeeper")]
    public async Task<ActionResult<ExportDto>> Submit(string id, CancellationToken ct)
    {
        try
        {
            var item = await exports.SubmitAsync(id, ct);
            return item is null ? NotFound() : Ok(item);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(string id, CancellationToken ct)
    {
        try
        {
            var deleted = await exports.DeleteAsync(id, ct);
            return deleted ? NoContent() : NotFound();
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
    }
}
