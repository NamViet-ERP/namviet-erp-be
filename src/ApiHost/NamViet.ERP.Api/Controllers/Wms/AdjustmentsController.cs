using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NamViet.ERP.Api.Application.Dtos.Wms;
using NamViet.ERP.Api.Application.Mappers.Wms;
using NamViet.ERP.Modules.Warehouse.Domain.Entities;
using NamViet.ERP.Modules.Warehouse.Infrastructure.Persistence;
using NamViet.ERP.SharedKernel.Interfaces;

namespace NamViet.ERP.Api.Controllers.Wms;

[ApiController]
[Route("api/adjustments")]
[Authorize]
public class AdjustmentsController(WarehouseDbContext db, ICurrentUser currentUser) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<List<AdjustmentDto>>> List(CancellationToken ct)
    {
        var items = await db.Adjustments
            .Include(a => a.Items)
            .Where(a => a.DeletedAt == null)
            .OrderByDescending(a => a.CreatedAt)
            .ToListAsync(ct);
        return Ok(items.Select(WmsMapper.ToAdjustment).ToList());
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<AdjustmentDto>> GetById(string id, CancellationToken ct)
    {
        var a = await FindAsync(id, ct);
        if (a is null) return NotFound();
        return Ok(WmsMapper.ToAdjustment(a));
    }

    [HttpPost]
    public async Task<ActionResult<AdjustmentDto>> Create([FromBody] AdjustmentDto dto, CancellationToken ct)
    {
        var a = new Adjustment
        {
            Id = $"adj-{Guid.NewGuid():N}"[..11],
            VoucherCode = dto.VoucherCode,
            VoucherDate = WmsMapper.ParseClientDate(dto.Date),
            StocktakeCode = dto.StocktakeCode,
            Type = dto.Type,
            CreatedById = currentUser.Id,
            CreatedByName = string.IsNullOrEmpty(dto.CreatedBy) ? currentUser.Name : dto.CreatedBy,
            Condition = dto.Condition,
            WarehouseId = dto.WarehouseId,
            WarehouseName = dto.WarehouseName,
            Note = dto.Note,
            IsNew = dto.IsNew,
            Items = dto.Items.Select(i => new AdjustmentItem
            {
                MaterialCode = string.IsNullOrEmpty(i.Sku) ? i.ProductId : i.Sku,
                Sku = i.Sku,
                Name = i.Name,
                AdjustQty = i.AdjustQty,
                Reason = i.Reason
            }).ToList()
        };
        foreach (var item in a.Items) item.AdjustmentId = a.Id;
        db.Adjustments.Add(a);
        await db.SaveChangesAsync(ct);
        return Ok(WmsMapper.ToAdjustment(a));
    }

    [HttpPut("{id}")]
    public async Task<ActionResult<AdjustmentDto>> Update(string id, [FromBody] AdjustmentDto dto, CancellationToken ct)
    {
        var a = await FindAsync(id, ct);
        if (a is null) return NotFound();
        a.VoucherCode = dto.VoucherCode;
        a.VoucherDate = WmsMapper.ParseClientDate(dto.Date);
        a.StocktakeCode = dto.StocktakeCode;
        a.Type = dto.Type;
        if (!string.IsNullOrEmpty(dto.CreatedBy))
            a.CreatedByName = dto.CreatedBy;
        a.Condition = dto.Condition;
        a.WarehouseId = dto.WarehouseId;
        a.WarehouseName = dto.WarehouseName;
        a.Note = dto.Note;
        a.IsNew = dto.IsNew;
        a.UpdatedAt = DateTime.UtcNow;
        db.AdjustmentItems.RemoveRange(a.Items);
        a.Items = dto.Items.Select(i => new AdjustmentItem
        {
            AdjustmentId = a.Id,
            MaterialCode = string.IsNullOrEmpty(i.Sku) ? i.ProductId : i.Sku,
            Sku = i.Sku,
            Name = i.Name,
            AdjustQty = i.AdjustQty,
            Reason = i.Reason
        }).ToList();
        await db.SaveChangesAsync(ct);
        return Ok(WmsMapper.ToAdjustment(a));
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(string id, CancellationToken ct)
    {
        var a = await FindAsync(id, ct);
        if (a is null) return NotFound();
        a.DeletedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        return NoContent();
    }

    private async Task<Adjustment?> FindAsync(string id, CancellationToken ct) =>
        await db.Adjustments.Include(a => a.Items)
            .FirstOrDefaultAsync(a => a.Id == id && a.DeletedAt == null, ct);
}
