using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NamViet.ERP.Api.Application.Dtos.Wms;
using NamViet.ERP.Api.Application.Mappers.Wms;
using NamViet.ERP.Api.Infrastructure.Authorization;
using NamViet.ERP.Modules.Warehouse.Domain.Entities;
using NamViet.ERP.Modules.Warehouse.Infrastructure.Persistence;
using NamViet.ERP.SharedKernel.Interfaces;

namespace NamViet.ERP.Api.Controllers.Wms;

[ApiController]
[Route("api/conversions")]
[Authorize]
public class ConversionsController(WarehouseDbContext db, ICurrentUser currentUser) : ControllerBase
{
    [HttpGet]
    [RequirePermission("wms.conversions.view:all")]
    public async Task<ActionResult<List<ConversionDto>>> List(CancellationToken ct)
    {
        var items = await db.Conversions.Include(c => c.Items).Where(c => c.DeletedAt == null).ToListAsync(ct);
        return Ok(items.Select(WmsMapper.ToConversion).ToList());
    }

    [HttpPost]
    [RequirePermission("wms.conversions.create")]
    public async Task<ActionResult<ConversionDto>> Create([FromBody] ConversionDto dto, CancellationToken ct)
    {
        var c = new Conversion
        {
            Id = $"cv-{Guid.NewGuid():N}"[..10],
            VoucherCode = dto.VoucherCode,
            VoucherDate = WmsMapper.ParseClientDate(dto.Date),
            FromWarehouseId = dto.FromWarehouseId,
            FromWarehouseName = dto.FromWarehouseName,
            FromType = dto.FromType,
            ToWarehouseId = dto.ToWarehouseId,
            ToWarehouseName = dto.ToWarehouseName,
            ToType = dto.ToType,
            Status = dto.Status,
            CreatedById = currentUser.Id,
            CreatedByName = string.IsNullOrEmpty(dto.CreatedBy) ? currentUser.Name : dto.CreatedBy,
            ApprovedByName = dto.ApprovedBy,
            Note = dto.Note,
            Items = dto.Items.Select(i => new ConversionItem
            {
                MaterialCode = string.IsNullOrEmpty(i.Sku) ? i.ProductId : i.Sku,
                Sku = i.Sku, Name = i.Name,
                Qty = i.Qty, Unit = i.Unit, Reason = i.Reason
            }).ToList()
        };
        foreach (var item in c.Items) item.ConversionId = c.Id;
        db.Conversions.Add(c);
        await db.SaveChangesAsync(ct);
        return Ok(WmsMapper.ToConversion(c));
    }
}
