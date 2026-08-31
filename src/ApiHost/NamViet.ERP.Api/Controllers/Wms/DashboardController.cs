using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NamViet.ERP.Api.Application.Dtos.Wms;
using NamViet.ERP.Api.Application.Services.Wms;
using NamViet.ERP.Api.Infrastructure.Authorization;
using NamViet.ERP.Modules.Warehouse.Domain.Enums;
using NamViet.ERP.Modules.Warehouse.Infrastructure.Persistence;
using NamViet.ERP.SharedKernel.Authorization;

namespace NamViet.ERP.Api.Controllers.Wms;

[ApiController]
[Route("api/dashboard")]
[Authorize]
[RequirePermission("wms.dashboard.view")]
public class DashboardController(WarehouseDbContext db, IWmsCatalogQueryService catalog) : ControllerBase
{
    private bool CanViewPrices => PriceVisibility.CanViewPrices(User);

    [HttpGet("kpi")]
    public async Task<ActionResult<DashboardKpiDto>> Kpi(CancellationToken ct)
    {
        var activeProjects = await db.Projects.CountAsync(
            p => p.DeletedAt == null
                 && p.Status == ProjectStatus.Active
                 && p.ProjectType == ProjectType.Project,
            ct);

        var projectStock = await db.ProjectInventories.SumAsync(
            pi => pi.ActualImportQty - pi.ActualExportQty, ct);
        var warehouseStock = await db.WarehouseInventories.SumAsync(i => i.Qty, ct);
        var totalStock = (int)Math.Round(projectStock + warehouseStock, MidpointRounding.AwayFromZero);
        var totalValue = PriceVisibility.Mask(CanViewPrices, await catalog.GetTotalInventoryValueAsync(ct));
        var today = DateTime.UtcNow.Date;
        var todayTx = await db.WarehouseTransactions.Where(t => t.CreatedAt >= today).ToListAsync(ct);
        return Ok(new DashboardKpiDto(totalStock, totalValue, activeProjects,
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

            result.Add(new DashboardTrendDto(
                start.ToString("MM/yyyy"),
                PriceVisibility.Mask(CanViewPrices, importValue),
                PriceVisibility.Mask(CanViewPrices, exportValue)));
        }

        return Ok(result);
    }

    [HttpGet("stock-by-category")]
    public async Task<ActionResult<List<DashboardCategoryDto>>> StockByCategory(CancellationToken ct)
    {
        var projectRows = await db.ProjectInventories
            .AsNoTracking()
            .GroupBy(pi => pi.MaterialCode)
            .Select(g => new
            {
                MaterialCode = g.Key,
                Qty = g.Sum(x => x.ActualImportQty - x.ActualExportQty),
            })
            .ToListAsync(ct);

        var warehouseRows = await db.WarehouseInventories
            .AsNoTracking()
            .GroupBy(i => i.MaterialCode)
            .Select(g => new { MaterialCode = g.Key, Qty = g.Sum(x => x.Qty) })
            .ToListAsync(ct);

        var qtyByMaterial = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase);
        foreach (var row in projectRows)
            qtyByMaterial[row.MaterialCode] = qtyByMaterial.GetValueOrDefault(row.MaterialCode) + row.Qty;
        foreach (var row in warehouseRows)
            qtyByMaterial[row.MaterialCode] = qtyByMaterial.GetValueOrDefault(row.MaterialCode) + row.Qty;

        var codes = qtyByMaterial
            .Where(kv => kv.Value > 0)
            .Select(kv => kv.Key)
            .ToList();
        if (codes.Count == 0)
            return Ok(new List<DashboardCategoryDto>());

        var materials = await db.Materials
            .AsNoTracking()
            .Where(m => m.DeletedAt == null && codes.Contains(m.MaterialCode))
            .Select(m => new { m.MaterialCode, m.MaterialName })
            .ToListAsync(ct);

        var rows = materials
            .Select(m => new DashboardCategoryDto(
                m.MaterialName,
                qtyByMaterial.GetValueOrDefault(m.MaterialCode)))
            .Where(r => r.Value > 0)
            .OrderByDescending(r => r.Value)
            .Take(12)
            .ToList();

        return Ok(rows);
    }

    [HttpGet("stock-by-project")]
    public async Task<ActionResult<List<DashboardCategoryDto>>> StockByProject(CancellationToken ct)
    {
        var rows = await db.ProjectInventories
            .AsNoTracking()
            .Where(pi => pi.Project.DeletedAt == null)
            .GroupBy(pi => new { pi.Project.ProjectCode, pi.Project.ProjectName })
            .Select(g => new
            {
                g.Key.ProjectName,
                Qty = g.Sum(x => x.ActualImportQty) - g.Sum(x => x.ActualExportQty),
            })
            .Where(x => x.Qty > 0)
            .OrderByDescending(x => x.Qty)
            .Take(6)
            .ToListAsync(ct);

        return Ok(rows.Select(r => new DashboardCategoryDto(r.ProjectName, r.Qty)).ToList());
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

        return Ok(rows
            .Select(r => new TopProductDto(
                r.MaterialCode,
                r.MaterialName,
                r.Qty,
                PriceVisibility.Mask(CanViewPrices, r.Value)))
            .ToList());
    }

    [HttpGet("top-materials")]
    public Task<ActionResult<List<TopProductDto>>> TopMaterials(CancellationToken ct) => TopProducts(ct);
}
