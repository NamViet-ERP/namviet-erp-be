using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NamViet.ERP.Api.Application.Dtos.Wms;
using NamViet.ERP.Api.Application.Mappers.Wms;
using NamViet.ERP.Api.Application.Services.Wms;
using NamViet.ERP.Api.Infrastructure.Authorization;
using NamViet.ERP.Modules.Warehouse.Domain.Entities;
using NamViet.ERP.Modules.Warehouse.Infrastructure.Persistence;
using NamViet.ERP.SharedKernel.Authorization;
using NamViet.ERP.SharedKernel.Interfaces;
using NamViet.ERP.SharedKernel.Common;

namespace NamViet.ERP.Api.Controllers.Wms;

[ApiController]
[Route("api/stocktakes")]
[Authorize]
public class StocktakesController(
    WarehouseDbContext db,
    ICurrentUser currentUser,
    ProjectWarehouseAccessService projectAccess,
    IStocktakeApplicationService stocktakes) : ControllerBase
{
    /// <summary>Duyệt / hủy / đóng-mở băng là hành động vòng đời - cần quyền duyệt (Admin bypass như PermissionAuthorizationHandler).</summary>
    private bool CanApproveStocktakes =>
        currentUser.IsInRole(RolePermissionTemplates.AdminRole)
        || currentUser.HasPermission("wms.stocktakes.approve");

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

        var requestedStatus = string.IsNullOrWhiteSpace(dto.Status) ? "pending" : dto.Status;
        if (requestedStatus is "approved" or "cancelled")
            return BadRequest("Phiếu kiểm kê mới không được ở trạng thái đã duyệt hoặc đã hủy. Vui lòng dùng chức năng duyệt phiếu.");

        try
        {
            await projectAccess.EnsureCanOperateOnProjectAsync(projectCode, ct);
        }
        catch (UnauthorizedAccessException ex)
        {
            return StatusCode(StatusCodes.Status403Forbidden, ex.Message);
        }

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
            Status = requestedStatus,
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
                Reason = i.Reason,
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

        // Chặn bypass flow duyệt: update chỉ cho phép sửa nội dung; duyệt phải qua endpoint /approve,
        // hủy và đóng/mở băng yêu cầu quyền duyệt kiểm kê.
        if (!string.IsNullOrEmpty(dto.Status) && dto.Status != s.Status)
        {
            if (dto.Status == "approved")
                return BadRequest("Không thể chuyển phiếu kiểm kê sang trạng thái đã duyệt qua cập nhật. Vui lòng dùng chức năng duyệt phiếu.");
            if (dto.Status == "cancelled")
            {
                if (!CanApproveStocktakes)
                    return StatusCode(StatusCodes.Status403Forbidden, "Bạn không có quyền hủy phiếu kiểm kê.");
            }
            else if (dto.Status != "pending")
            {
                return BadRequest($"Trạng thái phiếu kiểm kê '{dto.Status}' không hợp lệ.");
            }
        }

        if (dto.IsFrozen != s.IsFrozen && !CanApproveStocktakes)
            return StatusCode(StatusCodes.Status403Forbidden, "Bạn không có quyền thay đổi trạng thái đóng băng của phiếu kiểm kê.");

        try
        {
            await projectAccess.EnsureCanOperateOnProjectAsync(s.WarehouseId, ct);
            if (!string.IsNullOrWhiteSpace(dto.WarehouseId) && dto.WarehouseId != s.WarehouseId)
                await projectAccess.EnsureCanOperateOnProjectAsync(dto.WarehouseId, ct);
        }
        catch (UnauthorizedAccessException ex)
        {
            return StatusCode(StatusCodes.Status403Forbidden, ex.Message);
        }

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
                Reason = i.Reason,
            }).ToList();
        }

        await db.SaveChangesAsync(ct);
        return Ok(WmsMapper.ToStocktake(s));
    }

    [HttpPost("{id}/approve")]
    [RequirePermission("wms.stocktakes.approve")]
    public async Task<ActionResult<StocktakeDto>> Approve(string id, CancellationToken ct)
    {
        var s = await FindAsync(id, ct);
        if (s is null) return NotFound();
        try
        {
            await projectAccess.EnsureCanOperateOnProjectAsync(s.WarehouseId, ct);
        }
        catch (UnauthorizedAccessException ex)
        {
            return StatusCode(StatusCodes.Status403Forbidden, ex.Message);
        }
        return await WmsVoucherControllerResponses.ExecuteAsync(() => stocktakes.ApproveAsync(id, ct), ct);
    }

    [HttpDelete("{id}")]
    [RequirePermission("wms.stocktakes.create")]
    public async Task<IActionResult> Delete(string id, CancellationToken ct)
    {
        var s = await FindAsync(id, ct);
        if (s is null) return NotFound();
        if (s.Status == "approved")
            throw new DomainRuleException("Phiếu kiểm kê đã duyệt, không thể xóa.");
        try
        {
            await projectAccess.EnsureCanOperateOnProjectAsync(s.WarehouseId, ct);
        }
        catch (UnauthorizedAccessException ex)
        {
            return StatusCode(StatusCodes.Status403Forbidden, ex.Message);
        }
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
