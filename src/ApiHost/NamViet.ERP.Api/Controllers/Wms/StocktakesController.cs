using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NamViet.ERP.Api.Application.Dtos.Wms;
using NamViet.ERP.Api.Application.Mappers.Wms;
using NamViet.ERP.Api.Application.Services.Wms;
using NamViet.ERP.Api.Infrastructure.Authorization;
using NamViet.ERP.Modules.Warehouse.Domain.Entities;
using NamViet.ERP.Modules.Warehouse.Infrastructure.Persistence;
using NamViet.ERP.SharedKernel.Interfaces;
using NamViet.ERP.SharedKernel.Common;

namespace NamViet.ERP.Api.Controllers.Wms;

[ApiController]
[Route("api/stocktakes")]
[Authorize]
public class StocktakesController(
    WarehouseDbContext db,
    ICurrentUser currentUser,
    IStocktakeApplicationService stocktakes) : ControllerBase
{
    [HttpGet]
    [RequirePermission("wms.stocktakes.view:all")]
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
    [RequirePermission("wms.stocktakes.view:all")]
    public async Task<ActionResult<StocktakeDto>> GetById(string id, CancellationToken ct)
    {
        var s = await FindAsync(id, ct);
        if (s is null) return NotFound();
        return Ok(WmsMapper.ToStocktake(s));
    }

    [HttpPost]
    [RequirePermission("wms.stocktakes.create")]
    public async Task<ActionResult<StocktakeDto>> Create([FromBody] CreateStocktakeDto dto, CancellationToken ct)
    {
        var projectCode = dto.WarehouseId?.Trim() ?? "";
        if (string.IsNullOrWhiteSpace(projectCode))
            return BadRequest("Vui lòng chọn kho dự án kiểm kê.");

        var project = await db.Projects
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.ProjectCode == projectCode && p.DeletedAt == null, ct);
        if (project is null)
            return BadRequest($"Không tìm thấy kho dự án '{projectCode}'.");

        var count = await db.Stocktakes.CountAsync(ct);
        var voucherCode = string.IsNullOrWhiteSpace(dto.VoucherCode)
            ? Stocktake.GenerateVoucherCode(count + 1)
            : dto.VoucherCode.Trim();

        var s = new Stocktake
        {
            Id = $"st-{Guid.NewGuid():N}"[..10],
            VoucherCode = voucherCode,
            VoucherDate = WmsMapper.ParseClientDate(dto.Date),
            CreatedById = currentUser.Id,
            CreatedByName = string.IsNullOrEmpty(dto.CreatedBy) ? currentUser.Name : dto.CreatedBy,
            Status = string.IsNullOrWhiteSpace(dto.Status) ? "pending" : dto.Status,
            WarehouseId = projectCode,
            WarehouseName = string.IsNullOrWhiteSpace(dto.WarehouseName) ? project.ProjectName : dto.WarehouseName,
            Condition = string.IsNullOrWhiteSpace(dto.Condition) ? "counting" : dto.Condition,
            IsFrozen = dto.IsFrozen,
            Note = dto.Note ?? "",
            Items = dto.Items.Select(i => new StocktakeItem
            {
                MaterialCode = string.IsNullOrEmpty(i.Sku) ? i.ProductId : i.Sku,
                Sku = i.Sku,
                Name = i.Name,
                SystemQty = i.SystemQty,
                ActualQty = i.ActualQty,
                Diff = i.ActualQty - i.SystemQty,
            }).ToList(),
        };
        foreach (var item in s.Items) item.StocktakeId = s.Id;
        await EnsureProjectWarehouseAsync(projectCode, project.ProjectName, ct);
        db.Stocktakes.Add(s);
        await db.SaveChangesAsync(ct);
        return Ok(WmsMapper.ToStocktake(s));
    }

    [HttpPut("{id}")]
    [RequirePermission("wms.stocktakes.create")]
    public async Task<ActionResult<StocktakeDto>> Update(string id, [FromBody] StocktakeDto dto, CancellationToken ct)
    {
        var s = await FindAsync(id, ct);
        if (s is null) return NotFound();
        if (s.Status == "approved")
            throw new DomainRuleException("Phiếu kiểm kê đã duyệt, không thể chỉnh sửa.");
        if (s.Status == "cancelled")
            throw new DomainRuleException("Phiếu kiểm kê đã hủy, không thể chỉnh sửa.");

        if (!string.IsNullOrWhiteSpace(dto.WarehouseId))
        {
            var project = await db.Projects
                .AsNoTracking()
                .FirstOrDefaultAsync(p => p.ProjectCode == dto.WarehouseId && p.DeletedAt == null, ct);
            if (project is null)
                return BadRequest($"Không tìm thấy kho dự án '{dto.WarehouseId}'.");
            await EnsureProjectWarehouseAsync(dto.WarehouseId, project.ProjectName, ct);
            s.WarehouseId = dto.WarehouseId;
            s.WarehouseName = string.IsNullOrWhiteSpace(dto.WarehouseName) ? project.ProjectName : dto.WarehouseName;
        }

        if (!string.IsNullOrEmpty(dto.VoucherCode))
            s.VoucherCode = dto.VoucherCode;
        if (!string.IsNullOrEmpty(dto.Date))
            s.VoucherDate = WmsMapper.ParseClientDate(dto.Date);
        if (!string.IsNullOrEmpty(dto.CreatedBy))
            s.CreatedByName = dto.CreatedBy;
        if (!string.IsNullOrEmpty(dto.Status))
            s.Status = dto.Status;
        if (!string.IsNullOrEmpty(dto.Condition))
            s.Condition = dto.Condition;
        s.IsFrozen = dto.IsFrozen;
        s.Note = dto.Note ?? s.Note;
        s.UpdatedAt = DateTime.UtcNow;

        if (dto.Items.Count > 0)
        {
            db.StocktakeItems.RemoveRange(s.Items);
            s.Items = dto.Items.Select(i => new StocktakeItem
            {
                StocktakeId = s.Id,
                MaterialCode = string.IsNullOrEmpty(i.Sku) ? i.ProductId : i.Sku,
                Sku = i.Sku,
                Name = i.Name,
                SystemQty = i.SystemQty,
                ActualQty = i.ActualQty,
                Diff = i.ActualQty - i.SystemQty,
            }).ToList();
        }

        await db.SaveChangesAsync(ct);
        return Ok(WmsMapper.ToStocktake(s));
    }

    [HttpPost("{id}/approve")]
    [RequirePermission("wms.stocktakes.approve")]
    public Task<ActionResult<StocktakeDto>> Approve(string id, CancellationToken ct) =>
        WmsVoucherControllerResponses.ExecuteAsync(() => stocktakes.ApproveAsync(id, ct), ct);

    [HttpDelete("{id}")]
    [RequirePermission("wms.stocktakes.create")]
    public async Task<IActionResult> Delete(string id, CancellationToken ct)
    {
        var s = await FindAsync(id, ct);
        if (s is null) return NotFound();
        if (s.Status == "approved")
            throw new DomainRuleException("Phiếu kiểm kê đã duyệt, không thể xóa.");
        s.DeletedAt = DateTime.UtcNow;
        s.IsFrozen = false;
        await db.SaveChangesAsync(ct);
        return NoContent();
    }

    private async Task<Stocktake?> FindAsync(string id, CancellationToken ct) =>
        await db.Stocktakes.Include(s => s.Items)
            .FirstOrDefaultAsync(s => s.Id == id && s.DeletedAt == null, ct);

    /// <summary>
    /// Stocktake.WarehouseId stores project code (app convention) but EF FK targets warehouses.Id.
    /// Ensure a mirror row exists so SaveChanges succeeds for projects without demo warehouse seed.
    /// </summary>
    private async Task EnsureProjectWarehouseAsync(string projectCode, string projectName, CancellationToken ct)
    {
        var exists = await db.WmsWarehouses.AnyAsync(w => w.Id == projectCode, ct);
        if (exists) return;

        db.WmsWarehouses.Add(new WmsWarehouse
        {
            Id = projectCode,
            Code = projectCode,
            Name = string.IsNullOrWhiteSpace(projectName) ? projectCode : projectName,
            Group = "Kho dự án",
            Address = "",
            Branch = "",
            Manager = "",
            Status = "active",
        });
        await db.SaveChangesAsync(ct);
    }
}
