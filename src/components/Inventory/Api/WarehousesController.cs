using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NamViet.ERP.Api.Application.Dtos.Wms;
using NamViet.ERP.Api.Application.Mappers.Wms;
using NamViet.ERP.Api.Application.Services.Wms;
using NamViet.ERP.Api.Infrastructure.Authorization;
using NamViet.ERP.Modules.Warehouse.Domain.Entities;
using NamViet.ERP.Modules.Warehouse.Infrastructure.Persistence;

namespace NamViet.ERP.Api.Controllers.Wms;

[ApiController]
[Route("api/warehouses")]
[Authorize]
public class WarehousesController(WarehouseDbContext db, IWmsCatalogQueryService catalog) : ControllerBase
{
    [HttpGet]
    [RequirePermission("wms.warehouses.view:all")]
    public async Task<ActionResult<List<WarehouseDto>>> List(CancellationToken ct)
    {
        var warehouses = await db.WmsWarehouses.Where(w => w.DeletedAt == null).ToListAsync(ct);
        var result = new List<WarehouseDto>();
        foreach (var w in warehouses)
        {
            var (stock, value) = await catalog.GetWarehouseTotalsAsync(w.Id, ct);
            result.Add(WmsMapper.ToWarehouse(w, stock, value));
        }
        return Ok(result);
    }

    [HttpGet("{id}")]
    [RequirePermission("wms.warehouses.view:all")]
    public async Task<ActionResult<WarehouseDto>> GetById(string id, CancellationToken ct)
    {
        var w = await db.WmsWarehouses.FirstOrDefaultAsync(x => x.Id == id && x.DeletedAt == null, ct);
        if (w is null) return NotFound();
        var (stock, value) = await catalog.GetWarehouseTotalsAsync(w.Id, ct);
        return Ok(WmsMapper.ToWarehouse(w, stock, value));
    }

    [HttpPost]
    [RequirePermission("wms.warehouses.create")]
    public async Task<ActionResult<WarehouseDto>> Create([FromBody] CreateWarehouseDto dto, CancellationToken ct)
    {
        var id = $"wh-{Guid.NewGuid():N}"[..10];
        var w = new WmsWarehouse
        {
            Id = id,
            Code = dto.Code,
            Name = dto.Name,
            Group = dto.Group,
            Address = dto.Address,
            Branch = dto.Branch,
            Manager = dto.Manager,
            Status = dto.Status
        };
        db.WmsWarehouses.Add(w);
        await db.SaveChangesAsync(ct);
        return Ok(WmsMapper.ToWarehouse(w, 0, 0));
    }

    [HttpPut("{id}")]
    [RequirePermission("wms.warehouses.update")]
    public async Task<ActionResult<WarehouseDto>> Update(string id, [FromBody] UpdateWarehouseDto dto, CancellationToken ct)
    {
        var w = await db.WmsWarehouses.FirstOrDefaultAsync(x => x.Id == id && x.DeletedAt == null, ct);
        if (w is null) return NotFound();
        if (dto.Code is not null) w.Code = dto.Code;
        if (dto.Name is not null) w.Name = dto.Name;
        if (dto.Group is not null) w.Group = dto.Group;
        if (dto.Address is not null) w.Address = dto.Address;
        if (dto.Branch is not null) w.Branch = dto.Branch;
        if (dto.Manager is not null) w.Manager = dto.Manager;
        if (dto.Status is not null) w.Status = dto.Status;
        w.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        var (stock, value) = await catalog.GetWarehouseTotalsAsync(w.Id, ct);
        return Ok(WmsMapper.ToWarehouse(w, stock, value));
    }

    [HttpDelete("{id}")]
    [RequirePermission("wms.warehouses.delete")]
    public async Task<IActionResult> Delete(string id, CancellationToken ct)
    {
        var w = await db.WmsWarehouses.FirstOrDefaultAsync(x => x.Id == id && x.DeletedAt == null, ct);
        if (w is null) return NotFound();
        w.DeletedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        return NoContent();
    }
}
