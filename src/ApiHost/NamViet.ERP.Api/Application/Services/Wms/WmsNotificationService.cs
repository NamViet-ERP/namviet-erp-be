using Microsoft.EntityFrameworkCore;
using NamViet.ERP.Api.Application.Dtos.Wms;
using NamViet.ERP.Modules.Warehouse.Domain.Entities;
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
        var canApproveEstimate = permissions.Contains("purchasing.estimates.approve_l1");

        var items = new List<WmsNotificationItemDto>();
        var importCount = 0;
        var exportCount = 0;
        var transferCount = 0;
        var stocktakeCount = 0;
        var estimateCount = 0;

        if (canImport)
        {
            importCount = await db.WarehouseTransactions.CountAsync(
                t => t.TransactionType == TransactionType.Import
                     && t.ApprovalStatus == ApprovalStatus.Pending
                     && !t.IsDirectProcurement, ct);

            var rows = await db.WarehouseTransactions
                .AsNoTracking()
                .Where(t => t.TransactionType == TransactionType.Import
                            && t.ApprovalStatus == ApprovalStatus.Pending
                            && !t.IsDirectProcurement)
                .OrderByDescending(t => t.CreatedAt)
                .Take(15)
                .Select(t => new
                {
                    t.TicketCode,
                    ProjectCode = t.BeneficiaryProjectCode ?? t.ProjectCode,
                    t.CreatedByName,
                    t.CreatedAt,
                })
                .ToListAsync(ct);

            items.AddRange(rows.Select(r => ToItem(
                $"import:{r.TicketCode}",
                "import",
                r.TicketCode,
                "Phiếu nhập chờ duyệt",
                "/warehouse/import",
                r.CreatedAt,
                r.ProjectCode,
                r.CreatedByName)));
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
                    ProjectCode = t.BeneficiaryProjectCode ?? t.ProjectCode,
                    t.CreatedByName,
                    t.CreatedAt,
                    t.IsDirectProcurement,
                })
                .ToListAsync(ct);

            items.AddRange(rows.Select(r => ToItem(
                $"export:{r.TicketCode}",
                "export",
                r.TicketCode,
                r.IsDirectProcurement ? "Xuất thẳng chờ duyệt" : "Phiếu xuất chờ duyệt",
                "/warehouse/export",
                r.CreatedAt,
                r.ProjectCode,
                r.CreatedByName)));
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
                    ProjectCode = t.BeneficiaryProjectCode ?? t.ProjectCode,
                    t.CreatedByName,
                    t.CreatedAt,
                })
                .ToListAsync(ct);

            items.AddRange(rows.Select(r => ToItem(
                $"transfer:{r.TicketCode}",
                "transfer",
                r.TicketCode,
                "Phiếu chuyển chờ duyệt",
                "/warehouse/transfer",
                r.CreatedAt,
                r.ProjectCode,
                r.CreatedByName)));
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

            items.AddRange(rows.Select(r => ToItem(
                $"stocktake:{r.VoucherCode}",
                "stocktake",
                r.VoucherCode,
                "Phiếu kiểm kê chờ duyệt",
                "/stocktake/list",
                r.CreatedAt,
                r.WarehouseName,
                r.CreatedByName)));
        }

        if (canApproveEstimate)
        {
            estimateCount = await db.PurchaseEstimates.CountAsync(
                e => e.Status == PurchaseEstimateStatuses.PendingApproval, ct);

            var rows = await db.PurchaseEstimates
                .AsNoTracking()
                .Where(e => e.Status == PurchaseEstimateStatuses.PendingApproval)
                .OrderByDescending(e => e.UpdatedAt)
                .Take(15)
                .Select(e => new
                {
                    e.Id,
                    e.Code,
                    e.ProjectCode,
                    e.Title,
                    e.UpdatedByName,
                    e.UpdatedAt,
                })
                .ToListAsync(ct);

            items.AddRange(rows.Select(r => ToItem(
                $"estimate:{r.Id:N}",
                "estimate",
                r.Code,
                "Dự toán chờ duyệt",
                $"/purchasing/estimate-procurement/{r.Id}",
                r.UpdatedAt,
                string.IsNullOrWhiteSpace(r.Title) ? r.ProjectCode : $"{r.ProjectCode} · {r.Title}",
                r.UpdatedByName)));
        }

        var ordered = items
            .OrderByDescending(i => i.TimeLabel)
            .Take(30)
            .ToList();

        return new WmsPendingNotificationsDto(
            importCount + exportCount + transferCount + stocktakeCount + estimateCount,
            importCount,
            exportCount,
            transferCount,
            stocktakeCount,
            ordered);
    }

    private static WmsNotificationItemDto ToItem(
        string id,
        string kind,
        string voucherCode,
        string title,
        string route,
        DateTime createdAt,
        string? contextLabel,
        string? createdByName)
    {
        var context = string.IsNullOrWhiteSpace(contextLabel) ? null : contextLabel.Trim();
        var actor = string.IsNullOrWhiteSpace(createdByName) ? null : createdByName.Trim();
        var message = string.Join(
            " - ",
            new[] { voucherCode, context, actor }.Where(part => !string.IsNullOrWhiteSpace(part)));

        return new WmsNotificationItemDto(
            id,
            kind,
            voucherCode,
            title,
            message,
            route,
            createdAt.ToString("dd/MM/yyyy HH:mm"),
            context,
            actor);
    }
}
