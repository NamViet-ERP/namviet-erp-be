using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NamViet.ERP.Api.Application.Dtos.Purchasing;
using NamViet.ERP.Api.Application.Services.Purchasing;
using NamViet.ERP.Api.Infrastructure.Authorization;
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
    ICurrentUser currentUser) : ControllerBase
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
        var query = db.Suppliers.AsQueryable();
        if (!string.IsNullOrWhiteSpace(search))
            query = query.Where(s => s.SupplierName.Contains(search) || s.SupplierCode.Contains(search));
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
            latestEvaluationMap.GetValueOrDefault(s.Id))).ToList();
        return Ok(PagedResult<SupplierDto>.Create(items, total, page, pageSize));
    }

    [HttpGet("next-code")]
    [RequirePermission("wms.suppliers.create")]
    public async Task<ActionResult<SupplierNextCodeDto>> NextCode(CancellationToken ct) =>
        Ok(new SupplierNextCodeDto(await NextSupplierCodeAsync(ct)));

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
            latestEvaluation));
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

        var s = new Supplier
        {
            SupplierCode = await NextSupplierCodeAsync(ct),
            SupplierName = name,
            ContactPhone = phone,
            MaxDebtDays = req.MaxDebtDays,
            CreditLimit = NormalizeCreditLimit(req.CreditLimit),
            PaymentTerms = string.IsNullOrWhiteSpace(req.PaymentTerms) ? null : req.PaymentTerms.Trim(),
            Rating = SupplierGrades.D,
            ApprovalStatus = SupplierApprovalStatuses.New,
        };
        db.Suppliers.Add(s);
        await db.SaveChangesAsync(ct);
        return await GetById(s.Id, ct);
    }

    [HttpPut("{id:int}")]
    [RequirePermission("wms.suppliers.update")]
    public async Task<IActionResult> Update(int id, [FromBody] UpsertSupplierRequest req, CancellationToken ct)
    {
        if (TryValidateUpsert(req, requireCode: false, out var phone, out _) is { } invalid)
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

        s.SupplierName = name;
        s.ContactPhone = phone;
        s.MaxDebtDays = req.MaxDebtDays;
        s.CreditLimit = NormalizeCreditLimit(req.CreditLimit);
        s.PaymentTerms = string.IsNullOrWhiteSpace(req.PaymentTerms) ? null : req.PaymentTerms.Trim();
        // Rating + ApprovalStatus are system-owned — never overwrite from client.
        s.UpdatedAt = DateTime.UtcNow;
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
