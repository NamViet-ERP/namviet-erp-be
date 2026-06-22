using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NamViet.ERP.Api.Application.Dtos.Wms;
using NamViet.ERP.Api.Application.Services.Wms;

namespace NamViet.ERP.Api.Controllers.Wms;

[ApiController]
[Route("api/transfers")]
[Authorize]
public class TransfersController(
    ITransferApplicationService transfers,
    IValidator<CreateTransferDto> createValidator) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<List<TransferDto>>> List(CancellationToken ct) =>
        Ok(await transfers.ListAsync(ct));

    [HttpGet("{id}")]
    public async Task<ActionResult<TransferDto>> GetById(string id, CancellationToken ct)
    {
        var item = await transfers.GetByIdAsync(id, ct);
        return item is null ? NotFound() : Ok(item);
    }

    [HttpPost]
    public async Task<ActionResult<TransferDto>> Create([FromBody] CreateTransferDto dto, CancellationToken ct)
    {
        var validation = await createValidator.ValidateAsync(dto, ct);
        if (!validation.IsValid)
            return BadRequest(string.Join("; ", validation.Errors.Select(e => e.ErrorMessage)));
        try
        {
            return Ok(await transfers.CreateAsync(dto, ct));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
    }

    [HttpPut("{id}")]
    public async Task<ActionResult<TransferDto>> Update(string id, [FromBody] CreateTransferDto dto, CancellationToken ct)
    {
        var validation = await createValidator.ValidateAsync(dto, ct);
        if (!validation.IsValid)
            return BadRequest(string.Join("; ", validation.Errors.Select(e => e.ErrorMessage)));
        try
        {
            var item = await transfers.UpdateAsync(id, dto, ct);
            return item is null ? NotFound() : Ok(item);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
    }

    [HttpPost("{id}/approve")]
    [Authorize(Roles = "Admin,Warehouse_Keeper,Storekeeper")]
    public async Task<ActionResult<TransferDto>> Approve(string id, CancellationToken ct)
    {
        try
        {
            var item = await transfers.ApproveAsync(id, ct);
            return item is null ? NotFound() : Ok(item);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
    }

    [HttpPost("{id}/submit")]
    [Authorize(Roles = "Admin,PM,Warehouse_Keeper,Storekeeper")]
    public async Task<ActionResult<TransferDto>> Submit(string id, CancellationToken ct)
    {
        try
        {
            var item = await transfers.SubmitAsync(id, ct);
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
            var deleted = await transfers.DeleteAsync(id, ct);
            return deleted ? NoContent() : NotFound();
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
    }
}
