using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using NamViet.ERP.Api.Application.Dtos.Purchasing;
using NamViet.ERP.Modules.Warehouse.Domain.Entities;
using NamViet.ERP.Modules.Warehouse.Infrastructure.Persistence;
using NamViet.ERP.SharedKernel.Common;

namespace NamViet.ERP.Api.Application.Services.Purchasing;

public sealed class ServiceExpenseImportExcelService(WarehouseDbContext db) : IServiceExpenseImportExcelService
{
    private const int MaxDataRows = 2000;
    private const long MaxFileBytes = 5 * 1024 * 1024; // 5MB

    private static readonly string[] TemplateHeaders =
    [
        "Mã (*)",
        "Tên dịch vụ / chi phí (*)",
        "Phân loại (*) [Dịch vụ / Chi phí ngoài]",
        "Đơn vị tính (*)",
        "Đơn giá tham chiếu",
        "Ghi chú",
    ];

    private static readonly Regex CodeRegex = new(@"^[A-Z0-9_\-\.\/]+$", RegexOptions.Compiled);

    public Task<byte[]> GenerateTemplateAsync(CancellationToken ct = default)
    {
        using var workbook = new XLWorkbook();

        // ── Sheet 1: Dịch vụ & Chi phí ngoài ─────────────────────────────────
        var sheet = workbook.Worksheets.Add("Dich vu & Chi phi");

        for (var col = 0; col < TemplateHeaders.Length; col++)
        {
            var cell = sheet.Cell(1, col + 1);
            cell.Value = TemplateHeaders[col];
            cell.Style.Font.Bold = true;
            cell.Style.Fill.BackgroundColor = XLColor.FromHtml("#1F4E79");
            cell.Style.Font.FontColor = XLColor.White;
            cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            cell.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
        }

        // 2 dòng dữ liệu mẫu chuẩn nghiệp vụ
        var sampleRows = new (string Code, string Name, string Type, string Unit, decimal? Price, string Note)[]
        {
            ("DV-VC-01", "Vận chuyển thiết bị công trình", "Dịch vụ", "Chuyến", 15000000m, "Vận chuyển nội thành và liên tỉnh"),
            ("CP-TK-01", "Chi phí tiếp khách và điều hành dự án", "Chi phí ngoài", "Gói", 5000000m, "Phê duyệt theo định mức dự toán"),
        };

        for (var i = 0; i < sampleRows.Length; i++)
        {
            var r = sampleRows[i];
            var rowIdx = i + 2;
            sheet.Cell(rowIdx, 1).Value = r.Code;
            sheet.Cell(rowIdx, 2).Value = r.Name;
            sheet.Cell(rowIdx, 3).Value = r.Type;
            sheet.Cell(rowIdx, 4).Value = r.Unit;
            if (r.Price.HasValue)
            {
                sheet.Cell(rowIdx, 5).Value = r.Price.Value;
                sheet.Cell(rowIdx, 5).Style.NumberFormat.Format = "#,##0";
            }
            sheet.Cell(rowIdx, 6).Value = r.Note;
        }

        sheet.Columns().AdjustToContents(15.0, 45.0);
        sheet.SheetView.FreezeRows(1);

        // ── Sheet 2: Hướng dẫn nhập liệu ──────────────────────────────────────
        var guideSheet = workbook.Worksheets.Add("Huong dan");
        guideSheet.Cell(1, 1).Value = "HƯỚNG DẪN NHẬP DỮ LIỆU DỊCH VỤ & CHI PHÍ NGOÀI";
        guideSheet.Cell(1, 1).Style.Font.Bold = true;
        guideSheet.Cell(1, 1).Style.Font.FontSize = 13;

        guideSheet.Cell(3, 1).Value = "1. Các cột có dấu (*) là bắt buộc: Mã (*), Tên dịch vụ / chi phí (*), Phân loại (*), Đơn vị tính (*).";
        guideSheet.Cell(4, 1).Value = "2. Mã định danh: Viết hoa, không dấu cách, không chứa ký tự đặc biệt (chỉ cho phép chữ, số, dấu gạch nối '-', '_', '.', '/').";
        guideSheet.Cell(5, 1).Value = "3. Phân loại: Nhập \"Dịch vụ\" (service) hoặc \"Chi phí ngoài\" (expense).";
        guideSheet.Cell(6, 1).Value = "4. Đơn vị tính: Chuyến, Gói, Ngày, Công, Lần, Tháng, Ca, Hệ...";
        guideSheet.Cell(7, 1).Value = "5. Đơn giá tham chiếu: Số tiền VNĐ dự kiến (lớn hơn hoặc bằng 0), có thể để trống.";
        guideSheet.Cell(8, 1).Value = "6. Ghi chú: Diễn giải phạm vi, điều kiện hoặc dự toán.";
        guideSheet.Cell(9, 1).Value = "7. Hệ thống tự động kiểm tra trùng mã trong file và kiểm tra mã đã có trong CSDL.";
        guideSheet.Cell(10, 1).Value = "8. Khi import, có thể tích chọn \"Ghi đè / Cập nhật dòng đã tồn tại\" để cập nhật thông tin.";

        guideSheet.Columns().AdjustToContents();

        using var ms = new MemoryStream();
        workbook.SaveAs(ms);
        return Task.FromResult(ms.ToArray());
    }

    public async Task<ServiceExpenseImportParseResultDto> ParseImportFileAsync(
        Stream stream, string fileName, CancellationToken ct = default)
    {
        if (stream.CanSeek && stream.Length > MaxFileBytes)
            throw new DomainRuleException("File vượt quá kích thước cho phép (5MB).");

        var extension = Path.GetExtension(fileName).ToLowerInvariant();
        var rawRows = extension switch
        {
            ".xlsx" or ".xls" => ReadExcelRows(stream),
            ".csv" => ReadCsvRows(stream),
            _ => throw new DomainRuleException("Chỉ chấp nhận file .xlsx, .xls hoặc .csv."),
        };

        if (rawRows.Count == 0)
            throw new DomainRuleException("File không có dữ liệu.");

        var headerIndex = FindHeaderRowIndex(rawRows);
        if (headerIndex < 0)
            throw new DomainRuleException("Không tìm thấy dòng tiêu đề chứa các cột (Mã, Tên, Phân loại, Đơn vị tính...).");

        var colMap = MapColumns(rawRows[headerIndex].Cells);
        if (colMap.Code < 0)
            throw new DomainRuleException("Không tìm thấy cột \"Mã\" (hoặc \"Mã dịch vụ / chi phí\", \"Code\") trong file.");
        if (colMap.Name < 0)
            throw new DomainRuleException("Không tìm thấy cột \"Tên dịch vụ / chi phí\" (hoặc \"Tên\", \"Name\") trong file.");

        var dataRows = rawRows.Skip(headerIndex + 1)
            .Where(r => !IsEmptyRow(r.Cells) && !IsSubHeaderRow(r.Cells, colMap.Code))
            .Take(MaxDataRows)
            .ToList();

        if (dataRows.Count == 0)
            throw new DomainRuleException("File không có dòng dữ liệu nào.");

        // Lấy danh sách các mã đã có trong database để kiểm tra tồn tại
        var existingCodesList = await db.ServiceExpenseItems
            .AsNoTracking()
            .Where(s => s.DeletedAt == null)
            .Select(s => s.Code)
            .ToListAsync(ct);
        var existingCodesSet = new HashSet<string>(existingCodesList, StringComparer.OrdinalIgnoreCase);

        var seenCodesInFile = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var parsedRows = new List<ServiceExpenseImportRowDto>(dataRows.Count);

        for (var i = 0; i < dataRows.Count; i++)
        {
            var rawRow = dataRows[i];
            var rowNum = rawRow.RowNumber;
            var cells = rawRow.Cells;
            var errors = new List<string>();

            // 1. Mã
            var rawCode = GetCell(cells, colMap.Code).Trim();
            var code = rawCode.ToUpperInvariant();

            if (string.IsNullOrWhiteSpace(code))
            {
                errors.Add("Mã không được để trống.");
            }
            else if (!CodeRegex.IsMatch(code))
            {
                errors.Add("Mã không được chứa khoảng trắng hoặc ký tự đặc biệt ngoài '-', '_', '.', '/'.");
            }
            else
            {
                if (seenCodesInFile.TryGetValue(code, out var prevRow))
                {
                    errors.Add($"Mã '{code}' bị trùng lặp với dòng {prevRow} trong file.");
                }
                else
                {
                    seenCodesInFile[code] = rowNum;
                }
            }

            // 2. Tên
            var name = GetCell(cells, colMap.Name).Trim();
            if (string.IsNullOrWhiteSpace(name))
            {
                errors.Add("Tên dịch vụ / chi phí không được để trống.");
            }

            // 3. Phân loại (service | expense)
            var rawType = colMap.Type >= 0 ? GetCell(cells, colMap.Type).Trim() : string.Empty;
            var normalizedType = NormalizeType(rawType);
            if (normalizedType is null)
            {
                errors.Add($"Phân loại '{rawType}' không hợp lệ (chỉ chấp nhận 'Dịch vụ' hoặc 'Chi phí ngoài').");
                normalizedType = "service";
            }

            // 4. Đơn vị tính
            var unit = colMap.Unit >= 0 ? GetCell(cells, colMap.Unit).Trim() : string.Empty;
            if (string.IsNullOrWhiteSpace(unit))
            {
                errors.Add("Đơn vị tính không được để trống.");
            }

            // 5. Đơn giá tham chiếu
            decimal? refPrice = null;
            if (colMap.Price >= 0)
            {
                var rawPrice = GetCell(cells, colMap.Price).Trim();
                if (!string.IsNullOrWhiteSpace(rawPrice))
                {
                    if (TryParseDecimal(rawPrice, out var parsedVal))
                    {
                        if (parsedVal < 0)
                        {
                            errors.Add("Đơn giá tham chiếu phải lớn hơn hoặc bằng 0.");
                        }
                        else
                        {
                            refPrice = parsedVal;
                        }
                    }
                    else
                    {
                        errors.Add($"Đơn giá '{rawPrice}' không đúng định dạng số.");
                    }
                }
            }

            // 6. Ghi chú
            var description = colMap.Description >= 0 ? GetCell(cells, colMap.Description).Trim() : string.Empty;
            var descValue = string.IsNullOrWhiteSpace(description) ? null : description;

            // Kiểm tra tồn tại trong DB
            var isExisting = !string.IsNullOrEmpty(code) && existingCodesSet.Contains(code);
            var isValid = errors.Count == 0;
            var errorMessage = isValid ? null : string.Join("; ", errors);

            parsedRows.Add(new ServiceExpenseImportRowDto(
                RowNumber: rowNum,
                Code: code,
                Name: name,
                Type: normalizedType,
                Unit: unit,
                ReferencePrice: refPrice,
                Description: descValue,
                IsValid: isValid,
                ErrorMessage: errorMessage,
                IsExisting: isExisting));
        }

        var total = parsedRows.Count;
        var valid = parsedRows.Count(r => r.IsValid);
        var error = parsedRows.Count(r => !r.IsValid);
        var existing = parsedRows.Count(r => r.IsExisting);

        return new ServiceExpenseImportParseResultDto(total, valid, error, existing, parsedRows);
    }

    public async Task<ServiceExpenseImportResultDto> ImportBatchAsync(
        ServiceExpenseImportBatchRequest request, CancellationToken ct = default)
    {
        if (request?.Rows == null || request.Rows.Count == 0)
            return new ServiceExpenseImportResultDto(0, 0, 0, 0, []);

        var validRows = request.Rows.Where(r => r.IsValid).ToList();
        if (validRows.Count == 0)
            return new ServiceExpenseImportResultDto(0, 0, 0, 0, ["Không có dòng dữ liệu hợp lệ nào để import."]);

        var isRelational = db.Database.IsRelational();
        IDbContextTransaction? tx = isRelational ? await db.Database.BeginTransactionAsync(ct) : null;

        try
        {
            var codes = validRows
                .Select(r => r.Code.Trim().ToUpperInvariant())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            var existingItems = await db.ServiceExpenseItems
                .Where(s => codes.Contains(s.Code))
                .ToListAsync(ct);

            var existingDict = existingItems.ToDictionary(s => s.Code, StringComparer.OrdinalIgnoreCase);

            var insertedCount = 0;
            var updatedCount = 0;
            var skippedCount = 0;
            var errors = new List<string>();
            var processedCodes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            var now = DateTime.UtcNow;

            foreach (var row in validRows)
            {
                var code = row.Code.Trim().ToUpperInvariant();
                if (!processedCodes.Add(code)) continue;

                if (existingDict.TryGetValue(code, out var existing))
                {
                    if (request.OverwriteExisting)
                    {
                        existing.Name = row.Name.Trim();
                        existing.Type = row.Type;
                        existing.Unit = row.Unit.Trim();
                        existing.ReferencePrice = row.ReferencePrice >= 0 ? row.ReferencePrice : null;
                        existing.Description = string.IsNullOrWhiteSpace(row.Description) ? null : row.Description.Trim();
                        existing.IsActive = true;
                        existing.DeletedAt = null;
                        existing.UpdatedAt = now;
                        updatedCount++;
                    }
                    else
                    {
                        skippedCount++;
                    }
                }
                else
                {
                    var newItem = new ServiceExpenseItem
                    {
                        Code = code,
                        Name = row.Name.Trim(),
                        Type = row.Type,
                        Unit = row.Unit.Trim(),
                        ReferencePrice = row.ReferencePrice >= 0 ? row.ReferencePrice : null,
                        Description = string.IsNullOrWhiteSpace(row.Description) ? null : row.Description.Trim(),
                        IsActive = true,
                        SortOrder = 0,
                        CreatedAt = now,
                        UpdatedAt = now,
                    };
                    db.ServiceExpenseItems.Add(newItem);
                    existingDict[code] = newItem;
                    insertedCount++;
                }
            }

            await db.SaveChangesAsync(ct);
            if (tx is not null) await tx.CommitAsync(ct);

            return new ServiceExpenseImportResultDto(
                TotalProcessed: insertedCount + updatedCount + skippedCount,
                InsertedCount: insertedCount,
                UpdatedCount: updatedCount,
                SkippedCount: skippedCount,
                Errors: errors);
        }
        catch (Exception ex)
        {
            if (tx is not null) await tx.RollbackAsync(ct);
            throw new DomainRuleException($"Lỗi trong quá trình import dữ liệu: {ex.Message}");
        }
    }

    #region Helper Methods

    private record RawExcelRow(int RowNumber, string[] Cells);

    private record ColumnMap(int Code, int Name, int Type, int Unit, int Price, int Description);

    private static int FindHeaderRowIndex(List<RawExcelRow> rows)
    {
        for (var i = 0; i < Math.Min(rows.Count, 15); i++)
        {
            var cells = rows[i].Cells;
            var text = string.Join(" ", cells.Select(NormalizeHeader));
            if (text.Contains("ma") && (text.Contains("ten") || text.Contains("dichvu") || text.Contains("chiphi") || text.Contains("phanloai")))
            {
                return i;
            }
        }
        return -1;
    }

    private static ColumnMap MapColumns(string[] headers)
    {
        var norm = headers.Select(NormalizeHeader).ToArray();

        var codeIdx = FindColumn(norm, "madichvuchiphi", "madichvu", "machiphi", "madinhdanh", "code", "servicecode", "ma");
        var nameIdx = FindColumn(norm, "tendichvuchiphi", "tendichvu", "tenchiphi", "tendv", "tencp", "name", "ten", "diengiai", "mota");
        var typeIdx = FindColumn(norm, "phanloaidichvuchiphingoai", "phanloai", "phanloaidvcp", "type", "loai", "nhom");
        var unitIdx = FindColumn(norm, "donvitinh", "donvi", "dvt", "unit");
        var priceIdx = FindColumn(norm, "dongiathamchieu", "giathamchieu", "dongia", "gia", "referenceprice", "price");
        var descIdx = FindColumn(norm, "ghichu", "diengiai", "mota", "description", "note", "notes");

        // Nếu cột name và desc trùng nhau do alias "diengiai", tách biệt lại
        if (nameIdx == descIdx && nameIdx >= 0)
        {
            descIdx = FindColumn(norm, "ghichu", "mota", "description", "note", "notes");
        }

        return new ColumnMap(codeIdx, nameIdx, typeIdx, unitIdx, priceIdx, descIdx);
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

    private static string? NormalizeType(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return "service";
        var norm = NormalizeHeader(raw);
        if (norm.Contains("dichvu") || norm.Equals("service") || norm.Equals("dv"))
            return "service";
        if (norm.Contains("chiphi") || norm.Equals("expense") || norm.Equals("cp"))
            return "expense";
        return null;
    }

    private static bool TryParseDecimal(string raw, out decimal value)
    {
        value = 0;
        var text = raw.Trim();
        if (string.IsNullOrEmpty(text)) return false;

        text = text.Replace(" ", string.Empty)
                   .Replace("đ", string.Empty, StringComparison.OrdinalIgnoreCase)
                   .Replace("VND", string.Empty, StringComparison.OrdinalIgnoreCase)
                   .Replace("VNĐ", string.Empty, StringComparison.OrdinalIgnoreCase);

        if (text.Contains('.') && text.Contains(','))
        {
            if (text.LastIndexOf('.') > text.LastIndexOf(','))
            {
                text = text.Replace(",", string.Empty);
            }
            else
            {
                text = text.Replace(".", string.Empty).Replace(',', '.');
            }
        }
        else if (text.Contains('.'))
        {
            var dotCount = text.Count(c => c == '.');
            if (dotCount > 1 || (text.Length - text.LastIndexOf('.') - 1 == 3 && dotCount == 1 && text.Length > 6))
            {
                text = text.Replace(".", string.Empty);
            }
        }
        else if (text.Contains(','))
        {
            var commaCount = text.Count(c => c == ',');
            if (commaCount > 1 || (text.Length - text.LastIndexOf(',') - 1 == 3 && commaCount == 1 && text.Length > 6))
            {
                text = text.Replace(",", string.Empty);
            }
            else
            {
                text = text.Replace(',', '.');
            }
        }

        if (decimal.TryParse(text, NumberStyles.Number, CultureInfo.InvariantCulture, out value))
            return true;
        if (decimal.TryParse(raw.Trim(), NumberStyles.Number, new CultureInfo("vi-VN"), out value))
            return true;

        return false;
    }

    private static List<RawExcelRow> ReadExcelRows(Stream stream)
    {
        using var workbook = new XLWorkbook(stream);
        var sheet = workbook.Worksheets.FirstOrDefault(w => w.RangeUsed() != null && w.RangeUsed().RowCount() > 1)
            ?? workbook.Worksheets.FirstOrDefault()
            ?? throw new DomainRuleException("File Excel không có sheet nào.");

        var used = sheet.RangeUsed();
        if (used is null) return [];

        var result = new List<RawExcelRow>();
        foreach (var row in used.Rows())
        {
            var cells = new string[row.CellCount()];
            for (var c = 0; c < row.CellCount(); c++)
                cells[c] = row.Cell(c + 1).GetFormattedString().Trim();
            result.Add(new RawExcelRow(row.RowNumber(), cells));
        }
        return result;
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

    private static bool IsSubHeaderRow(string[] row, int codeIndex)
    {
        var code = GetCell(row, codeIndex).Trim().ToUpperInvariant();
        if (code.StartsWith("STT") || code.StartsWith("NO.") || code.Equals("MÃ") || code.Equals("MA"))
            return true;
        return false;
    }

    private static string GetCell(string[] row, int index) =>
        index >= 0 && index < row.Length ? row[index] : string.Empty;

    #endregion
}
