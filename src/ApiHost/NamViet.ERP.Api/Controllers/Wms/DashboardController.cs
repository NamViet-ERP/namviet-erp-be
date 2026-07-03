using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NamViet.ERP.Api.Application.Dtos.Wms;
using NamViet.ERP.Api.Application.Mappers.Wms;
using NamViet.ERP.Api.Application.Services.Wms;
using NamViet.ERP.Api.Infrastructure.Authorization;
using NamViet.ERP.Modules.Warehouse.Domain.Enums;
using NamViet.ERP.Modules.Warehouse.Infrastructure.Persistence;

namespace NamViet.ERP.Api.Controllers.Wms;

[ApiController]
[Route("api/dashboard")]
[Authorize]
[RequirePermission("wms.dashboard.view")]
public class DashboardController(WarehouseDbContext db, IWmsCatalogQueryService catalog) : ControllerBase
{
    [HttpGet("kpi")]
    public async Task<ActionResult<DashboardKpiDto>> Kpi(CancellationToken ct)
    {
        var warehouses = await db.WmsWarehouses.Where(w => w.DeletedAt == null && w.Status == "active").CountAsync(ct);
        var inv = await db.WarehouseInventories.SumAsync(i => i.Qty, ct);
        var totalValue = await catalog.GetTotalInventoryValueAsync(ct);
        var today = DateTime.UtcNow.Date;
        var todayTx = await db.WarehouseTransactions.Where(t => t.CreatedAt >= today).ToListAsync(ct);
        return Ok(new DashboardKpiDto((int)inv, totalValue, warehouses,
            todayTx.Count(t => t.TransactionType == TransactionType.Import),
            todayTx.Count(t => t.TransactionType == TransactionType.Export),
            await db.WarehouseTransactions.CountAsync(t => t.ApprovalStatus == ApprovalStatus.Pending, ct)));
    }

    [HttpGet("trend")]
    public async Task<ActionResult<List<DashboardTrendDto>>> Trend([FromQuery] int months = 6, CancellationToken ct = default)
    {
        var monthCount = Math.Clamp(months, 1, 24);
        var now = DateTime.UtcNow;
        var result = new List<DashboardTrendDto>();

        for (var i = monthCount - 1; i >= 0; i--)
        {
            var start = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc).AddMonths(-i);
            var end = start.AddMonths(1);

            var importValue = await db.WarehouseTransactionItems
                .Where(item => item.Transaction.CreatedAt >= start
                    && item.Transaction.CreatedAt < end
                    && item.Transaction.TransactionType == TransactionType.Import)
                .SumAsync(item => (decimal?)(item.Quantity * item.UnitPrice), ct) ?? 0m;

            var exportValue = await db.WarehouseTransactionItems
                .Where(item => item.Transaction.CreatedAt >= start
                    && item.Transaction.CreatedAt < end
                    && item.Transaction.TransactionType == TransactionType.Export)
                .SumAsync(item => (decimal?)(item.Quantity * item.UnitPrice), ct) ?? 0m;

            result.Add(new DashboardTrendDto(start.ToString("MM/yyyy"), importValue, exportValue));
        }

        return Ok(result);
    }

    [HttpGet("stock-by-category")]
    public async Task<ActionResult<List<DashboardCategoryDto>>> StockByCategory(CancellationToken ct)
    {
        var rows = await db.Materials
            .Include(m => m.Category)
            .Where(m => m.DeletedAt == null)
            .GroupBy(m => m.Category.Name)
            .Select(g => new DashboardCategoryDto(g.Key, g.Count()))
            .ToListAsync(ct);
        return Ok(rows);
    }

    [HttpGet("stock-by-project")]
    public async Task<ActionResult<List<DashboardCategoryDto>>> StockByProject(CancellationToken ct)
    {
        var rows = await (
            from pi in db.ProjectInventories
            join p in db.Projects on pi.ProjectCode equals p.ProjectCode
            where p.DeletedAt == null
            group pi by new { p.ProjectCode, p.ProjectName } into g
            let qty = g.Sum(x => x.ActualImportQty - x.ActualExportQty)
            where qty > 0
            orderby qty descending
            select new DashboardCategoryDto(g.Key.ProjectName, qty)
        ).Take(6).ToListAsync(ct);

        return Ok(rows);
    }

    [HttpGet("activity-feed")]
    public async Task<ActionResult<List<DashboardActivityDto>>> Activity([FromQuery] int limit = 10, CancellationToken ct = default)
    {
        var txs = await db.WarehouseTransactions.OrderByDescending(t => t.CreatedAt).Take(limit).ToListAsync(ct);
        return Ok(txs.Select(t => new DashboardActivityDto(
            t.TransactionType.ToString(), t.TicketCode,
            $"{t.TransactionType} - {t.CreatedByName}", t.CreatedAt.ToString("HH:mm"))).ToList());
    }

    [HttpGet("top-products")]
    public async Task<ActionResult<List<TopProductDto>>> TopProducts(CancellationToken ct)
    {
        var rows = await db.WarehouseTransactionItems
            .Join(db.Materials.Where(m => m.DeletedAt == null),
                ti => ti.MaterialCode,
                m => m.MaterialCode,
                (ti, m) => new { ti, m })
            .GroupBy(x => new { x.ti.MaterialCode, x.m.MaterialName })
            .OrderByDescending(g => g.Sum(x => x.ti.Quantity))
            .Take(5)
            .Select(g => new
            {
                g.Key.MaterialCode,
                g.Key.MaterialName,
                Qty = g.Sum(x => x.ti.Quantity),
                Value = g.Sum(x => x.ti.Quantity * x.ti.UnitPrice)
            })
            .ToListAsync(ct);

        return Ok(rows.Select(r => new TopProductDto(r.MaterialCode, r.MaterialName, r.Qty, r.Value)).ToList());
    }

    [HttpGet("top-materials")]
    public Task<ActionResult<List<TopProductDto>>> TopMaterials(CancellationToken ct) => TopProducts(ct);
}
