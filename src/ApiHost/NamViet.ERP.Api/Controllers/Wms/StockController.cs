using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NamViet.ERP.Api.Application.Dtos.Wms;
using NamViet.ERP.Modules.Warehouse.Domain;
using NamViet.ERP.Modules.Warehouse.Domain.Enums;
using NamViet.ERP.Modules.Warehouse.Domain.Services;
using NamViet.ERP.Modules.Warehouse.Infrastructure.Persistence;

namespace NamViet.ERP.Api.Controllers.Wms;

[ApiController]
[Route("api/stock")]
[Authorize]
public class StockController(WarehouseDbContext db, IInventoryDomainService inventory) : ControllerBase
{
    [HttpGet("matrix")]
    public async Task<ActionResult<List<StockMatrixRowDto>>> Matrix(CancellationToken ct)
    {
        var generalWarehouseId = await inventory.GetGeneralWarehouseIdAsync(ct);

        var projectInventories = await db.ProjectInventories.AsNoTracking().ToListAsync(ct);
        var warehouseInventories = await db.WarehouseInventories
            .AsNoTracking()
            .Where(i => i.WarehouseId == generalWarehouseId)
            .ToListAsync(ct);

        var codesWithStock = projectInventories
            .Where(pi =>
                pi.ActualImportQty - pi.ActualExportQty != 0 ||
                pi.PlannedImportQty - pi.PlannedExportQty != 0 ||
                pi.ReservedQty != 0)
            .Select(pi => pi.MaterialCode)
            .Concat(warehouseInventories.Where(wi => wi.Qty != 0).Select(wi => wi.MaterialCode))
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

        // WAC (Weighted Average Cost) — IAS 2: chỉ phiếu nhập đã duyệt, có đơn giá > 0
        var wacByMaterial = await db.WarehouseTransactionItems
            .AsNoTracking()
            .Where(i =>
                i.Transaction!.TransactionType == TransactionType.Import
                && i.Transaction.ApprovalStatus == ApprovalStatus.Approved
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
            var generalActual = warehouseInventories
                .FirstOrDefault(i => i.MaterialCode == material.MaterialCode)?.Qty ?? 0m;
            var generalReserved = holdingReservations.GetValueOrDefault(material.MaterialCode);
            var generalAvailable = generalActual - generalReserved;

            var projectColumns = projects.Select(p =>
            {
                var pi = projectInventories.FirstOrDefault(x =>
                    x.ProjectCode == p.ProjectCode && x.MaterialCode == material.MaterialCode);
                return new StockMatrixProjectColumnDto(
                    p.ProjectCode,
                    p.ClientProjectId ?? p.ProjectCode,
                    p.ProjectName,
                    pi?.ActualImportQty - pi?.ActualExportQty ?? 0m,
                    pi?.PlannedImportQty - pi?.PlannedExportQty ?? 0m,
                    pi?.ReservedQty ?? 0m);
            }).ToList();

            rows.Add(new StockMatrixRowDto(
                material.MaterialCode,
                material.MaterialName,
                material.Unit,
                material.IsActive,
                generalActual,
                generalReserved,
                generalAvailable,
                wacByMaterial.GetValueOrDefault(material.MaterialCode),
                projectColumns));
        }

        return Ok(rows);
    }
}
