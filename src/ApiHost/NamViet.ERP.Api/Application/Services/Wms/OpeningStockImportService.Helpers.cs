using System.Globalization;
using System.Text;
using ClosedXML.Excel;

namespace NamViet.ERP.Api.Application.Services.Wms;

public sealed partial class OpeningStockImportService
{
    private sealed record OpeningStockRowValues(
        string Code,
        string Name,
        string Unit,
        string QtyStr,
        string PriceStr,
        string Note);

    private sealed record OpeningStockColumnMapping(
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

    private static OpeningStockRowValues ReadRowValues(IXLRow row, OpeningStockColumnMapping mapping) => new(
        Code: mapping.CodeCol > 0 ? GetSafeCellString(row.Cell(mapping.CodeCol)) : string.Empty,
        Name: mapping.NameCol > 0 ? GetSafeCellString(row.Cell(mapping.NameCol)) : string.Empty,
        Unit: mapping.UnitCol > 0 ? GetSafeCellString(row.Cell(mapping.UnitCol)) : string.Empty,
        QtyStr: mapping.QtyCol > 0 ? GetSafeCellString(row.Cell(mapping.QtyCol)) : string.Empty,
        PriceStr: mapping.PriceCol > 0 ? GetSafeCellString(row.Cell(mapping.PriceCol)) : string.Empty,
        Note: mapping.NoteCol > 0 ? GetSafeCellString(row.Cell(mapping.NoteCol)) : string.Empty);

    private static bool ShouldSkipRow(string code, string name, string qtyStr)
    {
        if (string.IsNullOrWhiteSpace(qtyStr) && (string.IsNullOrWhiteSpace(code) || code.StartsWith("F1-", StringComparison.OrdinalIgnoreCase)))
            return true;
        if (string.IsNullOrWhiteSpace(code) && string.IsNullOrWhiteSpace(qtyStr))
            return true;
        if (code.Contains("tổng cộng", StringComparison.OrdinalIgnoreCase) ||
            name.Contains("tổng cộng", StringComparison.OrdinalIgnoreCase))
            return true;
        return string.IsNullOrWhiteSpace(qtyStr);
    }

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
            else if (norm.Contains(cleanKw, StringComparison.OrdinalIgnoreCase) ||
                     normNoSpace.Contains(cleanKwNoSpace, StringComparison.OrdinalIgnoreCase))
            {
                return true;
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

                if (qtyCol == 0 && MatchHeaderKeyword(norm, QtyKeywords)) { qtyCol = col; continue; }
                if (priceCol == 0 && MatchHeaderKeyword(norm, PriceKeywords)) { priceCol = col; continue; }
                if (unitCol == 0 && MatchHeaderKeyword(norm, UnitKeywords)) { unitCol = col; continue; }
                if (codeCol == 0 && MatchHeaderKeyword(norm, CodeKeywords)) { codeCol = col; continue; }
                if (nameCol == 0 && MatchHeaderKeyword(norm, PrimaryNameKeywords)) { nameCol = col; continue; }
                if (fallbackNameCol == 0 && MatchHeaderKeyword(norm, FallbackNameKeywords)) { fallbackNameCol = col; continue; }
                if (noteCol == 0 && MatchHeaderKeyword(norm, NoteKeywords)) { noteCol = col; }
            }

            if (nameCol == 0 && fallbackNameCol > 0)
                nameCol = fallbackNameCol;

            if (codeCol > 0 && qtyCol > 0)
                return new OpeningStockColumnMapping(row, codeCol, nameCol, unitCol, qtyCol, priceCol, noteCol);
        }

        return new OpeningStockColumnMapping(
            HeaderRow: 1, CodeCol: 1, NameCol: 2, UnitCol: 3, QtyCol: 4, PriceCol: 5, NoteCol: 6);
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

        if (decimal.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out result))
            return true;

        var vn = CultureInfo.GetCultureInfo("vi-VN");
        if (decimal.TryParse(s, NumberStyles.Any, vn, out result))
            return true;

        var cleaned = s.Replace(" ", "").Replace("\u00A0", "");
        if (cleaned.Contains(',') && cleaned.Contains('.'))
        {
            cleaned = cleaned.IndexOf(',') < cleaned.IndexOf('.')
                ? cleaned.Replace(",", "")
                : cleaned.Replace(".", "").Replace(",", ".");
        }
        else if (cleaned.Contains(','))
        {
            cleaned = cleaned.Replace(',', '.');
        }

        return decimal.TryParse(cleaned, NumberStyles.Any, CultureInfo.InvariantCulture, out result);
    }
}
