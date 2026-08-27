using System.Globalization;
using System.Text;
using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;
using NamViet.ERP.Api.Application.Dtos.Wms;
using NamViet.ERP.Modules.Warehouse.Infrastructure.Persistence;
using NamViet.ERP.SharedKernel.Common;

namespace NamViet.ERP.Api.Application.Services.Wms;

public interface IImportExcelService
{
    byte[] GenerateTemplate();
    Task<ImportParseResultDto> ParseImportFileAsync(Stream stream, string fileName, CancellationToken ct = default);
}

public sealed class ImportExcelService(WarehouseDbContext db) : IImportExcelService
{
    private const int MaxDataRows = 500;
    private const long MaxFileBytes = 2 * 1024 * 1024;

    private static readonly string[] TemplateHeaders =
    [
        "Mã vật tư",
        "Tên vật tư",
        "Đơn vị tính",
        "Số lượng",
        "Đơn giá",
    ];

    public byte[] GenerateTemplate()
    {
        using var workbook = new XLWorkbook();
        var sheet = workbook.Worksheets.Add("Phieu nhap");

        for (var col = 0; col < TemplateHeaders.Length; col++)
        {
            var cell = sheet.Cell(1, col + 1);
            cell.Value = TemplateHeaders[col];
            cell.Style.Font.Bold = true;
            cell.Style.Fill.BackgroundColor = XLColor.FromHtml("#4472C4");
            cell.Style.Font.FontColor = XLColor.White;
        }

        sheet.Cell(2, 1).Value = "VD-001-0001-01";
        sheet.Cell(2, 2).Value = "(Tên tự điền theo mã — có thể để trống)";
        sheet.Cell(2, 3).Value = "(ĐVT tự điền — có thể để trống)";
        sheet.Cell(2, 4).Value = 10;
        sheet.Cell(2, 5).Value = 150000;

        sheet.Columns().AdjustToContents();
        sheet.SheetView.FreezeRows(1);

        using var ms = new MemoryStream();
        workbook.SaveAs(ms);
        return ms.ToArray();
    }

    public async Task<ImportParseResultDto> ParseImportFileAsync(
        Stream stream, string fileName, CancellationToken ct = default)
    {
        if (stream.CanSeek && stream.Length > MaxFileBytes)
            throw new DomainRuleException("File vượt quá 2MB.");

        var extension = Path.GetExtension(fileName).ToLowerInvariant();
        var rows = extension switch
        {
            ".xlsx" or ".xls" => ReadExcelRows(stream),
            ".csv" => ReadCsvRows(stream),
            _ => throw new DomainRuleException("Chỉ chấp nhận file .xlsx, .xls hoặc .csv."),
        };

        if (rows.Count == 0)
            throw new DomainRuleException("File không có dữ liệu.");

        var headerRow = rows[0];
        var columnMap = MapColumns(headerRow);
        if (columnMap.MaterialCode < 0)
            throw new DomainRuleException("Không tìm thấy cột \"Mã vật tư\" trong file.");

        var dataRows = rows.Skip(1)
            .Where(r => !IsEmptyRow(r))
            .Take(MaxDataRows)
            .ToList();

        if (dataRows.Count == 0)
            throw new DomainRuleException("File không có dòng vật tư nào.");

        var parsed = new List<(int RowNumber, string Code, decimal Qty, decimal UnitPrice)>();
        var errors = new List<ImportParseErrorDto>();

        for (var i = 0; i < dataRows.Count; i++)
        {
            var excelRow = i + 2;
            var row = dataRows[i];
            var code = GetCell(row, columnMap.MaterialCode).Trim();
            if (string.IsNullOrWhiteSpace(code))
            {
                errors.Add(new ImportParseErrorDto(excelRow, "materialCode", "Mã vật tư không được để trống."));
                continue;
            }

            if (!TryParseQty(GetCell(row, columnMap.Qty), out var qty))
            {
                errors.Add(new ImportParseErrorDto(excelRow, "qty", "Số lượng không hợp lệ hoặc phải lớn hơn 0."));
                continue;
            }

            if (!TryParsePrice(GetCell(row, columnMap.UnitPrice), out var unitPrice))
            {
                errors.Add(new ImportParseErrorDto(excelRow, "unitPrice", "Đơn giá phải lớn hơn 0."));
                continue;
            }

            parsed.Add((excelRow, code, qty, unitPrice));
        }

        var merged = MergeDuplicateCodes(parsed);
        var codes = merged.Select(m => m.Code).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        var materials = await db.Materials.AsNoTracking()
            .Where(m => m.DeletedAt == null && codes.Contains(m.MaterialCode))
            .ToDictionaryAsync(m => m.MaterialCode, StringComparer.OrdinalIgnoreCase, ct);

        var lines = new List<ImportParsedLineDto>();
        foreach (var item in merged)
        {
            if (!materials.TryGetValue(item.Code, out var material))
            {
                errors.Add(new ImportParseErrorDto(
                    item.RowNumber,
                    "materialCode",
                    $"Mã vật tư \"{item.Code}\" không tồn tại trong danh mục."));
                continue;
            }

            if (!material.IsActive)
            {
                errors.Add(new ImportParseErrorDto(
                    item.RowNumber,
                    "materialCode",
                    $"Mã vật tư \"{item.Code}\" đã ngừng sử dụng."));
                continue;
            }

            lines.Add(new ImportParsedLineDto(
                item.RowNumber,
                material.MaterialCode,
                material.MaterialName,
                material.Unit,
                item.Qty,
                item.UnitPrice,
                true));
        }

        return new ImportParseResultDto(
            dataRows.Count,
            lines.Count,
            errors.Count,
            lines,
            errors);
    }

    private static List<(int RowNumber, string Code, decimal Qty, decimal UnitPrice)> MergeDuplicateCodes(
        List<(int RowNumber, string Code, decimal Qty, decimal UnitPrice)> parsed)
    {
        var map = new Dictionary<string, (int RowNumber, string Code, decimal Qty, decimal UnitPrice)>(
            StringComparer.OrdinalIgnoreCase);
        foreach (var row in parsed)
        {
            if (map.TryGetValue(row.Code, out var existing))
            {
                map[row.Code] = (existing.RowNumber, existing.Code, existing.Qty + row.Qty, row.UnitPrice);
            }
            else
            {
                map[row.Code] = row;
            }
        }

        return map.Values.OrderBy(v => v.RowNumber).ToList();
    }

    private static List<string[]> ReadExcelRows(Stream stream)
    {
        using var workbook = new XLWorkbook(stream);
        var sheet = workbook.Worksheets.First();
        var used = sheet.RangeUsed();
        if (used is null) return [];

        var result = new List<string[]>();
        foreach (var row in used.Rows())
        {
            var cells = new string[row.CellCount()];
            for (var c = 0; c < row.CellCount(); c++)
                cells[c] = row.Cell(c + 1).GetFormattedString().Trim();
            result.Add(cells);
        }

        return result;
    }

    private static List<string[]> ReadCsvRows(Stream stream)
    {
        using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
        var result = new List<string[]>();
        while (!reader.EndOfStream)
        {
            var line = reader.ReadLine();
            if (line is null) break;
            result.Add(SplitCsvLine(line));
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

    private sealed record ColumnMap(int MaterialCode, int Qty, int UnitPrice);

    private static ColumnMap MapColumns(string[] headerRow)
    {
        var normalized = headerRow.Select(NormalizeHeader).ToArray();
        return new ColumnMap(
            FindColumn(normalized, "mavattu", "materialcode", "sku", "mahang"),
            FindColumn(normalized, "soluong", "qty", "sl", "quantity"),
            FindColumn(normalized, "dongia", "unitprice", "price", "gia"));
    }

    private static int FindColumn(string[] headers, params string[] aliases)
    {
        for (var i = 0; i < headers.Length; i++)
        {
            if (aliases.Any(a => headers[i] == a || headers[i].EndsWith(a, StringComparison.Ordinal)))
                return i;
        }

        return -1;
    }

    private static string NormalizeHeader(string value)
    {
        var formD = value.Trim().ToLowerInvariant().Normalize(NormalizationForm.FormD);
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

    private static bool IsEmptyRow(string[] row) =>
        row.All(string.IsNullOrWhiteSpace);

    private static string GetCell(string[] row, int index) =>
        index >= 0 && index < row.Length ? row[index] : string.Empty;

    private static bool TryParseQty(string raw, out decimal qty)
    {
        qty = 0;
        if (!TryParseDecimal(raw, out var value)) return false;
        if (value <= 0) return false;
        qty = value;
        return true;
    }

    private static bool TryParsePrice(string raw, out decimal price)
    {
        price = 0;
        if (string.IsNullOrWhiteSpace(raw))
            return false;

        if (!TryParseDecimal(raw, out var value)) return false;
        if (value <= 0) return false;
        price = value;
        return true;
    }

    private static bool TryParseDecimal(string raw, out decimal value)
    {
        value = 0;
        var text = raw.Trim();
        if (string.IsNullOrEmpty(text)) return false;

        text = text.Replace(" ", string.Empty).Replace("đ", string.Empty, StringComparison.OrdinalIgnoreCase);
        if (decimal.TryParse(text, NumberStyles.Number, CultureInfo.InvariantCulture, out value))
            return true;
        if (decimal.TryParse(text, NumberStyles.Number, new CultureInfo("vi-VN"), out value))
            return true;

        return false;
    }
}
