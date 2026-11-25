using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;
using NamViet.ERP.Api.Application.Dtos.Wms;
using NamViet.ERP.Modules.Warehouse.Application.Helpers;
using NamViet.ERP.Modules.Warehouse.Infrastructure.Persistence;
using NamViet.ERP.SharedKernel.Common;

namespace NamViet.ERP.Api.Application.Services.Wms;

public interface IMaterialImportExcelService
{
    Task<byte[]> GenerateTemplateAsync(CancellationToken ct = default);
    Task<MaterialImportParseResultDto> ParseImportFileAsync(Stream stream, string fileName, string? targetSheet = null, CancellationToken ct = default);
    Task<MaterialImportResultDto> ImportBatchAsync(MaterialImportBatchRequest request, CancellationToken ct = default);
}

public sealed partial class MaterialImportExcelService(WarehouseDbContext db) : IMaterialImportExcelService
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
        
        // ── Sheet 1: Danh mục vật tư mẫu (KHMH) ───────────────────────────────
        var sheet = workbook.Worksheets.Add("KHMH");

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
        Stream stream, string fileName, string? targetSheet = null, CancellationToken ct = default)
    {
        if (stream.CanSeek && stream.Length > MaxFileBytes)
            throw new DomainRuleException("File vượt quá kích thước cho phép (5MB).");

        var extension = Path.GetExtension(fileName).ToLowerInvariant();
        List<RawExcelRow> rows;
        IReadOnlyList<string>? availableSheets = null;
        string? selectedSheet = null;

        if (extension is ".xlsx" or ".xls")
        {
            var (excelRows, sheets, currentSheet) = ReadExcelRows(stream, targetSheet);
            rows = excelRows;
            availableSheets = sheets;
            selectedSheet = currentSheet;
        }
        else if (extension == ".csv")
        {
            rows = ReadCsvRows(stream);
        }
        else
        {
            throw new DomainRuleException("Chỉ chấp nhận file .xlsx, .xls hoặc .csv.");
        }

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
            .Where(r => !IsEmptyRow(r.Cells) && !IsSubHeaderOrFormulaRow(r.Cells, colMap.MaterialCode, colMap.Unit, colMap.CeilingPrice))
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
        var seenCodesInFile = new Dictionary<string, List<int>>(StringComparer.OrdinalIgnoreCase);

        var validCount = 0;
        var existingCount = 0;
        var errorCount = 0;

        for (var i = 0; i < dataRows.Count; i++)
        {
            var rawRow = dataRows[i];
            var excelRow = rawRow.RowNumber;
            var row = rawRow.Cells;

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

            var isEquipment = IsEquipmentCategoryOrItem(danhMuc, nhomVatTu, loaiVatTu, name, code);

            // Tự động kết hợp thông số hình học cho Thép / Kim khí / Bảo ôn / Ống
            var techSpec = GetCell(row, colMap.TechnicalSpec).Trim();
            var phiRong = colMap.PhiRong >= 0 ? GetCell(row, colMap.PhiRong).Trim() : string.Empty;
            var doDay = colMap.DoDay >= 0 ? GetCell(row, colMap.DoDay).Trim() : string.Empty;
            var chieuDai = colMap.ChieuDai >= 0 ? GetCell(row, colMap.ChieuDai).Trim() : string.Empty;

            // Nếu là thiết bị và các cột hình học trùng với cột công suất/lưu lượng/cột áp -> không gán nhầm kích thước
            if (isEquipment)
            {
                if (colMap.PhiRong == colMap.CongSuat) phiRong = string.Empty;
                if (colMap.DoDay == colMap.LuuLuong) doDay = string.Empty;
                if (colMap.ChieuDai == colMap.CotAp) chieuDai = string.Empty;
            }

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

            // Tự động kết hợp thông số công suất / lưu lượng cho Bơm / Quạt / Động cơ / Thiết bị
            var genSpec = GetCell(row, colMap.GeneralTechnicalSpec).Trim();
            var moTa = colMap.MoTa >= 0 ? GetCell(row, colMap.MoTa).Trim() : string.Empty;

            if (isEquipment)
            {
                var congSuat = colMap.CongSuat >= 0 ? GetCell(row, colMap.CongSuat).Trim() : string.Empty;
                var luuLuong = colMap.LuuLuong >= 0 ? GetCell(row, colMap.LuuLuong).Trim() : string.Empty;
                var cotAp = colMap.CotAp >= 0 ? GetCell(row, colMap.CotAp).Trim() : string.Empty;
                var dienAp = colMap.DienAp >= 0 ? GetCell(row, colMap.DienAp).Trim() : string.Empty;

                if (LooksLikeDimension(congSuat)) congSuat = string.Empty;
                if (LooksLikeDimension(luuLuong)) luuLuong = string.Empty;
                if (LooksLikeDimension(cotAp)) cotAp = string.Empty;

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
            }
            else
            {
                // Vật tư không phải thiết bị: Tuyệt đối KHÔNG gán kW, m³/h, Pa
                if (string.IsNullOrEmpty(genSpec) && !string.IsNullOrEmpty(moTa) && !moTa.Equals(name, StringComparison.OrdinalIgnoreCase))
                {
                    genSpec = moTa;
                }
                if (!string.IsNullOrEmpty(genSpec) && (genSpec.Contains("kW", StringComparison.OrdinalIgnoreCase) || genSpec.Contains("Pa", StringComparison.OrdinalIgnoreCase) || genSpec.Contains("m³/h", StringComparison.OrdinalIgnoreCase)))
                {
                    genSpec = string.Empty;
                }
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

                if (seenCodesInFile.TryGetValue(code, out var existingRows))
                {
                    var rowsList = string.Join(", ", existingRows);
                    rowErrors.Add($"Trùng mã \"{code}\" với dòng {rowsList} trong chính file tải lên.");
                    globalErrors.Add(new MaterialImportErrorDto(excelRow, "materialCode", $"Trùng mã \"{code}\" với dòng {rowsList} trong file."));
                    existingRows.Add(excelRow);
                }
                else
                {
                    seenCodesInFile[code] = [excelRow];
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
            globalErrors,
            availableSheets,
            selectedSheet);
    }
}
