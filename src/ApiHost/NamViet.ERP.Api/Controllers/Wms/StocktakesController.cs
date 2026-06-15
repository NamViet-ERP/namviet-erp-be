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
[Route("api/stocktakes")]
[Authorize]
public class StocktakesController(WarehouseDbContext db, ICurrentUser currentUser) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<List<StocktakeDto>>> List(CancellationToken ct)
    {
        var items = await db.Stocktakes
            .Include(s => s.Items)
            .Where(s => s.DeletedAt == null)
            .OrderByDescending(s => s.CreatedAt)
            .ToListAsync(ct);
        return Ok(items.Select(WmsMapper.ToStocktake).ToList());
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<StocktakeDto>> GetById(string id, CancellationToken ct)
    {
        var s = await FindAsync(id, ct);
        if (s is null) return NotFound();
        return Ok(WmsMapper.ToStocktake(s));
    }

    [HttpPost]
    public async Task<ActionResult<StocktakeDto>> Create([FromBody] StocktakeDto dto, CancellationToken ct)
    {
        var s = new Stocktake
        {
            Id = $"st-{Guid.NewGuid():N}"[..10],
            VoucherCode = dto.VoucherCode,
            VoucherDate = WmsMapper.ParseClientDate(dto.Date),
            CreatedById = currentUser.Id,
            CreatedByName = string.IsNullOrEmpty(dto.CreatedBy) ? currentUser.Name : dto.CreatedBy,
            Status = dto.Status,
            WarehouseId = dto.WarehouseId,
            WarehouseName = dto.WarehouseName,
            Condition = dto.Condition,
            IsFrozen = dto.IsFrozen,
            Note = dto.Note,
            Items = dto.Items.Select(i => new StocktakeItem
            {
                MaterialCode = string.IsNullOrEmpty(i.Sku) ? i.ProductId : i.Sku,
                Sku = i.Sku,
                Name = i.Name,
                SystemQty = i.SystemQty,
                ActualQty = i.ActualQty,
                Diff = i.Diff
            }).ToList()
        };
        foreach (var item in s.Items) item.StocktakeId = s.Id;
        db.Stocktakes.Add(s);
        await db.SaveChangesAsync(ct);
        return Ok(WmsMapper.ToStocktake(s));
    }

    [HttpPut("{id}")]
    public async Task<ActionResult<StocktakeDto>> Update(string id, [FromBody] StocktakeDto dto, CancellationToken ct)
    {
        var s = await FindAsync(id, ct);
        if (s is null) return NotFound();
        s.VoucherCode = dto.VoucherCode;
        s.VoucherDate = WmsMapper.ParseClientDate(dto.Date);
        if (!string.IsNullOrEmpty(dto.CreatedBy))
            s.CreatedByName = dto.CreatedBy;
        s.Status = dto.Status;
        s.WarehouseId = dto.WarehouseId;
        s.WarehouseName = dto.WarehouseName;
        s.Condition = dto.Condition;
        s.IsFrozen = dto.IsFrozen;
        s.Note = dto.Note;
        s.UpdatedAt = DateTime.UtcNow;
        db.StocktakeItems.RemoveRange(s.Items);
        s.Items = dto.Items.Select(i => new StocktakeItem
        {
            StocktakeId = s.Id,
            MaterialCode = string.IsNullOrEmpty(i.Sku) ? i.ProductId : i.Sku,
            Sku = i.Sku,
            Name = i.Name,
            SystemQty = i.SystemQty,
            ActualQty = i.ActualQty,
            Diff = i.Diff
        }).ToList();
        await db.SaveChangesAsync(ct);
        return Ok(WmsMapper.ToStocktake(s));
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(string id, CancellationToken ct)
    {
        var s = await FindAsync(id, ct);
        if (s is null) return NotFound();
        s.DeletedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        return NoContent();
    }

    private async Task<Stocktake?> FindAsync(string id, CancellationToken ct) =>
        await db.Stocktakes.Include(s => s.Items)
            .FirstOrDefaultAsync(s => s.Id == id && s.DeletedAt == null, ct);
}
