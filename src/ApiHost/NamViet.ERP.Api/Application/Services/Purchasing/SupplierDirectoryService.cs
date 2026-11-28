using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using NamViet.ERP.Api.Application.Dtos.Purchasing;
using NamViet.ERP.Api.Services.Storage;
using NamViet.ERP.Modules.Warehouse.Application.DTOs;
using NamViet.ERP.Modules.Warehouse.Domain.Entities;
using NamViet.ERP.Modules.Warehouse.Domain.SupplierEvaluation;
using NamViet.ERP.Modules.Warehouse.Infrastructure.Persistence;
using NamViet.ERP.SharedKernel.Common;
using NamViet.ERP.SharedKernel.Interfaces;

namespace NamViet.ERP.Api.Application.Services.Purchasing;

public enum SupplierServiceErrorKind
{
    None,
    ValidationFields,
    BadRequest,
    NotFound,
    Conflict,
    StorageUnavailable,
}

public sealed record SupplierAttachmentDownload(Stream Stream, string ContentType, string FileName);

public sealed record SupplierServiceOutcome<T>(
    T? Data,
    SupplierServiceErrorKind ErrorKind = SupplierServiceErrorKind.None,
    string? Message = null,
    IReadOnlyDictionary<string, string[]>? FieldErrors = null);

public interface ISupplierDirectoryService
{
    Task<PagedResult<SupplierDto>> GetAllAsync(
        int page = 1,
        int pageSize = 20,
        string? search = null,
        string? rating = null,
        int? groupId = null,
        bool? isBlacklisted = null,
        string? approvalStatus = null,
        CancellationToken ct = default);

    Task<string> NextSupplierCodeAsync(CancellationToken ct = default);

    Task<IReadOnlyList<SupplierBlacklistDto>> ListBlacklistAsync(CancellationToken ct = default);

    Task<SupplierDto?> GetByIdAsync(int id, CancellationToken ct = default);

    Task<SupplierServiceOutcome<SupplierDto>> CreateAsync(UpsertSupplierRequest req, CancellationToken ct = default);

    Task<SupplierServiceOutcome<bool>> UpdateAsync(int id, UpsertSupplierRequest req, CancellationToken ct = default);

    Task<SupplierServiceOutcome<SupplierDto>> ApproveAsync(int id, CancellationToken ct = default);

    Task<SupplierServiceOutcome<SupplierDto>> RejectAsync(int id, CancellationToken ct = default);

    Task<SupplierServiceOutcome<SupplierDto>> AddToBlacklistAsync(
        int id, BlacklistSupplierRequest request, CancellationToken ct = default);

    Task<SupplierServiceOutcome<SupplierDto>> RemoveFromBlacklistAsync(int id, CancellationToken ct = default);

    Task<bool> DeleteAsync(int id, CancellationToken ct = default);

    Task<SupplierServiceOutcome<IReadOnlyList<SupplierAttachmentDto>>> ListAttachmentsAsync(
        int id, CancellationToken ct = default);

    Task<SupplierServiceOutcome<SupplierAttachmentDto>> UploadAttachmentAsync(
        int id, IFormFile? file, string docType, CancellationToken ct = default);

    Task<SupplierServiceOutcome<SupplierAttachmentDownload>> DownloadAttachmentAsync(
        int id, Guid attachmentId, CancellationToken ct = default);

    Task<SupplierServiceOutcome<bool>> DeleteAttachmentAsync(
        int id, Guid attachmentId, CancellationToken ct = default);
}

public sealed class SupplierDirectoryService(
    WarehouseDbContext db,
    ICurrentUser currentUser,
    IFileStorage? fileStorage = null) : ISupplierDirectoryService
{
    private static readonly HashSet<string> AllowedAttachmentExtensions =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ".jpg", ".jpeg", ".png", ".webp", ".gif", ".bmp",
            ".pdf",
            ".doc", ".docx", ".rtf",
            ".xls", ".xlsx", ".xlsm", ".xlsb", ".csv",
            ".ppt", ".pptx",
            ".txt", ".zip", ".rar",
        };

    public async Task<PagedResult<SupplierDto>> GetAllAsync(
        int page = 1,
        int pageSize = 20,
        string? search = null,
        string? rating = null,
        int? groupId = null,
        bool? isBlacklisted = null,
        string? approvalStatus = null,
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
            var childIds = await db.SupplierGroups.AsNoTracking()
                .Where(g => g.ParentGroupId == gid)
                .Select(g => g.Id)
                .ToListAsync(ct);
            var targetIds = childIds.Append(gid).Distinct().ToList();
            query = query.Where(s => s.GroupMemberships.Any(m => targetIds.Contains(m.SupplierGroupId)));
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
            .Where(m => supplierIds.Contains(m.SupplierId) && m.Group != null)
            .Select(m => new
            {
                m.SupplierId,
                GroupName = m.Group!.Name,
                ParentGroupName = m.Group.Parent != null ? m.Group.Parent.Name : null,
            })
            .ToListAsync(ct);
        var industryGroupMap = industryGroups
            .GroupBy(x => x.SupplierId)
            .ToDictionary(
                g => g.Key,
                g => (IReadOnlyList<string>)g
                    .Select(x => string.IsNullOrEmpty(x.ParentGroupName)
                        ? x.GroupName
                        : $"{x.ParentGroupName} › {x.GroupName}")
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

        return PagedResult<SupplierDto>.Create(items, total, page, pageSize);
    }

    public async Task<string> NextSupplierCodeAsync(CancellationToken ct = default)
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

    public async Task<IReadOnlyList<SupplierBlacklistDto>> ListBlacklistAsync(CancellationToken ct = default) =>
        await db.Suppliers.AsNoTracking()
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

    public async Task<SupplierDto?> GetByIdAsync(int id, CancellationToken ct = default)
    {
        var s = await db.Suppliers.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct);
        if (s is null)
            return null;

        var groupRowsOfSupplier = await db.SupplierGroupMembers.AsNoTracking()
            .Where(m => m.SupplierId == id && m.Group != null)
            .Select(m => new
            {
                GroupName = m.Group!.Name,
                ParentGroupName = m.Group.Parent != null ? m.Group.Parent.Name : null,
            })
            .ToListAsync(ct);
        var groupsOfSupplier = groupRowsOfSupplier
            .Select(x => string.IsNullOrEmpty(x.ParentGroupName)
                ? x.GroupName
                : $"{x.ParentGroupName} › {x.GroupName}")
            .Distinct()
            .OrderBy(n => n)
            .ToList();

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
                $"/api/warehouse/suppliers/{id}/attachments/{a.Id}/download"))
            .ToListAsync(ct);

        return new SupplierDto(
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
            contacts.Count);
    }

    public async Task<SupplierServiceOutcome<SupplierDto>> CreateAsync(
        UpsertSupplierRequest req,
        CancellationToken ct = default)
    {
        if (!TryValidateUpsert(req, requireCode: false, out var phone, out var errors))
            return new SupplierServiceOutcome<SupplierDto>(null, SupplierServiceErrorKind.ValidationFields, FieldErrors: errors);

        var name = req.SupplierName.Trim();
        if (await SupplierNameTakenAsync(name, exceptId: null, currentName: null, ct))
        {
            errors["supplierName"] = ["Tên nhà cung cấp đã tồn tại."];
            return new SupplierServiceOutcome<SupplierDto>(null, SupplierServiceErrorKind.ValidationFields, FieldErrors: errors);
        }

        string supplierCode;
        if (!string.IsNullOrWhiteSpace(req.SupplierCode))
        {
            var customCode = req.SupplierCode.Trim();
            if (await db.Suppliers.IgnoreQueryFilters().AnyAsync(s => s.SupplierCode == customCode, ct))
            {
                errors["supplierCode"] = [$"Mã nhà cung cấp '{customCode}' đã tồn tại."];
                return new SupplierServiceOutcome<SupplierDto>(null, SupplierServiceErrorKind.ValidationFields, FieldErrors: errors);
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

        var dto = await GetByIdAsync(s.Id, ct);
        return new SupplierServiceOutcome<SupplierDto>(dto);
    }

    public async Task<SupplierServiceOutcome<bool>> UpdateAsync(
        int id,
        UpsertSupplierRequest req,
        CancellationToken ct = default)
    {
        if (!TryValidateUpsert(req, requireCode: false, out var phone, out var errors))
            return new SupplierServiceOutcome<bool>(false, SupplierServiceErrorKind.ValidationFields, FieldErrors: errors);

        var s = await db.Suppliers.FindAsync([id], ct);
        if (s is null)
            return new SupplierServiceOutcome<bool>(false, SupplierServiceErrorKind.NotFound);

        var name = req.SupplierName.Trim();
        if (await SupplierNameTakenAsync(name, exceptId: id, currentName: s.SupplierName, ct))
        {
            return new SupplierServiceOutcome<bool>(
                false,
                SupplierServiceErrorKind.ValidationFields,
                FieldErrors: new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
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
                    return new SupplierServiceOutcome<bool>(
                        false,
                        SupplierServiceErrorKind.ValidationFields,
                        FieldErrors: new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
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
        return new SupplierServiceOutcome<bool>(true);
    }

    public async Task<SupplierServiceOutcome<SupplierDto>> ApproveAsync(int id, CancellationToken ct = default)
    {
        var s = await db.Suppliers.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (s is null)
            return new SupplierServiceOutcome<SupplierDto>(null, SupplierServiceErrorKind.NotFound);
        if (s.IsBlacklisted)
            return new SupplierServiceOutcome<SupplierDto>(null, SupplierServiceErrorKind.Conflict, "NCC đang blacklist, không thể duyệt để đặt hàng.");

        s.ApprovalStatus = SupplierApprovalStatuses.Approved;
        s.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);

        return new SupplierServiceOutcome<SupplierDto>(await GetByIdAsync(id, ct));
    }

    public async Task<SupplierServiceOutcome<SupplierDto>> RejectAsync(int id, CancellationToken ct = default)
    {
        var s = await db.Suppliers.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (s is null)
            return new SupplierServiceOutcome<SupplierDto>(null, SupplierServiceErrorKind.NotFound);

        s.ApprovalStatus = SupplierApprovalStatuses.Rejected;
        s.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);

        return new SupplierServiceOutcome<SupplierDto>(await GetByIdAsync(id, ct));
    }

    public async Task<SupplierServiceOutcome<SupplierDto>> AddToBlacklistAsync(
        int id,
        BlacklistSupplierRequest request,
        CancellationToken ct = default)
    {
        var s = await db.Suppliers.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (s is null)
            return new SupplierServiceOutcome<SupplierDto>(null, SupplierServiceErrorKind.NotFound);

        var reason = (request.Reason ?? string.Empty).Trim();
        if (reason.Length == 0)
            return new SupplierServiceOutcome<SupplierDto>(null, SupplierServiceErrorKind.BadRequest, "Lý do đưa vào blacklist là bắt buộc.");
        if (reason.Length > 1000)
            return new SupplierServiceOutcome<SupplierDto>(null, SupplierServiceErrorKind.BadRequest, "Lý do blacklist tối đa 1000 ký tự.");

        s.IsBlacklisted = true;
        s.BlacklistedAt = DateTime.UtcNow;
        s.BlacklistedById = currentUser.IsAuthenticated ? currentUser.Id : null;
        s.BlacklistedByName = currentUser.IsAuthenticated ? currentUser.Name : null;
        s.BlacklistReason = reason;
        s.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);

        return new SupplierServiceOutcome<SupplierDto>(await GetByIdAsync(id, ct));
    }

    public async Task<SupplierServiceOutcome<SupplierDto>> RemoveFromBlacklistAsync(int id, CancellationToken ct = default)
    {
        var s = await db.Suppliers.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (s is null)
            return new SupplierServiceOutcome<SupplierDto>(null, SupplierServiceErrorKind.NotFound);

        s.IsBlacklisted = false;
        s.BlacklistedAt = null;
        s.BlacklistedById = null;
        s.BlacklistedByName = null;
        s.BlacklistReason = null;
        s.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);

        return new SupplierServiceOutcome<SupplierDto>(await GetByIdAsync(id, ct));
    }

    public async Task<bool> DeleteAsync(int id, CancellationToken ct = default)
    {
        var s = await db.Suppliers.FindAsync([id], ct);
        if (s is null)
            return false;

        s.DeletedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<SupplierServiceOutcome<IReadOnlyList<SupplierAttachmentDto>>> ListAttachmentsAsync(
        int id,
        CancellationToken ct = default)
    {
        var supplier = await db.Suppliers.AsNoTracking().FirstOrDefaultAsync(s => s.Id == id, ct);
        if (supplier is null)
            return new SupplierServiceOutcome<IReadOnlyList<SupplierAttachmentDto>>(null, SupplierServiceErrorKind.NotFound, $"Không tìm thấy nhà cung cấp #{id}.");

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

        return new SupplierServiceOutcome<IReadOnlyList<SupplierAttachmentDto>>(list);
    }

    public async Task<SupplierServiceOutcome<SupplierAttachmentDto>> UploadAttachmentAsync(
        int id,
        IFormFile? file,
        string docType,
        CancellationToken ct = default)
    {
        if (file is null || file.Length == 0)
            return new SupplierServiceOutcome<SupplierAttachmentDto>(null, SupplierServiceErrorKind.BadRequest, "File tải lên trống.");

        var supplier = await db.Suppliers.FirstOrDefaultAsync(s => s.Id == id, ct);
        if (supplier is null)
            return new SupplierServiceOutcome<SupplierAttachmentDto>(null, SupplierServiceErrorKind.NotFound, $"Không tìm thấy nhà cung cấp #{id}.");

        if (fileStorage is null)
            return new SupplierServiceOutcome<SupplierAttachmentDto>(null, SupplierServiceErrorKind.StorageUnavailable, "Dịch vụ lưu trữ tệp chưa được cấu hình.");

        var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (!AllowedAttachmentExtensions.Contains(ext))
        {
            return new SupplierServiceOutcome<SupplierAttachmentDto>(
                null,
                SupplierServiceErrorKind.BadRequest,
                $"Định dạng tệp không được hỗ trợ: {ext}. Vui lòng chọn tệp PDF, Word, Excel hoặc hình ảnh.");
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

        return new SupplierServiceOutcome<SupplierAttachmentDto>(dto);
    }

    public async Task<SupplierServiceOutcome<SupplierAttachmentDownload>> DownloadAttachmentAsync(
        int id,
        Guid attachmentId,
        CancellationToken ct = default)
    {
        var att = await db.SupplierAttachments.AsNoTracking()
            .FirstOrDefaultAsync(a => a.SupplierId == id && a.Id == attachmentId, ct);
        if (att is null)
            return new SupplierServiceOutcome<SupplierAttachmentDownload>(null, SupplierServiceErrorKind.NotFound, "Không tìm thấy hồ sơ đính kèm.");

        if (fileStorage is null)
            return new SupplierServiceOutcome<SupplierAttachmentDownload>(null, SupplierServiceErrorKind.StorageUnavailable, "Dịch vụ lưu trữ tệp chưa được cấu hình.");

        var stored = await fileStorage.OpenReadAsync(att.StorageKey, ct);
        if (stored is null)
            return new SupplierServiceOutcome<SupplierAttachmentDownload>(null, SupplierServiceErrorKind.NotFound, "Tệp đính kèm không còn tồn tại trên máy chủ lưu trữ. Vui lòng tải lại hồ sơ.");

        return new SupplierServiceOutcome<SupplierAttachmentDownload>(
            new SupplierAttachmentDownload(stored.Stream, stored.ResolveContentType(att.FileName), att.FileName));
    }

    public async Task<SupplierServiceOutcome<bool>> DeleteAttachmentAsync(
        int id,
        Guid attachmentId,
        CancellationToken ct = default)
    {
        var att = await db.SupplierAttachments
            .FirstOrDefaultAsync(a => a.SupplierId == id && a.Id == attachmentId, ct);
        if (att is null)
            return new SupplierServiceOutcome<bool>(false, SupplierServiceErrorKind.NotFound, "Không tìm thấy hồ sơ đính kèm.");

        var supplier = await db.Suppliers.FirstOrDefaultAsync(s => s.Id == id, ct);
        if (supplier is not null)
            supplier.UpdatedAt = DateTime.UtcNow;

        att.DeletedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        return new SupplierServiceOutcome<bool>(true);
    }

    private static bool TryValidateUpsert(
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

        return errors.Count == 0;
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

    private static decimal? NormalizeCreditLimit(decimal? value) =>
        value is > 0 ? Math.Round(value.Value, 2) : null;
}
