using System.Globalization;
using System.Text;
using ClosedXML.Excel;
using NamViet.ERP.Modules.Warehouse.Domain.Entities;

namespace NamViet.ERP.Api.Application.Services.Wms;

public sealed partial class MaterialImportExcelService
{
    private static bool CheckIfNewCategory(
        List<MaterialCategory> categories,
        string l1, string l2, string? l3, string? l4, string? l5)
    {
        var c1 = categories.FirstOrDefault(c => c.Level == 1 && c.ParentId == null && string.Equals(c.Name.Trim(), l1.Trim(), StringComparison.OrdinalIgnoreCase));
        if (c1 is null) return true;

        var c2 = categories.FirstOrDefault(c => c.Level == 2 && c.ParentId == c1.Id && string.Equals(c.Name.Trim(), l2.Trim(), StringComparison.OrdinalIgnoreCase));
        if (c2 is null) return true;

        if (!string.IsNullOrWhiteSpace(l3) && l3.Trim() != "-")
        {
            var c3 = categories.FirstOrDefault(c => c.Level == 3 && c.ParentId == c2.Id && string.Equals(c.Name.Trim(), l3.Trim(), StringComparison.OrdinalIgnoreCase));
            if (c3 is null) return true;

            if (!string.IsNullOrWhiteSpace(l4) && l4.Trim() != "-")
            {
                var c4 = categories.FirstOrDefault(c => c.Level == 4 && c.ParentId == c3.Id && string.Equals(c.Name.Trim(), l4.Trim(), StringComparison.OrdinalIgnoreCase));
                if (c4 is null) return true;

                if (!string.IsNullOrWhiteSpace(l5) && l5.Trim() != "-")
                {
                    var c5 = categories.FirstOrDefault(c => c.Level == 5 && c.ParentId == c4.Id && string.Equals(c.Name.Trim(), l5.Trim(), StringComparison.OrdinalIgnoreCase));
                    if (c5 is null) return true;
                }
            }
        }

        return false;
    }

    private static bool IsSubHeaderOrFormulaRow(string[] row, int codeIndex, int unitIndex = -1, int priceIndex = -1)
    {
        var code = codeIndex >= 0 && codeIndex < row.Length ? row[codeIndex].Trim().ToUpperInvariant() : string.Empty;
        if (!string.IsNullOrEmpty(code))
        {
            if (code.StartsWith("F1-F2")
                || code.Contains("MÃ HÀNG")
                || code.Contains("MA HANG")
                || code.Equals("DIỄN GIẢI")
                || code.Equals("DIEN GIAI")
                || code.StartsWith("PHI/")
                || code.StartsWith("CÔNG SUẤT")
                || code.StartsWith("CONG SUAT"))
            {
                return true;
            }
        }

        // Nếu mã rỗng, kiểm tra xem dòng này có phải là dòng tiêu đề phụ (sub-header), chú thích F-code hoặc Tiêu đề phân nhóm (Section Header) không
        if (string.IsNullOrEmpty(code))
        {
            var rowText = string.Join(" ", row.Select(NormalizeHeader));
            if (rowText.Contains("nhanhieu")
                || rowText.Contains("nuocsanxuat")
                || rowText.Contains("nhomnuoc")
                || rowText.Contains("congsuat")
                || rowText.Contains("luuluong")
                || rowText.Contains("cotap")
                || rowText.Contains("phichieurong")
                || rowText.Contains("doday")
                || rowText.Contains("chieudai")
                || rowText.Contains("f1f2")
                || string.IsNullOrWhiteSpace(rowText))
            {
                return true;
            }

            var nonEmptyCells = row.Where(c => !string.IsNullOrWhiteSpace(c)).Select(c => c.Trim()).ToList();
            if (nonEmptyCells.Count <= 1)
            {
                return true;
            }

            var unit = unitIndex >= 0 && unitIndex < row.Length ? row[unitIndex].Trim() : string.Empty;
            var price = priceIndex >= 0 && priceIndex < row.Length ? row[priceIndex].Trim() : string.Empty;

            // STT hoặc ô đầu tiên trong 2 ô đầu là số La Mã (I., II., III., IV., V., VI., ...)
            if (nonEmptyCells.Take(2).Any(c =>
                System.Text.RegularExpressions.Regex.IsMatch(c, @"^(?i:I|II|III|IV|V|VI|VII|VIII|IX|X|XI|XII|XIII|XIV|XV)[\.\s\:\-]*$")
                || (string.IsNullOrEmpty(unit) && string.IsNullOrEmpty(price) && System.Text.RegularExpressions.Regex.IsMatch(c, @"^(?i:I|II|III|IV|V|VI|VII|VIII|IX|X|XI|XII|XIII|XIV|XV)[\.\s\:\-]+\S+"))))
            {
                return true;
            }

            // Bắt đầu bằng từ khóa tiêu đề phân nhóm
            if (nonEmptyCells.Take(2).Any(c =>
            {
                var upper = c.ToUpperInvariant();
                return upper.StartsWith("PHẦN ") || upper.StartsWith("PHAN ")
                    || upper.StartsWith("MỤC ") || upper.StartsWith("MUC ")
                    || upper.StartsWith("HỆ THỐNG ") || upper.StartsWith("HE THONG ")
                    || upper.StartsWith("NHÓM ") || upper.StartsWith("NHOM ")
                    || upper.StartsWith("CHƯƠNG ") || upper.StartsWith("CHUONG ")
                    || upper.StartsWith("TIỂU MỤC ") || upper.StartsWith("TIEU MUC ");
            }))
            {
                return true;
            }
        }

        return false;
    }

    private sealed record RawExcelRow(int RowNumber, string[] Cells);

    private static int FindHeaderRowIndex(List<RawExcelRow> rows)
    {
        for (var i = 0; i < Math.Min(10, rows.Count); i++)
        {
            var row = rows[i].Cells;
            var normalized = row.Select(NormalizeHeader).ToArray();
            var hasCode = FindColumn(normalized, "mavattu", "mavat tu", "ma", "sku", "materialcode", "mahanghoa", "mahang") >= 0;
            var hasName = FindColumn(normalized, "tenvattu", "tenvat tu", "ten", "materialname", "tenhanghoa", "tenhang", "diengiai", "tengoi") >= 0;
            if (hasCode || (hasName && row.Length >= 3))
                return i;
        }
        return -1;
    }

    private sealed record MaterialColumnMap(
        int MaterialCode,
        int MaterialName,
        int Unit,
        int DanhMuc,
        int NhomVatTu,
        int LoaiVatTu,
        int PhanLoai,
        int Kieu,
        int Origin,
        int TechnicalSpec,
        int GeneralTechnicalSpec,
        int QualityStandard,
        int CeilingPrice,
        int AbcClass,
        int IsConsumable,
        int NhanHieu,
        int NuocSanXuat,
        int NhomNuoc,
        int PhiRong,
        int DoDay,
        int ChieuDai,
        int CongSuat,
        int LuuLuong,
        int CotAp,
        int DienAp,
        int MoTa,
        int Diengiai);

    private static MaterialColumnMap MapColumns(List<RawExcelRow> rows, int headerIndex)
    {
        var maxCols = rows[headerIndex].Cells.Length;
        for (var r = headerIndex; r < Math.Min(headerIndex + 4, rows.Count); r++)
        {
            if (rows[r].Cells.Length > maxCols) maxCols = rows[r].Cells.Length;
        }

        var combinedHeaders = new string[maxCols];
        for (var c = 0; c < maxCols; c++)
        {
            var tokens = new List<string>();
            for (var r = headerIndex; r < Math.Min(headerIndex + 4, rows.Count); r++)
            {
                if (c < rows[r].Cells.Length && !string.IsNullOrWhiteSpace(rows[r].Cells[c]))
                {
                    tokens.Add(NormalizeHeader(rows[r].Cells[c]));
                }
            }
            combinedHeaders[c] = string.Join(" ", tokens);
        }

        return new MaterialColumnMap(
            FindColumn(combinedHeaders, "mavattu", "mavat tu", "mahanghoa", "materialcode", "mahang", "f1f2", "sku", "ma"),
            FindColumn(combinedHeaders, "tenvattu", "tenvat tu", "tenhanghoa", "materialname", "tenhang", "ten"),
            FindColumn(combinedHeaders, "donvitinh", "dvt", "unit", "donvi"),
            FindColumn(combinedHeaders, "danhmuc", "danhmucvattu", "category", "f0", "cap1"),
            FindColumn(combinedHeaders, "nhomvattu", "nhomhang", "hangmuc", "nhom", "subcategory", "f1", "cap2"),
            FindColumn(combinedHeaders, "loaivattu", "loai", "f2", "cap3"),
            FindColumn(combinedHeaders, "phanloai", "f3", "cap4"),
            FindColumn(combinedHeaders, "kieu", "f4", "f8", "cap5"),
            FindColumn(combinedHeaders, "xuatxu", "origin"),
            FindColumn(combinedHeaders, "quycach", "technicalspec", "thongsokythuat", "spec"),
            FindColumn(combinedHeaders, "thongsokythuatchung", "thongsochung", "generalspec", "thongso"),
            FindColumn(combinedHeaders, "tieuchuanchatluong", "tieuchuan", "qualitystandard", "chungloai", "tengoi"),
            FindColumn(combinedHeaders, "giatran", "ceilingprice", "dongia", "gia", "price"),
            FindColumn(combinedHeaders, "abc", "phanloaiabc", "abcclass"),
            FindColumn(combinedHeaders, "tieuhao", "vattutieuhao", "isconsumable", "consumable"),
            FindColumn(combinedHeaders, "nhanhieu", "thuonghieu", "hangsx", "hangsanxuat", "brand", "maker"),
            FindColumn(combinedHeaders, "nuocsanxuat", "nuocsx", "country"),
            FindColumn(combinedHeaders, "nhomnuoc", "nhomnuocg7", "nhomxuatxu", "region"),
            FindColumn(combinedHeaders, "phichieurong", "chieurong", "phi"),
            FindColumn(combinedHeaders, "doday", "thickness"),
            FindColumn(combinedHeaders, "chieudai", "length"),
            FindColumn(combinedHeaders, "congsuat", "power", "kw"),
            FindColumn(combinedHeaders, "luuluong", "flow", "m3h"),
            FindColumn(combinedHeaders, "cotap", "cotappa", "cotaph20", "pressure", "pa"),
            FindColumn(combinedHeaders, "dienap", "voltage"),
            FindColumn(combinedHeaders, "mota", "description"),
            FindColumn(combinedHeaders, "diengiai")
        );
    }

    private static int FindColumn(string[] headers, params string[] aliases)
    {
        for (var i = 0; i < headers.Length; i++)
        {
            var h = headers[i];
            if (aliases.Any(a => h.Contains(a, StringComparison.OrdinalIgnoreCase)))
                return i;
        }
        return -1;
    }

    private static string NormalizeHeader(string value)
    {
        var formD = value.Trim().ToLowerInvariant()
            .Replace("đ", "d")
            .Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder(formD.Length);
        foreach (var ch in formD)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(ch) == UnicodeCategory.NonSpacingMark)
                continue;
            if (char.IsLetterOrDigit(ch))
                sb.Append(ch);
        }
        return sb.ToString().Normalize(NormalizationForm.FormC);
    }

    private static (List<RawExcelRow> Rows, IReadOnlyList<string> AvailableSheets, string SelectedSheet) ReadExcelRows(
        Stream stream, string? targetSheet = null)
    {
        using var workbook = new XLWorkbook(stream);
        var availableSheets = workbook.Worksheets.Select(w => w.Name).ToList();
        if (availableSheets.Count == 0)
            return ([], availableSheets, string.Empty);

        IXLWorksheet? sheet = null;

        if (!string.IsNullOrWhiteSpace(targetSheet))
        {
            var targetTrimmed = targetSheet.Trim();
            sheet = workbook.Worksheets.FirstOrDefault(w =>
                string.Equals(w.Name.Trim(), targetTrimmed, StringComparison.OrdinalIgnoreCase))
                ?? workbook.Worksheets.FirstOrDefault(w =>
                w.Name.Contains(targetTrimmed, StringComparison.OrdinalIgnoreCase));
        }

        if (sheet is null)
        {
            sheet = workbook.Worksheets.FirstOrDefault(w => w.Name.Trim().Equals("KHMH", StringComparison.OrdinalIgnoreCase))
                ?? workbook.Worksheets.FirstOrDefault(w => w.Name.Contains("KHMH", StringComparison.OrdinalIgnoreCase))
                ?? workbook.Worksheets.FirstOrDefault(w => w.Name.Contains("kế hoạch mua hàng", StringComparison.OrdinalIgnoreCase))
                ?? workbook.Worksheets.FirstOrDefault(w => w.Name.Contains("ke hoach mua hang", StringComparison.OrdinalIgnoreCase))
                ?? workbook.Worksheets.FirstOrDefault(w => w.Name.Trim().Equals("FINAL", StringComparison.OrdinalIgnoreCase))
                ?? workbook.Worksheets.FirstOrDefault(w => w.Name.Trim().Equals("MÃ VẬT TƯ", StringComparison.OrdinalIgnoreCase))
                ?? workbook.Worksheets.FirstOrDefault(w => w.Name.Trim().Equals("MA VAT TU", StringComparison.OrdinalIgnoreCase))
                ?? workbook.Worksheets.FirstOrDefault(w => w.Name.Trim().Equals("THÉP", StringComparison.OrdinalIgnoreCase))
                ?? workbook.Worksheets.FirstOrDefault(w => w.Name.Contains("danh muc", StringComparison.OrdinalIgnoreCase))
                ?? workbook.Worksheets.FirstOrDefault(w => w.Name.Contains("vat tu", StringComparison.OrdinalIgnoreCase))
                ?? workbook.Worksheets.FirstOrDefault(w => w.RangeUsed() != null && w.RangeUsed().RowCount() > 1)
                ?? workbook.Worksheets.First();
        }

        var selectedSheet = sheet.Name;
        var used = sheet.RangeUsed();
        if (used is null) return ([], availableSheets, selectedSheet);

        var result = new List<RawExcelRow>();
        foreach (var row in used.Rows())
        {
            var cells = new string[row.CellCount()];
            for (var c = 0; c < row.CellCount(); c++)
                cells[c] = row.Cell(c + 1).GetFormattedString().Trim();
            result.Add(new RawExcelRow(row.RowNumber(), cells));
        }
        return (result, availableSheets, selectedSheet);
    }

    private static List<RawExcelRow> ReadCsvRows(Stream stream)
    {
        using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
        var result = new List<RawExcelRow>();
        var lineNum = 1;
        while (!reader.EndOfStream)
        {
            var line = reader.ReadLine();
            if (line is null) break;
            result.Add(new RawExcelRow(lineNum++, SplitCsvLine(line)));
        }
        return result;
    }

    private static string[] SplitCsvLine(string line)
    {
        var cells = new List<string>();
        var current = new StringBuilder();
        var inQuotes = false;
        for (var i = 0; i < line.Length; i++)
        {
            var ch = line[i];
            if (ch == '"')
            {
                inQuotes = !inQuotes;
                continue;
            }
            if (ch == ',' && !inQuotes)
            {
                cells.Add(current.ToString().Trim());
                current.Clear();
                continue;
            }
            current.Append(ch);
        }
        cells.Add(current.ToString().Trim());
        return cells.ToArray();
    }

    private static bool IsEmptyRow(string[] row) => row.All(string.IsNullOrWhiteSpace);

    private static string GetCell(string[] row, int index) =>
        index >= 0 && index < row.Length ? row[index] : string.Empty;

    private static bool TryParseDecimal(string raw, out decimal value)
    {
        value = 0;
        var text = raw.Trim();
        if (string.IsNullOrEmpty(text)) return false;

        text = text.Replace(" ", string.Empty).Replace("đ", string.Empty, StringComparison.OrdinalIgnoreCase).Replace(",", ".");
        if (decimal.TryParse(text, NumberStyles.Number, CultureInfo.InvariantCulture, out value))
            return true;
        if (decimal.TryParse(raw.Trim(), NumberStyles.Number, new CultureInfo("vi-VN"), out value))
            return true;

        return false;
    }

    private static bool IsEquipmentCategoryOrItem(
        string? danhMuc, string? nhomVatTu, string? loaiVatTu, string? name, string? code)
    {
        var combined = $"{danhMuc} {nhomVatTu} {loaiVatTu} {name} {code}".ToLowerInvariant();
        return combined.Contains("động cơ") || combined.Contains("dong co") ||
               combined.Contains("quạt") || combined.Contains("quat") ||
               combined.Contains("bơm") || combined.Contains("bom") ||
               combined.Contains("máy") || combined.Contains("may") ||
               combined.Contains("thiết bị") || combined.Contains("thiet bi") ||
               combined.Contains("biến tần") || combined.Contains("bien tan") ||
               combined.Contains("đc-") || combined.Contains("dc-");
    }

    private static bool LooksLikeDimension(string s)
    {
        if (string.IsNullOrWhiteSpace(s)) return false;
        var t = s.Trim();
        return t.Contains('Ø') ||
               t.Contains("tube", StringComparison.OrdinalIgnoreCase) ||
               t.Contains("mm", StringComparison.OrdinalIgnoreCase) ||
               t.Contains('x') || t.Contains('X');
    }

    private static string? NormalizeAbc(string? value)
    {
        var v = value?.Trim().ToUpperInvariant();
        return v is "A" or "B" or "C" ? v : null;
    }

    private static bool ParseBool(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return false;
        var r = raw.Trim().ToLowerInvariant();
        return r is "co" or "có" or "true" or "1" or "yes" or "x";
    }
}
