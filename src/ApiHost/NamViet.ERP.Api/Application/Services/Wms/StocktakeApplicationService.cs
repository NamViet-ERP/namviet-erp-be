using Microsoft.EntityFrameworkCore;
using NamViet.ERP.Api.Application.Dtos.Wms;
using NamViet.ERP.Api.Application.Mappers.Wms;
using NamViet.ERP.Modules.Warehouse.Domain.Services;
using NamViet.ERP.Modules.Warehouse.Infrastructure.Persistence;
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
        var stocktake = await db.Stocktakes
            .Include(s => s.Items)
            .FirstOrDefaultAsync(s => s.Id == id && s.DeletedAt == null, ct);

        if (stocktake is null) return null;
        if (stocktake.Status == "approved")
            throw new DomainRuleException("Phiếu kiểm kê đã duyệt.");
        if (stocktake.Status == "cancelled")
            throw new DomainRuleException("Phiếu kiểm kê đã hủy.");

        if (string.IsNullOrWhiteSpace(stocktake.WarehouseId))
            throw new DomainRuleException("Phiếu kiểm kê chưa chọn kho dự án.");

        var projectExists = await db.Projects
            .AnyAsync(p => p.ProjectCode == stocktake.WarehouseId && p.DeletedAt == null, ct);
        if (!projectExists)
            throw new DomainRuleException($"Không tìm thấy kho dự án '{stocktake.WarehouseId}'.");

        await using var tx = await db.Database.BeginTransactionAsync(ct);

        foreach (var item in stocktake.Items.Where(i => i.Diff != 0))
        {
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
        await tx.CommitAsync(ct);

        return WmsMapper.ToStocktake(stocktake);
    }
}
