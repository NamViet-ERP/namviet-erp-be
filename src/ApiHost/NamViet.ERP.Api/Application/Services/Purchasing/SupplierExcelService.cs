using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;
using NamViet.ERP.Modules.Warehouse.Application.DTOs;
using NamViet.ERP.Modules.Warehouse.Domain.Entities;
using NamViet.ERP.Modules.Warehouse.Domain.SupplierEvaluation;
using NamViet.ERP.Modules.Warehouse.Infrastructure.Persistence;
using NamViet.ERP.SharedKernel.Common;

namespace NamViet.ERP.Api.Application.Services.Purchasing;

public interface ISupplierExcelService
{
    byte[] BuildExcelTemplate();

    Task<(SupplierImportPreviewResponseDto? Response, string? ErrorMessage)> PreviewExcelAsync(
        Stream stream,
        string? requestedSheetName,
        CancellationToken ct = default);

    Task<(SupplierImportResultDto? Result, string? ErrorMessage)> ImportExcelAsync(
        Stream stream,
        string? requestedSheetName,
        CancellationToken ct = default);
}

public sealed class SupplierExcelService(WarehouseDbContext db) : ISupplierExcelService
{
    private static readonly char[] GroupSeparators = [',', ';'];
    private static readonly string[] PrioritySheetNames =
        ["OPTION 3", "OPTION", "MÃ NCC", "MA NCC", "NHÀ CUNG CẤP", "SUPPLIER", "FINAL"];

    public byte[] BuildExcelTemplate()
    {
        using var workbook = new XLWorkbook();
        var ws = workbook.Worksheets.Add("DanhSachNCC");

        string[] headers =
        [
            "Mã NCC (để trống tự sinh)",
            "Tên nhà cung cấp",
            "Mã số thuế",
            "Số điện thoại",
            "Website",
            "Loại hình (Nhà sản xuất / Nhà phân phối / Đại lý)",
            "Phạm vi (Trong nước / Nhập khẩu)",
            "Hạn nợ (ngày)",
            "Hạn mức nợ (tiền)",
            "Điều khoản thanh toán",
            "Nhóm ngành hàng"
        ];

        for (var col = 0; col < headers.Length; col++)
        {
            var cell = ws.Cell(1, col + 1);
            cell.Value = headers[col];
            cell.Style.Font.Bold = true;
            cell.Style.Fill.BackgroundColor = XLColor.FromHtml("#1E40AF");
            cell.Style.Font.FontColor = XLColor.White;
        }

        ws.Cell(2, 1).Value = "";
        ws.Cell(2, 2).Value = "Công ty TNHH Thép Hòa Phát Hưng Yên";
        ws.Cell(2, 3).Value = "0100123456";
        ws.Cell(2, 4).Value = "0987654321";
        ws.Cell(2, 5).Value = "https://hoaphat.com.vn";
        ws.Cell(2, 6).Value = "Nhà sản xuất";
        ws.Cell(2, 7).Value = "Trong nước";
        ws.Cell(2, 8).Value = 45;
        ws.Cell(2, 9).Value = 500000000;
        ws.Cell(2, 10).Value = "Thanh toán trong vòng 45 ngày";
        ws.Cell(2, 11).Value = "Hệ ống thép & phụ kiện hàn";

        ws.Cell(3, 1).Value = "";
        ws.Cell(3, 2).Value = "Công ty Cổ phần Van Kitz Nhật Bản";
        ws.Cell(3, 3).Value = "0309988776";
        ws.Cell(3, 4).Value = "0903123456";
        ws.Cell(3, 5).Value = "https://kitz.co.jp";
        ws.Cell(3, 6).Value = "Nhà phân phối";
        ws.Cell(3, 7).Value = "Nhập khẩu";
        ws.Cell(3, 8).Value = 30;
        ws.Cell(3, 9).Value = 300000000;
        ws.Cell(3, 10).Value = "Net 30 kể từ ngày giao hàng";
        ws.Cell(3, 11).Value = "Hệ van công nghiệp & phụ kiện mặt bích";

        ws.Columns().AdjustToContents();

        using var ms = new MemoryStream();
        workbook.SaveAs(ms);
        return ms.ToArray();
    }

    public async Task<(SupplierImportPreviewResponseDto? Response, string? ErrorMessage)> PreviewExcelAsync(
        Stream stream,
        string? requestedSheetName,
        CancellationToken ct = default)
    {
        using var workbook = new XLWorkbook(stream);
        var (rawRows, availableSheets, selectedSheet) = ExtractSupplierRowsFromExcel(workbook, requestedSheetName);
        if (availableSheets.Count == 0)
            return (null, "File Excel không có sheet nào.");

        var (existingNames, existingCodes, existingTaxCodes) = await LoadExistingLookupsAsync(ct);

        var previewRows = new List<SupplierImportRowPreviewDto>(rawRows.Count);
        var seenNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var seenCodes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var seenTaxCodes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var raw in rawRows)
        {
            var errorMsg = new List<string>();

            if (string.IsNullOrWhiteSpace(raw.Name))
            {
                errorMsg.Add("Tên NCC là bắt buộc");
            }
            else if (IsDuplicateName(raw.Name, existingNames, seenNames))
            {
                errorMsg.Add("Tên NCC đã tồn tại hoặc bị trùng trong file");
            }
            else
            {
                seenNames.Add(raw.Name);
            }

            if (HasExplicitCode(raw.Code))
            {
                if (IsDuplicateCode(raw.Code!, existingCodes, seenCodes))
                    errorMsg.Add($"Mã NCC '{raw.Code}' đã tồn tại trên hệ thống hoặc bị trùng trong file");
                else
                    seenCodes.Add(raw.Code!);
            }

            if (!string.IsNullOrWhiteSpace(raw.TaxCode))
            {
                if (raw.TaxCode.Length > 50)
                    errorMsg.Add("Mã số thuế tối đa 50 ký tự");
                else if (IsDuplicateTaxCode(raw.TaxCode, existingTaxCodes, seenTaxCodes))
                    errorMsg.Add($"MST '{raw.TaxCode}' đã tồn tại trên hệ thống hoặc bị trùng trong file");
                else
                    seenTaxCodes.Add(raw.TaxCode);
            }

            previewRows.Add(new SupplierImportRowPreviewDto(
                SupplierCode: string.IsNullOrWhiteSpace(raw.Code) ? "(Tự sinh)" : raw.Code,
                SupplierName: raw.Name,
                TaxCode: string.IsNullOrWhiteSpace(raw.TaxCode) ? null : raw.TaxCode,
                ContactPhone: string.IsNullOrWhiteSpace(raw.Phone) ? null : raw.Phone,
                Website: string.IsNullOrWhiteSpace(raw.Website) ? null : raw.Website,
                SupplierType: string.IsNullOrWhiteSpace(raw.SupplierType) ? null : raw.SupplierType,
                Scope: string.IsNullOrWhiteSpace(raw.Scope) ? null : raw.Scope,
                MaxDebtDays: raw.Debt,
                CreditLimit: raw.CreditLimit,
                PaymentTerms: string.IsNullOrWhiteSpace(raw.Terms) ? null : raw.Terms,
                IndustryGroups: string.IsNullOrWhiteSpace(raw.Groups) ? null : raw.Groups,
                IsValid: errorMsg.Count == 0,
                Error: errorMsg.Count > 0 ? string.Join("; ", errorMsg) : null));
        }

        var total = previewRows.Count;
        var valid = previewRows.Count(r => r.IsValid);
        var errors = total - valid;

        return (new SupplierImportPreviewResponseDto(total, valid, errors, previewRows, availableSheets, selectedSheet), null);
    }

    public async Task<(SupplierImportResultDto? Result, string? ErrorMessage)> ImportExcelAsync(
        Stream stream,
        string? requestedSheetName,
        CancellationToken ct = default)
    {
        using var workbook = new XLWorkbook(stream);
        var (rawRows, availableSheets, _) = ExtractSupplierRowsFromExcel(workbook, requestedSheetName);
        if (availableSheets.Count == 0)
            return (null, "File Excel không có sheet nào.");

        var (existingNames, existingCodes, existingTaxCodes) = await LoadExistingLookupsAsync(ct);

        // Include soft-deleted supplier codes so auto-generated codes never violate unique index.
        var allCodesIncludingDeleted = await db.Suppliers.IgnoreQueryFilters()
            .Select(s => s.SupplierCode)
            .ToListAsync(ct);

        var nextSeq = ComputeNextSequence(allCodesIncludingDeleted);
        var usedCodeSet = new HashSet<string>(allCodesIncludingDeleted, StringComparer.OrdinalIgnoreCase);

        var allGroups = await db.SupplierGroups.AsNoTracking().ToListAsync(ct);
        var groupMap = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var group in allGroups)
            groupMap.TryAdd(group.Name.Trim(), group.Id);

        var failedCount = 0;
        var errors = new List<string>();
        var seenNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var seenCodes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var seenTaxCodes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var stagedSuppliers = new List<(Supplier Entity, string? RawGroups)>(rawRows.Count);
        var nowUtc = DateTime.UtcNow;

        foreach (var raw in rawRows)
        {
            if (string.IsNullOrWhiteSpace(raw.Name))
            {
                failedCount++;
                errors.Add($"Dòng {raw.RowIndex}: Tên nhà cung cấp không được để trống.");
                continue;
            }

            if (IsDuplicateName(raw.Name, existingNames, seenNames))
            {
                failedCount++;
                errors.Add($"Dòng {raw.RowIndex}: Tên nhà cung cấp '{raw.Name}' đã tồn tại hoặc bị trùng trong file.");
                continue;
            }

            var hasExplicitCode = HasExplicitCode(raw.Code);
            if (hasExplicitCode && IsDuplicateCode(raw.Code!, existingCodes, seenCodes))
            {
                failedCount++;
                errors.Add($"Dòng {raw.RowIndex}: Mã NCC '{raw.Code}' đã tồn tại trên hệ thống hoặc bị trùng trong file.");
                continue;
            }

            if (!string.IsNullOrWhiteSpace(raw.TaxCode))
            {
                if (raw.TaxCode.Length > 50)
                {
                    failedCount++;
                    errors.Add($"Dòng {raw.RowIndex}: Mã số thuế tối đa 50 ký tự.");
                    continue;
                }
                if (IsDuplicateTaxCode(raw.TaxCode, existingTaxCodes, seenTaxCodes))
                {
                    failedCount++;
                    errors.Add($"Dòng {raw.RowIndex}: MST '{raw.TaxCode}' đã tồn tại trên hệ thống hoặc bị trùng trong file.");
                    continue;
                }
            }

            seenNames.Add(raw.Name);
            existingNames.Add(raw.Name);

            string finalCode;
            if (hasExplicitCode)
            {
                finalCode = raw.Code!;
                seenCodes.Add(finalCode);
            }
            else
            {
                do
                {
                    finalCode = Supplier.GenerateCode(nextSeq++);
                }
                while (usedCodeSet.Contains(finalCode));
            }
            usedCodeSet.Add(finalCode);
            existingCodes.Add(finalCode);

            if (!string.IsNullOrWhiteSpace(raw.TaxCode))
            {
                seenTaxCodes.Add(raw.TaxCode);
                existingTaxCodes.Add(raw.TaxCode);
            }

            var supplier = new Supplier
            {
                SupplierCode = finalCode,
                SupplierName = raw.Name,
                TaxCode = string.IsNullOrWhiteSpace(raw.TaxCode) ? null : raw.TaxCode,
                ContactPhone = NormalizeImportedPhone(raw.Phone),
                Website = TruncateOrNull(raw.Website, 255),
                SupplierType = TruncateOrNull(raw.SupplierType, 50),
                Scope = TruncateOrNull(raw.Scope, 50),
                MaxDebtDays = raw.Debt,
                CreditLimit = raw.CreditLimit,
                PaymentTerms = string.IsNullOrWhiteSpace(raw.Terms) ? null : raw.Terms,
                Rating = SupplierGrades.D,
                ApprovalStatus = SupplierApprovalStatuses.New,
                CreatedAt = nowUtc,
            };

            stagedSuppliers.Add((supplier, raw.Groups));
        }

        await using var tx = db.Database.IsRelational()
            ? await db.Database.BeginTransactionAsync(ct)
            : null;

        if (stagedSuppliers.Count > 0)
        {
            db.Suppliers.AddRange(stagedSuppliers.Select(x => x.Entity));
            await db.SaveChangesAsync(ct);

            var groupMembers = new List<SupplierGroupMember>();
            foreach (var (entity, rawGroups) in stagedSuppliers)
            {
                if (string.IsNullOrWhiteSpace(rawGroups))
                    continue;

                var groupNames = rawGroups.Split(
                    GroupSeparators,
                    StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

                foreach (var gn in groupNames)
                {
                    if (groupMap.TryGetValue(gn, out var groupId))
                    {
                        groupMembers.Add(new SupplierGroupMember
                        {
                            SupplierId = entity.Id,
                            SupplierGroupId = groupId,
                        });
                    }
                }
            }

            if (groupMembers.Count > 0)
            {
                db.SupplierGroupMembers.AddRange(groupMembers);
                await db.SaveChangesAsync(ct);
            }
        }

        if (tx is not null)
            await tx.CommitAsync(ct);

        var importedCount = stagedSuppliers.Count;
        var totalRows = importedCount + failedCount;
        return (new SupplierImportResultDto(totalRows, importedCount, failedCount, errors), null);
    }

    private async Task<(List<string> ExistingNames, List<string> ExistingCodes, List<string> ExistingTaxCodes)>
        LoadExistingLookupsAsync(CancellationToken ct)
    {
        var existingNames = await db.Suppliers.AsNoTracking()
            .Select(s => s.SupplierName)
            .ToListAsync(ct);

        var existingCodes = await db.Suppliers.AsNoTracking()
            .Select(s => s.SupplierCode)
            .ToListAsync(ct);

        var existingTaxCodes = await db.Suppliers.AsNoTracking()
            .Where(s => !string.IsNullOrEmpty(s.TaxCode))
            .Select(s => s.TaxCode!)
            .ToListAsync(ct);

        return (existingNames, existingCodes, existingTaxCodes);
    }

    private static int ComputeNextSequence(List<string> existingCodes)
    {
        var next = 1;
        foreach (var code in existingCodes)
        {
            if (Supplier.TryParseSequence(code, out var n))
                next = Math.Max(next, n + 1);
        }
        return next;
    }

    private static bool HasExplicitCode(string? code) =>
        !string.IsNullOrWhiteSpace(code) && !code.Equals("(Tự sinh)", StringComparison.OrdinalIgnoreCase);

    private static bool IsDuplicateName(string name, List<string> existingNames, HashSet<string> seenNames) =>
        existingNames.Any(n => Supplier.SameFoldedName(n, name)) || seenNames.Contains(name);

    private static bool IsDuplicateCode(string code, List<string> existingCodes, HashSet<string> seenCodes) =>
        existingCodes.Any(c => c.Equals(code, StringComparison.OrdinalIgnoreCase)) || seenCodes.Contains(code);

    private static bool IsDuplicateTaxCode(string taxCode, List<string> existingTaxCodes, HashSet<string> seenTaxCodes) =>
        existingTaxCodes.Any(t => t.Equals(taxCode, StringComparison.OrdinalIgnoreCase)) || seenTaxCodes.Contains(taxCode);

    private static string? NormalizeImportedPhone(string? phone)
    {
        if (string.IsNullOrWhiteSpace(phone))
            return null;

        try
        {
            return VietnamPhoneRules.NormalizeOptional(phone);
        }
        catch
        {
            return phone.Length <= 50 ? phone : phone[..50];
        }
    }

    private static string? TruncateOrNull(string? value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        return value.Length <= maxLength ? value : value[..maxLength];
    }

    private sealed record RawSupplierRow(
        int RowIndex,
        string? Code,
        string Name,
        string? TaxCode,
        string? Phone,
        string? Website,
        string? SupplierType,
        string? Scope,
        int Debt,
        decimal? CreditLimit,
        string? Terms,
        string? Groups);

    private static (List<RawSupplierRow> Rows, List<string> AvailableSheets, string SelectedSheet)
        ExtractSupplierRowsFromExcel(XLWorkbook workbook, string? requestedSheetName)
    {
        var availableSheets = workbook.Worksheets.Select(w => w.Name).ToList();
        IXLWorksheet? ws = null;

        if (!string.IsNullOrWhiteSpace(requestedSheetName))
        {
            ws = workbook.Worksheets.FirstOrDefault(w =>
                string.Equals(w.Name.Trim(), requestedSheetName.Trim(), StringComparison.OrdinalIgnoreCase));
        }

        if (ws is null)
        {
            foreach (var p in PrioritySheetNames)
            {
                ws = workbook.Worksheets.FirstOrDefault(w => w.Name.Contains(p, StringComparison.OrdinalIgnoreCase));
                if (ws != null) break;
            }
        }

        ws ??= workbook.Worksheets.FirstOrDefault(w => w.RowsUsed().Any()) ?? workbook.Worksheets.FirstOrDefault();
        if (ws is null)
            return ([], availableSheets, string.Empty);

        var selectedSheet = ws.Name;
        var rows = new List<RawSupplierRow>();
        if (!ws.RowsUsed().Any())
            return (rows, availableSheets, selectedSheet);

        var maxRow = ws.LastRowUsed()?.RowNumber() ?? 0;
        var maxCol = ws.LastColumnUsed()?.ColumnNumber() ?? 0;

        var headerCells = new Dictionary<(int Row, int Col), string>();
        for (int r = 1; r <= Math.Min(6, maxRow); r++)
        {
            var row = ws.Row(r);
            for (int c = 1; c <= Math.Min(40, maxCol); c++)
            {
                var val = row.Cell(c).GetString().Trim();
                if (!string.IsNullOrWhiteSpace(val))
                    headerCells[(r, c)] = val;
            }
        }

        bool isOptionSheet = headerCells.Values.Any(v =>
            v.Contains("F0-F1-F2", StringComparison.OrdinalIgnoreCase) ||
            v.Contains("F0-F1-F2-F3", StringComparison.OrdinalIgnoreCase))
            || (headerCells.Values.Any(v => v.Contains("HẠNG MỤC", StringComparison.OrdinalIgnoreCase) || v.Contains("HANG MUC", StringComparison.OrdinalIgnoreCase))
                && headerCells.Values.Any(v => v.Contains("F0", StringComparison.OrdinalIgnoreCase) || v.Contains("F1", StringComparison.OrdinalIgnoreCase)));

        bool isSummarySheet = !isOptionSheet && headerCells.Values.Any(v =>
            v.Contains("MÃ NCC VER", StringComparison.OrdinalIgnoreCase) ||
            v.Contains("MA NCC VER", StringComparison.OrdinalIgnoreCase) ||
            (v.Contains("Tên NCC", StringComparison.OrdinalIgnoreCase) && headerCells.Values.Any(x => x.Contains("VER.", StringComparison.OrdinalIgnoreCase))));

        if (isOptionSheet)
        {
            ExtractOptionSheetRows(ws, maxRow, maxCol, headerCells, rows);
        }
        else if (isSummarySheet)
        {
            ExtractSummarySheetRows(ws, maxRow, maxCol, headerCells, rows);
        }
        else
        {
            ExtractStandardSheetRows(ws, maxRow, maxCol, rows);
        }

        return (rows, availableSheets, selectedSheet);
    }

    private static void ExtractOptionSheetRows(
        IXLWorksheet ws,
        int maxRow,
        int maxCol,
        Dictionary<(int Row, int Col), string> headerCells,
        List<RawSupplierRow> rows)
    {
        int codeCol = 0;
        int nameCol = 0;
        int hmCol = 0;
        int hhCol = 0;

        for (int c = 1; c <= Math.Min(40, maxCol); c++)
        {
            var c1 = headerCells.TryGetValue((1, c), out var h1) ? h1 : "";
            var c2 = headerCells.TryGetValue((2, c), out var h2) ? h2 : "";
            var combined = $"{c1} {c2}".ToUpperInvariant();

            if (codeCol == 0 && (combined.Contains("F0-F1-F2") || combined.Contains("F0-F1-F2-F3") || c1.Contains("MÃ NCC", StringComparison.OrdinalIgnoreCase) || c1.Contains("MA NCC", StringComparison.OrdinalIgnoreCase)))
                codeCol = c;

            if (nameCol == 0 && (c1.Contains("TÊN NCC", StringComparison.OrdinalIgnoreCase) || c1.Contains("TEN NCC", StringComparison.OrdinalIgnoreCase)))
                nameCol = c;

            if (hmCol == 0 && (c1.Contains("HẠNG MỤC", StringComparison.OrdinalIgnoreCase) || c1.Contains("HANG MUC", StringComparison.OrdinalIgnoreCase)))
                hmCol = c;

            if (hhCol == 0 && (c1.Contains("TÊN HÀNG HÓA", StringComparison.OrdinalIgnoreCase) || c1.Contains("TEN HANG HOA", StringComparison.OrdinalIgnoreCase) || c1.Contains("LOẠI HÀNG CHỦ YẾU", StringComparison.OrdinalIgnoreCase) || c1.Contains("LOẠI", StringComparison.OrdinalIgnoreCase)))
                hhCol = c;
        }

        for (int r = 4; r <= maxRow; r++)
        {
            var row = ws.Row(r);
            var shortName = nameCol > 0 ? row.Cell(nameCol).GetString().Trim() : "";
            var fullName = nameCol > 0 ? row.Cell(nameCol + 1).GetString().Trim() : "";
            var name = !string.IsNullOrWhiteSpace(fullName) ? fullName : shortName;

            if (string.IsNullOrWhiteSpace(name))
                continue;

            var code = codeCol > 0 ? row.Cell(codeCol).GetString().Trim() : "";

            string? hmVal = hmCol > 0 ? (!string.IsNullOrWhiteSpace(row.Cell(hmCol + 1).GetString().Trim()) ? row.Cell(hmCol + 1).GetString().Trim() : row.Cell(hmCol).GetString().Trim()) : null;
            string? hhVal = hhCol > 0 ? (!string.IsNullOrWhiteSpace(row.Cell(hhCol + 1).GetString().Trim()) ? row.Cell(hhCol + 1).GetString().Trim() : row.Cell(hhCol).GetString().Trim()) : null;

            var groupsList = new List<string>();
            if (!string.IsNullOrWhiteSpace(hmVal)) groupsList.Add(hmVal);
            if (!string.IsNullOrWhiteSpace(hhVal)) groupsList.Add(hhVal);
            var groups = groupsList.Count > 0 ? string.Join(" - ", groupsList) : null;

            rows.Add(new RawSupplierRow(
                RowIndex: r,
                Code: string.IsNullOrWhiteSpace(code) ? null : code,
                Name: name,
                TaxCode: null,
                Phone: null,
                Website: null,
                SupplierType: "Nhà phân phối",
                Scope: "Trong nước",
                Debt: 30,
                CreditLimit: null,
                Terms: null,
                Groups: groups));
        }
    }

    private static void ExtractSummarySheetRows(
        IXLWorksheet ws,
        int maxRow,
        int maxCol,
        Dictionary<(int Row, int Col), string> headerCells,
        List<RawSupplierRow> rows)
    {
        int nameCol = 0;
        int ver3Col = 0;
        int ver2Col = 0;
        int ver1Col = 0;

        for (int c = 1; c <= Math.Min(40, maxCol); c++)
        {
            var val = headerCells.TryGetValue((1, c), out var h) ? h.ToUpperInvariant() : "";
            if (val.Contains("TÊN NCC") || val.Contains("TEN NCC")) nameCol = c;
            else if (val.Contains("VER.3") || val.Contains("VER 3")) ver3Col = c;
            else if (val.Contains("VER.2") || val.Contains("VER 2")) ver2Col = c;
            else if (val.Contains("VER.1") || val.Contains("VER 1")) ver1Col = c;
        }
        if (nameCol == 0) nameCol = 2;

        for (int r = 2; r <= maxRow; r++)
        {
            var row = ws.Row(r);
            var name = nameCol > 0 ? row.Cell(nameCol).GetString().Trim() : "";
            if (string.IsNullOrWhiteSpace(name))
                continue;

            var v3 = ver3Col > 0 ? row.Cell(ver3Col).GetString().Trim() : "";
            var v2 = ver2Col > 0 ? row.Cell(ver2Col).GetString().Trim() : "";
            var v1 = ver1Col > 0 ? row.Cell(ver1Col).GetString().Trim() : "";
            var code = !string.IsNullOrWhiteSpace(v3) ? v3 : (!string.IsNullOrWhiteSpace(v2) ? v2 : v1);

            rows.Add(new RawSupplierRow(
                RowIndex: r,
                Code: string.IsNullOrWhiteSpace(code) ? null : code,
                Name: name,
                TaxCode: null,
                Phone: null,
                Website: null,
                SupplierType: "Nhà phân phối",
                Scope: "Trong nước",
                Debt: 30,
                CreditLimit: null,
                Terms: null,
                Groups: null));
        }
    }

    private static void ExtractStandardSheetRows(
        IXLWorksheet ws,
        int maxRow,
        int maxCol,
        List<RawSupplierRow> rows)
    {
        int bestHeaderRow = 1;
        int bestMatchCount = 0;

        for (int r = 1; r <= Math.Min(5, maxRow); r++)
        {
            var row = ws.Row(r);
            int matches = 0;
            for (int c = 1; c <= Math.Min(30, maxCol); c++)
            {
                var txt = row.Cell(c).GetString().Trim().ToLowerInvariant();
                if (string.IsNullOrWhiteSpace(txt)) continue;
                if (txt.Contains("mã ncc") || txt.Contains("ma ncc") || txt.Contains("supplier code")) matches++;
                else if (txt.Contains("tên ncc") || txt.Contains("tên nhà cung cấp") || txt.Contains("ten nha cung cap") || txt.Contains("supplier name") || (txt.Contains("nhà cung cấp") && !txt.Contains("loại"))) matches++;
                else if (txt.Contains("mã số thuế") || txt.Contains("mst") || txt.Contains("tax code")) matches++;
                else if (txt.Contains("số điện thoại") || txt.Contains("sđt") || txt.Contains("sdt") || txt.Contains("phone")) matches++;
                else if (txt.Contains("website") || txt == "web") matches++;
                else if (txt.Contains("loại hình") || txt.Contains("loại ncc")) matches++;
                else if (txt.Contains("phạm vi") || txt.Contains("scope")) matches++;
                else if (txt.Contains("hạn nợ") || txt.Contains("ngày nợ")) matches++;
                else if (txt.Contains("hạn mức")) matches++;
                else if (txt.Contains("điều khoản") || txt.Contains("thanh toán")) matches++;
                else if (txt.Contains("ngành hàng") || txt.Contains("nhóm")) matches++;
            }
            if (matches > bestMatchCount)
            {
                bestMatchCount = matches;
                bestHeaderRow = r;
            }
        }

        int colCode = 0, colName = 0, colTax = 0, colPhone = 0, colWebsite = 0;
        int colType = 0, colScope = 0, colDebt = 0, colLimit = 0, colTerms = 0, colGroups = 0;

        if (bestMatchCount > 0)
        {
            var hRow = ws.Row(bestHeaderRow);
            for (int c = 1; c <= Math.Min(30, maxCol); c++)
            {
                var txt = hRow.Cell(c).GetString().Trim().ToLowerInvariant();
                if (string.IsNullOrWhiteSpace(txt)) continue;

                if (colCode == 0 && (txt.Contains("mã ncc") || txt.Contains("ma ncc") || txt.Contains("supplier code")) && !txt.Contains("thuế"))
                    colCode = c;
                else if (colName == 0 && (txt.Contains("tên nhà cung cấp") || txt.Contains("ten nha cung cap") || txt.Contains("tên ncc") || txt.Contains("ten ncc") || txt.Contains("supplier name") || (txt.Contains("nhà cung cấp") && !txt.Contains("loại")) || txt == "tên"))
                    colName = c;
                else if (colTax == 0 && (txt.Contains("mã số thuế") || txt.Contains("mst") || txt.Contains("tax code") || txt.Contains("tax")))
                    colTax = c;
                else if (colPhone == 0 && (txt.Contains("số điện thoại") || txt.Contains("sđt") || txt.Contains("sdt") || txt.Contains("phone") || txt.Contains("điện thoại")))
                    colPhone = c;
                else if (colWebsite == 0 && (txt.Contains("website") || txt == "web" || txt.Contains("url")))
                    colWebsite = c;
                else if (colType == 0 && (txt.Contains("loại hình") || txt.Contains("loại ncc") || txt.Contains("loại")))
                    colType = c;
                else if (colScope == 0 && (txt.Contains("phạm vi") || txt.Contains("scope")))
                    colScope = c;
                else if (colDebt == 0 && (txt.Contains("hạn nợ") || txt.Contains("ngày nợ") || txt.Contains("debt")))
                    colDebt = c;
                else if (colLimit == 0 && (txt.Contains("hạn mức") || txt.Contains("credit limit")))
                    colLimit = c;
                else if (colTerms == 0 && (txt.Contains("điều khoản") || txt.Contains("thanh toán") || txt.Contains("terms")))
                    colTerms = c;
                else if (colGroups == 0 && (txt.Contains("ngành hàng") || txt.Contains("nhóm")))
                    colGroups = c;
            }
        }

        if (colName == 0)
        {
            colCode = 1;
            colName = 2;
            colTax = 3;
            colPhone = 4;
            colWebsite = 5;
            colType = 6;
            colScope = 7;
            colDebt = 8;
            colLimit = 9;
            colTerms = 10;
            colGroups = 11;
        }

        int dataStartRow = bestMatchCount > 0 ? bestHeaderRow + 1 : 2;

        for (int r = dataStartRow; r <= maxRow; r++)
        {
            var row = ws.Row(r);
            var name = colName > 0 ? row.Cell(colName).GetString().Trim() : "";
            var code = colCode > 0 ? row.Cell(colCode).GetString().Trim() : "";
            var taxCode = colTax > 0 ? row.Cell(colTax).GetString().Trim() : "";
            var phone = colPhone > 0 ? row.Cell(colPhone).GetString().Trim() : "";
            var website = colWebsite > 0 ? row.Cell(colWebsite).GetString().Trim() : "";
            var supplierType = colType > 0 ? row.Cell(colType).GetString().Trim() : "";
            var scope = colScope > 0 ? row.Cell(colScope).GetString().Trim() : "";
            var debtStr = colDebt > 0 ? row.Cell(colDebt).GetString().Trim() : "";
            var limitStr = colLimit > 0 ? row.Cell(colLimit).GetString().Trim() : "";
            var terms = colTerms > 0 ? row.Cell(colTerms).GetString().Trim() : "";
            var groups = colGroups > 0 ? row.Cell(colGroups).GetString().Trim() : "";

            if (string.IsNullOrWhiteSpace(name) && string.IsNullOrWhiteSpace(taxCode) && string.IsNullOrWhiteSpace(phone) && string.IsNullOrWhiteSpace(code))
                continue;

            if (colCode == 1 && int.TryParse(code, out var sttNum) && sttNum == r - dataStartRow + 1)
                code = "";

            if (!int.TryParse(debtStr, out var debt) || debt <= 0)
                debt = 30;

            decimal? creditLimit = decimal.TryParse(limitStr, out var lim) && lim > 0 ? lim : null;

            rows.Add(new RawSupplierRow(
                RowIndex: r,
                Code: string.IsNullOrWhiteSpace(code) ? null : code,
                Name: name,
                TaxCode: string.IsNullOrWhiteSpace(taxCode) ? null : taxCode,
                Phone: string.IsNullOrWhiteSpace(phone) ? null : phone,
                Website: string.IsNullOrWhiteSpace(website) ? null : website,
                SupplierType: string.IsNullOrWhiteSpace(supplierType) ? null : supplierType,
                Scope: string.IsNullOrWhiteSpace(scope) ? null : scope,
                Debt: debt,
                CreditLimit: creditLimit,
                Terms: string.IsNullOrWhiteSpace(terms) ? null : terms,
                Groups: string.IsNullOrWhiteSpace(groups) ? null : groups));
        }
    }
}
