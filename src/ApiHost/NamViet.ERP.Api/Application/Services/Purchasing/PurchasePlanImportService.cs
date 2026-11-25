using System.Text.RegularExpressions;
using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;
using NamViet.ERP.Api.Application.Dtos.Purchasing;
using NamViet.ERP.Modules.Warehouse.Domain.Entities;
using NamViet.ERP.Modules.Warehouse.Infrastructure.Persistence;

namespace NamViet.ERP.Api.Application.Services.Purchasing;

public interface IPurchasePlanImportService
{
    PurchasePlanImportPreviewDto Preview(Stream excelStream, string projectCode, IReadOnlySet<string>? activeMaterialCodes = null);
    byte[] BuildImportTemplate();
}

public sealed partial class PurchasePlanImportService(WarehouseDbContext? db = null) : IPurchasePlanImportService
{
    public static readonly Regex RomanNumeralRegex = new(
        @"^[IVXLCDMivxlcdm]+[\.\,\:\s\-]*$",
        RegexOptions.Compiled);

    private static readonly (string Field, string[] Aliases)[] ColumnAliases =
    [
        ("LineNo", ["stt", "số thứ tự", "so thu tu", "no.", "no", "item no", "line no", "lineno"]),
        ("SystemName", ["hệ", "he", "system", "systemname"]),
        ("Category", ["danh mục", "danh muc", "nhóm", "nhom", "loại", "loai", "category", "itemtype"]),
        ("MaterialCode", ["mã vật tư", "ma vat tu", "materialcode", "material code", "mã vt", "ma vt", "sku"]),
        ("ProductName", ["tên sản phẩm", "ten san pham", "tên hàng", "ten hang", "tên vật tư", "sản phẩm", "san pham", "productname", "item", "hàng hóa"]),
        ("Spec", ["quy cách", "quy cach", "spec", "specification"]),
        ("Description", ["diễn giải", "dien giai", "mô tả chi tiết", "diễn giải chi tiết", "description", "mục đích sử dụng"]),
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
        ("", "DIỄN GIẢI", "Description"),
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

    public byte[] BuildImportTemplate()
    {
        using var workbook = new XLWorkbook();
        var sheet = workbook.Worksheets.Add("KHMH");
        const int groupRow = 1;
        const int headerRow = 2;
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

        ApplyGroupSeparators(sheet, groupRow, headerRow, colCount);

        sheet.SheetView.FreezeRows(headerRow);
        sheet.Columns().AdjustToContents(groupRow, headerRow, 8.0, 28.0);
        sheet.Row(groupRow).Height = 22;
        sheet.Row(headerRow).Height = 40;

        using var ms = new MemoryStream();
        workbook.SaveAs(ms);
        return ms.ToArray();
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

    public PurchasePlanImportPreviewDto Preview(Stream excelStream, string projectCode, IReadOnlySet<string>? activeMaterialCodes = null)
    {
        if (string.IsNullOrWhiteSpace(projectCode))
            throw new PurchasingConflictException("ProjectCode là bắt buộc khi import KHMH.");

        if (activeMaterialCodes == null && db != null)
        {
            activeMaterialCodes = db.Materials.AsNoTracking()
                .Where(m => m.DeletedAt == null && m.IsActive)
                .Select(m => m.MaterialCode)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
        }

        IReadOnlyDictionary<string, string>? activeServiceItemTypes = null;
        if (db != null)
        {
            activeServiceItemTypes = db.ServiceExpenseItems.AsNoTracking()
                .Where(s => s.DeletedAt == null && s.IsActive)
                .ToDictionary(s => s.Code.Trim(), s => s.Type, StringComparer.OrdinalIgnoreCase);
        }

        using var workbook = new XLWorkbook(excelStream);
        var sheet = FindKhmhSheet(workbook)
            ?? throw new PurchasingConflictException(
                $"Không tìm thấy sheet KHMH trong file Excel (các sheet hiện có trong file: {string.Join(", ", workbook.Worksheets.Select(w => $"'{w.Name}'"))}). Vui lòng đổi tên sheet cần import thành 'KHMH' hoặc bấm nút 'Tải mẫu'.");

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

            if (IsHeaderOrSeparatorRow(sheet, row, fieldMap, quoteGroups))
                continue;

            lineNo++;
            var preview = ParseRow(sheet, row, lineNo, fieldMap, quoteGroups, errors, activeMaterialCodes, activeServiceItemTypes);
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
}
