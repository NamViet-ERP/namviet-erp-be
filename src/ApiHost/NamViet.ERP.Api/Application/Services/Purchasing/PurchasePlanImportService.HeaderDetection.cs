using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using ClosedXML.Excel;
using NamViet.ERP.Api.Application.Dtos.Purchasing;

namespace NamViet.ERP.Api.Application.Services.Purchasing;

public sealed partial class PurchasePlanImportService
{
    public static IXLWorksheet? FindKhmhSheet(XLWorkbook workbook)
    {
        if (!workbook.Worksheets.Any()) return null;

        static bool IsKhmh(string name)
        {
            var n = name.Trim();
            return Regex.IsMatch(n, @"^KHMH(\s*\(\d+\))?$", RegexOptions.IgnoreCase);
        }

        // Ưu tiên 1: Tên sheet khớp regex ^KHMH(\s*\(\d+\))?$ hoặc chứa "KHMH" (case-insensitive)
        var exactMatch = workbook.Worksheets.FirstOrDefault(ws => IsKhmh(ws.Name))
            ?? workbook.Worksheets.FirstOrDefault(ws => ws.Name.Contains("KHMH", StringComparison.OrdinalIgnoreCase));
        if (exactMatch != null) return exactMatch;

        // Ưu tiên 2: Tên sheet chứa các từ khóa / alias
        string[] aliases =
        [
            "ke hoach mua hang", "kế hoạch mua hàng",
            "purchase plan", "plan",
            "vat tu", "vật tư",
            "danh muc", "danh mục"
        ];
        var aliasMatch = workbook.Worksheets.FirstOrDefault(ws =>
        {
            var norm = Normalize(ws.Name);
            return aliases.Any(a => norm.Contains(Normalize(a), StringComparison.OrdinalIgnoreCase)
                                 || ws.Name.Contains(a, StringComparison.OrdinalIgnoreCase));
        });
        if (aliasMatch != null) return aliasMatch;

        // Ưu tiên 3: Quét các sheet trong workbook, nếu sheet nào có DetectHeader(sheet) != null thì chọn sheet đó
        var headerMatch = workbook.Worksheets.FirstOrDefault(ws => DetectHeader(ws) != null);
        if (headerMatch != null) return headerMatch;

        // Ưu tiên 4: Nếu workbook chỉ có duy nhất 1 sheet, tự động chọn luôn sheet đó
        if (workbook.Worksheets.Count == 1)
            return workbook.Worksheets.First();

        return null;
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
                if (ColumnAliases.Any(a => a.Aliases.Any(alias => MatchAlias(norm, alias))))
                    hits++;
            }

            if (hits >= 3)
                return r;
        }

        return null;
    }

    private static bool MatchAlias(string norm, string alias)
    {
        var normAlias = Normalize(alias);
        if (normAlias.Length <= 2)
        {
            var parts = norm.Split([' ', '/', '-', '_', ',', '.', ':', ';'], StringSplitOptions.RemoveEmptyEntries);
            return parts.Contains(normAlias);
        }
        return norm.Contains(normAlias, StringComparison.Ordinal);
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
            if (field.StartsWith("Quote", StringComparison.Ordinal))
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
            if (!aliases.Any(a => MatchAlias(headerNorm, a))) continue;
            return field;
        }

        if (string.IsNullOrEmpty(groupNorm) || groupNorm.Contains("thong tin"))
        {
            if (MatchAny(headerNorm, ["stt", "so thu tu", "số thứ tự", "no", "item no", "line no"])) return "LineNo";
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
            "đơn giá dự toán", "don gia du toan", "đơn giá đề xuất", "don gia de xuat", "đơn giá mua", "don gia mua", "giá chọn", "gia chon",
            "thành tiền dự toán", "thanh tien du toan", "thành tiền đề xuất", "thanh tien de xuat", "thành tiền mua", "thanh tien mua",
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
