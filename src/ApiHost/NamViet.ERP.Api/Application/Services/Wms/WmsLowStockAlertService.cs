using Microsoft.EntityFrameworkCore;
using NamViet.ERP.Api.Application.Dtos.Wms;
using NamViet.ERP.Modules.Warehouse.Infrastructure.Persistence;

namespace NamViet.ERP.Api.Application.Services.Wms;

public interface IWmsLowStockAlertService
{
    Task<IReadOnlyList<WmsLowStockAlertDto>> GetAlertsAsync(
        string? projectCode, int limit = 30, CancellationToken ct = default);
}

public sealed class WmsLowStockAlertService(WarehouseDbContext db) : IWmsLowStockAlertService
{
    public async Task<IReadOnlyList<WmsLowStockAlertDto>> GetAlertsAsync(
        string? projectCode, int limit = 30, CancellationToken ct = default)
    {
        var take = Math.Clamp(limit, 1, 200);

        var query = db.ProjectInventories
            .AsNoTracking()
            .Include(pi => pi.Project)
            .Include(pi => pi.Material)
            .Where(pi => pi.Project.DeletedAt == null && pi.MinStockQty > 0)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(projectCode))
            query = query.Where(pi => pi.ProjectCode == projectCode);

        var rows = await query
            .Select(pi => new
            {
                pi.ProjectCode,
                pi.Project.ProjectName,
                pi.MaterialCode,
                MaterialName = pi.Material != null ? pi.Material.MaterialName : pi.MaterialCode,
                Unit = pi.Material != null ? pi.Material.Unit : string.Empty,
                ActualQty = pi.ActualImportQty - pi.ActualExportQty,
                pi.MinStockQty,
            })
            .Where(x => x.ActualQty < x.MinStockQty)
            .OrderBy(x => x.ProjectCode)
            .ThenBy(x => x.MaterialCode)
            .Take(take)
            .ToListAsync(ct);

        return rows.Select(r => new WmsLowStockAlertDto(
            r.ProjectCode,
            r.ProjectName,
            r.MaterialCode,
            r.MaterialName,
            r.Unit,
            r.ActualQty,
            r.MinStockQty,
            r.MinStockQty - r.ActualQty)).ToList();
    }
}
