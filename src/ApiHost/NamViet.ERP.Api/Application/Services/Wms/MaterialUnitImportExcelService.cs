using System.Globalization;
using System.Text;
using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using NamViet.ERP.Api.Application.Dtos.Wms;
using NamViet.ERP.Modules.Warehouse.Application.Helpers;
using NamViet.ERP.Modules.Warehouse.Domain.Entities;
using NamViet.ERP.Modules.Warehouse.Infrastructure.Persistence;
using NamViet.ERP.SharedKernel.Common;

namespace NamViet.ERP.Api.Application.Services.Wms;

public sealed class MaterialUnitImportExcelService(WarehouseDbContext db) : IMaterialUnitImportExcelService
{
    private const int MaxDataRows = 2000;
    private const long MaxFileBytes = 5 * 1024 * 1024; // 5MB

    private static readonly string[] TemplateHeaders =
    [
        "Tên đơn vị tính (*)",
        "Thứ tự sắp xếp",
        "Trạng thái hoạt động",
        "Ghi chú",
    ];

    public Task<byte[]> GenerateTemplateAsync(CancellationToken ct = default)
    {
        using var workbook = new XLWorkbook();

        // ── Sheet 1: Danh mục đơn vị tính mẫu ───────────────────────────────
        var sheet = workbook.Worksheets.Add("Don vi tinh");

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

        // Dòng mẫu chuẩn nghiệp vụ
        var sampleRows = new (string Name, int SortOrder, string Status, string Note)[]
        {
            ("Bộ", 1, "Bật", "Bộ thiết bị, phụ kiện"),
            ("Mét dài", 2, "Bật", "Ống, dây cáp, thanh thép"),
            ("Chuyến", 3, "Bật", "Vận tải, xe cẩu"),
            ("Cuộn", 4, "Bật", "Băng dính, cáp cuộn"),
            ("Tấn", 5, "Bật", "Vật liệu rời trọng lượng lớn"),
        };

        for (var i = 0; i < sampleRows.Length; i++)
        {
            var r = sampleRows[i];
            var rowIdx = i + 2;
            sheet.Cell(rowIdx, 1).Value = r.Name;
            sheet.Cell(rowIdx, 2).Value = r.SortOrder;
            sheet.Cell(rowIdx, 3).Value = r.Status;
            sheet.Cell(rowIdx, 4).Value = r.Note;
        }

        sheet.Columns().AdjustToContents(15.0, 45.0);
        sheet.SheetView.FreezeRows(1);

        // ── Sheet 2: Hướng dẫn nhập liệu ──────────────────────────────────────
        var guideSheet = workbook.Worksheets.Add("Huong dan");
        guideSheet.Cell(1, 1).Value = "HƯỚNG DẪN NHẬP DỮ LIỆU ĐƠN VỊ TÍNH (MATERIAL UNITS)";
        guideSheet.Cell(1, 1).Style.Font.Bold = true;
        guideSheet.Cell(1, 1).Style.Font.FontSize = 13;

        guideSheet.Cell(3, 1).Value = "1. Cột có dấu (*) là bắt buộc: Tên đơn vị tính (*).";
        guideSheet.Cell(4, 1).Value = "2. Tên đơn vị tính: Tự động chuẩn hóa chữ cái hoa đầu từ, loại bỏ khoảng trắng thừa (ví dụ: mét khối -> Mét khối).";
        guideSheet.Cell(5, 1).Value = "3. Thứ tự sắp xếp: Số nguyên dương (1, 2, 3...). Để trống hệ thống sẽ tự động đánh số tiếp theo.";
        guideSheet.Cell(6, 1).Value = "4. Trạng thái hoạt động: Nhập \"Bật\" (hoạt động) hoặc \"Tắt\" (tạm ngừng). Mặc định là \"Bật\".";
        guideSheet.Cell(7, 1).Value = "5. Ghi chú: Diễn giải ngữ cảnh sử dụng (không bắt buộc).";
        guideSheet.Cell(8, 1).Value = "6. Hệ thống tự động kiểm tra trùng tên trong file và kiểm tra tên đã có trong CSDL.";
        guideSheet.Cell(9, 1).Value = "7. Khi import, có thể tích chọn \"Ghi đè / Cập nhật dòng đã tồn tại\" để cập nhật trạng thái/thứ tự.";

        guideSheet.Columns().AdjustToContents();

        using var ms = new MemoryStream();
        workbook.SaveAs(ms);
        return Task.FromResult(ms.ToArray());
    }

    public async Task<MaterialUnitImportParseResultDto> ParseImportFileAsync(
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
            throw new DomainRuleException("Không tìm thấy dòng tiêu đề chứa các cột (Tên đơn vị tính, Thứ tự, Trạng thái...).");

        var colMap = MapColumns(rawRows[headerIndex].Cells);
        if (colMap.Name < 0)
            throw new DomainRuleException("Không tìm thấy cột \"Tên đơn vị tính\" (hoặc \"Đơn vị tính\", \"Unit\", \"Name\") trong file.");

        var dataRows = rawRows.Skip(headerIndex + 1)
            .Where(r => !IsEmptyRow(r.Cells) && !IsSubHeaderRow(r.Cells, colMap.Name))
            .Take(MaxDataRows)
            .ToList();

        if (dataRows.Count == 0)
            throw new DomainRuleException("File không có dòng dữ liệu nào.");

        // Danh sách tên đơn vị tính đã có trong DB
        var existingNamesList = await db.MaterialUnits
            .AsNoTracking()
            .Select(u => u.Name)
            .ToListAsync(ct);
        var existingNamesSet = new HashSet<string>(existingNamesList, StringComparer.OrdinalIgnoreCase);

        var seenNamesInFile = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var parsedRows = new List<MaterialUnitImportRowDto>(dataRows.Count);
        var errorDtos = new List<MaterialUnitImportErrorDto>();

        var defaultSortOrder = existingNamesList.Count;

        for (var i = 0; i < dataRows.Count; i++)
        {
            var rawRow = dataRows[i];
            var rowNum = rawRow.RowNumber;
            var cells = rawRow.Cells;
            var errors = new List<string>();

            // 1. Tên đơn vị tính
            var rawName = GetCell(cells, colMap.Name);
            var normalizedName = MaterialUnitHelper.NormalizeDisplayName(rawName);

            if (string.IsNullOrWhiteSpace(normalizedName))
            {
                var err = "Tên đơn vị tính không được để trống.";
                errors.Add(err);
                errorDtos.Add(new MaterialUnitImportErrorDto(rowNum, "Name", err));
            }
            else
            {
                if (seenNamesInFile.TryGetValue(normalizedName, out var prevRow))
                {
                    var err = $"Tên đơn vị tính '{normalizedName}' bị trùng lặp với dòng {prevRow} trong file.";
                    errors.Add(err);
                    errorDtos.Add(new MaterialUnitImportErrorDto(rowNum, "Name", err));
                }
                else
                {
                    seenNamesInFile[normalizedName] = rowNum;
                }
            }

            // 2. Thứ tự sắp xếp
            var sortOrder = 0;
            if (colMap.SortOrder >= 0)
            {
                var rawSort = GetCell(cells, colMap.SortOrder).Trim();
                if (!string.IsNullOrWhiteSpace(rawSort))
                {
                    if (int.TryParse(rawSort, out var parsedSort) && parsedSort >= 0)
                    {
                        sortOrder = parsedSort;
                    }
                    else
                    {
                        var err = $"Thứ tự sắp xếp '{rawSort}' không hợp lệ (phải là số nguyên không âm).";
                        errors.Add(err);
                        errorDtos.Add(new MaterialUnitImportErrorDto(rowNum, "SortOrder", err));
                    }
                }
            }
            if (sortOrder <= 0)
            {
                defaultSortOrder++;
                sortOrder = defaultSortOrder;
            }

            // 3. Trạng thái hoạt động
            var isActive = true;
            if (colMap.IsActive >= 0)
            {
                var rawStatus = GetCell(cells, colMap.IsActive).Trim();
                if (!string.IsNullOrWhiteSpace(rawStatus))
                {
                    var normStatus = NormalizeHeader(rawStatus);
                    if (normStatus is "tat" or "ngung" or "false" or "0" or "khong" or "inactive" or "disabled")
                    {
                        isActive = false;
                    }
                    else if (normStatus is "bat" or "hoatdong" or "true" or "1" or "co" or "active" or "enabled")
                    {
                        isActive = true;
                    }
                    else
                    {
                        var err = $"Trạng thái '{rawStatus}' không hợp lệ (chỉ chấp nhận Bật/Tắt).";
                        errors.Add(err);
                        errorDtos.Add(new MaterialUnitImportErrorDto(rowNum, "IsActive", err));
                    }
                }
            }

            // 4. Ghi chú
            var note = colMap.Note >= 0 ? GetCell(cells, colMap.Note).Trim() : string.Empty;
            var noteValue = string.IsNullOrWhiteSpace(note) ? null : note;

            var isExisting = !string.IsNullOrEmpty(normalizedName) && existingNamesSet.Contains(normalizedName);
            var isValid = errors.Count == 0;
            var errorMessage = isValid ? null : string.Join("; ", errors);

            parsedRows.Add(new MaterialUnitImportRowDto(
                RowNumber: rowNum,
                Name: normalizedName,
                SortOrder: sortOrder,
                IsActive: isActive,
                Note: noteValue,
                IsValid: isValid,
                ErrorMessage: errorMessage,
                IsExisting: isExisting));
        }

        var total = parsedRows.Count;
        var valid = parsedRows.Count(r => r.IsValid);
        var existing = parsedRows.Count(r => r.IsExisting);
        var error = parsedRows.Count(r => !r.IsValid);

        return new MaterialUnitImportParseResultDto(total, valid, existing, error, parsedRows, errorDtos);
    }

    public async Task<MaterialUnitImportResultDto> ImportBatchAsync(
        MaterialUnitImportBatchRequest request, CancellationToken ct = default)
    {
        if (request?.Rows == null || request.Rows.Count == 0)
            return new MaterialUnitImportResultDto(0, 0, 0, 0, [], []);

        var validRows = request.Rows.Where(r => r.IsValid).ToList();
        if (validRows.Count == 0)
            return new MaterialUnitImportResultDto(0, 0, 0, 0, [], ["Không có dòng dữ liệu hợp lệ nào để import."]);

        var isRelational = db.Database.IsRelational();
        IDbContextTransaction? tx = isRelational ? await db.Database.BeginTransactionAsync(ct) : null;

        try
        {
            var names = validRows
                .Select(r => MaterialUnitHelper.NormalizeDisplayName(r.Name))
                .Where(n => !string.IsNullOrEmpty(n))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            var existingItems = await db.MaterialUnits
                .Where(u => names.Contains(u.Name))
                .ToListAsync(ct);

            var existingDict = existingItems.ToDictionary(u => u.Name, StringComparer.OrdinalIgnoreCase);

            var createdCount = 0;
            var updatedCount = 0;
            var skippedCount = 0;
            var errors = new List<string>();
            var importedNames = new List<string>();
            var processedNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            var now = DateTime.UtcNow;
            var maxExistingSort = await db.MaterialUnits.AnyAsync(ct)
                ? await db.MaterialUnits.MaxAsync(u => u.SortOrder, ct)
                : 0;

            foreach (var row in validRows)
            {
                var name = MaterialUnitHelper.NormalizeDisplayName(row.Name);
                if (string.IsNullOrEmpty(name) || !processedNames.Add(name))
                    continue;

                if (existingDict.TryGetValue(name, out var existing))
                {
                    if (request.OverwriteExisting)
                    {
                        if (row.SortOrder > 0)
                            existing.SortOrder = row.SortOrder;
                        existing.IsActive = row.IsActive;
                        existing.UpdatedAt = now;
                        updatedCount++;
                        importedNames.Add(name);
                    }
                    else
                    {
                        skippedCount++;
                    }
                }
                else
                {
                    var sortOrder = row.SortOrder > 0 ? row.SortOrder : ++maxExistingSort;
                    var newUnit = new MaterialUnit
                    {
                        Name = name,
                        SortOrder = sortOrder,
                        IsActive = row.IsActive,
                        CreatedAt = now,
                        UpdatedAt = now,
                    };
                    db.MaterialUnits.Add(newUnit);
                    existingDict[name] = newUnit;
                    createdCount++;
                    importedNames.Add(name);
                }
            }

            await db.SaveChangesAsync(ct);
            if (tx is not null) await tx.CommitAsync(ct);

            return new MaterialUnitImportResultDto(
                CreatedCount: createdCount,
                UpdatedCount: updatedCount,
                SkippedCount: skippedCount,
                TotalProcessed: createdCount + updatedCount + skippedCount,
                ImportedNames: importedNames,
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

    private record ColumnMap(int Name, int SortOrder, int IsActive, int Note);

    private static int FindHeaderRowIndex(List<RawExcelRow> rows)
    {
        for (var i = 0; i < Math.Min(rows.Count, 15); i++)
        {
            var cells = rows[i].Cells;
            var text = string.Join(" ", cells.Select(NormalizeHeader));
            if (text.Contains("tendonvitinh") || text.Contains("donvitinh") || text.Contains("unit") || (text.Contains("ten") && text.Contains("donvi")))
            {
                return i;
            }
        }
        // Fallback: check if row has "ten" and "thutu" or "trangthai"
        for (var i = 0; i < Math.Min(rows.Count, 15); i++)
        {
            var cells = rows[i].Cells;
            var text = string.Join(" ", cells.Select(NormalizeHeader));
            if (text.Contains("ten") && (text.Contains("thutu") || text.Contains("trangthai") || text.Contains("sapxep")))
            {
                return i;
            }
        }
        return -1;
    }

    private static ColumnMap MapColumns(string[] headers)
    {
        var norm = headers.Select(NormalizeHeader).ToArray();

        var nameIdx = FindColumn(norm, "tendonvitinh", "tendonvi", "donvitinh", "donvi", "dvt", "unit", "unitname", "ten", "name");
        var sortIdx = FindColumn(norm, "thutusapxep", "thutu", "sapxep", "sortorder", "sort", "stt");
        var activeIdx = FindColumn(norm, "trangthaihoatdong", "trangthai", "kichhoat", "isactive", "status", "active");
        var noteIdx = FindColumn(norm, "ghichu", "mota", "diengiai", "note", "notes", "description");

        return new ColumnMap(nameIdx, sortIdx, activeIdx, noteIdx);
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

    private static bool IsSubHeaderRow(string[] row, int nameIndex)
    {
        var name = GetCell(row, nameIndex).Trim().ToUpperInvariant();
        if (name.StartsWith("STT") || name.StartsWith("NO.") || name.Equals("TÊN") || name.Equals("TEN"))
            return true;
        return false;
    }

    private static string GetCell(string[] row, int index) =>
        index >= 0 && index < row.Length ? row[index] : string.Empty;

    #endregion
}
