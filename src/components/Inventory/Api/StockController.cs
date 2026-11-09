using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using ClosedXML.Excel;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NamViet.ERP.Api.Application.Dtos.Wms;
using NamViet.ERP.Api.Infrastructure.Authorization;
using NamViet.ERP.Modules.Warehouse.Domain;
using NamViet.ERP.Modules.Warehouse.Domain.Entities;
using NamViet.ERP.Modules.Warehouse.Domain.Enums;
using NamViet.ERP.Modules.Warehouse.Domain.Services;
using NamViet.ERP.Modules.Warehouse.Infrastructure.Helpers;
using NamViet.ERP.Modules.Warehouse.Infrastructure.Persistence;
using NamViet.ERP.SharedKernel.Authorization;
using NamViet.ERP.SharedKernel.Interfaces;

namespace NamViet.ERP.Api.Controllers.Wms;

[ApiController]
[Route("api/stock")]
[Authorize]
public class StockController(
    WarehouseDbContext db,
    IInventoryDomainService inventory,
    ICurrentUser currentUser) : ControllerBase
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

    [HttpGet("admin/opening-balance/template")]
    [RequirePermission("wms.stock.view:all")]
    public IActionResult DownloadOpeningBalanceTemplate()
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
        return File(
            ms.ToArray(),
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            "Mau_Nhap_Ton_Kho_Dau_Ky.xlsx");
    }

    [HttpPost("admin/opening-balance/preview")]
    public async Task<ActionResult<OpeningStockPreviewResponseDto>> PreviewOpeningBalance(
        IFormFile file,
        [FromQuery] string? projectCode,
        CancellationToken ct)
    {
        if (!IsAdminUser())
            return Forbid();

        if (file == null || file.Length == 0)
            return BadRequest("Vui lòng chọn file Excel tồn kho đầu kỳ.");

        using var stream = file.OpenReadStream();
        using var workbook = new XLWorkbook(stream);
        var ws = workbook.Worksheets.FirstOrDefault();
        if (ws is null)
            return BadRequest("File Excel không có sheet nào.");

        if (!string.IsNullOrWhiteSpace(projectCode))
        {
            var projExists = await db.Projects.AsNoTracking().AnyAsync(p => p.ProjectCode == projectCode && p.DeletedAt == null, ct);
            if (!projExists)
                return BadRequest($"Không tìm thấy dự án với mã '{projectCode}'.");
        }

        var materialsList = await db.Materials.AsNoTracking()
            .Where(m => m.DeletedAt == null && !string.IsNullOrWhiteSpace(m.MaterialCode))
            .ToListAsync(ct);
        var materials = materialsList
            .GroupBy(m => m.MaterialCode.Trim().ToUpperInvariant())
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

        var mapping = DetectColumnMapping(ws);
        var previewItems = new List<OpeningStockItemPreviewDto>();
        var lastRow = ws.LastRowUsed()?.RowNumber() ?? 0;

        for (var rowIdx = mapping.HeaderRow + 1; rowIdx <= lastRow; rowIdx++)
        {
            var row = ws.Row(rowIdx);
            var code = mapping.CodeCol > 0 ? GetSafeCellString(row.Cell(mapping.CodeCol)) : string.Empty;
            var name = mapping.NameCol > 0 ? GetSafeCellString(row.Cell(mapping.NameCol)) : string.Empty;
            var unit = mapping.UnitCol > 0 ? GetSafeCellString(row.Cell(mapping.UnitCol)) : string.Empty;
            var qtyStr = mapping.QtyCol > 0 ? GetSafeCellString(row.Cell(mapping.QtyCol)) : string.Empty;
            var priceStr = mapping.PriceCol > 0 ? GetSafeCellString(row.Cell(mapping.PriceCol)) : string.Empty;
            var note = mapping.NoteCol > 0 ? GetSafeCellString(row.Cell(mapping.NoteCol)) : string.Empty;

            // Bỏ qua dòng placeholder công thức dạng F1-F2... nếu không có số lượng tồn
            if (string.IsNullOrWhiteSpace(qtyStr) && (string.IsNullOrWhiteSpace(code) || code.StartsWith("F1-", StringComparison.OrdinalIgnoreCase)))
                continue;

            // Bỏ qua dòng trống cả mã và số lượng
            if (string.IsNullOrWhiteSpace(code) && string.IsNullOrWhiteSpace(qtyStr))
                continue;

            // Bỏ qua dòng tổng cộng
            if (code.Contains("tổng cộng", StringComparison.OrdinalIgnoreCase) ||
                name.Contains("tổng cộng", StringComparison.OrdinalIgnoreCase))
                continue;

            // Bỏ qua các dòng không có số lượng tồn được ghi nhận trong file (chỉ là dòng danh mục tham chiếu)
            if (string.IsNullOrWhiteSpace(qtyStr))
                continue;

            var isValid = true;
            var errorList = new List<string>();

            if (string.IsNullOrWhiteSpace(code))
            {
                isValid = false;
                errorList.Add("Mã vật tư không được để trống");
            }
            else if (!materials.TryGetValue(code.ToUpperInvariant(), out var mat))
            {
                isValid = false;
                errorList.Add($"Mã vật tư '{code}' chưa tồn tại trong danh mục hệ thống");
            }
            else
            {
                if (string.IsNullOrWhiteSpace(name)) name = mat.MaterialName;
                if (string.IsNullOrWhiteSpace(unit)) unit = mat.Unit;
            }

            var hasValidQty = TryParseDecimalFlex(qtyStr, out var qty) && qty > 0;
            if (!hasValidQty)
            {
                isValid = false;
                errorList.Add("Số lượng tồn phải lớn hơn 0");
            }

            TryParseDecimalFlex(priceStr, out var price);
            if (price < 0)
            {
                isValid = false;
                errorList.Add("Đơn giá không được âm");
                price = 0;
            }

            previewItems.Add(new OpeningStockItemPreviewDto(
                MaterialCode: code,
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

        return Ok(new OpeningStockPreviewResponseDto(total, valid, errorCount, totalQty, totalAmount, previewItems));
    }

    [HttpPost("admin/opening-balance/import-excel")]
    public async Task<ActionResult<OpeningStockImportResultDto>> ImportOpeningBalance(
        IFormFile file,
        [FromQuery] string? projectCode,
        CancellationToken ct)
    {
        if (!IsAdminUser())
            return Forbid();

        if (file == null || file.Length == 0)
            return BadRequest("Vui lòng chọn file Excel tồn kho đầu kỳ.");

        using var stream = file.OpenReadStream();
        using var workbook = new XLWorkbook(stream);
        var ws = workbook.Worksheets.FirstOrDefault();
        if (ws is null)
            return BadRequest("File Excel không có sheet nào.");

        Project? project = null;
        if (!string.IsNullOrWhiteSpace(projectCode))
        {
            project = await db.Projects.FirstOrDefaultAsync(p => p.ProjectCode == projectCode && p.DeletedAt == null, ct);
            if (project is null)
                return BadRequest($"Không tìm thấy dự án với mã '{projectCode}'.");
        }

        var materialsList = await db.Materials
            .Where(m => m.DeletedAt == null && !string.IsNullOrWhiteSpace(m.MaterialCode))
            .ToListAsync(ct);
        var materials = materialsList
            .GroupBy(m => m.MaterialCode.Trim().ToUpperInvariant())
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

        var mapping = DetectColumnMapping(ws);
        var parsedItems = new List<(Material Mat, decimal Qty, decimal Price, string? Note)>();
        var errors = new List<string>();
        var lastRow = ws.LastRowUsed()?.RowNumber() ?? 0;

        for (var rowIdx = mapping.HeaderRow + 1; rowIdx <= lastRow; rowIdx++)
        {
            var row = ws.Row(rowIdx);
            var code = mapping.CodeCol > 0 ? GetSafeCellString(row.Cell(mapping.CodeCol)) : string.Empty;
            var name = mapping.NameCol > 0 ? GetSafeCellString(row.Cell(mapping.NameCol)) : string.Empty;
            var qtyStr = mapping.QtyCol > 0 ? GetSafeCellString(row.Cell(mapping.QtyCol)) : string.Empty;
            var priceStr = mapping.PriceCol > 0 ? GetSafeCellString(row.Cell(mapping.PriceCol)) : string.Empty;
            var note = mapping.NoteCol > 0 ? GetSafeCellString(row.Cell(mapping.NoteCol)) : string.Empty;

            // Bỏ qua dòng placeholder công thức dạng F1-F2... nếu không có số lượng tồn
            if (string.IsNullOrWhiteSpace(qtyStr) && (string.IsNullOrWhiteSpace(code) || code.StartsWith("F1-", StringComparison.OrdinalIgnoreCase)))
                continue;

            // Bỏ qua dòng trống cả mã và số lượng
            if (string.IsNullOrWhiteSpace(code) && string.IsNullOrWhiteSpace(qtyStr))
                continue;

            // Bỏ qua dòng tổng cộng
            if (code.Contains("tổng cộng", StringComparison.OrdinalIgnoreCase) ||
                name.Contains("tổng cộng", StringComparison.OrdinalIgnoreCase))
                continue;

            // Bỏ qua các dòng không có số lượng tồn được ghi nhận trong file
            if (string.IsNullOrWhiteSpace(qtyStr))
                continue;

            if (string.IsNullOrWhiteSpace(code))
            {
                errors.Add($"Dòng {rowIdx}: Mã vật tư không được để trống.");
                continue;
            }

            if (!materials.TryGetValue(code.ToUpperInvariant(), out var mat))
            {
                errors.Add($"Dòng {rowIdx}: Mã vật tư '{code}' chưa tồn tại trong danh mục vật tư.");
                continue;
            }

            if (!TryParseDecimalFlex(qtyStr, out var qty) || qty <= 0)
            {
                errors.Add($"Dòng {rowIdx}: Số lượng tồn '{qtyStr}' không hợp lệ (phải > 0).");
                continue;
            }

            TryParseDecimalFlex(priceStr, out var price);
            if (price < 0) price = 0;

            parsedItems.Add((mat, qty, price, string.IsNullOrWhiteSpace(note) ? null : note));
        }

        if (errors.Count > 0)
            return BadRequest(new { message = "File Excel có lỗi dữ liệu, vui lòng kiểm tra lại.", errors });

        if (parsedItems.Count == 0)
            return BadRequest("Không tìm thấy dòng dữ liệu nào hợp lệ trong file Excel.");

        var ticketCode = await TicketCodeAllocator.NextAsync(db, TransactionType.Import, ct);
        var userName = !string.IsNullOrWhiteSpace(currentUser.Name) ? currentUser.Name : "Admin";
        var generalWarehouseId = await inventory.GetGeneralWarehouseIdAsync(ct);

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

        foreach (var item in parsedItems)
        {
            var txItem = new WarehouseTransactionItem
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
            };
            db.WarehouseTransactionItems.Add(txItem);

            if (project == null)
            {
                // Kho tổng
                var inv = await db.WarehouseInventories.FirstOrDefaultAsync(
                    wi => wi.WarehouseId == generalWarehouseId && wi.MaterialCode == item.Mat.MaterialCode,
                    ct);

                if (inv is null)
                {
                    db.WarehouseInventories.Add(new WarehouseInventory
                    {
                        WarehouseId = generalWarehouseId,
                        MaterialCode = item.Mat.MaterialCode,
                        Qty = item.Qty,
                        UpdatedAt = DateTime.UtcNow,
                    });
                }
                else
                {
                    inv.Qty += item.Qty;
                    inv.UpdatedAt = DateTime.UtcNow;
                }
            }
            else
            {
                // Kho dự án
                var pi = await db.ProjectInventories.FirstOrDefaultAsync(
                    p => p.ProjectCode == project.ProjectCode && p.MaterialCode == item.Mat.MaterialCode,
                    ct);

                if (pi is null)
                {
                    db.ProjectInventories.Add(new ProjectInventory
                    {
                        ProjectCode = project.ProjectCode,
                        MaterialCode = item.Mat.MaterialCode,
                        ActualImportQty = item.Qty,
                        UpdatedAt = DateTime.UtcNow,
                    });
                }
                else
                {
                    pi.ActualImportQty += item.Qty;
                    pi.UpdatedAt = DateTime.UtcNow;
                }
            }
        }

        await db.SaveChangesAsync(ct);

        var totalQty = parsedItems.Sum(i => i.Qty);
        var totalAmount = parsedItems.Sum(i => i.Qty * i.Price);

        return Ok(new OpeningStockImportResultDto(
            ticketCode,
            parsedItems.Count,
            totalQty,
            totalAmount,
            $"Nạp thành công {parsedItems.Count} mục vật tư vào {(project != null ? $"kho dự án {project.ProjectCode}" : "kho tổng")}. Mã phiếu: {ticketCode}"));
    }

    #region Helper Methods for Smart Opening Balance Import

    private record OpeningStockColumnMapping(
        int HeaderRow,
        int CodeCol,
        int NameCol,
        int UnitCol,
        int QtyCol,
        int PriceCol,
        int NoteCol);

    private static readonly string[] CodeKeywords =
    [
        "mã hàng hóa", "mahanghoa", "mã vật tư", "mavattu", "mã hàng", "mahang", "mã vt", "sku", "materialcode", "mã"
    ];

    private static readonly string[] PrimaryNameKeywords =
    [
        "tên hàng hóa", "tenhanghoa", "tên vật tư", "tenvattu", "tên hàng", "tenhang", "tên vt", "materialname", "productname", "tên"
    ];

    private static readonly string[] FallbackNameKeywords =
    [
        "diễn giải", "diengiai", "mô tả", "mota", "description"
    ];

    private static readonly string[] QtyKeywords =
    [
        "sl tồn kho", "sltonkho", "sl tồn", "slton", "số lượng tồn", "soluongton", "số lượng", "soluong", "tồn kho", "tonkho", "qty", "quantity", "sl"
    ];

    private static readonly string[] UnitKeywords =
    [
        "đơn vị tính", "donvitinh", "đvt", "dvt", "đơn vị", "donvi", "unit"
    ];

    private static readonly string[] PriceKeywords =
    [
        "đơn giá vốn", "dongiavon", "đơn giá", "dongia", "giá vốn", "giavon", "giá", "price", "unitprice"
    ];

    private static readonly string[] NoteKeywords =
    [
        "ghi chú", "ghichu", "note", "notes", "remark", "remarks"
    ];

    private static string NormalizeHeader(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return string.Empty;
        var s = text.Trim().ToLowerInvariant()
            .Replace('đ', 'd')
            .Replace('Đ', 'd');
        s = s.Normalize(NormalizationForm.FormD);
        var chars = s.Where(c => CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark).ToArray();
        s = new string(chars).Normalize(NormalizationForm.FormC);
        return new string(s.Where(c => char.IsLetterOrDigit(c) || char.IsWhiteSpace(c)).ToArray()).Trim();
    }

    private static bool MatchHeaderKeyword(string norm, string[] keywords)
    {
        var normNoSpace = norm.Replace(" ", "");
        foreach (var kw in keywords)
        {
            var cleanKw = NormalizeHeader(kw);
            var cleanKwNoSpace = cleanKw.Replace(" ", "");
            if (string.IsNullOrEmpty(cleanKw)) continue;

            if (cleanKw.Length <= 3)
            {
                if (norm == cleanKw || normNoSpace == cleanKwNoSpace)
                    return true;

                var words = norm.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                if (words.Any(w => w == cleanKw))
                    return true;
            }
            else
            {
                if (norm.Contains(cleanKw, StringComparison.OrdinalIgnoreCase) ||
                    normNoSpace.Contains(cleanKwNoSpace, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
        }
        return false;
    }

    private static OpeningStockColumnMapping DetectColumnMapping(IXLWorksheet ws)
    {
        var maxScanRow = Math.Min(15, ws.LastRowUsed()?.RowNumber() ?? 1);

        for (var row = 1; row <= maxScanRow; row++)
        {
            var lastCol = ws.Row(row).LastCellUsed()?.Address.ColumnNumber ?? 0;
            if (lastCol == 0) continue;

            int codeCol = 0, nameCol = 0, unitCol = 0, qtyCol = 0, priceCol = 0, noteCol = 0;
            int fallbackNameCol = 0;

            for (var col = 1; col <= lastCol; col++)
            {
                var text = GetSafeCellString(ws.Cell(row, col));
                if (string.IsNullOrWhiteSpace(text)) continue;

                var norm = NormalizeHeader(text);
                if (string.IsNullOrWhiteSpace(norm)) continue;

                // 1. Qty match (ưu tiên nhận diện cột SL trước)
                if (qtyCol == 0 && MatchHeaderKeyword(norm, QtyKeywords))
                {
                    qtyCol = col;
                    continue;
                }

                // 2. Price match
                if (priceCol == 0 && MatchHeaderKeyword(norm, PriceKeywords))
                {
                    priceCol = col;
                    continue;
                }

                // 3. Unit match
                if (unitCol == 0 && MatchHeaderKeyword(norm, UnitKeywords))
                {
                    unitCol = col;
                    continue;
                }

                // 4. Code match
                if (codeCol == 0 && MatchHeaderKeyword(norm, CodeKeywords))
                {
                    codeCol = col;
                    continue;
                }

                // 5. Primary Name match
                if (nameCol == 0 && MatchHeaderKeyword(norm, PrimaryNameKeywords))
                {
                    nameCol = col;
                    continue;
                }

                // Fallback Name match (mô tả, diễn giải)
                if (fallbackNameCol == 0 && MatchHeaderKeyword(norm, FallbackNameKeywords))
                {
                    fallbackNameCol = col;
                    continue;
                }

                // 6. Note match
                if (noteCol == 0 && MatchHeaderKeyword(norm, NoteKeywords))
                {
                    noteCol = col;
                    continue;
                }
            }

            if (nameCol == 0 && fallbackNameCol > 0)
            {
                nameCol = fallbackNameCol;
            }

            // Dòng header hợp lệ bắt buộc phải có ít nhất cột Mã VT và Số lượng tồn
            if (codeCol > 0 && qtyCol > 0)
            {
                return new OpeningStockColumnMapping(row, codeCol, nameCol, unitCol, qtyCol, priceCol, noteCol);
            }
        }

        // Fallback mặc định cho file mẫu 6 cột
        return new OpeningStockColumnMapping(
            HeaderRow: 1,
            CodeCol: 1,
            NameCol: 2,
            UnitCol: 3,
            QtyCol: 4,
            PriceCol: 5,
            NoteCol: 6);
    }

    private static string GetSafeCellString(IXLCell? cell)
    {
        if (cell == null || cell.IsEmpty()) return string.Empty;
        try
        {
            if (cell.HasFormula)
            {
                var cached = cell.CachedValue.ToString()?.Trim() ?? string.Empty;
                if (!string.IsNullOrEmpty(cached)) return cached;
            }
            return cell.GetFormattedString().Trim();
        }
        catch
        {
            try
            {
                return cell.Value.ToString()?.Trim() ?? string.Empty;
            }
            catch
            {
                try
                {
                    return cell.CachedValue.ToString()?.Trim() ?? string.Empty;
                }
                catch
                {
                    return string.Empty;
                }
            }
        }
    }

    private static bool TryParseDecimalFlex(string? raw, out decimal result)
    {
        result = 0m;
        if (string.IsNullOrWhiteSpace(raw)) return false;
        var s = raw.Trim();

        // 1. Direct Invariant Parse
        if (decimal.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out result))
            return true;

        // 2. Vietnamese culture parse
        var vn = CultureInfo.GetCultureInfo("vi-VN");
        if (decimal.TryParse(s, NumberStyles.Any, vn, out result))
            return true;

        // 3. Normalized string handling (loại bỏ khoảng trắng, đổi dấu phân cách)
        var cleaned = s.Replace(" ", "").Replace("\u00A0", "");
        if (cleaned.Contains(',') && cleaned.Contains('.'))
        {
            if (cleaned.IndexOf(',') < cleaned.IndexOf('.'))
                cleaned = cleaned.Replace(",", "");
            else
                cleaned = cleaned.Replace(".", "").Replace(",", ".");
        }
        else if (cleaned.Contains(','))
        {
            cleaned = cleaned.Replace(',', '.');
        }

        return decimal.TryParse(cleaned, NumberStyles.Any, CultureInfo.InvariantCulture, out result);
    }

    #endregion

    private bool IsAdminUser() =>
        currentUser.IsInRole("Admin")
        || currentUser.Roles.Any(r => r.Equals("admin", StringComparison.OrdinalIgnoreCase))
        || currentUser.HasPermission("admin.users.view:all")
        || User.IsInRole("Admin")
        || User.HasClaim("role", "Admin");

    private static (string Project, string Material) MatrixKey(string projectCode, string materialCode) =>
        (projectCode.ToUpperInvariant(), materialCode.ToUpperInvariant());
}
