using Microsoft.EntityFrameworkCore;
using NamViet.ERP.Api.Application.Dtos.Wms;
using NamViet.ERP.Modules.Warehouse.Domain.Enums;
using NamViet.ERP.Modules.Warehouse.Infrastructure.Persistence;

namespace NamViet.ERP.Api.Application.Services.Wms;

public interface IWmsNotificationService
{
    Task<WmsPendingNotificationsDto> GetPendingAsync(
        IReadOnlyCollection<string> permissions, CancellationToken ct = default);
}

public sealed class WmsNotificationService(WarehouseDbContext db) : IWmsNotificationService
{
    public async Task<WmsPendingNotificationsDto> GetPendingAsync(
        IReadOnlyCollection<string> permissions, CancellationToken ct = default)
    {
        var canImport = permissions.Contains("wms.imports.view:all");
        var canExport = permissions.Contains("wms.exports.view:all");
        var canTransfer = permissions.Contains("wms.transfers.view:all");
        var canStocktake = permissions.Contains("wms.stocktakes.view:all");

        var items = new List<WmsNotificationItemDto>();
        var importCount = 0;
        var exportCount = 0;
        var transferCount = 0;
        var stocktakeCount = 0;

        if (canImport)
        {
            importCount = await db.WarehouseTransactions.CountAsync(
                t => t.TransactionType == TransactionType.Import
                     && t.ApprovalStatus == ApprovalStatus.Pending, ct);

            var rows = await db.WarehouseTransactions
                .AsNoTracking()
                .Where(t => t.TransactionType == TransactionType.Import
                            && t.ApprovalStatus == ApprovalStatus.Pending)
                .OrderByDescending(t => t.CreatedAt)
                .Take(15)
                .Select(t => new
                {
                    t.TicketCode,
                    t.ProjectCode,
                    t.CreatedByName,
                    t.CreatedAt,
                })
                .ToListAsync(ct);

            items.AddRange(rows.Select(r => new WmsNotificationItemDto(
                $"import:{r.TicketCode}",
                "import",
                r.TicketCode,
                "Phiếu nhập chờ duyệt",
                $"{r.TicketCode} — {r.ProjectCode ?? "—"} — {r.CreatedByName}",
                "/warehouse/import",
                r.CreatedAt.ToString("dd/MM/yyyy HH:mm"))));
        }

        if (canExport)
        {
            exportCount = await db.WarehouseTransactions.CountAsync(
                t => t.TransactionType == TransactionType.Export
                     && t.ApprovalStatus == ApprovalStatus.Pending, ct);

            var rows = await db.WarehouseTransactions
                .AsNoTracking()
                .Where(t => t.TransactionType == TransactionType.Export
                            && t.ApprovalStatus == ApprovalStatus.Pending)
                .OrderByDescending(t => t.CreatedAt)
                .Take(15)
                .Select(t => new
                {
                    t.TicketCode,
                    t.ProjectCode,
                    t.CreatedByName,
                    t.CreatedAt,
                })
                .ToListAsync(ct);

            items.AddRange(rows.Select(r => new WmsNotificationItemDto(
                $"export:{r.TicketCode}",
                "export",
                r.TicketCode,
                "Phiếu xuất chờ duyệt",
                $"{r.TicketCode} — {r.ProjectCode ?? "—"} — {r.CreatedByName}",
                "/warehouse/export",
                r.CreatedAt.ToString("dd/MM/yyyy HH:mm"))));
        }

        if (canTransfer)
        {
            transferCount = await db.WarehouseTransactions.CountAsync(
                t => t.TransactionType == TransactionType.Transfer
                     && t.ApprovalStatus == ApprovalStatus.Pending, ct);

            var rows = await db.WarehouseTransactions
                .AsNoTracking()
                .Where(t => t.TransactionType == TransactionType.Transfer
                            && t.ApprovalStatus == ApprovalStatus.Pending)
                .OrderByDescending(t => t.CreatedAt)
                .Take(15)
                .Select(t => new
                {
                    t.TicketCode,
                    t.ProjectCode,
                    t.CreatedByName,
                    t.CreatedAt,
                })
                .ToListAsync(ct);

            items.AddRange(rows.Select(r => new WmsNotificationItemDto(
                $"transfer:{r.TicketCode}",
                "transfer",
                r.TicketCode,
                "Phiếu chuyển chờ duyệt",
                $"{r.TicketCode} — {r.ProjectCode ?? "—"} — {r.CreatedByName}",
                "/warehouse/transfer",
                r.CreatedAt.ToString("dd/MM/yyyy HH:mm"))));
        }

        if (canStocktake)
        {
            stocktakeCount = await db.Stocktakes.CountAsync(s => s.Status == "pending", ct);

            var rows = await db.Stocktakes
                .AsNoTracking()
                .Where(s => s.Status == "pending")
                .OrderByDescending(s => s.CreatedAt)
                .Take(15)
                .Select(s => new
                {
                    s.VoucherCode,
                    s.WarehouseName,
                    s.CreatedByName,
                    s.CreatedAt,
                })
                .ToListAsync(ct);

            items.AddRange(rows.Select(r => new WmsNotificationItemDto(
                $"stocktake:{r.VoucherCode}",
                "stocktake",
                r.VoucherCode,
                "Phiếu kiểm kê chờ duyệt",
                $"{r.VoucherCode} — {r.WarehouseName} — {r.CreatedByName}",
                "/stocktake/list",
                r.CreatedAt.ToString("dd/MM/yyyy HH:mm"))));
        }

        var ordered = items
            .OrderByDescending(i => i.TimeLabel)
            .Take(30)
            .ToList();

        return new WmsPendingNotificationsDto(
            importCount + exportCount + transferCount + stocktakeCount,
            importCount,
            exportCount,
            transferCount,
            stocktakeCount,
            ordered);
    }
}
