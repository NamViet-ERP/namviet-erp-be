using Microsoft.EntityFrameworkCore;
using NamViet.ERP.Modules.Warehouse.Domain.Enums;
using NamViet.ERP.Modules.Warehouse.Infrastructure.Persistence;

namespace NamViet.ERP.Modules.Warehouse.Infrastructure.Helpers;

public static class MaterialUsageHelper
{
    public static async Task<bool> IsInUseAsync(
        WarehouseDbContext db, string materialCode, CancellationToken ct = default)
    {
        if (await db.ProjectInventories.AnyAsync(pi =>
                pi.MaterialCode == materialCode &&
                (pi.ActualImportQty != 0 || pi.ActualExportQty != 0 ||
                 pi.PlannedImportQty != 0 || pi.PlannedExportQty != 0 || pi.ReservedQty != 0),
            ct))
            return true;

        if (await db.WarehouseInventories.AnyAsync(
                wi => wi.MaterialCode == materialCode && wi.Qty != 0, ct))
            return true;

        if (await db.InventoryReservations.AnyAsync(
                r => r.MaterialCode == materialCode && r.Status == ReservationStatus.Holding, ct))
            return true;

        if (await db.WarehouseTransactionItems.AnyAsync(i => i.MaterialCode == materialCode, ct))
            return true;

        return false;
    }
}
