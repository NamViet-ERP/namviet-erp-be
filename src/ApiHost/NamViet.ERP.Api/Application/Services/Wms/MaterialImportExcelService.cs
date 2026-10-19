using System.Globalization;
using System.Text;
using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;
using NamViet.ERP.Api.Application.Dtos.Wms;
using NamViet.ERP.Modules.Warehouse.Application.Helpers;
using NamViet.ERP.Modules.Warehouse.Domain.Entities;
using NamViet.ERP.Modules.Warehouse.Infrastructure.Persistence;
using NamViet.ERP.SharedKernel.Common;

namespace NamViet.ERP.Api.Application.Services.Wms;

public interface IMaterialImportExcelService
{
    Task<byte[]> GenerateTemplateAsync(CancellationToken ct = default);
    Task<MaterialImportParseResultDto> ParseImportFileAsync(Stream stream, string fileName, CancellationToken ct = default);
    Task<MaterialImportResultDto> ImportBatchAsync(MaterialImportBatchRequest request, CancellationToken ct = default);
}

public sealed class MaterialImportExcelService(WarehouseDbContext db) : IMaterialImportExcelService
{
    private const int MaxDataRows = 2000;
    private const long MaxFileBytes = 5 * 1024 * 1024; // 5MB

    private static readonly string[] TemplateHeaders =
    [
        "Mã vật tư (*)",
        "Tên vật tư (*)",
        "Đơn vị tính (*)",
        "Danh mục (Cấp 1) (*)",
        "Nhóm vật tư (Cấp 2) (*)",
        "Loại vật tư (Cấp 3)",
        "Phân loại (Cấp 4)",
        "Kiểu (Cấp 5)",
        "Xuất xứ",
        "Quy cách",
        "Thông số kỹ thuật chung",
        "Tiêu chuẩn chất lượng",
        "Giá trần (VNĐ)",
    ];

    public async Task<byte[]> GenerateTemplateAsync(CancellationToken ct = default)
    {
        using var workbook = new XLWorkbook();
        
        // ── Sheet 1: Danh mục vật tư mẫu ──────────────────────────────────────
        var sheet = workbook.Worksheets.Add("Danh muc vat tu");

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

        // Dữ liệu mẫu thực tế
        var sampleRows = new (string Code, string Name, string Unit, string L1, string L2, string L3, string L4, string L5, string Origin, string Spec, string GenSpec, string Quality, decimal? Price)[]
        {
            ("IN-INH-SU304-N1-50x50x4x6000", "Inox 304", "Mét", "Kim khí & Inox", "Inox 304", "Thép hình Inox", "-", "-", "Nhật Bản", "V50x50x4x6000mm", "Tiêu chuẩn JIS G4303", "JIS G4303", 185000),
            ("TH-TAM-SS400-1500x4x6000", "Thép tấm SS400", "Tấm", "Kim khí & Thép", "Thép tấm", "Thép tấm thường", "-", "-", "Hàn Quốc", "1500x4x6000mm", "Thép tấm kết cấu cán nóng", "ASTM A36", 12500000),
            ("ONG-PVC-D90-C2", "Ống nhựa PVC D90 Class 2", "Cây", "Ống & Phụ kiện", "Ống nhựa PVC", "Ống cứng", "C2", "-", "Việt Nam", "D90x3.0x4000mm", "Ống thoát nước chịu áp PN6", "TCVN 8491", 95000),
            ("VT-KEO-SIL-300", "Keo silicone trung tính", "Tuýp", "Vật liệu kết dính", "Keo & Phụ gia", "Silicone", "-", "-", "Mỹ", "Tuýp 300ml màu trắng sữa", "Dùng trám khe chịu thời tiết", "ASTM C920", 55000),
        };

        for (var i = 0; i < sampleRows.Length; i++)
        {
            var r = sampleRows[i];
            var rowIdx = i + 2;
            sheet.Cell(rowIdx, 1).Value = r.Code;
            sheet.Cell(rowIdx, 2).Value = r.Name;
            sheet.Cell(rowIdx, 3).Value = r.Unit;
            sheet.Cell(rowIdx, 4).Value = r.L1;
            sheet.Cell(rowIdx, 5).Value = r.L2;
            sheet.Cell(rowIdx, 6).Value = r.L3;
            sheet.Cell(rowIdx, 7).Value = r.L4;
            sheet.Cell(rowIdx, 8).Value = r.L5;
            sheet.Cell(rowIdx, 9).Value = r.Origin;
            sheet.Cell(rowIdx, 10).Value = r.Spec;
            sheet.Cell(rowIdx, 11).Value = r.GenSpec;
            sheet.Cell(rowIdx, 12).Value = r.Quality;
            if (r.Price.HasValue) sheet.Cell(rowIdx, 13).Value = r.Price.Value;
        }

        sheet.Columns().AdjustToContents(15.0, 45.0);
        sheet.SheetView.FreezeRows(1);

        // ── Sheet 2: Tra cứu tham chiếu (Reference) ───────────────────────────
        var refSheet = workbook.Worksheets.Add("Huong dan & Tra cuu");
        
        refSheet.Cell(1, 1).Value = "HƯỚNG DẪN NHẬP DỮ LIỆU DANH MỤC VẬT TƯ";
        refSheet.Cell(1, 1).Style.Font.Bold = true;
        refSheet.Cell(1, 1).Style.Font.FontSize = 14;

        refSheet.Cell(3, 1).Value = "1. Các cột có dấu (*) là bắt buộc: Mã vật tư, Tên vật tư, Đơn vị tính, Danh mục (Cấp 1), Nhóm vật tư (Cấp 2).";
        refSheet.Cell(4, 1).Value = "2. Nếu Danh mục hoặc Nhóm vật tư chưa có trong hệ thống, hệ thống sẽ TỰ ĐỘNG TẠO MỚI nhánh trên cây phân cấp.";
        refSheet.Cell(5, 1).Value = "3. Đơn vị tính và Xuất xứ nếu chưa có trong danh mục tham chiếu cũng sẽ được tự động đồng bộ khi import.";
        refSheet.Cell(6, 1).Value = "4. Khi import, bạn có thể xem trước bảng dữ liệu, lọc dòng lỗi và chọn có cập nhật các mã đã tồn tại hay không.";

        refSheet.Cell(8, 1).Value = "Đơn vị tính hiện có trong hệ thống";
        refSheet.Cell(8, 1).Style.Font.Bold = true;
        refSheet.Cell(8, 3).Value = "Xuất xứ tham khảo";
        refSheet.Cell(8, 3).Style.Font.Bold = true;
        refSheet.Cell(8, 5).Value = "Cây danh mục mẫu (Cấp 1 > Cấp 2)";
        refSheet.Cell(8, 5).Style.Font.Bold = true;

        var units = await db.MaterialUnits.AsNoTracking().Where(u => u.IsActive).OrderBy(u => u.Name).Select(u => u.Name).ToListAsync(ct);
        for (var u = 0; u < units.Count; u++)
            refSheet.Cell(9 + u, 1).Value = units[u];

        var origins = await db.MaterialOrigins.AsNoTracking().Where(o => o.IsActive).OrderBy(o => o.Name).Select(o => o.Name).ToListAsync(ct);
        if (origins.Count == 0) origins = ["Việt Nam", "Nhật Bản", "Hàn Quốc", "Đức", "Mỹ", "Trung Quốc", "Đài Loan", "Ý", "Thái Lan"];
        for (var o = 0; o < origins.Count; o++)
            refSheet.Cell(9 + o, 3).Value = origins[o];

        var categories = await db.MaterialCategories.AsNoTracking().Where(c => c.IsActive && c.Level <= 2).OrderBy(c => c.Level).ThenBy(c => c.Name).ToListAsync(ct);
        var catL1s = categories.Where(c => c.Level == 1).ToDictionary(c => c.Id, c => c.Name);
        var catRows = categories.Where(c => c.Level == 2).Select(c => $"{(c.ParentId.HasValue && catL1s.TryGetValue(c.ParentId.Value, out var p) ? p : "Chung")} > {c.Name}").Distinct().Take(40).ToList();
        for (var c = 0; c < catRows.Count; c++)
            refSheet.Cell(9 + c, 5).Value = catRows[c];

        refSheet.Columns().AdjustToContents();

        using var ms = new MemoryStream();
        workbook.SaveAs(ms);
        return ms.ToArray();
    }

    public async Task<MaterialImportParseResultDto> ParseImportFileAsync(
        Stream stream, string fileName, CancellationToken ct = default)
    {
        if (stream.CanSeek && stream.Length > MaxFileBytes)
            throw new DomainRuleException("File vượt quá kích thước cho phép (5MB).");

        var extension = Path.GetExtension(fileName).ToLowerInvariant();
        var rows = extension switch
        {
            ".xlsx" or ".xls" => ReadExcelRows(stream),
            ".csv" => ReadCsvRows(stream),
            _ => throw new DomainRuleException("Chỉ chấp nhận file .xlsx, .xls hoặc .csv."),
        };

        if (rows.Count == 0)
            throw new DomainRuleException("File không có dữ liệu.");

        // Nhận diện dòng tiêu đề (Header row)
        var headerIndex = FindHeaderRowIndex(rows);
        if (headerIndex < 0)
            throw new DomainRuleException("Không tìm thấy dòng tiêu đề chứa các cột (Mã vật tư, Tên vật tư...).");

        var colMap = MapColumns(rows, headerIndex);
        if (colMap.MaterialCode < 0)
            throw new DomainRuleException("Không tìm thấy cột \"Mã vật tư\" (hoặc \"Mã\", \"Material Code\", \"SKU\", \"Mã hàng hóa\") trong file.");

        var dataRows = rows.Skip(headerIndex + 1)
            .Where(r => !IsEmptyRow(r) && !IsSubHeaderOrFormulaRow(r, colMap.MaterialCode))
            .Take(MaxDataRows)
            .ToList();

        if (dataRows.Count == 0)
            throw new DomainRuleException("File không có dòng dữ liệu vật tư nào.");

        // Đọc danh sách mã vật tư đã có trong DB để check duplicate
        var existingCodesList = await db.Materials.AsNoTracking()
            .Where(m => m.DeletedAt == null)
            .Select(m => m.MaterialCode)
            .ToListAsync(ct);
        var allExistingCodes = new HashSet<string>(existingCodesList, StringComparer.OrdinalIgnoreCase);

        // Đọc danh mục hiện có để check cây danh mục
        var existingCategories = await db.MaterialCategories.AsNoTracking()
            .Where(c => c.IsActive)
            .ToListAsync(ct);

        var unitsList = await db.MaterialUnits.AsNoTracking()
            .Where(u => u.IsActive)
            .Select(u => u.Name)
            .ToListAsync(ct);
        var existingUnits = new HashSet<string>(unitsList, StringComparer.OrdinalIgnoreCase);

        var parsedRows = new List<MaterialImportRowDto>();
        var globalErrors = new List<MaterialImportErrorDto>();
        var seenCodesInFile = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        var validCount = 0;
        var existingCount = 0;
        var errorCount = 0;

        for (var i = 0; i < dataRows.Count; i++)
        {
            var excelRow = headerIndex + 2 + i;
            var row = dataRows[i];

            var code = GetCell(row, colMap.MaterialCode).Trim();
            var name = GetCell(row, colMap.MaterialName).Trim();
            if (string.IsNullOrWhiteSpace(name))
            {
                var altName = GetCell(row, colMap.Diengiai).Trim();
                if (string.IsNullOrEmpty(altName)) altName = GetCell(row, colMap.MoTa).Trim();
                if (!string.IsNullOrEmpty(altName)) name = altName;
            }

            var danhMuc = GetCell(row, colMap.DanhMuc).Trim();
            var nhomVatTu = GetCell(row, colMap.NhomVatTu).Trim();
            var loaiVatTu = GetCell(row, colMap.LoaiVatTu).Trim();
            var phanLoai = GetCell(row, colMap.PhanLoai).Trim();
            var kieu = GetCell(row, colMap.Kieu).Trim();

            // Tự động kết hợp 3 cấp xuất xứ (Nhãn hiệu + Nước SX + Nhóm nước)
            var originRaw = GetCell(row, colMap.Origin).Trim();
            var brand = colMap.NhanHieu >= 0 ? GetCell(row, colMap.NhanHieu).Trim() : string.Empty;
            var country = colMap.NuocSanXuat >= 0 ? GetCell(row, colMap.NuocSanXuat).Trim() : string.Empty;
            var region = colMap.NhomNuoc >= 0 ? GetCell(row, colMap.NhomNuoc).Trim() : string.Empty;

            if (!string.IsNullOrEmpty(country) || !string.IsNullOrEmpty(region) || !string.IsNullOrEmpty(brand))
            {
                var effectiveBrand = !string.IsNullOrEmpty(brand) ? brand : originRaw;
                var parts = new List<string>();
                if (!string.IsNullOrEmpty(effectiveBrand)) parts.Add(effectiveBrand);
                if (!string.IsNullOrEmpty(country))
                {
                    parts.Add(!string.IsNullOrEmpty(region) ? $"{country} ({region})" : country);
                }
                else if (!string.IsNullOrEmpty(region))
                {
                    parts.Add($"({region})");
                }
                if (parts.Count > 0)
                {
                    originRaw = string.Join(" • ", parts);
                }
            }

            // Tự động kết hợp thông số hình học cho Thép / Kim khí
            var techSpec = GetCell(row, colMap.TechnicalSpec).Trim();
            var phiRong = colMap.PhiRong >= 0 ? GetCell(row, colMap.PhiRong).Trim() : string.Empty;
            var doDay = colMap.DoDay >= 0 ? GetCell(row, colMap.DoDay).Trim() : string.Empty;
            var chieuDai = colMap.ChieuDai >= 0 ? GetCell(row, colMap.ChieuDai).Trim() : string.Empty;

            if (!string.IsNullOrEmpty(doDay) || !string.IsNullOrEmpty(chieuDai))
            {
                var effectivePhiRong = !string.IsNullOrEmpty(phiRong) ? phiRong : techSpec;
                var dims = new List<string>();
                if (!string.IsNullOrEmpty(effectivePhiRong)) dims.Add(effectivePhiRong);
                if (!string.IsNullOrEmpty(doDay)) dims.Add(doDay);
                if (!string.IsNullOrEmpty(chieuDai)) dims.Add(chieuDai);
                if (dims.Count > 0)
                {
                    techSpec = string.Join("x", dims) + (dims.Any(d => d.EndsWith("mm", StringComparison.OrdinalIgnoreCase)) ? "" : "mm");
                }
            }
            else if (string.IsNullOrEmpty(techSpec) && !string.IsNullOrEmpty(phiRong))
            {
                techSpec = phiRong;
            }

            // Tự động kết hợp thông số công suất / lưu lượng cho Bơm / Quạt / Động cơ
            var genSpec = GetCell(row, colMap.GeneralTechnicalSpec).Trim();
            var congSuat = colMap.CongSuat >= 0 ? GetCell(row, colMap.CongSuat).Trim() : string.Empty;
            var luuLuong = colMap.LuuLuong >= 0 ? GetCell(row, colMap.LuuLuong).Trim() : string.Empty;
            var cotAp = colMap.CotAp >= 0 ? GetCell(row, colMap.CotAp).Trim() : string.Empty;
            var dienAp = colMap.DienAp >= 0 ? GetCell(row, colMap.DienAp).Trim() : string.Empty;
            var moTa = colMap.MoTa >= 0 ? GetCell(row, colMap.MoTa).Trim() : string.Empty;

            var eqSpecs = new List<string>();
            if (!string.IsNullOrEmpty(congSuat)) eqSpecs.Add(congSuat.EndsWith("kw", StringComparison.OrdinalIgnoreCase) ? congSuat : $"{congSuat}kW");
            if (!string.IsNullOrEmpty(luuLuong)) eqSpecs.Add(luuLuong.Contains("m3", StringComparison.OrdinalIgnoreCase) ? luuLuong : $"{luuLuong} m³/h");
            if (!string.IsNullOrEmpty(cotAp)) eqSpecs.Add(cotAp.Contains("pa", StringComparison.OrdinalIgnoreCase) || cotAp.Contains("h2o", StringComparison.OrdinalIgnoreCase) ? cotAp : $"{cotAp} Pa");
            if (!string.IsNullOrEmpty(dienAp)) eqSpecs.Add($"Điện áp: {dienAp}");

            if (eqSpecs.Count > 0)
            {
                var combinedEq = string.Join(" | ", eqSpecs);
                if (!string.IsNullOrEmpty(genSpec) && !genSpec.Equals(combinedEq, StringComparison.OrdinalIgnoreCase))
                {
                    genSpec = $"{combinedEq}\n{genSpec}";
                }
                else
                {
                    genSpec = combinedEq;
                }
                if (!string.IsNullOrEmpty(moTa) && !moTa.Equals(name, StringComparison.OrdinalIgnoreCase) && !genSpec.Contains(moTa))
                {
                    genSpec += $"\n{moTa}";
                }
            }
            else if (string.IsNullOrEmpty(genSpec) && !string.IsNullOrEmpty(moTa) && !moTa.Equals(name, StringComparison.OrdinalIgnoreCase))
            {
                genSpec = moTa;
            }

            // Suy luận Đơn vị tính nếu file không có cột ĐVT
            var unitRaw = GetCell(row, colMap.Unit).Trim();
            if (string.IsNullOrWhiteSpace(unitRaw))
            {
                var dLower = (danhMuc + " " + nhomVatTu + " " + name).ToLowerInvariant();
                if (dLower.Contains("bơm") || dLower.Contains("bom") || dLower.Contains("quạt") || dLower.Contains("quat") || dLower.Contains("động cơ") || dLower.Contains("dong co"))
                    unitRaw = "Bộ";
                else if (dLower.Contains("ống") || dLower.Contains("ong") || dLower.Contains("thép hình"))
                    unitRaw = "Cây";
                else if (dLower.Contains("tấm") || dLower.Contains("tam"))
                    unitRaw = "Tấm";
                else if (dLower.Contains("cuộn") || dLower.Contains("dây"))
                    unitRaw = "Cuộn";
                else if (dLower.Contains("keo") || dLower.Contains("sơn"))
                    unitRaw = "Thùng";
                else
                    unitRaw = "Cái";
            }

            var quality = GetCell(row, colMap.QualityStandard).Trim();
            var priceRaw = GetCell(row, colMap.CeilingPrice).Trim();
            var abcRaw = GetCell(row, colMap.AbcClass).Trim();
            var consumableRaw = GetCell(row, colMap.IsConsumable).Trim();

            var rowErrors = new List<string>();
            var rowWarnings = new List<string>();

            // 1. Kiểm tra Mã vật tư
            if (string.IsNullOrWhiteSpace(code))
            {
                rowErrors.Add("Mã vật tư không được để trống.");
                globalErrors.Add(new MaterialImportErrorDto(excelRow, "materialCode", "Mã vật tư không được để trống."));
            }
            else
            {
                if (code.Length > 100)
                {
                    rowErrors.Add("Mã vật tư tối đa 100 ký tự.");
                    globalErrors.Add(new MaterialImportErrorDto(excelRow, "materialCode", "Mã vật tư vượt quá 100 ký tự."));
                }

                if (!seenCodesInFile.Add(code))
                {
                    rowErrors.Add($"Trùng mã \"{code}\" với dòng khác trong chính file tải lên.");
                    globalErrors.Add(new MaterialImportErrorDto(excelRow, "materialCode", $"Trùng mã \"{code}\" trong file."));
                }
            }

            // 2. Kiểm tra Tên vật tư
            if (string.IsNullOrWhiteSpace(name))
            {
                // Fallback: nếu không có cột tên riêng biệt nhưng có mã, gán tên = mã
                if (!string.IsNullOrWhiteSpace(code))
                {
                    name = code;
                    rowWarnings.Add("Tên vật tư để trống, hệ thống tạm lấy theo Mã vật tư.");
                }
                else
                {
                    rowErrors.Add("Tên vật tư không được để trống.");
                    globalErrors.Add(new MaterialImportErrorDto(excelRow, "materialName", "Tên vật tư không được để trống."));
                }
            }
            else if (name.Length > 255)
            {
                rowErrors.Add("Tên vật tư tối đa 255 ký tự.");
                globalErrors.Add(new MaterialImportErrorDto(excelRow, "materialName", "Tên vật tư vượt quá 255 ký tự."));
            }

            // 3. Kiểm tra Đơn vị tính
            var unit = MaterialUnitHelper.NormalizeDisplayName(unitRaw);
            if (string.IsNullOrWhiteSpace(unit))
            {
                unit = "Cái"; // default fallback
                rowWarnings.Add("Đơn vị tính trống, hệ thống tạm gán mặc định \"Cái\".");
            }

            // 4. Kiểm tra Cây Danh mục (Cấp 1 & Cấp 2)
            if (string.IsNullOrWhiteSpace(danhMuc) && string.IsNullOrWhiteSpace(nhomVatTu))
            {
                danhMuc = "Vật tư chung";
                nhomVatTu = "Tổng hợp";
                rowWarnings.Add("Chưa chọn danh mục, hệ thống sẽ gán vào \"Vật tư chung > Tổng hợp\".");
            }
            else if (string.IsNullOrWhiteSpace(danhMuc))
            {
                danhMuc = "Vật tư chung";
            }
            else if (string.IsNullOrWhiteSpace(nhomVatTu))
            {
                nhomVatTu = danhMuc; // nếu chỉ có cấp 1, cấp 2 lấy tên cấp 1
            }

            // Check xem category đã có trong DB chưa
            var isNewCategory = CheckIfNewCategory(existingCategories, danhMuc, nhomVatTu, loaiVatTu, phanLoai, kieu);
            if (isNewCategory)
            {
                rowWarnings.Add($"Nhánh danh mục \"{danhMuc} > {nhomVatTu}\" sẽ được tự động tạo mới trên hệ thống.");
            }

            // 5. Xuất xứ
            var origin = MaterialOriginHelper.NormalizeDisplayName(originRaw);
            if (string.IsNullOrWhiteSpace(origin)) origin = null;

            // 6. Giá trần & ABC & Tiêu hao
            decimal? ceilingPrice = null;
            if (!string.IsNullOrWhiteSpace(priceRaw))
            {
                if (TryParseDecimal(priceRaw, out var parsedPrice) && parsedPrice >= 0)
                    ceilingPrice = parsedPrice;
                else
                    rowWarnings.Add($"Giá trần \"{priceRaw}\" không hợp lệ, đã bỏ qua giá trần.");
            }

            var abc = NormalizeAbc(abcRaw);
            var isConsumable = ParseBool(consumableRaw);

            // 7. Check mã đã tồn tại trong DB chưa
            var isExisting = !string.IsNullOrWhiteSpace(code) && allExistingCodes.Contains(code);
            if (isExisting)
            {
                existingCount++;
                rowWarnings.Add("Mã vật tư đã tồn tại trên hệ thống. Sẽ cập nhật dữ liệu nếu bật tùy chọn ghi đè.");
            }

            var isValid = rowErrors.Count == 0;
            if (isValid)
            {
                validCount++;
            }
            else
            {
                errorCount++;
            }

            parsedRows.Add(new MaterialImportRowDto(
                excelRow,
                code,
                name,
                unit,
                string.IsNullOrWhiteSpace(danhMuc) ? null : danhMuc,
                string.IsNullOrWhiteSpace(nhomVatTu) ? null : nhomVatTu,
                string.IsNullOrWhiteSpace(loaiVatTu) ? null : loaiVatTu,
                string.IsNullOrWhiteSpace(phanLoai) ? null : phanLoai,
                string.IsNullOrWhiteSpace(kieu) ? null : kieu,
                origin,
                string.IsNullOrWhiteSpace(techSpec) ? null : techSpec,
                string.IsNullOrWhiteSpace(genSpec) ? null : genSpec,
                string.IsNullOrWhiteSpace(quality) ? null : quality,
                ceilingPrice,
                abc,
                isConsumable,
                isValid,
                isExisting,
                isNewCategory,
                rowErrors,
                rowWarnings));
        }

        return new MaterialImportParseResultDto(
            dataRows.Count,
            validCount,
            existingCount,
            errorCount,
            parsedRows,
            globalErrors);
    }

    public async Task<MaterialImportResultDto> ImportBatchAsync(
        MaterialImportBatchRequest request, CancellationToken ct = default)
    {
        if (request.Rows.Count == 0)
            throw new DomainRuleException("Không có dòng dữ liệu nào để import.");

        var validRows = request.Rows.Where(r => r.IsValid).ToList();
        if (validRows.Count == 0)
            throw new DomainRuleException("Không có dòng dữ liệu hợp lệ nào để import.");

        var createdCount = 0;
        var updatedCount = 0;
        var skippedCount = 0;
        var importedCodes = new List<string>();
        var errors = new List<string>();

        await using var tx = await db.Database.BeginTransactionAsync(ct);
        try
        {
            // 1. Đồng bộ Đơn vị tính mới
            var existingUnits = await db.MaterialUnits.AsNoTracking()
                .ToDictionaryAsync(u => u.Name, u => u, StringComparer.OrdinalIgnoreCase, ct);
            var requiredUnits = validRows.Select(r => r.Unit.Trim())
                .Where(u => !string.IsNullOrEmpty(u))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            foreach (var unitName in requiredUnits)
            {
                if (!existingUnits.ContainsKey(unitName))
                {
                    var newUnit = new MaterialUnit { Name = unitName, IsActive = true };
                    db.MaterialUnits.Add(newUnit);
                    existingUnits[unitName] = newUnit;
                }
            }
            await db.SaveChangesAsync(ct);

            // 2. Đồng bộ Xuất xứ mới
            var existingOrigins = await db.MaterialOrigins.AsNoTracking()
                .ToDictionaryAsync(o => o.Name, o => o, StringComparer.OrdinalIgnoreCase, ct);
            var requiredOrigins = validRows.Select(r => r.Origin?.Trim())
                .Where(o => !string.IsNullOrEmpty(o))
                .Select(o => o!)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            foreach (var originName in requiredOrigins)
            {
                if (!existingOrigins.ContainsKey(originName))
                {
                    var newOrigin = new MaterialOrigin { Name = originName, IsActive = true };
                    db.MaterialOrigins.Add(newOrigin);
                    existingOrigins[originName] = newOrigin;
                }
            }
            await db.SaveChangesAsync(ct);

            // 3. Xử lý Cây Danh mục (Tự tạo nhánh nếu chưa có)
            var allCategories = await db.MaterialCategories.ToListAsync(ct);
            var categoryCache = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

            // Helper tìm hoặc tạo node danh mục
            int EnsureCategoryHierarchy(string l1, string l2, string? l3, string? l4, string? l5)
            {
                var key = $"{l1}|{l2}|{l3}|{l4}|{l5}";
                if (categoryCache.TryGetValue(key, out var cachedId)) return cachedId;

                // Level 1: Danh mục
                var c1 = allCategories.FirstOrDefault(c => c.Level == 1 && c.ParentId == null && string.Equals(c.Name.Trim(), l1.Trim(), StringComparison.OrdinalIgnoreCase));
                if (c1 is null)
                {
                    c1 = new MaterialCategory
                    {
                        Name = l1.Trim(),
                        Code = GenerateCategoryCode("CAT1", l1),
                        Level = 1,
                        ParentId = null,
                        SortOrder = allCategories.Count(c => c.Level == 1) + 1,
                        IsActive = true
                    };
                    db.MaterialCategories.Add(c1);
                    db.SaveChanges();
                    allCategories.Add(c1);
                }

                // Level 2: Nhóm vật tư
                var c2 = allCategories.FirstOrDefault(c => c.Level == 2 && c.ParentId == c1.Id && string.Equals(c.Name.Trim(), l2.Trim(), StringComparison.OrdinalIgnoreCase));
                if (c2 is null)
                {
                    c2 = new MaterialCategory
                    {
                        Name = l2.Trim(),
                        Code = GenerateCategoryCode("CAT2", l2),
                        Level = 2,
                        ParentId = c1.Id,
                        SortOrder = allCategories.Count(c => c.ParentId == c1.Id) + 1,
                        IsActive = true
                    };
                    db.MaterialCategories.Add(c2);
                    db.SaveChanges();
                    allCategories.Add(c2);
                }

                var currentLeaf = c2;

                // Level 3 (nếu có)
                if (!string.IsNullOrWhiteSpace(l3) && l3.Trim() != "-")
                {
                    var c3 = allCategories.FirstOrDefault(c => c.Level == 3 && c.ParentId == c2.Id && string.Equals(c.Name.Trim(), l3.Trim(), StringComparison.OrdinalIgnoreCase));
                    if (c3 is null)
                    {
                        c3 = new MaterialCategory
                        {
                            Name = l3.Trim(),
                            Code = GenerateCategoryCode("CAT3", l3),
                            Level = 3,
                            ParentId = c2.Id,
                            SortOrder = allCategories.Count(c => c.ParentId == c2.Id) + 1,
                            IsActive = true
                        };
                        db.MaterialCategories.Add(c3);
                        db.SaveChanges();
                        allCategories.Add(c3);
                    }
                    currentLeaf = c3;

                    // Level 4 (nếu có)
                    if (!string.IsNullOrWhiteSpace(l4) && l4.Trim() != "-")
                    {
                        var c4 = allCategories.FirstOrDefault(c => c.Level == 4 && c.ParentId == c3.Id && string.Equals(c.Name.Trim(), l4.Trim(), StringComparison.OrdinalIgnoreCase));
                        if (c4 is null)
                        {
                            c4 = new MaterialCategory
                            {
                                Name = l4.Trim(),
                                Code = GenerateCategoryCode("CAT4", l4),
                                Level = 4,
                                ParentId = c3.Id,
                                SortOrder = allCategories.Count(c => c.ParentId == c3.Id) + 1,
                                IsActive = true
                            };
                            db.MaterialCategories.Add(c4);
                            db.SaveChanges();
                            allCategories.Add(c4);
                        }
                        currentLeaf = c4;

                        // Level 5 (nếu có)
                        if (!string.IsNullOrWhiteSpace(l5) && l5.Trim() != "-")
                        {
                            var c5 = allCategories.FirstOrDefault(c => c.Level == 5 && c.ParentId == c4.Id && string.Equals(c.Name.Trim(), l5.Trim(), StringComparison.OrdinalIgnoreCase));
                            if (c5 is null)
                            {
                                c5 = new MaterialCategory
                                {
                                    Name = l5.Trim(),
                                    Code = GenerateCategoryCode("CAT5", l5),
                                    Level = 5,
                                    ParentId = c4.Id,
                                    SortOrder = allCategories.Count(c => c.ParentId == c4.Id) + 1,
                                    IsActive = true
                                };
                                db.MaterialCategories.Add(c5);
                                db.SaveChanges();
                                allCategories.Add(c5);
                            }
                            currentLeaf = c5;
                        }
                    }
                }

                categoryCache[key] = currentLeaf.Id;
                return currentLeaf.Id;
            }

            // 4. Lấy tất cả vật tư hiện tại theo danh sách mã
            var codes = validRows.Select(r => r.MaterialCode.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
            var existingMaterials = await db.Materials.IgnoreQueryFilters()
                .Where(m => codes.Contains(m.MaterialCode))
                .ToDictionaryAsync(m => m.MaterialCode, StringComparer.OrdinalIgnoreCase, ct);

            foreach (var row in validRows)
            {
                var code = row.MaterialCode.Trim();
                var leafCatId = EnsureCategoryHierarchy(
                    row.DanhMuc ?? "Vật tư chung",
                    row.NhomVatTu ?? "Tổng hợp",
                    row.LoaiVatTu,
                    row.PhanLoai,
                    row.Kieu);

                if (existingMaterials.TryGetValue(code, out var existing))
                {
                    if (request.OverwriteExisting)
                    {
                        existing.MaterialName = row.MaterialName.Trim();
                        existing.CategoryId = leafCatId;
                        existing.Unit = row.Unit.Trim();
                        existing.Origin = string.IsNullOrWhiteSpace(row.Origin) ? null : row.Origin.Trim();
                        existing.TechnicalSpec = string.IsNullOrWhiteSpace(row.TechnicalSpec) ? null : row.TechnicalSpec.Trim();
                        existing.GeneralTechnicalSpec = string.IsNullOrWhiteSpace(row.GeneralTechnicalSpec) ? null : row.GeneralTechnicalSpec.Trim();
                        existing.QualityStandard = string.IsNullOrWhiteSpace(row.QualityStandard) ? null : row.QualityStandard.Trim();
                        existing.CeilingPrice = row.CeilingPrice is > 0 ? row.CeilingPrice : null;
                        existing.AbcClass = NormalizeAbc(row.AbcClass);
                        existing.IsConsumable = row.IsConsumable;
                        existing.IsActive = true;
                        existing.DeletedAt = null;
                        existing.UpdatedAt = DateTime.UtcNow;

                        updatedCount++;
                        importedCodes.Add(code);
                    }
                    else
                    {
                        skippedCount++;
                    }
                }
                else
                {
                    var newMaterial = new Material
                    {
                        MaterialCode = code,
                        MaterialName = row.MaterialName.Trim(),
                        CategoryId = leafCatId,
                        Unit = row.Unit.Trim(),
                        Origin = string.IsNullOrWhiteSpace(row.Origin) ? null : row.Origin.Trim(),
                        TechnicalSpec = string.IsNullOrWhiteSpace(row.TechnicalSpec) ? null : row.TechnicalSpec.Trim(),
                        GeneralTechnicalSpec = string.IsNullOrWhiteSpace(row.GeneralTechnicalSpec) ? null : row.GeneralTechnicalSpec.Trim(),
                        QualityStandard = string.IsNullOrWhiteSpace(row.QualityStandard) ? null : row.QualityStandard.Trim(),
                        CeilingPrice = row.CeilingPrice is > 0 ? row.CeilingPrice : null,
                        AbcClass = NormalizeAbc(row.AbcClass),
                        IsConsumable = row.IsConsumable,
                        IsActive = true
                    };
                    db.Materials.Add(newMaterial);
                    existingMaterials[code] = newMaterial;
                    createdCount++;
                    importedCodes.Add(code);
                }
            }

            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);

            return new MaterialImportResultDto(
                createdCount,
                updatedCount,
                skippedCount,
                createdCount + updatedCount,
                importedCodes,
                errors);
        }
        catch (Exception ex)
        {
            await tx.RollbackAsync(ct);
            throw new DomainRuleException($"Lỗi trong quá trình import dữ liệu: {ex.Message}");
        }
    }

    private static string GenerateCategoryCode(string prefix, string name)
    {
        var slug = NormalizeHeader(name);
        if (slug.Length > 10) slug = slug[..10];
        if (string.IsNullOrEmpty(slug)) slug = "CAT";
        var uid = Guid.NewGuid().ToString("N")[..4].ToUpperInvariant();
        return $"{prefix}-{slug.ToUpperInvariant()}-{uid}";
    }

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

    private static bool IsSubHeaderOrFormulaRow(string[] row, int codeIndex)
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

        // Nếu mã rỗng, kiểm tra xem dòng này có phải là dòng tiêu đề phụ (sub-header) hoặc chú thích F-code không
        var rowText = string.Join(" ", row.Select(NormalizeHeader));
        if (string.IsNullOrEmpty(code))
        {
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
        }

        return false;
    }

    private static int FindHeaderRowIndex(List<string[]> rows)
    {
        for (var i = 0; i < Math.Min(10, rows.Count); i++)
        {
            var row = rows[i];
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

    private static MaterialColumnMap MapColumns(List<string[]> rows, int headerIndex)
    {
        var maxCols = rows[headerIndex].Length;
        for (var r = headerIndex; r < Math.Min(headerIndex + 4, rows.Count); r++)
        {
            if (rows[r].Length > maxCols) maxCols = rows[r].Length;
        }

        var combinedHeaders = new string[maxCols];
        for (var c = 0; c < maxCols; c++)
        {
            var tokens = new List<string>();
            for (var r = headerIndex; r < Math.Min(headerIndex + 4, rows.Count); r++)
            {
                if (c < rows[r].Length && !string.IsNullOrWhiteSpace(rows[r][c]))
                {
                    tokens.Add(NormalizeHeader(rows[r][c]));
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

    private static List<string[]> ReadExcelRows(Stream stream)
    {
        using var workbook = new XLWorkbook(stream);
        var sheet = workbook.Worksheets.FirstOrDefault(w => w.Name.Trim().Equals("FINAL", StringComparison.OrdinalIgnoreCase))
            ?? workbook.Worksheets.FirstOrDefault(w => w.Name.Trim().Equals("THÉP", StringComparison.OrdinalIgnoreCase))
            ?? workbook.Worksheets.FirstOrDefault(w => w.Name.Contains("danh muc", StringComparison.OrdinalIgnoreCase))
            ?? workbook.Worksheets.FirstOrDefault(w => w.RangeUsed() != null && w.RangeUsed().RowCount() > 1)
            ?? workbook.Worksheets.First();

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
