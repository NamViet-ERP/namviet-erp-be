using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using ClosedXML.Excel;
using NamViet.ERP.Api.Application.Dtos.Purchasing;
using NamViet.ERP.Modules.Warehouse.Application.Catalog;

namespace NamViet.ERP.Api.Application.Services.Purchasing;

public interface IPurchasePlanImportService
{
    PurchasePlanImportPreviewDto Preview(Stream excelStream, string projectCode);
    byte[] BuildImportTemplate();
}

public sealed class PurchasePlanImportService : IPurchasePlanImportService
{
    private static readonly (string Field, string[] Aliases)[] ColumnAliases =
    [
        ("SystemName", ["hệ", "he", "system", "systemname"]),
        ("Category", ["danh mục", "danh muc", "nhóm", "nhom", "loại", "loai", "category", "itemtype"]),
        ("MaterialCode", ["mã vật tư", "ma vat tu", "materialcode", "material code", "mã vt", "ma vt", "sku"]),
        ("ProductName", ["tên sản phẩm", "ten san pham", "tên hàng", "ten hang", "tên vật tư", "sản phẩm", "san pham", "productname", "item", "hàng hóa"]),
        ("Spec", ["quy cách", "quy cach", "spec", "specification", "mô tả", "mo ta"]),
        ("Unit", ["đvt", "dvt", "đơn vị", "don vi", "unit"]),
        ("Quantity", ["sl", "số lượng", "so luong", "quantity", "qty"]),
        ("KgPerUnit", ["kg/đvt", "kg/dvt", "kg per unit", "kgperunit", "trọng lượng"]),
        ("LineNote", ["ghi chú dòng", "ghi chu dong", "ghi chú", "ghi chu", "linenote", "note dòng"]),
        ("PurchaseStatus", ["tình trạng", "tinh trang", "tt mua", "trạng thái mua", "purchase status", "purchasestatus"]),
        ("StatusNote", ["ghi chú tt", "statusnote", "ghi chú trạng thái"]),
        ("BudgetUnitPrice", ["đơn giá dự toán", "don gia du toan", "budget unit price", "budgetunitprice"]),
        ("BudgetAmount", ["thành tiền dự toán", "thanh tien du toan", "budget amount", "budgetamount"]),
        ("ProposalKgPerUnit", ["kg đề xuất", "proposal kg", "proposalkgperunit"]),
        ("ProposalUnitPrice", ["đơn giá đề xuất", "don gia de xuat", "proposal unit price", "proposalunitprice"]),
        ("ProposalAmount", ["thành tiền đề xuất", "thanh tien de xuat", "proposal amount", "proposalamount"]),
        ("ProposalSupplierName", ["ncc đề xuất", "ncc de xuat", "proposal supplier", "proposalsuppliername"]),
        ("ProposalNote", ["ghi chú đề xuất", "ghi chu de xuat", "proposal note", "proposalnote"]),
        ("ProposalStatus", ["tt đề xuất", "proposal status", "proposalstatus"]),
        ("RequiredDate", ["ngày cần", "ngay can", "required date", "requireddate", "need by"]),
        ("OrderedDate", ["ngày đặt", "ngay dat", "ordered date", "ordereddate"]),
        ("ExpectedDeliveryDate", ["ngày giao dự kiến", "ngay giao du kien", "expected delivery", "expecteddeliverydate", "ecd"]),
        ("ActualReceiptDate", ["ngày nhận", "ngay nhan", "actual receipt", "actualreceiptdate"]),
        ("DeliveryNote", ["ghi chú giao", "delivery note", "deliverynote"]),
        ("PurchaseKgPerUnit", ["kg mua", "purchase kg", "purchasekgperunit"]),
        ("PurchaseUnitPrice", ["đơn giá mua", "don gia mua", "purchase unit price", "purchaseunitprice"]),
        ("PurchaseAmount", ["thành tiền mua", "thanh tien mua", "purchase amount", "purchaseamount"]),
        ("PurchaseSupplierName", ["ncc mua", "purchase supplier", "purchasesuppliername"]),
        ("PurchaseNote", ["ghi chú mua", "purchase note", "purchasenote"]),
        ("SelectedUnitPrice", ["đơn giá chọn", "gia chon", "giá chọn", "selected unit price", "selectedunitprice"]),
        ("SelectedAmount", ["thành tiền chọn", "selected amount", "selectedamount"]),
        ("SelectedSupplierName", ["ncc chọn", "selected supplier", "selectedsuppliername"]),
        ("SelectionReason", ["lý do chọn", "ly do chon", "selection reason", "selectionreason"]),
    ];

    private static readonly string[] QuoteGroupMarkers = ["báo giá", "bao gia", "quote", "ncc báo giá", "ncc bao gia"];

    /// <summary>
    /// Matches business Excel: group row + short headers (ĐƠN GIÁ under DỰ TOÁN / ĐỀ XUẤT…).
    /// </summary>
    private static readonly (string Group, string Header, string Field)[] TemplateColumns =
    [
        ("", "STT", "LineNo"),
        ("", "HỆ", "SystemName"),
        ("", "DANH MỤC", "Category"),
        ("", "MÃ VẬT TƯ", "MaterialCode"),
        ("", "TÊN SẢN PHẨM", "ProductName"),
        ("", "QUY CÁCH", "Spec"),
        ("", "ĐVT", "Unit"),
        ("", "SỐ LƯỢNG", "Quantity"),
        ("", "KG/ĐVT", "KgPerUnit"),
        ("DỰ TOÁN", "ĐƠN GIÁ", "BudgetUnitPrice"),
        ("DỰ TOÁN", "THÀNH TIỀN", "BudgetAmount"),
        ("TÌNH TRẠNG MUA HÀNG", "TÌNH TRẠNG", "PurchaseStatus"),
        ("ĐỀ XUẤT", "KG/ĐVT", "ProposalKgPerUnit"),
        ("ĐỀ XUẤT", "ĐƠN GIÁ", "ProposalUnitPrice"),
        ("ĐỀ XUẤT", "THÀNH TIỀN", "ProposalAmount"),
        ("ĐỀ XUẤT", "NCC", "ProposalSupplierName"),
        ("ĐỀ XUẤT", "GHI CHÚ", "ProposalNote"),
        ("GIAO HÀNG", "NGÀY CẦN HÀNG", "RequiredDate"),
        ("GIAO HÀNG", "NGÀY ĐẶT HÀNG", "OrderedDate"),
        ("GIAO HÀNG", "NGÀY GIAO HÀNG DỰ KIẾN", "ExpectedDeliveryDate"),
        ("GIAO HÀNG", "NGÀY NHẬN HÀNG THỰC TẾ", "ActualReceiptDate"),
        ("GIAO HÀNG", "GHI CHÚ", "DeliveryNote"),
        ("MUA HÀNG", "KG/ĐVT", "PurchaseKgPerUnit"),
        ("MUA HÀNG", "ĐƠN GIÁ", "PurchaseUnitPrice"),
        ("MUA HÀNG", "THÀNH TIỀN", "PurchaseAmount"),
        ("MUA HÀNG", "NCC", "PurchaseSupplierName"),
        ("MUA HÀNG", "GHI CHÚ", "PurchaseNote"),
        ("NCC CHỌN", "GIÁ CHỌN", "SelectedUnitPrice"),
        ("NCC CHỌN", "THÀNH TIỀN CHỌN", "SelectedAmount"),
        ("NCC CHỌN", "NCC CHỌN", "SelectedSupplierName"),
        ("NCC CHỌN", "LÝ DO CHỌN", "SelectionReason"),
    ];

    private static readonly XLColor GroupHeaderFill = XLColor.FromHtml("#F8CBAD");
    private static readonly XLColor GroupHeaderFont = XLColor.FromHtml("#0070C0");
    private static readonly XLColor ColumnHeaderFill = XLColor.FromHtml("#002060");
    private static readonly XLColor ColumnHeaderFont = XLColor.FromHtml("#FFC000");
    private static readonly XLColor AccentRed = XLColor.FromHtml("#C00000");
    private static readonly XLColor FillHe = XLColor.FromHtml("#FFF2CC");
    private static readonly XLColor FillStatus = XLColor.FromHtml("#DDEBF7");
    private static readonly XLColor FillBudget = XLColor.FromHtml("#E2EFDA");
    private static readonly XLColor FillProposal = XLColor.FromHtml("#FCE4D6");
    private static readonly XLColor FillDelivery = XLColor.FromHtml("#DEEBF7");
    private static readonly XLColor FillPurchase = XLColor.FromHtml("#EDEDED");
    private static readonly XLColor FillSelected = XLColor.FromHtml("#FFF2CC");

    public byte[] BuildImportTemplate()
    {
        using var workbook = new XLWorkbook();
        var sheet = workbook.Worksheets.Add("KHMH");
        const int groupRow = 1;
        const int headerRow = 2;
        const int firstDataRow = 3;
        const int sampleRowCount = 50;
        var lastDataRow = firstDataRow + sampleRowCount - 1;
        var colCount = TemplateColumns.Length;

        for (var i = 0; i < colCount; i++)
        {
            var col = i + 1;
            var (group, header, _) = TemplateColumns[i];
            sheet.Cell(groupRow, col).Value = group;
            sheet.Cell(headerRow, col).Value = header;
        }

        for (var i = 0; i < colCount; )
        {
            var group = TemplateColumns[i].Group;
            var startCol = i + 1;
            var j = i + 1;
            while (j < colCount && TemplateColumns[j].Group == group)
                j++;
            if (j > startCol)
                sheet.Range(groupRow, startCol, groupRow, j).Merge();
            i = j;
        }

        StyleGroupHeaderRow(sheet, groupRow, colCount);
        StyleColumnHeaderRow(sheet, headerRow, colCount);

        for (var i = 0; i < colCount; i++)
        {
            if (!string.IsNullOrEmpty(TemplateColumns[i].Group)) continue;
            var cell = sheet.Cell(groupRow, i + 1);
            cell.Value = string.Empty;
            cell.Style.Fill.BackgroundColor = XLColor.White;
            cell.Style.Font.FontColor = XLColor.Black;
        }

        for (var i = 0; i < colCount; i++)
        {
            sheet.Cell(headerRow, i + 1).Style.Font.FontColor = string.IsNullOrEmpty(TemplateColumns[i].Group)
                ? XLColor.White
                : ColumnHeaderFont;
        }

        WriteSampleRows(sheet, firstDataRow);
        StyleDataArea(sheet, firstDataRow, lastDataRow, colCount);
        ApplyGroupSeparators(sheet, groupRow, lastDataRow, colCount);

        sheet.SheetView.FreezeRows(headerRow);
        sheet.Columns().AdjustToContents(groupRow, lastDataRow, 8.0, 28.0);
        sheet.Row(groupRow).Height = 22;
        sheet.Row(headerRow).Height = 40;

        using var ms = new MemoryStream();
        workbook.SaveAs(ms);
        return ms.ToArray();
    }

    private static void WriteSampleRows(IXLWorksheet sheet, int firstDataRow)
    {
        var samples = KhmhDemoMaterialCatalog.All;

        for (var i = 0; i < samples.Count; i++)
        {
            var row = firstDataRow + i;
            var s = samples[i];
            var budgetAmount = Math.Round(s.Qty * s.KgPerUnit * s.BudgetUnitPrice, 0);
            var proposalAmount = Math.Round(s.Qty * s.KgPerUnit * s.ProposalUnitPrice, 0);
            var purchasePrice = Math.Round(s.ProposalUnitPrice * 0.99m, 0);
            var purchaseAmount = Math.Round(s.Qty * s.KgPerUnit * purchasePrice, 0);

            sheet.Cell(row, 1).Value = i + 1;
            sheet.Cell(row, 2).Value = s.SystemName;
            sheet.Cell(row, 3).Value = s.Category;
            sheet.Cell(row, 4).Value = s.MaterialCode;
            sheet.Cell(row, 5).Value = s.ProductName;
            sheet.Cell(row, 6).Value = s.Spec;
            sheet.Cell(row, 7).Value = s.Unit;
            sheet.Cell(row, 8).Value = s.Qty;
            sheet.Cell(row, 9).Value = s.KgPerUnit;
            sheet.Cell(row, 10).Value = s.BudgetUnitPrice;
            sheet.Cell(row, 11).Value = budgetAmount;
            sheet.Cell(row, 12).Value = s.PurchaseStatus;
            sheet.Cell(row, 13).Value = s.KgPerUnit;
            sheet.Cell(row, 14).Value = s.ProposalUnitPrice;
            sheet.Cell(row, 15).Value = proposalAmount;
            sheet.Cell(row, 16).Value = s.ProposalSupplier;
            sheet.Cell(row, 17).Value = s.ProposalNote;
            sheet.Cell(row, 18).Value = new DateTime(2026, 5, 27);
            sheet.Cell(row, 19).Value = new DateTime(2026, 5, 20);
            sheet.Cell(row, 20).Value = new DateTime(2026, 5, 25);
            sheet.Cell(row, 21).Value = new DateTime(2026, 5, 25);
            sheet.Cell(row, 22).Value = string.Empty;
            sheet.Cell(row, 23).Value = Math.Round(s.KgPerUnit * 0.995m, 2);
            sheet.Cell(row, 24).Value = purchasePrice;
            sheet.Cell(row, 25).Value = purchaseAmount;
            sheet.Cell(row, 26).Value = s.PurchaseSupplier;
            sheet.Cell(row, 27).Value = string.Empty;
            sheet.Cell(row, 28).Value = s.ProposalUnitPrice;
            sheet.Cell(row, 29).Value = proposalAmount;
            sheet.Cell(row, 30).Value = s.SelectedSupplier;
            sheet.Cell(row, 31).Value = s.SelectionReason;

            for (var c = 18; c <= 21; c++)
                sheet.Cell(row, c).Style.DateFormat.Format = "dd-mm-yyyy";
        }
    }

    private static void StyleDataArea(IXLWorksheet sheet, int firstRow, int lastRow, int colCount)
    {
        var range = sheet.Range(firstRow, 1, lastRow, colCount);
        range.Style.Font.FontSize = 10;
        range.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
        range.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
        range.Style.Border.OutsideBorderColor = ColumnHeaderFill;
        range.Style.Border.InsideBorder = XLBorderStyleValues.Hair;
        range.Style.Border.InsideBorderColor = XLColor.FromHtml("#7F7F7F");

        for (var row = firstRow; row <= lastRow; row++)
        {
            var rowRange = sheet.Range(row, 1, row, colCount);
            rowRange.Style.Border.BottomBorder = XLBorderStyleValues.Dashed;
            rowRange.Style.Border.BottomBorderColor = ColumnHeaderFill;
            rowRange.Style.Border.TopBorder = XLBorderStyleValues.Dashed;
            rowRange.Style.Border.TopBorderColor = ColumnHeaderFill;
        }

        ApplyColumnFill(sheet, firstRow, lastRow, 2, FillHe);
        ApplyColumnFill(sheet, firstRow, lastRow, 10, FillBudget);
        ApplyColumnFill(sheet, firstRow, lastRow, 11, FillBudget);
        ApplyColumnFill(sheet, firstRow, lastRow, 12, FillStatus);
        for (var c = 13; c <= 17; c++)
            ApplyColumnFill(sheet, firstRow, lastRow, c, FillProposal);
        for (var c = 18; c <= 22; c++)
            ApplyColumnFill(sheet, firstRow, lastRow, c, FillDelivery);
        for (var c = 23; c <= 27; c++)
            ApplyColumnFill(sheet, firstRow, lastRow, c, FillPurchase);
        for (var c = 28; c <= 31; c++)
            ApplyColumnFill(sheet, firstRow, lastRow, c, FillSelected);

        // Default: text left, money right. Center only columns from user screenshots.
        var leftCols = new[] { 3, 5, 6, 17, 22, 27 }; // danh mục, tên SP, quy cách, ghi chú
        var rightCols = new[] { 10, 11, 14, 15, 24, 25, 28, 29 }; // đơn giá / thành tiền
        var centerCols = new[]
        {
            1, 2, 4,       // STT, HỆ, MÃ VẬT TƯ
            7, 8, 9,        // ĐVT, SỐ LƯỢNG, KG/ĐVT
            12,             // TÌNH TRẠNG
            13, 23,         // KG/ĐVT đề xuất / mua hàng
            16, 26,         // NCC
            18, 19, 20, 21, // ngày giao hàng
            30, 31,         // NCC CHỌN, LÝ DO CHỌN
        };

        var integerCols = new[] { 1, 8, 10, 11, 14, 15, 24, 25, 28, 29 };
        var kgCols = new[] { 9, 13, 23 }; // KG/ĐVT — format per value (no trailing ".")

        for (var row = firstRow; row <= lastRow; row++)
        {
            foreach (var c in leftCols)
                sheet.Cell(row, c).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Left;
            foreach (var c in rightCols)
                sheet.Cell(row, c).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;
            foreach (var c in centerCols)
                sheet.Cell(row, c).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

            foreach (var c in integerCols)
                sheet.Cell(row, c).Style.NumberFormat.Format = "#,##0";

            foreach (var c in kgCols)
                ApplyKgNumberFormat(sheet.Cell(row, c));

            sheet.Cell(row, 5).Style.Alignment.WrapText = true;
            sheet.Cell(row, 6).Style.Alignment.WrapText = true;
            sheet.Cell(row, 17).Style.Alignment.WrapText = true;
            sheet.Cell(row, 31).Style.Alignment.WrapText = true;

            sheet.Cell(row, 11).Style.Font.FontColor = AccentRed;
            sheet.Cell(row, 11).Style.Font.Bold = true;
            sheet.Cell(row, 16).Style.Font.FontColor = AccentRed;
            sheet.Cell(row, 16).Style.Font.Bold = true;
            sheet.Cell(row, 21).Style.Font.FontColor = AccentRed;
            sheet.Cell(row, 26).Style.Font.FontColor = AccentRed;
            sheet.Cell(row, 26).Style.Font.Bold = true;
            sheet.Cell(row, 30).Style.Font.FontColor = AccentRed;
            sheet.Cell(row, 30).Style.Font.Bold = true;
        }
    }

    /// <summary>
    /// Avoid Excel showing "2,651." — optional-decimal formats still leave a trailing point for integers.
    /// </summary>
    private static void ApplyKgNumberFormat(IXLCell cell)
    {
        if (cell.TryGetValue(out double n))
        {
            var isWhole = Math.Abs(n - Math.Truncate(n)) < 0.0000001;
            cell.Style.NumberFormat.Format = isWhole ? "#,##0" : "#,##0.00";
            return;
        }

        cell.Style.NumberFormat.Format = "#,##0.##";
    }

    private static void ApplyColumnFill(IXLWorksheet sheet, int firstRow, int lastRow, int col, XLColor fill)
    {
        sheet.Range(firstRow, col, lastRow, col).Style.Fill.BackgroundColor = fill;
    }

    /// <summary>Thick navy vertical borders between major header groups (matches business Excel).</summary>
    private static void ApplyGroupSeparators(IXLWorksheet sheet, int firstRow, int lastRow, int colCount)
    {
        var separatorAfterCols = new HashSet<int>();
        for (var i = 0; i < colCount - 1; i++)
        {
            if (TemplateColumns[i].Group != TemplateColumns[i + 1].Group)
                separatorAfterCols.Add(i + 1);
        }

        foreach (var col in separatorAfterCols)
        {
            for (var row = firstRow; row <= lastRow; row++)
            {
                var cell = sheet.Cell(row, col);
                cell.Style.Border.RightBorder = XLBorderStyleValues.Medium;
                cell.Style.Border.RightBorderColor = ColumnHeaderFill;
                var next = sheet.Cell(row, col + 1);
                next.Style.Border.LeftBorder = XLBorderStyleValues.Medium;
                next.Style.Border.LeftBorderColor = ColumnHeaderFill;
            }
        }

        // Outer frame on header + data
        sheet.Range(firstRow, 1, lastRow, colCount).Style.Border.OutsideBorder = XLBorderStyleValues.Medium;
        sheet.Range(firstRow, 1, lastRow, colCount).Style.Border.OutsideBorderColor = ColumnHeaderFill;
    }

    private static void StyleGroupHeaderRow(IXLWorksheet sheet, int row, int colCount)
    {
        var range = sheet.Range(row, 1, row, colCount);
        range.Style.Fill.BackgroundColor = GroupHeaderFill;
        range.Style.Font.Bold = true;
        range.Style.Font.FontColor = GroupHeaderFont;
        range.Style.Font.FontSize = 11;
        range.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
        range.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
        range.Style.Alignment.WrapText = true;
        range.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
        range.Style.Border.InsideBorder = XLBorderStyleValues.Thin;
        range.Style.Border.OutsideBorderColor = ColumnHeaderFill;
        range.Style.Border.InsideBorderColor = ColumnHeaderFill;
    }

    private static void StyleColumnHeaderRow(IXLWorksheet sheet, int row, int colCount)
    {
        var range = sheet.Range(row, 1, row, colCount);
        range.Style.Fill.BackgroundColor = ColumnHeaderFill;
        range.Style.Font.Bold = true;
        range.Style.Font.FontColor = ColumnHeaderFont;
        range.Style.Font.FontSize = 11;
        range.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
        range.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
        range.Style.Alignment.WrapText = true;
        range.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
        range.Style.Border.InsideBorder = XLBorderStyleValues.Thin;
        range.Style.Border.OutsideBorderColor = XLColor.Black;
        range.Style.Border.InsideBorderColor = XLColor.FromHtml("#2E75B6");
    }

    public PurchasePlanImportPreviewDto Preview(Stream excelStream, string projectCode)
    {
        if (string.IsNullOrWhiteSpace(projectCode))
            throw new PurchasingConflictException("ProjectCode là bắt buộc khi import KHMH.");

        using var workbook = new XLWorkbook(excelStream);
        var sheet = FindKhmhSheet(workbook)
            ?? throw new PurchasingConflictException("Không tìm thấy sheet KHMH / KHMH (n) trong file Excel.");

        var header = DetectHeader(sheet);
        if (header is null)
            throw new PurchasingConflictException("Không phát hiện được dòng tiêu đề cột trên sheet KHMH.");

        var (fieldMap, quoteGroups, mappedColumns) = BuildColumnMap(sheet, header.Value);
        if (!fieldMap.ContainsKey("MaterialCode"))
            throw new PurchasingConflictException("Thiếu cột bắt buộc (Mã vật tư) trên sheet KHMH.");
        if (!fieldMap.ContainsKey("Quantity"))
            throw new PurchasingConflictException("Thiếu cột bắt buộc (Số lượng) trên sheet KHMH.");

        var lines = new List<PurchasePlanImportLinePreviewDto>();
        var errors = new List<PurchasePlanImportErrorDto>();
        var lineNo = 0;

        for (var row = header.Value + 1; row <= sheet.LastRowUsed()?.RowNumber(); row++)
        {
            if (IsBlankRow(sheet, row, fieldMap, quoteGroups))
                continue;

            lineNo++;
            var preview = ParseRow(sheet, row, lineNo, fieldMap, quoteGroups, errors);
            lines.Add(preview);
        }

        var valid = lines.Count(l => l.IsValid);
        return new PurchasePlanImportPreviewDto(
            projectCode.Trim(),
            sheet.Name,
            lines.Count,
            valid,
            lines.Count - valid,
            mappedColumns,
            lines,
            errors);
    }

    public static IXLWorksheet? FindKhmhSheet(XLWorkbook workbook)
    {
        static bool IsKhmh(string name)
        {
            var n = name.Trim();
            return Regex.IsMatch(n, @"^KHMH(\s*\(\d+\))?$", RegexOptions.IgnoreCase);
        }

        return workbook.Worksheets.FirstOrDefault(ws => IsKhmh(ws.Name))
            ?? workbook.Worksheets.FirstOrDefault(ws =>
                ws.Name.Contains("KHMH", StringComparison.OrdinalIgnoreCase));
    }

    public static int? DetectHeader(IXLWorksheet sheet)
    {
        var last = sheet.LastRowUsed()?.RowNumber() ?? 0;
        var scanTo = Math.Min(last, 30);
        for (var r = 1; r <= scanTo; r++)
        {
            var hits = 0;
            foreach (var cell in sheet.Row(r).CellsUsed())
            {
                var norm = Normalize(cell.GetString());
                if (string.IsNullOrEmpty(norm)) continue;
                if (ColumnAliases.Any(a => a.Aliases.Any(alias =>
                        norm.Contains(Normalize(alias), StringComparison.Ordinal))))
                    hits++;
            }

            if (hits >= 3)
                return r;
        }

        return null;
    }

    private static (Dictionary<string, int> FieldMap, List<QuoteGroupColumns> QuoteGroups, List<PurchasePlanImportColumnMapDto> Mapped)
        BuildColumnMap(IXLWorksheet sheet, int headerRow)
    {
        var fieldMap = new Dictionary<string, int>(StringComparer.Ordinal);
        var mapped = new List<PurchasePlanImportColumnMapDto>();
        var quoteGroups = new List<QuoteGroupColumns>();
        QuoteGroupColumns? currentQuote = null;
        var quoteSort = 0;
        var currentQuoteGroupNorm = string.Empty;
        var groupRow = headerRow > 1 ? headerRow - 1 : 0;

        var lastCol = sheet.Row(headerRow).LastCellUsed()?.Address.ColumnNumber ?? 0;
        for (var col = 1; col <= lastCol; col++)
        {
            var header = sheet.Cell(headerRow, col).GetString().Trim();
            if (string.IsNullOrWhiteSpace(header))
            {
                currentQuote = null;
                currentQuoteGroupNorm = string.Empty;
                continue;
            }

            var group = groupRow > 0 ? GetMergedCellText(sheet, groupRow, col) : string.Empty;
            var norm = Normalize(header);
            var groupNorm = Normalize(group);
            var headerIsQuoteMarker = QuoteGroupMarkers.Any(m => norm.Contains(m, StringComparison.Ordinal));

            if (headerIsQuoteMarker)
            {
                quoteSort++;
                currentQuote = new QuoteGroupColumns { SortOrder = quoteSort };
                currentQuoteGroupNorm = groupNorm;
                quoteGroups.Add(currentQuote);
                mapped.Add(new PurchasePlanImportColumnMapDto(
                    $"Quote{quoteSort}",
                    string.IsNullOrEmpty(group) ? header : group,
                    col));
                continue;
            }

            if (IsQuoteGroup(groupNorm))
            {
                if (currentQuote is null || currentQuoteGroupNorm != groupNorm)
                {
                    quoteSort++;
                    currentQuote = new QuoteGroupColumns { SortOrder = quoteSort };
                    currentQuoteGroupNorm = groupNorm;
                    quoteGroups.Add(currentQuote);
                    mapped.Add(new PurchasePlanImportColumnMapDto($"Quote{quoteSort}", group, col));
                }

                MapQuoteSubColumn(currentQuote, norm, col);
                continue;
            }

            // Flat layout: columns after "Báo giá N" marker until blank separator
            if (currentQuote is not null && IsQuoteSubHeader(norm))
            {
                MapQuoteSubColumn(currentQuote, norm, col);
                continue;
            }

            currentQuote = null;
            currentQuoteGroupNorm = string.Empty;

            var field = ResolveField(groupNorm, norm);
            if (field is null) continue;
            if (fieldMap.ContainsKey(field)) continue;
            fieldMap[field] = col;
            mapped.Add(new PurchasePlanImportColumnMapDto(
                field,
                string.IsNullOrEmpty(group) ? header : $"{group} / {header}",
                col));
        }

        if (quoteGroups.Count == 0)
            quoteGroups.AddRange(DetectAnonymousQuoteGroups(sheet, headerRow, fieldMap));

        return (fieldMap, quoteGroups, mapped);
    }

    private static bool IsQuoteGroup(string groupNorm) =>
        QuoteGroupMarkers.Any(m => groupNorm.Contains(m, StringComparison.Ordinal));

    private static bool IsQuoteSubHeader(string norm) =>
        MatchAny(norm, ["kg", "kg/đvt", "kg/dvt", "kgperunit"]) ||
        MatchAny(norm, ["đơn giá", "don gia", "unitprice", "unit price", "giá", "gia"]) ||
        MatchAny(norm, ["thành tiền", "thanh tien", "amount"]) ||
        MatchAny(norm, ["ncc", "supplier", "nhà cung cấp", "nha cung cap"]) ||
        MatchAny(norm, ["ghi chú", "ghi chu", "note"]) ||
        MatchAny(norm, ["status", "trạng thái", "trang thai", "tình trạng", "tinh trang"]);

    private static void MapQuoteSubColumn(QuoteGroupColumns quote, string norm, int col)
    {
        if (MatchAny(norm, ["kg", "kg/đvt", "kg/dvt", "kgperunit"]))
            quote.KgPerUnitCol ??= col;
        else if (MatchAny(norm, ["đơn giá", "don gia", "unitprice", "unit price"]) ||
                 (MatchAny(norm, ["giá", "gia"]) && !MatchAny(norm, ["giá chọn", "gia chon"])))
            quote.UnitPriceCol ??= col;
        else if (MatchAny(norm, ["thành tiền", "thanh tien", "amount"]) &&
                 !MatchAny(norm, ["thành tiền chọn", "thanh tien chon"]))
            quote.AmountCol ??= col;
        else if (MatchAny(norm, ["ncc", "supplier", "nhà cung cấp", "nha cung cap"]) &&
                 !MatchAny(norm, ["ncc chọn", "ncc chon"]))
            quote.SupplierNameCol ??= col;
        else if (MatchAny(norm, ["ghi chú", "ghi chu", "note"]))
            quote.NoteCol ??= col;
        else if (MatchAny(norm, ["status", "trạng thái", "trang thai", "tình trạng", "tinh trang"]))
            quote.StatusCol ??= col;
    }

    /// <summary>Map short Excel headers using group context (ĐƠN GIÁ under DỰ TOÁN ≠ under ĐỀ XUẤT).</summary>
    private static string? ResolveField(string groupNorm, string headerNorm)
    {
        foreach (var (group, header, field) in TemplateColumns)
        {
            if (field.StartsWith("Quote", StringComparison.Ordinal) || field == "LineNo")
                continue;
            if (Normalize(header) != headerNorm) continue;
            if (Normalize(group) != groupNorm) continue;
            return field;
        }

        if (groupNorm.Contains("du toan"))
        {
            if (MatchAny(headerNorm, ["đơn giá", "don gia"])) return "BudgetUnitPrice";
            if (MatchAny(headerNorm, ["thành tiền", "thanh tien"])) return "BudgetAmount";
        }

        if (groupNorm.Contains("tinh trang mua"))
        {
            if (MatchAny(headerNorm, ["tình trạng", "tinh trang"])) return "PurchaseStatus";
        }

        if (groupNorm.Contains("de xuat"))
        {
            if (MatchAny(headerNorm, ["kg/đvt", "kg/dvt"]) || headerNorm == "kg") return "ProposalKgPerUnit";
            if (MatchAny(headerNorm, ["đơn giá", "don gia"])) return "ProposalUnitPrice";
            if (MatchAny(headerNorm, ["thành tiền", "thanh tien"])) return "ProposalAmount";
            if (MatchAny(headerNorm, ["ncc", "supplier"])) return "ProposalSupplierName";
            if (MatchAny(headerNorm, ["ghi chú", "ghi chu", "note"])) return "ProposalNote";
            if (MatchAny(headerNorm, ["tình trạng", "tinh trang", "status"])) return "ProposalStatus";
        }

        if (groupNorm.Contains("giao hang"))
        {
            if (MatchAny(headerNorm, ["ngay can", "can hang"])) return "RequiredDate";
            if (MatchAny(headerNorm, ["ngay dat", "dat hang"])) return "OrderedDate";
            if (MatchAny(headerNorm, ["ngay giao", "du kien"])) return "ExpectedDeliveryDate";
            if (MatchAny(headerNorm, ["ngay nhan", "thuc te"])) return "ActualReceiptDate";
            if (MatchAny(headerNorm, ["ghi chu", "note"])) return "DeliveryNote";
        }

        if (groupNorm.Contains("mua hang"))
        {
            if (MatchAny(headerNorm, ["kg/đvt", "kg/dvt"]) || headerNorm == "kg") return "PurchaseKgPerUnit";
            if (MatchAny(headerNorm, ["đơn giá", "don gia"])) return "PurchaseUnitPrice";
            if (MatchAny(headerNorm, ["thành tiền", "thanh tien"])) return "PurchaseAmount";
            if (MatchAny(headerNorm, ["ncc", "supplier"])) return "PurchaseSupplierName";
            if (MatchAny(headerNorm, ["ghi chú", "ghi chu", "note"])) return "PurchaseNote";
        }

        if (groupNorm.Contains("ncc chon"))
        {
            if (MatchAny(headerNorm, ["gia chon", "giá chọn", "đơn giá", "don gia"])) return "SelectedUnitPrice";
            if (MatchAny(headerNorm, ["thanh tien chon", "thành tiền chọn", "thành tiền", "thanh tien"])) return "SelectedAmount";
            if (MatchAny(headerNorm, ["ncc chon", "ncc chọn"]) || headerNorm == "ncc") return "SelectedSupplierName";
            if (MatchAny(headerNorm, ["ly do", "lý do"])) return "SelectionReason";
        }

        foreach (var (field, aliases) in ColumnAliases)
        {
            if (IsAmbiguousShortHeader(headerNorm)) continue;
            if (!aliases.Any(a => headerNorm.Contains(Normalize(a), StringComparison.Ordinal))) continue;
            return field;
        }

        if (string.IsNullOrEmpty(groupNorm) || groupNorm.Contains("thong tin"))
        {
            if (MatchAny(headerNorm, ["hệ", "he", "system"])) return "SystemName";
            if (MatchAny(headerNorm, ["danh mục", "danh muc", "category"])) return "Category";
            if (MatchAny(headerNorm, ["mã vật tư", "ma vat tu", "materialcode", "material code", "mã vt", "ma vt", "sku"])) return "MaterialCode";
            if (MatchAny(headerNorm, ["tên sản phẩm", "ten san pham", "tên hàng", "product"])) return "ProductName";
            if (MatchAny(headerNorm, ["quy cách", "quy cach", "spec"])) return "Spec";
            if (MatchAny(headerNorm, ["đvt", "dvt", "đơn vị", "unit"]) && !headerNorm.Contains("kg")) return "Unit";
            if (MatchAny(headerNorm, ["số lượng", "so luong", "quantity"]) || headerNorm == "sl") return "Quantity";
            if (MatchAny(headerNorm, ["kg/đvt", "kg/dvt", "kg per unit"])) return "KgPerUnit";
            if (MatchAny(headerNorm, ["ghi chú", "ghi chu", "note"])) return "LineNote";
            if (MatchAny(headerNorm, ["tình trạng", "tinh trang"])) return "PurchaseStatus";
        }

        return null;
    }

    private static bool IsAmbiguousShortHeader(string headerNorm) =>
        MatchAny(headerNorm, ["đơn giá", "don gia", "thành tiền", "thanh tien", "ncc", "ghi chú", "ghi chu", "kg/đvt", "kg/dvt"])
        && !MatchAny(headerNorm, [
            "đơn giá dự toán", "đơn giá đề xuất", "đơn giá mua", "giá chọn", "gia chon",
            "thành tiền chọn", "thanh tien chon", "ncc chọn", "ncc chon", "lý do chọn", "ly do chon",
            "ghi chú dòng", "ghi chu dong"
        ]);

    private static string GetMergedCellText(IXLWorksheet sheet, int row, int col)
    {
        var cell = sheet.Cell(row, col);
        if (cell.IsMerged())
            return cell.MergedRange().FirstCell().GetString().Trim();

        var direct = cell.GetString().Trim();
        if (!string.IsNullOrEmpty(direct))
            return direct;

        foreach (var range in sheet.MergedRanges)
        {
            if (range.FirstRow().RowNumber() != row) continue;
            if (col < range.FirstColumn().ColumnNumber() || col > range.LastColumn().ColumnNumber()) continue;
            return range.FirstCell().GetString().Trim();
        }

        return string.Empty;
    }

    private static List<QuoteGroupColumns> DetectAnonymousQuoteGroups(
        IXLWorksheet sheet, int headerRow, Dictionary<string, int> fieldMap)
    {
        var used = fieldMap.Values.ToHashSet();
        var groups = new List<QuoteGroupColumns>();
        var lastCol = sheet.Row(headerRow).LastCellUsed()?.Address.ColumnNumber ?? 0;
        QuoteGroupColumns? current = null;
        var sort = 0;

        for (var col = 1; col <= lastCol; col++)
        {
            if (used.Contains(col))
            {
                current = null;
                continue;
            }

            var norm = Normalize(sheet.Cell(headerRow, col).GetString());
            if (string.IsNullOrEmpty(norm))
            {
                current = null;
                continue;
            }

            var isQuoteField =
                MatchAny(norm, ["đơn giá", "don gia", "unitprice", "giá"]) ||
                MatchAny(norm, ["thành tiền", "thanh tien", "amount"]) ||
                MatchAny(norm, ["ncc", "supplier"]) ||
                MatchAny(norm, ["kg"]);

            if (!isQuoteField)
            {
                current = null;
                continue;
            }

            if (current is null)
            {
                sort++;
                current = new QuoteGroupColumns { SortOrder = sort };
                groups.Add(current);
            }

            if (MatchAny(norm, ["kg"])) current.KgPerUnitCol ??= col;
            else if (MatchAny(norm, ["đơn giá", "don gia", "unitprice", "giá"])) current.UnitPriceCol ??= col;
            else if (MatchAny(norm, ["thành tiền", "thanh tien", "amount"])) current.AmountCol ??= col;
            else if (MatchAny(norm, ["ncc", "supplier"])) current.SupplierNameCol ??= col;
            else if (MatchAny(norm, ["ghi chú", "ghi chu", "note"])) current.NoteCol ??= col;
            else if (MatchAny(norm, ["status", "tt", "trạng thái"])) current.StatusCol ??= col;
        }

        return groups.Where(g => g.UnitPriceCol.HasValue || g.SupplierNameCol.HasValue || g.AmountCol.HasValue).ToList();
    }

    private static PurchasePlanImportLinePreviewDto ParseRow(
        IXLWorksheet sheet,
        int row,
        int lineNo,
        Dictionary<string, int> fieldMap,
        List<QuoteGroupColumns> quoteGroups,
        List<PurchasePlanImportErrorDto> errors)
    {
        string? Get(string field) =>
            fieldMap.TryGetValue(field, out var col) ? CellText(sheet.Cell(row, col)) : null;

        var materialCode = Get("MaterialCode") ?? string.Empty;
        var productName = Get("ProductName") ?? string.Empty;
        var unit = Get("Unit") ?? string.Empty;
        var qtyRaw = Get("Quantity");
        var qty = ParseDecimal(qtyRaw);
        var isValid = true;

        if (string.IsNullOrWhiteSpace(materialCode))
        {
            errors.Add(new PurchasePlanImportErrorDto(row, "MaterialCode", "Thiếu mã vật tư."));
            isValid = false;
        }

        if (qty is null or <= 0)
        {
            errors.Add(new PurchasePlanImportErrorDto(row, "Quantity", "Số lượng phải > 0."));
            isValid = false;
            qty ??= 0;
        }

        var quotes = new List<PurchasePlanImportQuotePreviewDto>();
        foreach (var g in quoteGroups)
        {
            var supplier = g.SupplierNameCol is int sc ? CellText(sheet.Cell(row, sc)) : null;
            var unitPrice = g.UnitPriceCol is int uc ? ParseDecimal(CellText(sheet.Cell(row, uc))) : null;
            var amount = g.AmountCol is int ac ? ParseDecimal(CellText(sheet.Cell(row, ac))) : null;
            var kg = g.KgPerUnitCol is int kc ? ParseDecimal(CellText(sheet.Cell(row, kc))) : null;
            var note = g.NoteCol is int nc ? CellText(sheet.Cell(row, nc)) : null;
            var status = g.StatusCol is int stc ? CellText(sheet.Cell(row, stc)) : null;

            if (string.IsNullOrWhiteSpace(supplier) && unitPrice is null && amount is null && kg is null)
                continue;

            quotes.Add(new PurchasePlanImportQuotePreviewDto(
                g.SortOrder, kg, unitPrice, amount,
                string.IsNullOrWhiteSpace(supplier) ? null : supplier,
                string.IsNullOrWhiteSpace(note) ? null : note,
                string.IsNullOrWhiteSpace(status) ? null : status));
        }

        return new PurchasePlanImportLinePreviewDto(
            row,
            lineNo,
            NullIfEmpty(Get("SystemName")),
            NullIfEmpty(Get("Category")),
            materialCode.Trim(),
            productName,
            NullIfEmpty(Get("Spec")),
            unit,
            qty.Value,
            ParseDecimal(Get("KgPerUnit")),
            NullIfEmpty(Get("LineNote")),
            NullIfEmpty(Get("PurchaseStatus")),
            NullIfEmpty(Get("StatusNote")),
            ParseDecimal(Get("BudgetUnitPrice")),
            ParseDecimal(Get("BudgetAmount")),
            ParseDecimal(Get("ProposalKgPerUnit")),
            ParseDecimal(Get("ProposalUnitPrice")),
            ParseDecimal(Get("ProposalAmount")),
            NullIfEmpty(Get("ProposalSupplierName")),
            NullIfEmpty(Get("ProposalNote")),
            NullIfEmpty(Get("ProposalStatus")),
            ParseDate(Get("RequiredDate")),
            ParseDate(Get("OrderedDate")),
            ParseDate(Get("ExpectedDeliveryDate")),
            ParseDate(Get("ActualReceiptDate")),
            NullIfEmpty(Get("DeliveryNote")),
            ParseDecimal(Get("PurchaseKgPerUnit")),
            ParseDecimal(Get("PurchaseUnitPrice")),
            ParseDecimal(Get("PurchaseAmount")),
            NullIfEmpty(Get("PurchaseSupplierName")),
            NullIfEmpty(Get("PurchaseNote")),
            ParseDecimal(Get("SelectedUnitPrice")),
            ParseDecimal(Get("SelectedAmount")),
            NullIfEmpty(Get("SelectedSupplierName")),
            NullIfEmpty(Get("SelectionReason")),
            quotes,
            isValid);
    }

    private static bool IsBlankRow(
        IXLWorksheet sheet, int row, Dictionary<string, int> fieldMap, List<QuoteGroupColumns> quoteGroups)
    {
        foreach (var col in fieldMap.Values)
        {
            if (!string.IsNullOrWhiteSpace(CellText(sheet.Cell(row, col))))
                return false;
        }

        foreach (var g in quoteGroups)
        {
            foreach (var col in new int?[] { g.KgPerUnitCol, g.UnitPriceCol, g.AmountCol, g.SupplierNameCol, g.NoteCol, g.StatusCol })
            {
                if (col is int c && !string.IsNullOrWhiteSpace(CellText(sheet.Cell(row, c))))
                    return false;
            }
        }

        return true;
    }

    private static string CellText(IXLCell cell)
    {
        if (cell.IsEmpty()) return string.Empty;
        return cell.DataType switch
        {
            XLDataType.Number => cell.GetDouble().ToString(CultureInfo.InvariantCulture),
            XLDataType.DateTime => cell.GetDateTime().ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            XLDataType.Boolean => cell.GetBoolean() ? "true" : "false",
            _ => cell.GetString().Trim(),
        };
    }

    public static string Normalize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return string.Empty;
        var s = value.Trim().ToLowerInvariant()
            .Replace('đ', 'd')
            .Replace('Đ', 'd');
        s = s.Normalize(NormalizationForm.FormD);
        var chars = s.Where(c => CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark).ToArray();
        s = new string(chars).Normalize(NormalizationForm.FormC);
        s = Regex.Replace(s, @"\s+", " ");
        return s;
    }

    private static bool MatchAny(string normalized, IEnumerable<string> aliases) =>
        aliases.Any(a => normalized.Contains(Normalize(a), StringComparison.Ordinal));

    private static decimal? ParseDecimal(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return null;
        var s = raw.Trim().Replace(" ", "").Replace(",", "");
        return decimal.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out var v) ? v : null;
    }

    private static DateOnly? ParseDate(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return null;
        if (DateOnly.TryParse(raw, CultureInfo.InvariantCulture, DateTimeStyles.None, out var d))
            return d;
        if (DateOnly.TryParseExact(raw.Trim(), ["dd/MM/yyyy", "d/M/yyyy", "yyyy-MM-dd"],
                CultureInfo.InvariantCulture, DateTimeStyles.None, out d))
            return d;
        if (DateTime.TryParse(raw, CultureInfo.InvariantCulture, DateTimeStyles.None, out var dt))
            return DateOnly.FromDateTime(dt);
        return null;
    }

    private static string? NullIfEmpty(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private sealed class QuoteGroupColumns
    {
        public int SortOrder { get; set; }
        public int? KgPerUnitCol { get; set; }
        public int? UnitPriceCol { get; set; }
        public int? AmountCol { get; set; }
        public int? SupplierNameCol { get; set; }
        public int? NoteCol { get; set; }
        public int? StatusCol { get; set; }
    }
}
