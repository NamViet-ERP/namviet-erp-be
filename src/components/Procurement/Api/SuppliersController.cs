using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NamViet.ERP.Api.Application.Dtos.Purchasing;
using NamViet.ERP.Api.Application.Services.Purchasing;
using NamViet.ERP.Api.Infrastructure.Authorization;
using NamViet.ERP.Api.Services.Storage;
using NamViet.ERP.Modules.Warehouse.Application.DTOs;
using NamViet.ERP.Modules.Warehouse.Domain.Entities;
using NamViet.ERP.Modules.Warehouse.Domain.SupplierEvaluation;
using NamViet.ERP.Modules.Warehouse.Infrastructure.Persistence;
using NamViet.ERP.SharedKernel.Common;
using NamViet.ERP.SharedKernel.Interfaces;

namespace NamViet.ERP.Api.Controllers;

[ApiController]
[Route("api/warehouse/suppliers")]
[Authorize]
public class SuppliersController(
    WarehouseDbContext db,
    ISupplierGroupService groups,
    ICurrentUser currentUser,
    IFileStorage? fileStorage = null) : ControllerBase
{
    [HttpGet("{id:int}/groups")]
    [RequirePermission("wms.suppliers.view:all")]
    public async Task<ActionResult<IReadOnlyList<SupplierGroupRefDto>>> GetGroups(
        int id, CancellationToken ct) =>
        Ok(await groups.ListGroupsOfSupplierAsync(id, ct));

    [HttpPut("{id:int}/groups")]
    [RequirePermission("wms.suppliers.update")]
    public async Task<ActionResult<IReadOnlyList<SupplierGroupRefDto>>> SetGroups(
        int id, [FromBody] SetSupplierGroupsRequest request, CancellationToken ct) =>
        Ok(await groups.SetGroupsOfSupplierAsync(id, request, ct));

    [HttpGet("sourcing-kpi")]
    [RequirePermission("wms.suppliers.view:all")]
    public async Task<ActionResult<SourcingKpiSummaryDto>> GetSourcingKpi(CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        var startOfMonth = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);

        var suppliers = await db.Suppliers.AsNoTracking().ToListAsync(ct);
        var totalSuppliers = suppliers.Count;
        var totalNewThisMonth = suppliers.Count(s => s.CreatedAt >= startOfMonth);

        var gradeACount = suppliers.Count(s => s.Rating == SupplierGrades.A);
        var gradeBCount = suppliers.Count(s => s.Rating == SupplierGrades.B);
        var gradeCCount = suppliers.Count(s => s.Rating == SupplierGrades.C);
        var gradeDCount = suppliers.Count(s => s.Rating == SupplierGrades.D);
        var otherCount = totalSuppliers - (gradeACount + gradeBCount + gradeCCount + gradeDCount);

        var promotionsThisMonth = await db.SupplierTierEvaluations
            .AsNoTracking()
            .Where(e => e.CreatedAt >= startOfMonth && e.OldGrade != e.NewGrade)
            .CountAsync(ct);

        var promotionRate = totalNewThisMonth > 0
            ? Math.Round((decimal)promotionsThisMonth / totalNewThisMonth * 100m, 1)
            : 0m;

        return Ok(new SourcingKpiSummaryDto(
            totalSuppliers,
            totalNewThisMonth,
            promotionsThisMonth,
            gradeACount,
            gradeBCount,
            gradeCCount,
            gradeDCount,
            otherCount,
            promotionRate
        ));
    }

    [HttpPost("{id:int}/tier-evaluation")]
    [RequirePermission("wms.suppliers.update")]
    public async Task<ActionResult<SupplierTierEvaluationDto>> SubmitTierEvaluation(
        int id, [FromBody] SubmitTierEvaluationRequest request, CancellationToken ct)
    {
        var supplier = await db.Suppliers.FirstOrDefaultAsync(s => s.Id == id, ct);
        if (supplier == null)
            return NotFound(new { message = $"Không tìm thấy nhà cung cấp id={id}." });

        var price = Math.Clamp(request.PriceScore, 0m, 50m);
        var profile = Math.Clamp(request.ProfileScore, 0m, 20m);
        var payment = Math.Clamp(request.PaymentScore, 0m, 20m);
        var service = Math.Clamp(request.ServiceScore, 0m, 10m);
        var total = price + profile + payment + service;

        var oldGrade = supplier.Rating;
        string newGrade;

        if (total >= 80m)
        {
            if (!string.IsNullOrWhiteSpace(request.TargetGrade))
            {
                newGrade = request.TargetGrade.Trim().ToUpperInvariant();
            }
            else
            {
                newGrade = oldGrade switch
                {
                    SupplierGrades.D => SupplierGrades.C,
                    SupplierGrades.C => SupplierGrades.B,
                    SupplierGrades.B => SupplierGrades.A,
                    _ => SupplierGrades.C
                };
            }
            supplier.Rating = newGrade;
            supplier.UpdatedAt = DateTime.UtcNow;
        }
        else
        {
            if (!string.IsNullOrWhiteSpace(request.TargetGrade) && request.TargetGrade.Trim().ToUpperInvariant() != oldGrade)
            {
                return BadRequest(new { message = $"Tổng điểm đánh giá là {total}/100, chưa đạt mức tối thiểu 80 điểm để thăng hạng lên {request.TargetGrade}." });
            }
            newGrade = oldGrade;
        }

        var eval = new SupplierTierEvaluation
        {
            SupplierId = supplier.Id,
            OldGrade = oldGrade,
            NewGrade = newGrade,
            TotalScore = total,
            PriceScore = price,
            ProfileScore = profile,
            PaymentScore = payment,
            ServiceScore = service,
            EvaluationDetailsJson = request.DetailsJson,
            Reason = request.Reason?.Trim(),
            EvaluatedById = currentUser.Id.ToString(),
            EvaluatedByName = !string.IsNullOrWhiteSpace(currentUser.Name) ? currentUser.Name : currentUser.Email,
            CreatedAt = DateTime.UtcNow
        };

        db.SupplierTierEvaluations.Add(eval);
        await db.SaveChangesAsync(ct);

        return Ok(new SupplierTierEvaluationDto(
            eval.Id,
            eval.SupplierId,
            eval.OldGrade,
            eval.NewGrade,
            eval.TotalScore,
            eval.PriceScore,
            eval.ProfileScore,
            eval.PaymentScore,
            eval.ServiceScore,
            eval.EvaluationDetailsJson,
            eval.Reason,
            eval.EvaluatedById,
            eval.EvaluatedByName,
            eval.CreatedAt
        ));
    }

    [HttpGet("{id:int}/grade-history")]
    [RequirePermission("wms.suppliers.view:all")]
    public async Task<ActionResult<IReadOnlyList<SupplierTierEvaluationDto>>> GetGradeHistory(
        int id, CancellationToken ct)
    {
        var exists = await db.Suppliers.AnyAsync(s => s.Id == id, ct);
        if (!exists)
            return NotFound(new { message = $"Không tìm thấy nhà cung cấp id={id}." });

        var items = await db.SupplierTierEvaluations
            .AsNoTracking()
            .Where(e => e.SupplierId == id)
            .OrderByDescending(e => e.CreatedAt)
            .Select(e => new SupplierTierEvaluationDto(
                e.Id,
                e.SupplierId,
                e.OldGrade,
                e.NewGrade,
                e.TotalScore,
                e.PriceScore,
                e.ProfileScore,
                e.PaymentScore,
                e.ServiceScore,
                e.EvaluationDetailsJson,
                e.Reason,
                e.EvaluatedById,
                e.EvaluatedByName,
                e.CreatedAt
            ))
            .ToListAsync(ct);

        return Ok(items);
    }

    [HttpGet]
    [RequirePermission("wms.suppliers.view:all")]
    public async Task<ActionResult<PagedResult<SupplierDto>>> GetAll(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? search = null,
        [FromQuery] string? rating = null,
        [FromQuery] int? groupId = null,
        [FromQuery] bool? isBlacklisted = null,
        [FromQuery] string? approvalStatus = null,
        CancellationToken ct = default)
    {
        (page, pageSize) = Paging.Normalize(page, pageSize);
        var query = db.Suppliers.AsQueryable();
        if (!string.IsNullOrWhiteSpace(search))
            query = query.Where(s => s.SupplierName.Contains(search) || s.SupplierCode.Contains(search) || (s.TaxCode != null && s.TaxCode.Contains(search)));
        if (!string.IsNullOrWhiteSpace(rating))
            query = query.Where(s => s.Rating == rating);
        if (isBlacklisted is bool bl)
            query = query.Where(s => s.IsBlacklisted == bl);
        if (!string.IsNullOrWhiteSpace(approvalStatus))
            query = query.Where(s => s.ApprovalStatus == approvalStatus);
        if (groupId is int gid)
        {
            query = query.Where(s => s.GroupMemberships.Any(m => m.SupplierGroupId == gid));
        }

        var total = await query.CountAsync(ct);
        var suppliers = await query
            .OrderBy(s => s.SupplierCode)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(s => new
            {
                s.Id,
                s.SupplierCode,
                s.SupplierName,
                s.TaxCode,
                s.SupplierType,
                s.Scope,
                s.Website,
                s.ContactPhone,
                s.MaxDebtDays,
                s.CreditLimit,
                s.PaymentTerms,
                s.Rating,
                s.ApprovalStatus,
                s.IsBlacklisted,
                s.BlacklistReason,
                s.BlacklistedAt,
                s.BlacklistedByName,
            })
            .ToListAsync(ct);

        var supplierIds = suppliers.Select(s => s.Id).ToArray();
        var industryGroups = await db.SupplierGroupMembers
            .AsNoTracking()
            .Where(m => supplierIds.Contains(m.SupplierId))
            .Select(m => new { m.SupplierId, GroupName = m.Group!.Name })
            .ToListAsync(ct);
        var industryGroupMap = industryGroups
            .GroupBy(x => x.SupplierId)
            .ToDictionary(
                g => g.Key,
                g => (IReadOnlyList<string>)g
                    .Select(x => x.GroupName)
                    .Distinct()
                    .OrderBy(n => n)
                    .ToList());

        var revenueRows = await db.PurchaseOrders
            .AsNoTracking()
            .Where(o => o.SupplierId != null
                        && supplierIds.Contains(o.SupplierId.Value)
                        && o.DeliveryStatus == PurchaseOrderDeliveryStatuses.Complete)
            .GroupBy(o => o.SupplierId!.Value)
            .Select(g => new { SupplierId = g.Key, PurchasedRevenue = g.Sum(o => o.TotalAmount) })
            .ToListAsync(ct);
        var revenueMap = revenueRows.ToDictionary(x => x.SupplierId, x => Math.Round(x.PurchasedRevenue, 2));

        var latestEvaluations = await db.SupplierEvaluations
            .AsNoTracking()
            .Where(v => supplierIds.Contains(v.SupplierId))
            .GroupBy(v => v.SupplierId)
            .Select(g => g
                .OrderByDescending(v => v.EvaluatedAt)
                .ThenByDescending(v => v.Id)
                .Select(v => new
                {
                    v.SupplierId,
                    v.Period,
                    v.Score,
                    v.Grade,
                    v.EvaluatedAt,
                })
                .FirstOrDefault()!)
            .ToListAsync(ct);
        var latestEvaluationMap = latestEvaluations
            .Where(x => x != null)
            .ToDictionary(
                x => x.SupplierId,
                x => (SupplierLatestEvaluationDto?)new SupplierLatestEvaluationDto(
                    x.Period,
                    x.Score,
                    x.Grade,
                    SupplierGradeMapper.KpiGradeLabel(x.Grade),
                    x.EvaluatedAt));

        var supplierContacts = await db.SupplierContacts
            .AsNoTracking()
            .Where(c => supplierIds.Contains(c.SupplierId))
            .OrderBy(c => c.Role == SupplierContactRoles.Primary ? 0 : 1)
            .ThenBy(c => c.Id)
            .ToListAsync(ct);

        var contactCountMap = supplierContacts
            .GroupBy(c => c.SupplierId)
            .ToDictionary(g => g.Key, g => g.Count());

        var primaryContactMap = supplierContacts
            .GroupBy(c => c.SupplierId)
            .ToDictionary(
                g => g.Key,
                g =>
                {
                    var pc = g.FirstOrDefault(c => c.Role == SupplierContactRoles.Primary) ?? g.FirstOrDefault();
                    return pc == null ? null : new SupplierPrimaryContactDto(
                        pc.Id,
                        pc.FullName,
                        pc.Position,
                        pc.Phone,
                        pc.Email,
                        pc.Role);
                });

        var items = suppliers.Select(s => new SupplierDto(
            s.Id,
            s.SupplierCode,
            s.SupplierName,
            s.ContactPhone,
            s.MaxDebtDays,
            s.CreditLimit,
            s.PaymentTerms,
            s.Rating,
            s.ApprovalStatus,
            s.IsBlacklisted,
            s.BlacklistReason,
            s.BlacklistedAt,
            s.BlacklistedByName,
            revenueMap.GetValueOrDefault(s.Id, 0m),
            industryGroupMap.GetValueOrDefault(s.Id, Array.Empty<string>()),
            latestEvaluationMap.GetValueOrDefault(s.Id),
            s.TaxCode,
            s.SupplierType,
            s.Scope,
            s.Website,
            primaryContactMap.GetValueOrDefault(s.Id),
            null,
            contactCountMap.GetValueOrDefault(s.Id, 0))).ToList();
        return Ok(PagedResult<SupplierDto>.Create(items, total, page, pageSize));
    }

    [HttpGet("next-code")]
    [RequirePermission("wms.suppliers.create")]
    public async Task<ActionResult<SupplierNextCodeDto>> NextCode(CancellationToken ct) =>
        Ok(new SupplierNextCodeDto(await NextSupplierCodeAsync(ct)));

    [HttpGet("template-excel")]
    [RequirePermission("wms.suppliers.view:all")]
    public IActionResult DownloadExcelTemplate()
    {
        using var workbook = new ClosedXML.Excel.XLWorkbook();
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
            cell.Style.Fill.BackgroundColor = ClosedXML.Excel.XLColor.FromHtml("#1E40AF");
            cell.Style.Font.FontColor = ClosedXML.Excel.XLColor.White;
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
        return File(
            ms.ToArray(),
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            "Mau_Nhap_NhaCungCap.xlsx");
    }

    [HttpPost("preview-excel")]
    [RequirePermission("wms.suppliers.create")]
    public async Task<ActionResult<SupplierImportPreviewResponseDto>> PreviewExcel(IFormFile file, CancellationToken ct)
    {
        if (file == null || file.Length == 0)
            return BadRequest("Vui lòng chọn file Excel danh sách NCC.");

        using var stream = file.OpenReadStream();
        using var workbook = new ClosedXML.Excel.XLWorkbook(stream);
        var ws = workbook.Worksheets.FirstOrDefault();
        if (ws is null)
            return BadRequest("File Excel không có sheet nào.");

        var existingNames = await db.Suppliers.AsNoTracking()
            .Select(s => s.SupplierName)
            .ToListAsync(ct);

        var existingTaxCodes = await db.Suppliers.AsNoTracking()
            .Where(s => !string.IsNullOrEmpty(s.TaxCode))
            .Select(s => s.TaxCode!)
            .ToListAsync(ct);

        var previewRows = new List<SupplierImportRowPreviewDto>();
        var seenNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        var rowsUsed = ws.RowsUsed().Skip(1);
        foreach (var row in rowsUsed)
        {
            var code = row.Cell(1).GetString().Trim();
            var name = row.Cell(2).GetString().Trim();
            var taxCode = row.Cell(3).GetString().Trim();
            var phone = row.Cell(4).GetString().Trim();
            var website = row.Cell(5).GetString().Trim();
            var supplierType = row.Cell(6).GetString().Trim();
            var scope = row.Cell(7).GetString().Trim();

            var debtStr = row.Cell(8).GetString().Trim();
            int.TryParse(debtStr, out var debt);
            if (debt <= 0) debt = 30;

            var limitStr = row.Cell(9).GetString().Trim();
            decimal? creditLimit = decimal.TryParse(limitStr, out var lim) && lim > 0 ? lim : null;

            var terms = row.Cell(10).GetString().Trim();
            var groups = row.Cell(11).GetString().Trim();

            if (string.IsNullOrWhiteSpace(name) && string.IsNullOrWhiteSpace(taxCode) && string.IsNullOrWhiteSpace(phone))
                continue;

            var isValid = true;
            var errorMsg = new List<string>();

            if (string.IsNullOrWhiteSpace(name))
            {
                isValid = false;
                errorMsg.Add("Tên NCC là bắt buộc");
            }
            else if (existingNames.Any(n => Supplier.SameFoldedName(n, name)) || seenNames.Contains(name))
            {
                isValid = false;
                errorMsg.Add("Tên NCC đã tồn tại hoặc bị trùng trong file");
            }
            else
            {
                seenNames.Add(name);
            }

            if (!string.IsNullOrWhiteSpace(taxCode))
            {
                if (taxCode.Length > 50)
                {
                    isValid = false;
                    errorMsg.Add("Mã số thuế tối đa 50 ký tự");
                }
                else if (existingTaxCodes.Any(t => t.Equals(taxCode, StringComparison.OrdinalIgnoreCase)))
                {
                    isValid = false;
                    errorMsg.Add($"MST '{taxCode}' đã tồn tại trên hệ thống");
                }
            }

            previewRows.Add(new SupplierImportRowPreviewDto(
                SupplierCode: string.IsNullOrWhiteSpace(code) ? "(Tự sinh)" : code,
                SupplierName: name,
                TaxCode: string.IsNullOrWhiteSpace(taxCode) ? null : taxCode,
                ContactPhone: string.IsNullOrWhiteSpace(phone) ? null : phone,
                Website: string.IsNullOrWhiteSpace(website) ? null : website,
                SupplierType: string.IsNullOrWhiteSpace(supplierType) ? null : supplierType,
                Scope: string.IsNullOrWhiteSpace(scope) ? null : scope,
                MaxDebtDays: debt,
                CreditLimit: creditLimit,
                PaymentTerms: string.IsNullOrWhiteSpace(terms) ? null : terms,
                IndustryGroups: string.IsNullOrWhiteSpace(groups) ? null : groups,
                IsValid: isValid,
                Error: errorMsg.Count > 0 ? string.Join("; ", errorMsg) : null));
        }

        var total = previewRows.Count;
        var valid = previewRows.Count(r => r.IsValid);
        var errors = total - valid;

        return Ok(new SupplierImportPreviewResponseDto(total, valid, errors, previewRows));
    }

    [HttpPost("import-excel")]
    [RequirePermission("wms.suppliers.create")]
    public async Task<ActionResult<SupplierImportResultDto>> ImportExcel(IFormFile file, CancellationToken ct)
    {
        if (file == null || file.Length == 0)
            return BadRequest("Vui lòng chọn file Excel danh sách NCC.");

        using var stream = file.OpenReadStream();
        using var workbook = new ClosedXML.Excel.XLWorkbook(stream);
        var ws = workbook.Worksheets.FirstOrDefault();
        if (ws is null)
            return BadRequest("File Excel không có sheet nào.");

        var existingNames = await db.Suppliers.AsNoTracking()
            .Select(s => s.SupplierName)
            .ToListAsync(ct);

        var existingTaxCodes = await db.Suppliers.AsNoTracking()
            .Where(s => !string.IsNullOrEmpty(s.TaxCode))
            .Select(s => s.TaxCode!)
            .ToListAsync(ct);

        var allGroups = await db.SupplierGroups.AsNoTracking().ToListAsync(ct);
        var groupMap = allGroups.ToDictionary(g => g.Name.Trim().ToLower(), g => g.Id);

        var importedCount = 0;
        var failedCount = 0;
        var errors = new List<string>();
        var seenNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        var rowsUsed = ws.RowsUsed().Skip(1);
        var rowIndex = 1;

        foreach (var row in rowsUsed)
        {
            rowIndex++;
            var code = row.Cell(1).GetString().Trim();
            var name = row.Cell(2).GetString().Trim();
            var taxCode = row.Cell(3).GetString().Trim();
            var phone = row.Cell(4).GetString().Trim();
            var website = row.Cell(5).GetString().Trim();
            var supplierType = row.Cell(6).GetString().Trim();
            var scope = row.Cell(7).GetString().Trim();

            var debtStr = row.Cell(8).GetString().Trim();
            int.TryParse(debtStr, out var debt);
            if (debt <= 0) debt = 30;

            var limitStr = row.Cell(9).GetString().Trim();
            decimal? creditLimit = decimal.TryParse(limitStr, out var lim) && lim > 0 ? lim : null;

            var terms = row.Cell(10).GetString().Trim();
            var groups = row.Cell(11).GetString().Trim();

            if (string.IsNullOrWhiteSpace(name) && string.IsNullOrWhiteSpace(taxCode) && string.IsNullOrWhiteSpace(phone))
                continue;

            if (string.IsNullOrWhiteSpace(name))
            {
                failedCount++;
                errors.Add($"Dòng {rowIndex}: Tên nhà cung cấp không được để trống.");
                continue;
            }

            if (existingNames.Any(n => Supplier.SameFoldedName(n, name)) || seenNames.Contains(name))
            {
                failedCount++;
                errors.Add($"Dòng {rowIndex}: Tên nhà cung cấp '{name}' đã tồn tại hoặc bị trùng trong file.");
                continue;
            }

            if (!string.IsNullOrWhiteSpace(taxCode))
            {
                if (taxCode.Length > 50)
                {
                    failedCount++;
                    errors.Add($"Dòng {rowIndex}: Mã số thuế tối đa 50 ký tự.");
                    continue;
                }
                if (existingTaxCodes.Any(t => t.Equals(taxCode, StringComparison.OrdinalIgnoreCase)))
                {
                    failedCount++;
                    errors.Add($"Dòng {rowIndex}: MST '{taxCode}' đã tồn tại trên hệ thống.");
                    continue;
                }
            }

            seenNames.Add(name);

            string? normalizedPhone = null;
            if (!string.IsNullOrWhiteSpace(phone))
            {
                try
                {
                    normalizedPhone = VietnamPhoneRules.NormalizeOptional(phone);
                }
                catch
                {
                    normalizedPhone = phone.Length <= 50 ? phone : phone[..50];
                }
            }

            var finalCode = !string.IsNullOrWhiteSpace(code)
                ? code
                : await NextSupplierCodeAsync(ct);

            var supplier = new Supplier
            {
                SupplierCode = finalCode,
                SupplierName = name,
                TaxCode = string.IsNullOrWhiteSpace(taxCode) ? null : taxCode,
                ContactPhone = normalizedPhone,
                Website = string.IsNullOrWhiteSpace(website) ? null : (website.Length <= 255 ? website : website[..255]),
                SupplierType = string.IsNullOrWhiteSpace(supplierType) ? null : (supplierType.Length <= 50 ? supplierType : supplierType[..50]),
                Scope = string.IsNullOrWhiteSpace(scope) ? null : (scope.Length <= 50 ? scope : scope[..50]),
                MaxDebtDays = debt,
                CreditLimit = creditLimit,
                PaymentTerms = string.IsNullOrWhiteSpace(terms) ? null : terms,
                Rating = SupplierGrades.D,
                ApprovalStatus = SupplierApprovalStatuses.New,
                CreatedAt = DateTime.UtcNow,
            };

            db.Suppliers.Add(supplier);
            await db.SaveChangesAsync(ct);
            existingNames.Add(name);
            if (!string.IsNullOrEmpty(supplier.TaxCode)) existingTaxCodes.Add(supplier.TaxCode);

            if (!string.IsNullOrWhiteSpace(groups))
            {
                var groupNames = groups.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                foreach (var gn in groupNames)
                {
                    if (groupMap.TryGetValue(gn.ToLower(), out var gId))
                    {
                        db.SupplierGroupMembers.Add(new SupplierGroupMember
                        {
                            SupplierId = supplier.Id,
                            SupplierGroupId = gId,
                        });
                    }
                }
                await db.SaveChangesAsync(ct);
            }

            importedCount++;
        }

        var totalRows = importedCount + failedCount;
        return Ok(new SupplierImportResultDto(totalRows, importedCount, failedCount, errors));
    }

    [HttpGet("blacklist")]
    [RequirePermission("wms.suppliers.view:all")]
    public async Task<ActionResult<IReadOnlyList<SupplierBlacklistDto>>> ListBlacklist(
        CancellationToken ct)
    {
        var items = await db.Suppliers.AsNoTracking()
            .Where(s => s.IsBlacklisted)
            .OrderByDescending(s => s.BlacklistedAt)
            .ThenBy(s => s.SupplierCode)
            .Select(s => new SupplierBlacklistDto(
                s.Id,
                s.SupplierCode,
                s.SupplierName,
                s.ContactPhone,
                s.Rating,
                s.BlacklistedAt ?? s.UpdatedAt,
                s.BlacklistedByName,
                s.BlacklistReason))
            .ToListAsync(ct);
        return Ok(items);
    }

    [HttpGet("{id:int}")]
    [RequirePermission("wms.suppliers.view:all")]
    public async Task<ActionResult<SupplierDto>> GetById(int id, CancellationToken ct)
    {
        var s = await db.Suppliers.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct);
        if (s is null)
            return NotFound();

        var groupsOfSupplier = await db.SupplierGroupMembers.AsNoTracking()
            .Where(m => m.SupplierId == id)
            .Select(m => m.Group!.Name)
            .Distinct()
            .OrderBy(n => n)
            .ToListAsync(ct);

        var purchasedRevenue = await db.PurchaseOrders.AsNoTracking()
            .Where(o => o.SupplierId == id && o.DeliveryStatus == PurchaseOrderDeliveryStatuses.Complete)
            .SumAsync(o => o.TotalAmount, ct);

        var latest = await db.SupplierEvaluations.AsNoTracking()
            .Where(v => v.SupplierId == id)
            .OrderByDescending(v => v.EvaluatedAt)
            .ThenByDescending(v => v.Id)
            .Select(v => new { v.Period, v.Score, v.Grade, v.EvaluatedAt })
            .FirstOrDefaultAsync(ct);

        SupplierLatestEvaluationDto? latestEvaluation = latest is null
            ? null
            : new SupplierLatestEvaluationDto(
                latest.Period,
                latest.Score,
                latest.Grade,
                SupplierGradeMapper.KpiGradeLabel(latest.Grade),
                latest.EvaluatedAt);

        var contacts = await db.SupplierContacts.AsNoTracking()
            .Where(c => c.SupplierId == id)
            .OrderBy(c => c.Role == SupplierContactRoles.Primary ? 0 : 1)
            .ThenBy(c => c.Id)
            .ToListAsync(ct);

        var primaryContactEntity = contacts.FirstOrDefault(c => c.Role == SupplierContactRoles.Primary) ?? contacts.FirstOrDefault();
        SupplierPrimaryContactDto? primaryContact = primaryContactEntity is null
            ? null
            : new SupplierPrimaryContactDto(
                primaryContactEntity.Id,
                primaryContactEntity.FullName,
                primaryContactEntity.Position,
                primaryContactEntity.Phone,
                primaryContactEntity.Email,
                primaryContactEntity.Role);

        var attachments = await db.SupplierAttachments.AsNoTracking()
            .Where(a => a.SupplierId == id)
            .OrderByDescending(a => a.UploadedAt)
            .Select(a => new SupplierAttachmentDto(
                a.Id,
                a.SupplierId,
                a.DocType,
                SupplierAttachmentDocTypes.ToClientLabel(a.DocType),
                a.FileName,
                a.FileSizeBytes,
                a.ContentType,
                a.UploadedAt,
                a.UploadedByName,
                $"/api/warehouse/suppliers/{id}/attachments/{a.Id}/download"
            ))
            .ToListAsync(ct);

        return Ok(new SupplierDto(
            s.Id,
            s.SupplierCode,
            s.SupplierName,
            s.ContactPhone,
            s.MaxDebtDays,
            s.CreditLimit,
            s.PaymentTerms,
            s.Rating,
            s.ApprovalStatus,
            s.IsBlacklisted,
            s.BlacklistReason,
            s.BlacklistedAt,
            s.BlacklistedByName,
            Math.Round(purchasedRevenue, 2),
            groupsOfSupplier,
            latestEvaluation,
            s.TaxCode,
            s.SupplierType,
            s.Scope,
            s.Website,
            primaryContact,
            attachments,
            contacts.Count));
    }

    [HttpPost]
    [RequirePermission("wms.suppliers.create")]
    public async Task<ActionResult<SupplierDto>> Create([FromBody] UpsertSupplierRequest req, CancellationToken ct)
    {
        if (TryValidateUpsert(req, requireCode: false, out var phone, out var errors) is BadRequestObjectResult bad)
            return bad;

        var name = req.SupplierName.Trim();
        if (await SupplierNameTakenAsync(name, exceptId: null, currentName: null, ct))
        {
            errors["supplierName"] = ["Tên nhà cung cấp đã tồn tại."];
            return FieldErrors(errors);
        }

        string supplierCode;
        if (!string.IsNullOrWhiteSpace(req.SupplierCode))
        {
            var customCode = req.SupplierCode.Trim();
            if (await db.Suppliers.IgnoreQueryFilters().AnyAsync(s => s.SupplierCode == customCode, ct))
            {
                errors["supplierCode"] = [$"Mã nhà cung cấp '{customCode}' đã tồn tại."];
                return FieldErrors(errors);
            }
            supplierCode = customCode;
        }
        else
        {
            supplierCode = await NextSupplierCodeAsync(ct);
        }

        var s = new Supplier
        {
            SupplierCode = supplierCode,
            SupplierName = name,
            TaxCode = string.IsNullOrWhiteSpace(req.TaxCode) ? null : req.TaxCode.Trim(),
            SupplierType = string.IsNullOrWhiteSpace(req.SupplierType) ? null : req.SupplierType.Trim(),
            Scope = string.IsNullOrWhiteSpace(req.Scope) ? null : req.Scope.Trim(),
            Website = string.IsNullOrWhiteSpace(req.Website) ? null : req.Website.Trim(),
            ContactPhone = phone,
            MaxDebtDays = req.MaxDebtDays,
            CreditLimit = NormalizeCreditLimit(req.CreditLimit),
            PaymentTerms = string.IsNullOrWhiteSpace(req.PaymentTerms) ? null : req.PaymentTerms.Trim(),
            Rating = SupplierGrades.D,
            ApprovalStatus = SupplierApprovalStatuses.New,
        };
        db.Suppliers.Add(s);
        await db.SaveChangesAsync(ct);

        var contactName = req.ResolvedContactName;
        var contactEmail = req.ResolvedContactEmail;
        var contactPosition = req.ResolvedContactPosition;

        if (!string.IsNullOrWhiteSpace(contactName) || !string.IsNullOrWhiteSpace(phone) || !string.IsNullOrWhiteSpace(contactEmail))
        {
            var primaryContact = new SupplierContact
            {
                SupplierId = s.Id,
                FullName = !string.IsNullOrWhiteSpace(contactName) ? contactName : (!string.IsNullOrWhiteSpace(phone) ? $"Liên hệ {phone}" : s.SupplierName),
                Position = contactPosition,
                Phone = phone,
                Email = contactEmail,
                Role = SupplierContactRoles.Primary,
                IsPrimary = true
            };
            db.SupplierContacts.Add(primaryContact);
            await db.SaveChangesAsync(ct);
        }

        return await GetById(s.Id, ct);
    }

    [HttpPut("{id:int}")]
    [RequirePermission("wms.suppliers.update")]
    public async Task<IActionResult> Update(int id, [FromBody] UpsertSupplierRequest req, CancellationToken ct)
    {
        if (TryValidateUpsert(req, requireCode: false, out var phone, out var errors) is BadRequestObjectResult invalid)
            return invalid;

        var s = await db.Suppliers.FindAsync([id], ct);
        if (s is null) return NotFound();

        var name = req.SupplierName.Trim();
        if (await SupplierNameTakenAsync(name, exceptId: id, currentName: s.SupplierName, ct))
        {
            return FieldErrors(new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
            {
                ["supplierName"] = ["Tên nhà cung cấp đã tồn tại."],
            });
        }

        if (!string.IsNullOrWhiteSpace(req.SupplierCode))
        {
            var customCode = req.SupplierCode.Trim();
            if (customCode != s.SupplierCode)
            {
                if (await db.Suppliers.IgnoreQueryFilters().AnyAsync(x => x.Id != id && x.SupplierCode == customCode, ct))
                {
                    return FieldErrors(new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
                    {
                        ["supplierCode"] = [$"Mã nhà cung cấp '{customCode}' đã tồn tại."],
                    });
                }
                s.SupplierCode = customCode;
            }
        }

        s.SupplierName = name;
        s.TaxCode = string.IsNullOrWhiteSpace(req.TaxCode) ? null : req.TaxCode.Trim();
        s.SupplierType = string.IsNullOrWhiteSpace(req.SupplierType) ? null : req.SupplierType.Trim();
        s.Scope = string.IsNullOrWhiteSpace(req.Scope) ? null : req.Scope.Trim();
        s.Website = string.IsNullOrWhiteSpace(req.Website) ? null : req.Website.Trim();
        s.ContactPhone = phone;
        s.MaxDebtDays = req.MaxDebtDays;
        s.CreditLimit = NormalizeCreditLimit(req.CreditLimit);
        s.PaymentTerms = string.IsNullOrWhiteSpace(req.PaymentTerms) ? null : req.PaymentTerms.Trim();
        // Rating + ApprovalStatus are system-owned — never overwrite from client.
        s.UpdatedAt = DateTime.UtcNow;

        var contactName = req.ResolvedContactName;
        var contactEmail = req.ResolvedContactEmail;
        var contactPosition = req.ResolvedContactPosition;

        var existingPrimary = await db.SupplierContacts
            .FirstOrDefaultAsync(c => c.SupplierId == id && c.Role == SupplierContactRoles.Primary, ct);

        if (existingPrimary != null)
        {
            if (!string.IsNullOrWhiteSpace(contactName))
                existingPrimary.FullName = contactName;
            if (contactPosition != null)
                existingPrimary.Position = contactPosition;
            if (phone != null)
                existingPrimary.Phone = phone;
            if (contactEmail != null)
                existingPrimary.Email = contactEmail;
            existingPrimary.UpdatedAt = DateTime.UtcNow;
        }
        else if (!string.IsNullOrWhiteSpace(contactName) || !string.IsNullOrWhiteSpace(phone) || !string.IsNullOrWhiteSpace(contactEmail))
        {
            var primaryContact = new SupplierContact
            {
                SupplierId = s.Id,
                FullName = !string.IsNullOrWhiteSpace(contactName) ? contactName : (!string.IsNullOrWhiteSpace(phone) ? $"Liên hệ {phone}" : s.SupplierName),
                Position = contactPosition,
                Phone = phone,
                Email = contactEmail,
                Role = SupplierContactRoles.Primary,
                IsPrimary = true
            };
            db.SupplierContacts.Add(primaryContact);
        }

        await db.SaveChangesAsync(ct);
        return NoContent();
    }

    [HttpPost("{id:int}/approve")]
    [RequirePermission("wms.suppliers.approve")]
    public async Task<ActionResult<SupplierDto>> Approve(int id, CancellationToken ct)
    {
        var s = await db.Suppliers.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (s is null) return NotFound();
        if (s.IsBlacklisted)
            return Conflict("NCC đang blacklist, không thể duyệt để đặt hàng.");

        s.ApprovalStatus = SupplierApprovalStatuses.Approved;
        s.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        return await GetById(id, ct);
    }

    [HttpPost("{id:int}/reject")]
    [RequirePermission("wms.suppliers.approve")]
    public async Task<ActionResult<SupplierDto>> Reject(int id, CancellationToken ct)
    {
        var s = await db.Suppliers.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (s is null) return NotFound();

        s.ApprovalStatus = SupplierApprovalStatuses.Rejected;
        s.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        return await GetById(id, ct);
    }

    [HttpPost("{id:int}/blacklist")]
    [RequirePermission("wms.suppliers.update")]
    public async Task<ActionResult<SupplierDto>> AddToBlacklist(
        int id, [FromBody] BlacklistSupplierRequest request, CancellationToken ct)
    {
        var s = await db.Suppliers.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (s is null) return NotFound();

        var reason = (request.Reason ?? string.Empty).Trim();
        if (reason.Length == 0)
            return BadRequest("Lý do đưa vào blacklist là bắt buộc.");
        if (reason.Length > 1000)
            return BadRequest("Lý do blacklist tối đa 1000 ký tự.");

        s.IsBlacklisted = true;
        s.BlacklistedAt = DateTime.UtcNow;
        s.BlacklistedById = currentUser.IsAuthenticated ? currentUser.Id : null;
        s.BlacklistedByName = currentUser.IsAuthenticated ? currentUser.Name : null;
        s.BlacklistReason = reason;
        s.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);

        return await GetById(id, ct);
    }

    [HttpDelete("{id:int}/blacklist")]
    [RequirePermission("wms.suppliers.update")]
    public async Task<ActionResult<SupplierDto>> RemoveFromBlacklist(int id, CancellationToken ct)
    {
        var s = await db.Suppliers.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (s is null) return NotFound();

        s.IsBlacklisted = false;
        s.BlacklistedAt = null;
        s.BlacklistedById = null;
        s.BlacklistedByName = null;
        s.BlacklistReason = null;
        s.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);

        return await GetById(id, ct);
    }

    [HttpDelete("{id:int}")]
    [RequirePermission("wms.suppliers.delete")]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        var s = await db.Suppliers.FindAsync([id], ct);
        if (s is null) return NotFound();
        s.DeletedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        return NoContent();
    }

    [HttpGet("{id:int}/attachments")]
    [RequirePermission("wms.suppliers.view:all")]
    public async Task<ActionResult<IReadOnlyList<SupplierAttachmentDto>>> ListAttachments(
        int id, CancellationToken ct)
    {
        var supplier = await db.Suppliers.AsNoTracking().FirstOrDefaultAsync(s => s.Id == id, ct);
        if (supplier is null) return NotFound(new { message = $"Không tìm thấy nhà cung cấp #{id}." });

        var list = await db.SupplierAttachments.AsNoTracking()
            .Where(a => a.SupplierId == id)
            .OrderByDescending(a => a.UploadedAt)
            .Select(a => new SupplierAttachmentDto(
                a.Id,
                a.SupplierId,
                a.DocType,
                SupplierAttachmentDocTypes.ToClientLabel(a.DocType),
                a.FileName,
                a.FileSizeBytes,
                a.ContentType,
                a.UploadedAt,
                a.UploadedByName,
                $"/api/warehouse/suppliers/{id}/attachments/{a.Id}/download"))
            .ToListAsync(ct);

        return Ok(list);
    }

    [HttpPost("{id:int}/attachments")]
    [RequirePermission("wms.suppliers.update")]
    [RequestSizeLimit(20 * 1024 * 1024)]
    public async Task<ActionResult<SupplierAttachmentDto>> UploadAttachment(
        int id,
        IFormFile file,
        [FromForm] string docType = "other",
        CancellationToken ct = default)
    {
        if (file is null || file.Length == 0)
            return BadRequest(new { message = "File tải lên trống." });

        var supplier = await db.Suppliers.FirstOrDefaultAsync(s => s.Id == id, ct);
        if (supplier is null)
            return NotFound(new { message = $"Không tìm thấy nhà cung cấp #{id}." });

        if (fileStorage is null)
            return StatusCode(500, new { message = "Dịch vụ lưu trữ tệp chưa được cấu hình." });

        var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
        string[] allowed =
        [
            ".jpg", ".jpeg", ".png", ".webp", ".gif", ".bmp",
            ".pdf",
            ".doc", ".docx", ".rtf",
            ".xls", ".xlsx", ".xlsm", ".xlsb", ".csv",
            ".ppt", ".pptx",
            ".txt", ".zip", ".rar"
        ];
        if (!allowed.Contains(ext))
        {
            return BadRequest(new { message = $"Định dạng tệp không được hỗ trợ: {ext}. Vui lòng chọn tệp PDF, Word, Excel hoặc hình ảnh." });
        }

        var normalizedDocType = SupplierAttachmentDocTypes.Allowed.Contains(docType.Trim())
            ? docType.Trim().ToLowerInvariant()
            : SupplierAttachmentDocTypes.Other;

        var relativePath = $"purchasing/suppliers/{id}/{normalizedDocType}/{Guid.NewGuid():N}{ext}";
        var contentType = !string.IsNullOrWhiteSpace(file.ContentType) ? file.ContentType : "application/octet-stream";

        await using var stream = file.OpenReadStream();
        var stored = await fileStorage.SaveAsync(stream, relativePath, contentType, ct);

        var attachment = new SupplierAttachment
        {
            Id = Guid.NewGuid(),
            SupplierId = id,
            DocType = normalizedDocType,
            FileName = Path.GetFileName(file.FileName),
            StorageKey = stored.StorageKey,
            FileSizeBytes = file.Length,
            ContentType = contentType,
            UploadedAt = DateTime.UtcNow,
            UploadedByName = currentUser.IsAuthenticated ? currentUser.Name : null,
        };

        db.SupplierAttachments.Add(attachment);
        supplier.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);

        var dto = new SupplierAttachmentDto(
            attachment.Id,
            attachment.SupplierId,
            attachment.DocType,
            SupplierAttachmentDocTypes.ToClientLabel(attachment.DocType),
            attachment.FileName,
            attachment.FileSizeBytes,
            attachment.ContentType,
            attachment.UploadedAt,
            attachment.UploadedByName,
            $"/api/warehouse/suppliers/{id}/attachments/{attachment.Id}/download");

        return Ok(dto);
    }

    [HttpGet("{id:int}/attachments/{attachmentId:guid}/download")]
    [RequirePermission("wms.suppliers.view:all")]
    public async Task<IActionResult> DownloadAttachment(int id, Guid attachmentId, CancellationToken ct)
    {
        var att = await db.SupplierAttachments.AsNoTracking()
            .FirstOrDefaultAsync(a => a.SupplierId == id && a.Id == attachmentId, ct);
        if (att is null)
            return NotFound(new { message = "Không tìm thấy hồ sơ đính kèm." });

        if (fileStorage is null)
            return StatusCode(500, new { message = "Dịch vụ lưu trữ tệp chưa được cấu hình." });

        var stored = await fileStorage.OpenReadAsync(att.StorageKey, ct);
        if (stored is null)
            return NotFound(new { message = "Tệp đính kèm không còn tồn tại trên máy chủ lưu trữ. Vui lòng tải lại hồ sơ." });

        return File(stored.Stream, stored.ResolveContentType(att.FileName), att.FileName);
    }

    [HttpDelete("{id:int}/attachments/{attachmentId:guid}")]
    [RequirePermission("wms.suppliers.update")]
    public async Task<IActionResult> DeleteAttachment(int id, Guid attachmentId, CancellationToken ct)
    {
        var att = await db.SupplierAttachments
            .FirstOrDefaultAsync(a => a.SupplierId == id && a.Id == attachmentId, ct);
        if (att is null)
            return NotFound(new { message = "Không tìm thấy hồ sơ đính kèm." });

        var supplier = await db.Suppliers.FirstOrDefaultAsync(s => s.Id == id, ct);
        if (supplier is not null)
            supplier.UpdatedAt = DateTime.UtcNow;

        att.DeletedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        return NoContent();
    }

    private static IActionResult? TryValidateUpsert(
        UpsertSupplierRequest req,
        bool requireCode,
        out string? phone,
        out Dictionary<string, string[]> errors)
    {
        errors = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase);
        phone = null;

        var code = req.SupplierCode?.Trim() ?? string.Empty;
        if (requireCode)
        {
            if (code.Length == 0)
                errors["supplierCode"] = ["Mã NCC là bắt buộc."];
            else if (code.Length > SupplierFieldLimits.CodeMax)
                errors["supplierCode"] = [$"Mã NCC tối đa {SupplierFieldLimits.CodeMax} ký tự."];
        }
        else if (code.Length > SupplierFieldLimits.CodeMax)
        {
            errors["supplierCode"] = [$"Mã NCC tối đa {SupplierFieldLimits.CodeMax} ký tự."];
        }

        var name = req.SupplierName?.Trim() ?? string.Empty;
        if (name.Length == 0)
            errors["supplierName"] = ["Tên nhà cung cấp là bắt buộc."];
        else if (name.Length > SupplierFieldLimits.NameMax)
            errors["supplierName"] = [$"Tên nhà cung cấp tối đa {SupplierFieldLimits.NameMax} ký tự."];

        if (req.MaxDebtDays < SupplierFieldLimits.MaxDebtDaysMin
            || req.MaxDebtDays > SupplierFieldLimits.MaxDebtDaysMax)
            errors["maxDebtDays"] = ["Hạn công nợ từ 0 đến 3650 ngày."];

        if (req.CreditLimit is < SupplierFieldLimits.CreditLimitMin
            or > SupplierFieldLimits.CreditLimitMax)
            errors["creditLimit"] = ["Hạn mức công nợ phải từ 0 trở lên."];

        var terms = req.PaymentTerms?.Trim() ?? string.Empty;
        if (terms.Length > SupplierFieldLimits.PaymentTermsMax)
            errors["paymentTerms"] = [$"Điều khoản thanh toán tối đa {SupplierFieldLimits.PaymentTermsMax} ký tự."];

        var tax = req.TaxCode?.Trim() ?? string.Empty;
        if (tax.Length > 50)
            errors["taxCode"] = ["Mã số thuế tối đa 50 ký tự."];

        if ((req.SupplierType?.Trim().Length ?? 0) > 50)
            errors["supplierType"] = ["Loại hình nhà cung cấp tối đa 50 ký tự."];

        if ((req.Scope?.Trim().Length ?? 0) > 50)
            errors["scope"] = ["Phạm vi hoạt động tối đa 50 ký tự."];

        if ((req.Website?.Trim().Length ?? 0) > 255)
            errors["website"] = ["Website tối đa 255 ký tự."];

        try
        {
            phone = VietnamPhoneRules.NormalizeOptional(req.ContactPhone);
        }
        catch (ArgumentException ex)
        {
            errors["contactPhone"] = [ex.Message];
        }

        return errors.Count > 0 ? FieldErrors(errors) : null;
    }

    private async Task<bool> SupplierNameTakenAsync(
        string name, int? exceptId, string? currentName, CancellationToken ct)
    {
        if (exceptId is not null && Supplier.SameFoldedName(name, currentName))
            return false;

        var names = await db.Suppliers.AsNoTracking()
            .Where(s => exceptId == null || s.Id != exceptId)
            .Select(s => s.SupplierName)
            .ToListAsync(ct);
        return names.Exists(existing => Supplier.SameFoldedName(existing, name));
    }

    private async Task<string> NextSupplierCodeAsync(CancellationToken ct)
    {
        var codes = await db.Suppliers.IgnoreQueryFilters()
            .Select(s => s.SupplierCode)
            .ToListAsync(ct);
        var next = 1;
        foreach (var code in codes)
        {
            if (Supplier.TryParseSequence(code, out var n))
                next = Math.Max(next, n + 1);
        }

        for (var i = 0; i < 8; i++)
        {
            var candidate = Supplier.GenerateCode(next + i);
            if (!await db.Suppliers.IgnoreQueryFilters().AnyAsync(s => s.SupplierCode == candidate, ct))
                return candidate;
        }

        return Supplier.GenerateCode(next);
    }

    private static decimal? NormalizeCreditLimit(decimal? value) =>
        value is > 0 ? Math.Round(value.Value, 2) : null;

    private static BadRequestObjectResult FieldErrors(IReadOnlyDictionary<string, string[]> errors) =>
        new(new { errors });
}
