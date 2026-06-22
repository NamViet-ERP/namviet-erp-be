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
        try
        {
            var item = await imports.UpdateAsync(id, dto, ct);
            return item is null ? NotFound() : Ok(item);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
    }

    [HttpPost("{id}/approve")]
    [Authorize(Roles = "Admin,Warehouse_Keeper,Storekeeper")]
    public async Task<ActionResult<ImportDto>> Approve(string id, CancellationToken ct)
    {
        try
        {
            var item = await imports.ApproveAsync(id, ct);
            return item is null ? NotFound() : Ok(item);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
    }

    [HttpPost("{id}/submit")]
    [Authorize(Roles = "Admin,PM,Warehouse_Keeper,Storekeeper")]
    public async Task<ActionResult<ImportDto>> Submit(string id, CancellationToken ct)
    {
        try
        {
            var item = await imports.SubmitAsync(id, ct);
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
            var deleted = await imports.DeleteAsync(id, ct);
            return deleted ? NoContent() : NotFound();
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
    }
}
