using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;
using NamViet.ERP.Api.Application.Dtos.Wms;
using NamViet.ERP.Api.Application;
using NamViet.ERP.Modules.Warehouse.Domain.Enums;
using NamViet.ERP.Modules.Warehouse.Infrastructure.Persistence;

namespace NamViet.ERP.Api.Application.Services.Wms;

public interface IWmsReportService
{
    Task<WmsMovementSummaryDto> GetMovementSummaryAsync(
        DateOnly? dateFrom, DateOnly? dateTo, CancellationToken ct = default);

    Task<IReadOnlyList<WmsStockReportRowDto>> GetStockReportAsync(
        string? projectCode, CancellationToken ct = default);

    Task<WmsPendingReportDto> GetPendingReportAsync(CancellationToken ct = default);

    byte[] BuildMovementSummaryWorkbook(WmsMovementSummaryDto summary);
    byte[] BuildStockReportWorkbook(IReadOnlyList<WmsStockReportRowDto> rows);
    byte[] BuildPendingReportWorkbook(WmsPendingReportDto report);
}

public sealed class WmsReportService(WarehouseDbContext db) : IWmsReportService
{
    private static readonly string[] MovementHeaders = ["Chỉ tiêu", "Giá trị"];
    private static readonly string[] StockHeaders =
        ["Mã dự án", "Tên dự án", "Mã vật tư", "Tên vật tư", "ĐVT", "SL tồn"];
    private static readonly string[] PendingSummaryHeaders = ["Chỉ tiêu", "Số lượng"];
    private static readonly string[] PendingDetailHeaders =
        ["Loại", "Mã phiếu", "Ngày", "Người tạo", "Dự án"];

    public async Task<WmsMovementSummaryDto> GetMovementSummaryAsync(
        DateOnly? dateFrom, DateOnly? dateTo, CancellationToken ct = default)
    {
        var importQuery = db.WarehouseTransactions
            .AsNoTracking()
            .Where(t => t.TransactionType == TransactionType.Import);
        var exportQuery = db.WarehouseTransactions
            .AsNoTracking()
            .Where(t => t.TransactionType == TransactionType.Export);
        var transferQuery = db.WarehouseTransactions
            .AsNoTracking()
            .Where(t => t.TransactionType == TransactionType.Transfer);

        var fromUtc = UtcDateRange.Start(dateFrom);
        if (fromUtc.HasValue)
        {
            importQuery = importQuery.Where(t => t.CreatedAt >= fromUtc.Value);
            exportQuery = exportQuery.Where(t => t.CreatedAt >= fromUtc.Value);
            transferQuery = transferQuery.Where(t => t.CreatedAt >= fromUtc.Value);
        }

        var toUtc = UtcDateRange.ExclusiveEnd(dateTo);
        if (toUtc.HasValue)
        {
            importQuery = importQuery.Where(t => t.CreatedAt < toUtc.Value);
            exportQuery = exportQuery.Where(t => t.CreatedAt < toUtc.Value);
            transferQuery = transferQuery.Where(t => t.CreatedAt < toUtc.Value);
        }

        var importCount = await importQuery.CountAsync(ct);
        var exportCount = await exportQuery.CountAsync(ct);
        var transferCount = await transferQuery.CountAsync(ct);

        var importValue = await db.WarehouseTransactionItems
            .AsNoTracking()
            .Where(i => i.Transaction!.TransactionType == TransactionType.Import)
            .Where(i => !fromUtc.HasValue || i.Transaction.CreatedAt >= fromUtc.Value)
            .Where(i => !toUtc.HasValue || i.Transaction.CreatedAt < toUtc.Value)
            .SumAsync(i => (decimal?)(i.Quantity * i.UnitPrice), ct) ?? 0m;

        var exportValue = await db.WarehouseTransactionItems
            .AsNoTracking()
            .Where(i => i.Transaction!.TransactionType == TransactionType.Export)
            .Where(i => !fromUtc.HasValue || i.Transaction.CreatedAt >= fromUtc.Value)
            .Where(i => !toUtc.HasValue || i.Transaction.CreatedAt < toUtc.Value)
            .SumAsync(i => (decimal?)(i.Quantity * i.UnitPrice), ct) ?? 0m;

        return new WmsMovementSummaryDto(importCount, importValue, exportCount, exportValue, transferCount);
    }

    public async Task<IReadOnlyList<WmsStockReportRowDto>> GetStockReportAsync(
        string? projectCode, CancellationToken ct = default)
    {
        var query = db.ProjectInventories
            .AsNoTracking()
            .Include(pi => pi.Project)
            .Include(pi => pi.Material)
            .Where(pi => pi.Project.DeletedAt == null)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(projectCode))
            query = query.Where(pi => pi.ProjectCode == projectCode);

        var rows = await query
            .Select(pi => new
            {
                pi.ProjectCode,
                pi.Project.ProjectName,
                pi.MaterialCode,
                pi.Material.MaterialName,
                pi.Material.Unit,
                Qty = pi.ActualImportQty - pi.ActualExportQty,
            })
            .Where(x => x.Qty > 0)
            .OrderBy(x => x.ProjectCode)
            .ThenBy(x => x.MaterialCode)
            .ToListAsync(ct);

        return rows.Select(r => new WmsStockReportRowDto(
            r.ProjectCode, r.ProjectName, r.MaterialCode, r.MaterialName, r.Unit, r.Qty)).ToList();
    }

    public async Task<WmsPendingReportDto> GetPendingReportAsync(CancellationToken ct = default)
    {
        var pendingImports = await db.WarehouseTransactions
            .AsNoTracking()
            .Where(t => t.TransactionType == TransactionType.Import
                        && t.ApprovalStatus == ApprovalStatus.Pending)
            .OrderByDescending(t => t.CreatedAt)
            .Take(50)
            .Select(t => new WmsPendingVoucherRowDto(
                "import",
                t.TicketCode,
                t.CreatedAt.ToString("dd/MM/yyyy"),
                t.CreatedByName,
                t.ProjectCode))
            .ToListAsync(ct);

        var pendingExports = await db.WarehouseTransactions
            .AsNoTracking()
            .Where(t => t.TransactionType == TransactionType.Export
                        && t.ApprovalStatus == ApprovalStatus.Pending)
            .OrderByDescending(t => t.CreatedAt)
            .Take(50)
            .Select(t => new WmsPendingVoucherRowDto(
                "export",
                t.TicketCode,
                t.CreatedAt.ToString("dd/MM/yyyy"),
                t.CreatedByName,
                t.ProjectCode))
            .ToListAsync(ct);

        var pendingTransfers = await db.WarehouseTransactions
            .AsNoTracking()
            .Where(t => t.TransactionType == TransactionType.Transfer
                        && t.ApprovalStatus == ApprovalStatus.Pending)
            .OrderByDescending(t => t.CreatedAt)
            .Take(50)
            .Select(t => new WmsPendingVoucherRowDto(
                "transfer",
                t.TicketCode,
                t.CreatedAt.ToString("dd/MM/yyyy"),
                t.CreatedByName,
                t.ProjectCode))
            .ToListAsync(ct);

        var pendingStocktakes = await db.Stocktakes
            .AsNoTracking()
            .Where(s => s.Status == "pending")
            .OrderByDescending(s => s.CreatedAt)
            .Take(50)
            .Select(s => new WmsPendingVoucherRowDto(
                "stocktake",
                s.VoucherCode,
                s.VoucherDate.ToString("dd/MM/yyyy"),
                s.CreatedByName,
                s.WarehouseName))
            .ToListAsync(ct);

        var importCount = await db.WarehouseTransactions.CountAsync(
            t => t.TransactionType == TransactionType.Import && t.ApprovalStatus == ApprovalStatus.Pending, ct);
        var exportCount = await db.WarehouseTransactions.CountAsync(
            t => t.TransactionType == TransactionType.Export && t.ApprovalStatus == ApprovalStatus.Pending, ct);
        var transferCount = await db.WarehouseTransactions.CountAsync(
            t => t.TransactionType == TransactionType.Transfer && t.ApprovalStatus == ApprovalStatus.Pending, ct);
        var stocktakeCount = await db.Stocktakes.CountAsync(s => s.Status == "pending", ct);

        var items = pendingImports
            .Concat(pendingExports)
            .Concat(pendingTransfers)
            .Concat(pendingStocktakes)
            .OrderByDescending(i => i.Date)
            .Take(100)
            .ToList();

        return new WmsPendingReportDto(importCount, exportCount, transferCount, stocktakeCount, items);
    }

    public byte[] BuildMovementSummaryWorkbook(WmsMovementSummaryDto summary) =>
        BuildWorkbook(
            "Nhap xuat",
            MovementHeaders,
            new[]
            {
                new object?[] { "Số phiếu nhập", summary.ImportCount },
                new object?[] { "Giá trị nhập", summary.ImportValue },
                new object?[] { "Số phiếu xuất", summary.ExportCount },
                new object?[] { "Giá trị xuất", summary.ExportValue },
                new object?[] { "Số phiếu chuyển", summary.TransferCount },
            });

    public byte[] BuildStockReportWorkbook(IReadOnlyList<WmsStockReportRowDto> rows) =>
        BuildWorkbook(
            "Ton kho",
            StockHeaders,
            rows.Select(r => new object?[]
            {
                r.ProjectCode,
                r.ProjectName,
                r.MaterialCode,
                r.MaterialName,
                r.Unit,
                r.Qty,
            }));

    public byte[] BuildPendingReportWorkbook(WmsPendingReportDto report)
    {
        var summaryRows = new List<object?[]>
        {
            new object?[] { "Phiếu nhập chờ duyệt", report.ImportCount },
            new object?[] { "Phiếu xuất chờ duyệt", report.ExportCount },
            new object?[] { "Phiếu chuyển chờ duyệt", report.TransferCount },
            new object?[] { "Phiếu kiểm kê chờ duyệt", report.StocktakeCount },
        };

        using var workbook = new XLWorkbook();
        var summarySheet = workbook.Worksheets.Add("Tong hop");
        WriteHeaders(summarySheet, PendingSummaryHeaders);
        WriteDataRows(summarySheet, summaryRows);

        var detailSheet = workbook.Worksheets.Add("Chi tiet");
        WriteHeaders(detailSheet, PendingDetailHeaders);
        WriteDataRows(detailSheet, report.Items.Select(i => new object?[]
        {
            i.Kind, i.VoucherCode, i.Date, i.CreatedBy, i.Project ?? "",
        }));

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }

    private static byte[] BuildWorkbook(string sheetName, string[] headers, IEnumerable<object?[]> dataRows)
    {
        using var workbook = new XLWorkbook();
        var sheet = workbook.Worksheets.Add(sheetName);
        WriteHeaders(sheet, headers);
        WriteDataRows(sheet, dataRows);
        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }

    private static void WriteHeaders(IXLWorksheet sheet, string[] headers)
    {
        for (var col = 0; col < headers.Length; col++)
        {
            sheet.Cell(1, col + 1).Value = headers[col];
            sheet.Cell(1, col + 1).Style.Font.Bold = true;
        }
    }

    private static void WriteDataRows(IXLWorksheet sheet, IEnumerable<object?[]> dataRows)
    {
        var rowIndex = 2;
        foreach (var row in dataRows)
        {
            for (var col = 0; col < row.Length; col++)
                sheet.Cell(rowIndex, col + 1).Value = row[col]?.ToString() ?? "";
            rowIndex++;
        }
    }
}
