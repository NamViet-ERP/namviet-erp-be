using System.Diagnostics.CodeAnalysis;
using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using NamViet.ERP.Api.Application.Dtos.Wms;
using NamViet.ERP.Modules.Warehouse.Domain.Entities;
using NamViet.ERP.Modules.Warehouse.Domain.Enums;
using NamViet.ERP.Modules.Warehouse.Domain.Services;
using NamViet.ERP.Modules.Warehouse.Infrastructure.Helpers;
using NamViet.ERP.Modules.Warehouse.Infrastructure.Persistence;
using NamViet.ERP.SharedKernel.Interfaces;

namespace NamViet.ERP.Api.Application.Services.Wms;

public interface IOpeningStockImportService
{
    byte[] DownloadTemplate();

    Task<OpeningStockPreviewResponseDto> PreviewAsync(
        Stream stream,
        string? projectCode,
        CancellationToken ct = default);

    Task<OpeningStockImportResultDto> ImportAsync(
        Stream stream,
        string? projectCode,
        CancellationToken ct = default);
}

public sealed class OpeningStockValidationException : Exception
{
    public IReadOnlyList<string> Errors { get; }

    public OpeningStockValidationException(string message, IReadOnlyList<string>? errors = null)
        : base(message)
    {
        Errors = errors ?? [];
    }
}

public sealed partial class OpeningStockImportService(
    WarehouseDbContext db,
    IInventoryDomainService inventory,
    ICurrentUser currentUser) : IOpeningStockImportService
{
    public byte[] DownloadTemplate()
    {
        using var workbook = new XLWorkbook();
        var ws = workbook.Worksheets.Add("TonKhoDauKy");

        string[] headers =
        [
            "Mã vật tư",
            "Tên vật tư",
            "Đơn vị tính",
            "Số lượng tồn",
            "Đơn giá vốn",
            "Ghi chú"
        ];

        for (var col = 0; col < headers.Length; col++)
        {
            var cell = ws.Cell(1, col + 1);
            cell.Value = headers[col];
            cell.Style.Font.Bold = true;
            cell.Style.Fill.BackgroundColor = XLColor.FromHtml("#059669");
            cell.Style.Font.FontColor = XLColor.White;
        }

        ws.Cell(2, 1).Value = "VT-THEP-001";
        ws.Cell(2, 2).Value = "Ống thép đúc DN100 SCH40";
        ws.Cell(2, 3).Value = "Cây";
        ws.Cell(2, 4).Value = 50;
        ws.Cell(2, 5).Value = 850000;
        ws.Cell(2, 6).Value = "Tồn kho đầu kỳ kiểm kê 2026";

        ws.Cell(3, 1).Value = "VT-VAN-002";
        ws.Cell(3, 2).Value = "Van cổng ty chìm gang DN100 PN16";
        ws.Cell(3, 3).Value = "Cái";
        ws.Cell(3, 4).Value = 20;
        ws.Cell(3, 5).Value = 1250000;
        ws.Cell(3, 6).Value = "Tồn kho đầu kỳ kiểm kê 2026";

        ws.Columns().AdjustToContents();

        using var ms = new MemoryStream();
        workbook.SaveAs(ms);
        return ms.ToArray();
    }

    public async Task<OpeningStockPreviewResponseDto> PreviewAsync(
        Stream stream,
        string? projectCode,
        CancellationToken ct = default)
    {
        using var workbook = new XLWorkbook(stream);
        var ws = workbook.Worksheets.FirstOrDefault()
            ?? throw new OpeningStockValidationException("File Excel không có sheet nào.");

        if (!string.IsNullOrWhiteSpace(projectCode))
        {
            var projExists = await db.Projects.AsNoTracking()
                .AnyAsync(p => p.ProjectCode == projectCode && p.DeletedAt == null, ct);
            if (!projExists)
                throw new OpeningStockValidationException($"Không tìm thấy dự án với mã '{projectCode}'.");
        }

        var materials = await LoadMaterialLookupAsync(ct);
        var mapping = DetectColumnMapping(ws);
        var previewItems = new List<OpeningStockItemPreviewDto>();
        var lastRow = ws.LastRowUsed()?.RowNumber() ?? 0;

        for (var rowIdx = mapping.HeaderRow + 1; rowIdx <= lastRow; rowIdx++)
        {
            var values = ReadRowValues(ws.Row(rowIdx), mapping);
            if (ShouldSkipRow(values.Code, values.Name, values.QtyStr))
                continue;

            var isValid = true;
            var errorList = new List<string>();
            var name = values.Name;
            var unit = values.Unit;
            var note = values.Note;

            Material? mat = null;
            if (string.IsNullOrWhiteSpace(values.Code))
            {
                isValid = false;
                errorList.Add("Mã vật tư không được để trống");
            }
            else if (!TryLookupMaterial(materials, values.Code, out mat))
            {
                isValid = false;
                errorList.Add($"Mã vật tư '{values.Code}' chưa tồn tại trong danh mục hệ thống");
            }
            else
            {
                if (string.IsNullOrWhiteSpace(name)) name = mat.MaterialName;
                if (string.IsNullOrWhiteSpace(unit)) unit = mat.Unit;
            }

            if (!TryParseDecimalFlex(values.QtyStr, out var qty) || qty < 0)
            {
                isValid = false;
                errorList.Add("Số lượng tồn không hợp lệ hoặc bị âm");
            }
            else if (qty == 0)
            {
                note = string.IsNullOrWhiteSpace(note)
                    ? "Tồn 0 (Bỏ qua khi tạo phiếu)"
                    : $"{note} (Tồn 0 - Bỏ qua)";
            }

            var price = TryParseDecimalFlex(values.PriceStr, out var parsedPrice) ? parsedPrice : 0m;
            if (price < 0)
            {
                isValid = false;
                errorList.Add("Đơn giá không được âm");
                price = 0;
            }

            previewItems.Add(new OpeningStockItemPreviewDto(
                MaterialCode: mat != null ? mat.MaterialCode : values.Code,
                MaterialName: name,
                Unit: unit,
                Qty: qty,
                UnitPrice: price,
                TotalAmount: Math.Round(qty * price, 2),
                Note: string.IsNullOrWhiteSpace(note) ? null : note,
                IsValid: isValid,
                Error: errorList.Count > 0 ? string.Join("; ", errorList) : null));
        }

        var total = previewItems.Count;
        var valid = previewItems.Count(i => i.IsValid);
        var errorCount = total - valid;
        var totalQty = previewItems.Where(i => i.IsValid).Sum(i => i.Qty);
        var totalAmount = previewItems.Where(i => i.IsValid).Sum(i => i.TotalAmount);

        return new OpeningStockPreviewResponseDto(total, valid, errorCount, totalQty, totalAmount, previewItems);
    }

    public async Task<OpeningStockImportResultDto> ImportAsync(
        Stream stream,
        string? projectCode,
        CancellationToken ct = default)
    {
        using var workbook = new XLWorkbook(stream);
        var ws = workbook.Worksheets.FirstOrDefault()
            ?? throw new OpeningStockValidationException("File Excel không có sheet nào.");

        Project? project = null;
        if (!string.IsNullOrWhiteSpace(projectCode))
        {
            project = await db.Projects
                .FirstOrDefaultAsync(p => p.ProjectCode == projectCode && p.DeletedAt == null, ct);
            if (project is null)
                throw new OpeningStockValidationException($"Không tìm thấy dự án với mã '{projectCode}'.");
        }

        var materials = await LoadMaterialLookupAsync(ct);
        var mapping = DetectColumnMapping(ws);
        var parsedItems = new List<(Material Mat, decimal Qty, decimal Price, string? Note)>();
        var errors = new List<string>();
        var lastRow = ws.LastRowUsed()?.RowNumber() ?? 0;

        for (var rowIdx = mapping.HeaderRow + 1; rowIdx <= lastRow; rowIdx++)
        {
            var values = ReadRowValues(ws.Row(rowIdx), mapping);
            if (ShouldSkipRow(values.Code, values.Name, values.QtyStr))
                continue;

            if (string.IsNullOrWhiteSpace(values.Code))
            {
                errors.Add($"Dòng {rowIdx}: Mã vật tư không được để trống.");
                continue;
            }

            if (!TryLookupMaterial(materials, values.Code, out var mat))
            {
                errors.Add($"Dòng {rowIdx}: Mã vật tư '{values.Code}' chưa tồn tại trong danh mục vật tư.");
                continue;
            }

            if (!TryParseDecimalFlex(values.QtyStr, out var qty) || qty < 0)
            {
                errors.Add($"Dòng {rowIdx}: Số lượng tồn '{values.QtyStr}' không hợp lệ hoặc bị âm.");
                continue;
            }

            if (qty == 0)
                continue;

            var price = TryParseDecimalFlex(values.PriceStr, out var parsedPrice) && parsedPrice > 0m
                ? parsedPrice
                : 0m;

            parsedItems.Add((mat, qty, price, string.IsNullOrWhiteSpace(values.Note) ? null : values.Note));
        }

        if (parsedItems.Count == 0)
        {
            throw errors.Count > 0
                ? new OpeningStockValidationException("File Excel không có dòng nào hợp lệ để nhập kho.", errors)
                : new OpeningStockValidationException("Không tìm thấy dòng dữ liệu nào hợp lệ trong file Excel để nhập kho.");
        }

        await using IDbContextTransaction? tx = db.Database.IsRelational()
            ? await db.Database.BeginTransactionAsync(ct)
            : null;

        var ticketCode = await TicketCodeAllocator.NextAsync(db, TransactionType.Import, ct);
        var userName = !string.IsNullOrWhiteSpace(currentUser.Name) ? currentUser.Name : "Admin";
        var generalWarehouseId = await inventory.GetGeneralWarehouseIdAsync(ct);
        if (project == null)
            await inventory.EnsureGeneralWarehouseAsync(generalWarehouseId, ct);

        var transaction = new WarehouseTransaction
        {
            TicketCode = ticketCode,
            TransactionType = TransactionType.Import,
            ProjectCode = project?.ProjectCode,
            InvoiceNumber = "OPENING-STOCK",
            CreatedById = currentUser.IsAuthenticated ? currentUser.Id : Guid.Empty,
            CreatedByName = userName,
            ApprovalStatus = ApprovalStatus.Approved,
            WarehouseApprovalStatus = WarehouseKeeperApprovalStatus.Approved,
            ApprovedById = currentUser.IsAuthenticated ? currentUser.Id : null,
            ApprovedByName = userName,
            ApprovedAt = DateTime.UtcNow,
            Notes = $"Nạp số dư tồn kho đầu kỳ{(project != null ? $" cho dự án {project.ProjectName}" : " kho tổng")} từ file Excel",
            WarehouseId = project == null ? generalWarehouseId : null,
            BeneficiaryProjectCode = project?.ProjectCode,
            WarehouseKeeper = userName,
            CreatedAt = DateTime.UtcNow,
        };
        db.WarehouseTransactions.Add(transaction);

        var distinctCodes = parsedItems
            .Select(x => x.Mat.MaterialCode)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        Dictionary<string, WarehouseInventory>? warehouseInvByCode = null;
        Dictionary<string, ProjectInventory>? projectInvByCode = null;

        if (project == null)
        {
            var existingWarehouseInvs = await db.WarehouseInventories
                .Where(wi => wi.WarehouseId == generalWarehouseId && distinctCodes.Contains(wi.MaterialCode))
                .ToListAsync(ct);
            warehouseInvByCode = existingWarehouseInvs
                .ToDictionary(wi => wi.MaterialCode, StringComparer.OrdinalIgnoreCase);
        }
        else
        {
            var existingProjectInvs = await db.ProjectInventories
                .Where(pi => pi.ProjectCode == project.ProjectCode && distinctCodes.Contains(pi.MaterialCode))
                .ToListAsync(ct);
            projectInvByCode = existingProjectInvs
                .ToDictionary(pi => pi.MaterialCode, StringComparer.OrdinalIgnoreCase);
        }

        foreach (var item in parsedItems)
        {
            db.WarehouseTransactionItems.Add(new WarehouseTransactionItem
            {
                TicketCode = ticketCode,
                MaterialCode = item.Mat.MaterialCode,
                MaterialNameSnapshot = item.Mat.MaterialName,
                UnitSnapshot = item.Mat.Unit,
                Quantity = item.Qty,
                PlannedQuantity = item.Qty,
                AcceptedQty = item.Qty,
                UnitPrice = item.Price,
                Notes = item.Note,
            });

            if (project == null)
            {
                if (!warehouseInvByCode!.TryGetValue(item.Mat.MaterialCode, out var inv))
                {
                    inv = new WarehouseInventory
                    {
                        WarehouseId = generalWarehouseId,
                        MaterialCode = item.Mat.MaterialCode,
                        Qty = item.Qty,
                        UpdatedAt = DateTime.UtcNow,
                    };
                    db.WarehouseInventories.Add(inv);
                    warehouseInvByCode[item.Mat.MaterialCode] = inv;
                }
                else
                {
                    inv.Qty += item.Qty;
                    inv.UpdatedAt = DateTime.UtcNow;
                }
            }
            else
            {
                if (!projectInvByCode!.TryGetValue(item.Mat.MaterialCode, out var pi))
                {
                    pi = new ProjectInventory
                    {
                        ProjectCode = project.ProjectCode,
                        MaterialCode = item.Mat.MaterialCode,
                        ActualImportQty = item.Qty,
                        UpdatedAt = DateTime.UtcNow,
                    };
                    db.ProjectInventories.Add(pi);
                    projectInvByCode[item.Mat.MaterialCode] = pi;
                }
                else
                {
                    pi.ActualImportQty += item.Qty;
                    pi.UpdatedAt = DateTime.UtcNow;
                }
            }
        }

        await db.SaveChangesAsync(ct);
        if (tx is not null)
            await tx.CommitAsync(ct);

        var totalQty = parsedItems.Sum(i => i.Qty);
        var totalAmount = parsedItems.Sum(i => i.Qty * i.Price);

        var destination = project != null ? $"kho dự án {project.ProjectCode}" : "kho tổng";
        var message = errors.Count > 0
            ? $"Đã nạp thành công {parsedItems.Count} mặt hàng hợp lệ vào {destination} (bỏ qua {errors.Count} dòng lỗi). Mã phiếu: {ticketCode}"
            : $"Đã nạp thành công toàn bộ {parsedItems.Count} mặt hàng vào {destination}. Mã phiếu: {ticketCode}";

        return new OpeningStockImportResultDto(
            ticketCode,
            parsedItems.Count,
            totalQty,
            totalAmount,
            message,
            errors.Count);
    }

    private async Task<Dictionary<string, Material>> LoadMaterialLookupAsync(CancellationToken ct)
    {
        var materialsList = await db.Materials.AsNoTracking()
            .Where(m => m.DeletedAt == null && !string.IsNullOrWhiteSpace(m.MaterialCode))
            .ToListAsync(ct);
        return materialsList
            .GroupBy(m => m.MaterialCode.Trim().ToUpperInvariant())
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);
    }

    private static bool TryLookupMaterial(
        Dictionary<string, Material> materials,
        string? code,
        [NotNullWhen(true)] out Material? mat)
    {
        mat = null;
        if (string.IsNullOrWhiteSpace(code)) return false;

        var trimmed = code.Trim().ToUpperInvariant();
        if (materials.TryGetValue(trimmed, out mat))
            return true;

        if (trimmed.Contains(','))
        {
            var normalized = trimmed.Replace(',', '.');
            if (materials.TryGetValue(normalized, out mat))
                return true;
        }

        return false;
    }
}
