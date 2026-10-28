using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NamViet.ERP.Api.Application.Dtos.Wms;
using NamViet.ERP.Api.Application.Mappers.Wms;
using NamViet.ERP.Api.Application.Services.Wms;
using NamViet.ERP.Api.Infrastructure.Authorization;
using NamViet.ERP.Modules.Warehouse.Domain.Entities;
using NamViet.ERP.Modules.Warehouse.Infrastructure.Persistence;
using NamViet.ERP.Modules.Warehouse.Infrastructure.Helpers;
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

    private static bool _schemaEnsured;
    private static readonly SemaphoreSlim _schemaLock = new(1, 1);

    [HttpGet]
    [RequirePermission("wms.stocktakes.view:all")]
    public async Task<ActionResult<List<StocktakeDto>>> List(CancellationToken ct)
    {
        await EnsureStocktakeSchemaAsync(ct);
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
        await EnsureStocktakeSchemaAsync(ct);
        var s = await FindAsync(id, ct);
        if (s is null) return NotFound();
        return Ok(WmsMapper.ToStocktake(s));
    }

    [HttpPost]
    [RequirePermission("wms.stocktakes.create")]
    public async Task<ActionResult<StocktakeDto>> Create([FromBody] CreateStocktakeDto dto, CancellationToken ct)
    {
        await EnsureStocktakeSchemaAsync(ct);

        var projectCode = dto.WarehouseId?.Trim() ?? "";
        if (string.IsNullOrWhiteSpace(projectCode))
            return BadRequest("Vui lòng chọn kho dự án kiểm kê.");

        var requestedStatus = string.IsNullOrWhiteSpace(dto.Status) ? "pending" : dto.Status;
        if (requestedStatus != "pending")
            return BadRequest("Phiếu kiểm kê mới phải ở trạng thái chờ duyệt. Vui lòng dùng chức năng duyệt phiếu.");

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

        if (dto.Items == null || dto.Items.Count == 0)
            return BadRequest("Phiếu kiểm kê phải có ít nhất 1 vật tư.");

        var items = dto.Items.Select(i =>
        {
            var matCode = string.IsNullOrWhiteSpace(i.Sku) ? (i.ProductId ?? "").Trim() : i.Sku.Trim();
            var sku = string.IsNullOrWhiteSpace(i.Sku) ? matCode : i.Sku.Trim();
            var name = string.IsNullOrWhiteSpace(i.Name) ? matCode : i.Name.Trim();
            var reason = string.IsNullOrWhiteSpace(i.Reason) ? null : i.Reason.Trim();
            if (reason != null && reason.Length > 500) reason = reason[..500];

            return new StocktakeItem
            {
                MaterialCode = matCode,
                Sku = sku,
                Name = name,
                ActualQty = i.ActualQty,
                Reason = reason,
            };
        }).ToList();

        if (items.Any(i => string.IsNullOrWhiteSpace(i.MaterialCode)))
            return BadRequest("Mã vật tư trong phiếu kiểm kê không được để trống.");
        if (items.Any(i => i.ActualQty < 0))
            return BadRequest("Số lượng kiểm kê thực tế không được âm.");
        if (items.Select(i => i.MaterialCode).Distinct(StringComparer.OrdinalIgnoreCase).Count() != items.Count)
            return BadRequest("Mỗi vật tư chỉ được xuất hiện một lần trong phiếu kiểm kê.");

        var warehouseId = await EnsureProjectWarehouseAsync(projectCode, project.ProjectName, ct);
        await EnsureMaterialsExistAsync(items, ct);
        await CaptureInventorySnapshotAsync(projectCode, items, ct);

        var s = await SequentialCodeHelper.ExecuteWithUniqueRetryAsync(async () =>
        {
            string voucherCode;
            if (!string.IsNullOrWhiteSpace(dto.VoucherCode))
            {
                voucherCode = dto.VoucherCode.Trim();
            }
            else
            {
                var existingCodes = await db.Stocktakes
                    .IgnoreQueryFilters()
                    .Select(st => st.VoucherCode)
                    .ToListAsync(ct);
                var nextSeq = SequentialCodeHelper.NextSequence(existingCodes, "KK-");
                voucherCode = Stocktake.GenerateVoucherCode(nextSeq);
            }

            var stocktake = new Stocktake
            {
                Id = $"st-{Guid.NewGuid():N}"[..10],
                VoucherCode = voucherCode,
                VoucherDate = WmsMapper.ParseClientDate(dto.Date),
                CreatedById = currentUser.Id,
                CreatedByName = string.IsNullOrEmpty(dto.CreatedBy) ? currentUser.Name : dto.CreatedBy,
                Status = requestedStatus,
                WarehouseId = warehouseId,
                WarehouseName = string.IsNullOrWhiteSpace(dto.WarehouseName) ? project.ProjectName : dto.WarehouseName,
                Condition = string.IsNullOrWhiteSpace(dto.Condition) ? "counting" : dto.Condition,
                IsFrozen = dto.IsFrozen,
                Note = dto.Note ?? "",
                Items = items.Select(i => new StocktakeItem
                {
                    MaterialCode = i.MaterialCode,
                    Sku = i.Sku,
                    Name = i.Name,
                    SystemQty = i.SystemQty,
                    ActualQty = i.ActualQty,
                    Diff = i.Diff,
                    Reason = i.Reason,
                }).ToList(),
            };
            foreach (var item in stocktake.Items) item.StocktakeId = stocktake.Id;

            db.Stocktakes.Add(stocktake);
            await db.SaveChangesAsync(ct);
            return stocktake;
        }, maxAttempts: 5, ct: ct);

        return Ok(WmsMapper.ToStocktake(s));
    }

    [HttpPut("{id}")]
    [RequirePermission("wms.stocktakes.create")]
    public async Task<ActionResult<StocktakeDto>> Update(string id, [FromBody] StocktakeDto dto, CancellationToken ct)
    {
        await EnsureStocktakeSchemaAsync(ct);
        await using var tx = db.Database.IsRelational() ? await db.Database.BeginTransactionAsync(ct) : null;
        await TicketCodeAllocator.AcquireNamedLockAsync(db, $"wms_stocktake_{id}", ct);
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
            var whId = await EnsureProjectWarehouseAsync(dto.WarehouseId, project.ProjectName, ct);
            s.WarehouseId = whId;
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
            var items = dto.Items.Select(i =>
            {
                var matCode = string.IsNullOrWhiteSpace(i.Sku) ? (i.ProductId ?? "").Trim() : i.Sku.Trim();
                var sku = string.IsNullOrWhiteSpace(i.Sku) ? matCode : i.Sku.Trim();
                var name = string.IsNullOrWhiteSpace(i.Name) ? matCode : i.Name.Trim();
                var reason = string.IsNullOrWhiteSpace(i.Reason) ? null : i.Reason.Trim();
                if (reason != null && reason.Length > 500) reason = reason[..500];

                return new StocktakeItem
                {
                    StocktakeId = s.Id,
                    MaterialCode = matCode,
                    Sku = sku,
                    Name = name,
                    ActualQty = i.ActualQty,
                    Reason = reason,
                };
            }).ToList();

            if (items.Any(i => string.IsNullOrWhiteSpace(i.MaterialCode)))
                return BadRequest("Mã vật tư trong phiếu kiểm kê không được để trống.");
            if (items.Any(i => i.ActualQty < 0))
                return BadRequest("Số lượng kiểm kê thực tế không được âm.");
            if (items.Select(i => i.MaterialCode).Distinct(StringComparer.OrdinalIgnoreCase).Count() != items.Count)
                return BadRequest("Mỗi vật tư chỉ được xuất hiện một lần trong phiếu kiểm kê.");

            await EnsureMaterialsExistAsync(items, ct);
            await CaptureInventorySnapshotAsync(s.WarehouseId, items, ct);

            db.StocktakeItems.RemoveRange(s.Items);
            s.Items = items;
        }

        await db.SaveChangesAsync(ct);
        if (tx is not null) await tx.CommitAsync(ct);
        return Ok(WmsMapper.ToStocktake(s));
    }

    [HttpPost("{id}/approve")]
    [RequirePermission("wms.stocktakes.approve")]
    public async Task<ActionResult<StocktakeDto>> Approve(string id, CancellationToken ct)
    {
        var s = await db.Stocktakes.AsNoTracking()
            .FirstOrDefaultAsync(s => s.Id == id && s.DeletedAt == null, ct);
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
        await using var tx = db.Database.IsRelational() ? await db.Database.BeginTransactionAsync(ct) : null;
        await TicketCodeAllocator.AcquireNamedLockAsync(db, $"wms_stocktake_{id}", ct);
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
        if (tx is not null) await tx.CommitAsync(ct);
        return NoContent();
    }

    private async Task CaptureInventorySnapshotAsync(string projectCode, List<StocktakeItem> items, CancellationToken ct)
    {
        var codes = items.Select(i => i.MaterialCode).ToList();
        var quantities = await db.ProjectInventories.AsNoTracking()
            .Where(i => i.ProjectCode == projectCode && codes.Contains(i.MaterialCode))
            .ToDictionaryAsync(i => i.MaterialCode, i => i.ActualImportQty - i.ActualExportQty, ct);
        foreach (var item in items)
        {
            item.SystemQty = quantities.GetValueOrDefault(item.MaterialCode);
            item.Diff = item.ActualQty - item.SystemQty;
        }
    }

    private async Task<Stocktake?> FindAsync(string id, CancellationToken ct) =>
        await db.Stocktakes.Include(s => s.Items)
            .FirstOrDefaultAsync(s => s.Id == id && s.DeletedAt == null, ct);

    private async Task EnsureStocktakeSchemaAsync(CancellationToken ct)
    {
        if (_schemaEnsured || !db.Database.IsRelational()) return;
        await _schemaLock.WaitAsync(ct);
        try
        {
            if (_schemaEnsured) return;
            await db.Database.ExecuteSqlRawAsync(
                "ALTER TABLE warehouse.stocktake_items ADD COLUMN IF NOT EXISTS \"Reason\" character varying(500);",
                ct);
            _schemaEnsured = true;
        }
        catch
        {
            // Bỏ qua nếu user DB không có quyền DDL hoặc cột đã tồn tại
        }
        finally
        {
            _schemaLock.Release();
        }
    }

    private async Task EnsureMaterialsExistAsync(List<StocktakeItem> items, CancellationToken ct)
    {
        var itemMaterialCodes = items.Select(i => i.MaterialCode).Distinct().ToList();
        var existingMaterialCodes = await db.Materials.IgnoreQueryFilters()
            .Where(m => itemMaterialCodes.Contains(m.MaterialCode))
            .Select(m => m.MaterialCode)
            .ToListAsync(ct);
        var missingCodes = itemMaterialCodes.Except(existingMaterialCodes, StringComparer.OrdinalIgnoreCase).ToList();
        if (missingCodes.Count > 0)
        {
            foreach (var code in missingCodes)
            {
                var refItem = items.FirstOrDefault(i => string.Equals(i.MaterialCode, code, StringComparison.OrdinalIgnoreCase));
                db.Materials.Add(new Material
                {
                    MaterialCode = code,
                    MaterialName = refItem?.Name ?? code,
                    Unit = "cái",
                    IsActive = true,
                });
            }
            await db.SaveChangesAsync(ct);
        }
    }

    /// <summary>
    /// Stocktake.WarehouseId stores project code (app convention) but EF FK targets warehouses.Id.
    /// Ensure a mirror row exists so SaveChanges succeeds for projects without demo warehouse seed.
    /// </summary>
    private async Task<string> EnsureProjectWarehouseAsync(string projectCode, string projectName, CancellationToken ct)
    {
        var existing = await db.WmsWarehouses
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(w => w.Id == projectCode || w.Code == projectCode, ct);

        if (existing != null)
        {
            if (existing.DeletedAt != null)
            {
                existing.DeletedAt = null;
                existing.Status = "active";
                if (!string.IsNullOrWhiteSpace(projectName))
                    existing.Name = projectName;
                await db.SaveChangesAsync(ct);
            }
            return existing.Id;
        }

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
        return projectCode;
    }
}
