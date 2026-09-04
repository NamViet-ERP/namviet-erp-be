using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NamViet.ERP.Api.Application.Dtos.Wms;
using NamViet.ERP.Api.Infrastructure.Authorization;
using NamViet.ERP.Modules.Warehouse.Domain;
using NamViet.ERP.Modules.Warehouse.Domain.Entities;
using NamViet.ERP.Modules.Warehouse.Domain.Enums;
using NamViet.ERP.Modules.Warehouse.Domain.Services;
using NamViet.ERP.Modules.Warehouse.Infrastructure.Persistence;
using NamViet.ERP.SharedKernel.Authorization;

namespace NamViet.ERP.Api.Controllers.Wms;

[ApiController]
[Route("api/stock")]
[Authorize]
public class StockController(WarehouseDbContext db, IInventoryDomainService inventory) : ControllerBase
{
    [HttpGet("matrix")]
    [RequirePermission("wms.stock.view:all")]
    public async Task<ActionResult<List<StockMatrixRowDto>>> Matrix(CancellationToken ct)
    {
        var canViewPrices = PriceVisibility.CanViewPrices(User);
        var generalWarehouseId = await inventory.GetGeneralWarehouseIdAsync(ct);

        var projectInventories = await db.ProjectInventories.AsNoTracking().ToListAsync(ct);
        var warehouseInventories = await db.WarehouseInventories
            .AsNoTracking()
            .Where(i => i.WarehouseId == generalWarehouseId)
            .ToListAsync(ct);

        // HL trên tồn kho: mọi phiếu chưa từ chối (draft/pending/approved).
        // Join tường minh theo TicketCode — tránh LINQ navigation filter làm trống kết quả.
        var defectedRows = await (
            from i in db.PurchaseDefectItems.AsNoTracking()
            join d in db.PurchaseDefects.AsNoTracking() on i.TicketCode equals d.TicketCode
            where d.Status != PurchaseDefectStatuses.Rejected
                  && i.MaterialCode != null
                  && i.MaterialCode != ""
            group i by i.MaterialCode into g
            select new { MaterialCode = g.Key!, Qty = g.Sum(x => x.Quantity) }
        ).ToListAsync(ct);
        var defectedByMaterial = defectedRows.ToDictionary(
            x => x.MaterialCode,
            x => x.Qty,
            StringComparer.OrdinalIgnoreCase);

        var codesWithStock = projectInventories
            .Where(pi =>
                pi.ActualImportQty - pi.ActualExportQty != 0 ||
                pi.PlannedImportQty != 0 ||
                pi.PlannedExportQty != 0 ||
                pi.ReservedQty != 0)
            .Select(pi => pi.MaterialCode)
            .Concat(warehouseInventories.Where(wi => wi.Qty != 0).Select(wi => wi.MaterialCode))
            .Concat(defectedByMaterial.Keys)
            .ToHashSet(StringComparer.Ordinal);

        var materials = await db.Materials
            .AsNoTracking()
            .Where(m => m.DeletedAt == null && (m.IsActive || codesWithStock.Contains(m.MaterialCode)))
            .OrderBy(m => m.MaterialCode)
            .ToListAsync(ct);

        var projects = await db.Projects
            .AsNoTracking()
            .Where(p => p.DeletedAt == null && p.ProjectType == ProjectType.Project)
            .OrderBy(p => p.ProjectName)
            .ToListAsync(ct);

        var holdingReservations = await db.InventoryReservations
            .AsNoTracking()
            .Where(r => r.WarehouseId == generalWarehouseId && r.Status == ReservationStatus.Holding)
            .GroupBy(r => r.MaterialCode)
            .Select(g => new { MaterialCode = g.Key, Qty = g.Sum(x => x.ReservedQty) })
            .ToDictionaryAsync(x => x.MaterialCode, x => x.Qty, ct);

        var pendingHlHolds = await inventory.ListPendingStockDefectHoldsAsync(ct);
        var pendingHlLookup = new Dictionary<(string Project, string Material), decimal>();
        foreach (var h in pendingHlHolds)
        {
            var key = MatrixKey(h.ProjectCode, h.MaterialCode);
            pendingHlLookup[key] = pendingHlLookup.GetValueOrDefault(key) + h.Quantity;
        }

        decimal PendingHlFor(string projectCode, string materialCode) =>
            pendingHlLookup.GetValueOrDefault(MatrixKey(projectCode, materialCode));

        var projectInventoryLookup = projectInventories.ToDictionary(
            pi => MatrixKey(pi.ProjectCode, pi.MaterialCode));

        var warehouseInventoryLookup = warehouseInventories.ToDictionary(
            wi => wi.MaterialCode,
            wi => wi.Qty,
            StringComparer.OrdinalIgnoreCase);

        // WAC (Weighted Average Cost) — IAS 2: phiếu nhập đã duyệt trong 12 tháng theo ngày duyệt
        var wacFrom = DateTime.UtcNow.AddYears(-1);
        var wacByMaterial = await db.WarehouseTransactionItems
            .AsNoTracking()
            .Where(i =>
                i.Transaction!.TransactionType == TransactionType.Import
                && i.Transaction.ApprovalStatus == ApprovalStatus.Approved
                && i.Transaction.ApprovedAt != null
                && i.Transaction.ApprovedAt >= wacFrom
                && i.UnitPrice > 0
                && i.Quantity > 0)
            .GroupBy(i => i.MaterialCode)
            .Select(g => new
            {
                MaterialCode = g.Key,
                TotalQty = g.Sum(x => x.Quantity),
                TotalCost = g.Sum(x => x.Quantity * x.UnitPrice),
            })
            .ToDictionaryAsync(
                x => x.MaterialCode,
                x => x.TotalQty > 0 ? x.TotalCost / x.TotalQty : 0m,
                ct);

        var rows = new List<StockMatrixRowDto>();
        foreach (var material in materials)
        {
            var generalActual = warehouseInventoryLookup.GetValueOrDefault(material.MaterialCode);
            var generalReserved = holdingReservations.GetValueOrDefault(material.MaterialCode);
            var generalAvailable = generalActual - generalReserved;

            var projectColumns = projects.Select(p =>
            {
                projectInventoryLookup.TryGetValue(MatrixKey(p.ProjectCode, material.MaterialCode), out var pi);
                return new StockMatrixProjectColumnDto(
                    p.ProjectCode,
                    p.ClientProjectId ?? p.ProjectCode,
                    p.ProjectName,
                    pi?.ActualImportQty - pi?.ActualExportQty ?? 0m,
                    pi?.PlannedImportQty ?? 0m,
                    pi?.PlannedExportQty ?? 0m,
                    PendingHlFor(p.ProjectCode, material.MaterialCode));
            }).ToList();

            rows.Add(new StockMatrixRowDto(
                material.MaterialCode,
                material.MaterialName,
                material.Unit,
                material.IsActive,
                generalActual,
                generalReserved,
                generalAvailable,
                PriceVisibility.Mask(canViewPrices, wacByMaterial.GetValueOrDefault(material.MaterialCode)),
                defectedByMaterial.GetValueOrDefault(material.MaterialCode),
                projectColumns));
        }

        return Ok(rows);
    }

    private static (string Project, string Material) MatrixKey(string projectCode, string materialCode) =>
        (projectCode.ToUpperInvariant(), materialCode.ToUpperInvariant());
}
