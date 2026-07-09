using Microsoft.EntityFrameworkCore;
using NamViet.ERP.Modules.Warehouse.Infrastructure.Persistence;

namespace NamViet.ERP.Api.Application.Services.Wms;

public interface IStocktakeFreezeGuard
{
    Task EnsureProjectsNotFrozenAsync(IEnumerable<string?> projectCodes, CancellationToken ct = default);
}

public sealed class StocktakeFreezeGuard(WarehouseDbContext db) : IStocktakeFreezeGuard
{
    public async Task EnsureProjectsNotFrozenAsync(IEnumerable<string?> projectCodes, CancellationToken ct = default)
    {
        var codes = projectCodes
            .Where(code => !string.IsNullOrWhiteSpace(code))
            .Select(code => code!.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        if (codes.Count == 0)
            return;

        var frozen = await db.Stocktakes
            .AsNoTracking()
            .Where(s => s.IsFrozen
                        && s.Status == "pending"
                        && codes.Contains(s.WarehouseId))
            .Select(s => new { s.VoucherCode, s.WarehouseId, s.WarehouseName })
            .FirstOrDefaultAsync(ct);

        if (frozen is null)
            return;

        var warehouseLabel = string.IsNullOrWhiteSpace(frozen.WarehouseName)
            ? frozen.WarehouseId
            : frozen.WarehouseName;

        throw new InvalidOperationException(
            $"Kho dự án {warehouseLabel} đang đóng băng kiểm kê (phiếu {frozen.VoucherCode}). " +
            "Hãy mở băng hoặc duyệt/hủy phiếu kiểm kê trước khi nhập, xuất, chuyển hoặc hoàn trả vật tư.");
    }
}
