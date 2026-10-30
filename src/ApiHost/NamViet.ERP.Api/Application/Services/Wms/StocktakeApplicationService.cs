using Microsoft.EntityFrameworkCore;
using NamViet.ERP.Api.Application.Dtos.Wms;
using NamViet.ERP.Api.Application.Mappers.Wms;
using NamViet.ERP.Modules.Warehouse.Domain.Services;
using NamViet.ERP.Modules.Warehouse.Infrastructure.Persistence;
using NamViet.ERP.Modules.Warehouse.Infrastructure.Helpers;
using NamViet.ERP.SharedKernel.Common;

namespace NamViet.ERP.Api.Application.Services.Wms;

public interface IStocktakeApplicationService
{
    Task<StocktakeDto?> ApproveAsync(string id, CancellationToken ct = default);
}

public sealed class StocktakeApplicationService(
    WarehouseDbContext db,
    IInventoryDomainService inventory) : IStocktakeApplicationService
{
    public async Task<StocktakeDto?> ApproveAsync(string id, CancellationToken ct = default)
    {
        await using var tx = db.Database.IsRelational() ? await db.Database.BeginTransactionAsync(ct) : null;
        await TicketCodeAllocator.AcquireNamedLockAsync(db, $"wms_stocktake_{id}", ct);

        var stocktake = await db.Stocktakes
            .Include(s => s.Items)
            .FirstOrDefaultAsync(s => s.Id == id && s.DeletedAt == null, ct);

        if (stocktake is null) return null;
        if (stocktake.Status == "approved")
            throw new DomainRuleException("Phiếu kiểm kê đã duyệt.");
        if (stocktake.Status == "cancelled")
            throw new DomainRuleException("Phiếu kiểm kê đã hủy.");
        if (stocktake.Status != "pending")
            throw new DomainRuleException("Chỉ duyệt được phiếu kiểm kê đang chờ duyệt.");
        if (stocktake.Items.Count == 0 || stocktake.Items.Any(i => i.ActualQty < 0))
            throw new DomainRuleException("Phiếu kiểm kê phải có vật tư và số lượng thực tế không âm.");
        if (stocktake.Items.Select(i => i.MaterialCode).Distinct(StringComparer.OrdinalIgnoreCase).Count() != stocktake.Items.Count)
            throw new DomainRuleException("Mỗi vật tư chỉ được xuất hiện một lần trong phiếu kiểm kê.");

        if (string.IsNullOrWhiteSpace(stocktake.WarehouseId))
            throw new DomainRuleException("Phiếu kiểm kê chưa chọn kho dự án.");

        var projectExists = await db.Projects
            .AnyAsync(p => p.ProjectCode == stocktake.WarehouseId && p.DeletedAt == null, ct);
        if (!projectExists)
            throw new DomainRuleException($"Không tìm thấy kho dự án '{stocktake.WarehouseId}'.");

        // Prevent another approval/movement from changing rows between snapshot validation
        // and adjustment. A stable order prevents stocktakes locking each other in reverse.
        var materialCodes = stocktake.Items.Select(i => i.MaterialCode).Order(StringComparer.Ordinal).ToList();
        if (db.Database.IsNpgsql())
        {
            foreach (var materialCode in materialCodes)
                await db.Database.ExecuteSqlInterpolatedAsync(
                    $"SELECT 1 FROM warehouse.project_inventory WHERE \"ProjectCode\" = {stocktake.WarehouseId} AND \"MaterialCode\" = {materialCode} FOR UPDATE", ct);
        }

        var quantities = await db.ProjectInventories.AsNoTracking()
            .Where(i => i.ProjectCode == stocktake.WarehouseId && materialCodes.Contains(i.MaterialCode))
            .ToDictionaryAsync(i => i.MaterialCode, i => i.ActualImportQty - i.ActualExportQty, ct);
        foreach (var item in stocktake.Items)
        {
            if (quantities.GetValueOrDefault(item.MaterialCode) != item.SystemQty)
                throw new DomainRuleException($"Tồn kho vật tư '{item.MaterialCode}' đã thay đổi sau khi lập phiếu. Vui lòng hủy phiếu này, kiểm đếm lại và tạo phiếu kiểm kê mới trước khi duyệt.");
        }

        foreach (var item in stocktake.Items)
        {
            item.Diff = item.ActualQty - item.SystemQty;
            await inventory.ApplyStocktakeAdjustmentAsync(
                stocktake.WarehouseId,
                item.MaterialCode,
                item.Diff,
                ct);
        }

        stocktake.Status = "approved";
        stocktake.Condition = "completed";
        stocktake.IsFrozen = false;
        stocktake.UpdatedAt = DateTime.UtcNow;

        await db.SaveChangesAsync(ct);
        if (tx is not null) await tx.CommitAsync(ct);

        return WmsMapper.ToStocktake(stocktake);
    }
}
