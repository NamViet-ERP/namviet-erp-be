using Microsoft.EntityFrameworkCore;
using NamViet.ERP.Api.Application.Dtos.Purchasing;
using NamViet.ERP.Modules.Warehouse.Domain.Entities;
using NamViet.ERP.Modules.Warehouse.Domain.Enums;
using NamViet.ERP.Modules.Warehouse.Infrastructure.Persistence;
using NamViet.ERP.SharedKernel.Interfaces;
using NamViet.ERP.SharedKernel.Time;

namespace NamViet.ERP.Api.Application.Services.Purchasing;

public interface IPurchaseWarrantyService
{
    Task<IReadOnlyList<PurchaseWarrantyDto>> ListAsync(
        string? projectCode,
        string? materialCode,
        int? supplierId,
        string? status,
        string? search,
        CancellationToken ct = default);

    Task<PurchaseWarrantyDetailDto?> GetByIdAsync(Guid id, CancellationToken ct = default);

    Task<PurchaseWarrantyDetailDto?> UpdateAsync(
        Guid id,
        UpdatePurchaseWarrantyRequest request,
        CancellationToken ct = default);

    Task<IReadOnlyList<PurchaseWarrantyClaimListDto>> ListClaimsAsync(
        string? kind,
        string? status,
        string? search,
        CancellationToken ct = default);

    Task<PurchaseWarrantyClaimDto> CreateClaimAsync(
        Guid warrantyId,
        CreatePurchaseWarrantyClaimRequest request,
        CancellationToken ct = default);

    Task<PurchaseWarrantyClaimDto?> SubmitClaimAsync(Guid claimId, CancellationToken ct = default);

    Task<PurchaseWarrantyClaimDto?> CloseClaimAsync(
        Guid claimId,
        ClosePurchaseWarrantyClaimRequest request,
        CancellationToken ct = default);

    Task<(string FileName, string StorageKey)?> GetEvidenceFileAsync(
        Guid warrantyId,
        Guid attachmentId,
        CancellationToken ct = default);

    /// <summary>Upsert BH sau khi duyệt nhập kho gắn PO.</summary>
    Task UpsertFromApprovedImportAsync(WarehouseTransaction import, CancellationToken ct = default);

    /// <summary>Đồng bộ BH từ dòng PO đã nhận hàng + đã set số tháng BH.</summary>
    Task<SyncPurchaseWarrantiesResult> SyncFromReceivedPoLinesAsync(CancellationToken ct = default);
}

public sealed class PurchaseWarrantyService(
    WarehouseDbContext db,
    ICurrentUser currentUser) : IPurchaseWarrantyService
{
    public async Task<IReadOnlyList<PurchaseWarrantyDto>> ListAsync(
        string? projectCode,
        string? materialCode,
        int? supplierId,
        string? status,
        string? search,
        CancellationToken ct = default)
    {
        var today = VietnamClock.Today;
        var query = db.PurchaseWarranties.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(projectCode))
            query = query.Where(w => w.ProjectCode == projectCode.Trim());
        if (!string.IsNullOrWhiteSpace(materialCode))
            query = query.Where(w => w.MaterialCode == materialCode.Trim());
        if (supplierId is int sid)
            query = query.Where(w => w.SupplierId == sid);
        if (!string.IsNullOrWhiteSpace(status))
            query = query.Where(w => w.Status == status.Trim().ToLowerInvariant());
        if (!string.IsNullOrWhiteSpace(search))
        {
            var q = search.Trim().ToLowerInvariant();
            query = query.Where(w =>
                w.Code.ToLower().Contains(q)
                || w.PoCode.ToLower().Contains(q)
                || w.MaterialName.ToLower().Contains(q)
                || (w.MaterialCode != null && w.MaterialCode.ToLower().Contains(q))
                || (w.SupplierName != null && w.SupplierName.ToLower().Contains(q))
                || (w.ProjectCode != null && w.ProjectCode.ToLower().Contains(q)));
        }

        var rows = await query
            .OrderByDescending(w => w.WarrantyEndDate)
            .ThenBy(w => w.Code)
            .Select(w => new
            {
                w.Id,
                w.Code,
                w.PoCode,
                w.PoItemId,
                w.ProjectCode,
                w.PlanCode,
                w.SupplierId,
                w.SupplierName,
                w.MaterialCode,
                w.MaterialName,
                w.Unit,
                w.Quantity,
                w.WarrantyMonths,
                w.AnchorType,
                w.DeliveryDate,
                w.AcceptanceDate,
                w.WarrantyEndDate,
                w.Status,
                w.ImportTicketCode,
                ClaimCount = w.Claims.Count(),
                w.CreatedAt,
            })
            .Take(500)
            .ToListAsync(ct);

        return rows.Select(w =>
        {
            var statusResolved = ResolveListStatus(w.Status, w.WarrantyEndDate, today);
            return new PurchaseWarrantyDto(
                w.Id, w.Code, w.PoCode, w.PoItemId, w.ProjectCode, w.PlanCode,
                w.SupplierId, w.SupplierName, w.MaterialCode, w.MaterialName, w.Unit,
                w.Quantity, w.WarrantyMonths, w.AnchorType, w.DeliveryDate, w.AcceptanceDate,
                w.WarrantyEndDate, statusResolved, w.ImportTicketCode, w.ClaimCount, w.CreatedAt);
        }).ToList();
    }

    public async Task<IReadOnlyList<PurchaseWarrantyClaimListDto>> ListClaimsAsync(
        string? kind,
        string? status,
        string? search,
        CancellationToken ct = default)
    {
        var today = VietnamClock.Today;
        var query = db.PurchaseWarrantyClaims.AsNoTracking().Include(c => c.Warranty).AsQueryable();

        if (!string.IsNullOrWhiteSpace(kind))
        {
            var k = kind.Trim().ToLowerInvariant();
            query = query.Where(c => c.Kind == k);
        }
        if (!string.IsNullOrWhiteSpace(status))
        {
            var st = status.Trim().ToLowerInvariant();
            query = query.Where(c => c.Status == st);
        }
        if (!string.IsNullOrWhiteSpace(search))
        {
            var q = search.Trim().ToLowerInvariant();
            query = query.Where(c =>
                c.TicketCode.ToLower().Contains(q)
                || c.Reason.ToLower().Contains(q)
                || (c.Warranty != null && (
                    c.Warranty.Code.ToLower().Contains(q)
                    || c.Warranty.PoCode.ToLower().Contains(q)
                    || c.Warranty.MaterialName.ToLower().Contains(q)
                    || (c.Warranty.MaterialCode != null && c.Warranty.MaterialCode.ToLower().Contains(q))
                    || (c.Warranty.SupplierName != null && c.Warranty.SupplierName.ToLower().Contains(q))
                    || (c.Warranty.ProjectCode != null && c.Warranty.ProjectCode.ToLower().Contains(q)))));
        }

        var rows = await query
            .OrderByDescending(c => c.CreatedAt)
            .Take(500)
            .ToListAsync(ct);

        return rows.Select(c =>
        {
            var w = c.Warranty;
            var warrantyStatus = w is null
                ? ""
                : ResolveListStatus(w.Status, w.WarrantyEndDate, today);
            return new PurchaseWarrantyClaimListDto(
                c.Id, c.TicketCode, c.WarrantyId, c.Kind, c.Status, c.Reason, c.Note,
                c.DefectType, c.Severity, c.CreatedById, c.CreatedByName,
                c.SubmittedAt, c.ClosedAt, c.CreatedAt, c.ServiceRating, c.ServiceNote,
                w?.Code ?? "", w?.PoCode ?? "", w?.ProjectCode, w?.SupplierName,
                w?.MaterialCode, w?.MaterialName ?? "", w?.WarrantyEndDate, warrantyStatus);
        }).ToList();
    }

    public async Task<PurchaseWarrantyDetailDto?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var w = await db.PurchaseWarranties
            .AsNoTracking()
            .Include(x => x.Claims)
            .FirstOrDefaultAsync(x => x.Id == id, ct);
        if (w is null) return null;

        w.Recalculate(VietnamClock.Today);
        return await ToDetailDtoAsync(w, ct);
    }

    public async Task<PurchaseWarrantyDetailDto?> UpdateAsync(
        Guid id,
        UpdatePurchaseWarrantyRequest request,
        CancellationToken ct = default)
    {
        var w = await db.PurchaseWarranties
            .Include(x => x.Claims)
            .FirstOrDefaultAsync(x => x.Id == id, ct);
        if (w is null) return null;

        if (request.WarrantyMonths is int months)
        {
            if (months < 0 || months > 120)
                throw new PurchasingConflictException("Thời hạn bảo hành phải từ 0 đến 120 tháng.");
            w.WarrantyMonths = months;
        }

        if (!string.IsNullOrWhiteSpace(request.AnchorType))
        {
            var anchor = request.AnchorType.Trim().ToLowerInvariant();
            if (!PurchaseWarrantyAnchorTypes.Allowed.Contains(anchor))
                throw new PurchasingConflictException("Mốc BH phải là delivery hoặc acceptance.");
            w.AnchorType = anchor;
        }

        if (request.AcceptanceDate is DateOnly acceptance)
            w.AcceptanceDate = acceptance;
        if (request.DeliveryDate is DateOnly delivery)
            w.DeliveryDate = delivery;

        w.Recalculate(VietnamClock.Today);
        w.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        return await ToDetailDtoAsync(w, ct);
    }

    public async Task<(string FileName, string StorageKey)?> GetEvidenceFileAsync(
        Guid warrantyId,
        Guid attachmentId,
        CancellationToken ct = default)
    {
        var w = await db.PurchaseWarranties.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == warrantyId, ct);
        if (w is null) return null;

        var tickets = await ResolveEvidenceTicketCodesAsync(w, ct);
        if (tickets.Count == 0) return null;

        var attachment = await db.WarehouseTransactionAttachments.AsNoTracking()
            .FirstOrDefaultAsync(
                a => a.Id == attachmentId && tickets.Contains(a.TicketCode),
                ct);
        if (attachment is null) return null;
        if (!IsWarrantyEvidenceKind(attachment.Kind)) return null;

        return (attachment.FileName, attachment.StorageKey);
    }

    public async Task<PurchaseWarrantyClaimDto> CreateClaimAsync(
        Guid warrantyId,
        CreatePurchaseWarrantyClaimRequest request,
        CancellationToken ct = default)
    {
        var w = await db.PurchaseWarranties.FirstOrDefaultAsync(x => x.Id == warrantyId, ct)
            ?? throw new PurchasingConflictException("Không tìm thấy hồ sơ bảo hành.");

        var reason = (request.Reason ?? string.Empty).Trim();
        if (reason.Length < 3)
            throw new PurchasingConflictException("Vui lòng nhập nguyên nhân (tối thiểu 3 ký tự).");

        w.Recalculate(VietnamClock.Today);
        if (w.Status == PurchaseWarrantyStatuses.Void)
            throw new PurchasingConflictException("Hồ sơ bảo hành đã hủy - không tạo phiếu.");

        var kind = NormalizeKind(request.Kind);
        var defectType = NormalizeOptional(request.DefectType, PurchaseWarrantyDefectTypes.Allowed, "Loại lỗi");
        var severity = NormalizeOptional(request.Severity, PurchaseWarrantySeverities.Allowed, "Mức độ");
        if (kind == PurchaseWarrantyClaimKinds.Complaint)
        {
            if (defectType is null)
                throw new PurchasingConflictException("Phiếu khiếu nại cần chọn loại lỗi.");
            if (severity is null)
                throw new PurchasingConflictException("Phiếu khiếu nại cần chọn mức độ.");
        }

        var seq = await db.PurchaseWarrantyClaims.IgnoreQueryFilters().CountAsync(ct) + 1;
        var claim = new PurchaseWarrantyClaim
        {
            TicketCode = PurchaseWarrantyClaim.GenerateTicketCode(seq, kind),
            WarrantyId = w.Id,
            Kind = kind,
            Reason = reason,
            Note = string.IsNullOrWhiteSpace(request.Note) ? null : request.Note.Trim(),
            DefectType = kind == PurchaseWarrantyClaimKinds.Complaint ? defectType : null,
            Severity = kind == PurchaseWarrantyClaimKinds.Complaint ? severity : null,
            CreatedById = currentUser.Id,
            CreatedByName = currentUser.IsAuthenticated ? (currentUser.Name ?? "-") : "-",
            Status = PurchaseWarrantyClaimStatuses.Draft,
        };

        if (request.Submit)
        {
            claim.Status = PurchaseWarrantyClaimStatuses.Submitted;
            claim.SubmittedAt = DateTime.UtcNow;
        }

        db.PurchaseWarrantyClaims.Add(claim);
        await db.SaveChangesAsync(ct);
        return ToClaimDto(claim);
    }

    public async Task<PurchaseWarrantyClaimDto?> SubmitClaimAsync(Guid claimId, CancellationToken ct = default)
    {
        var claim = await db.PurchaseWarrantyClaims.FirstOrDefaultAsync(c => c.Id == claimId, ct);
        if (claim is null) return null;
        if (!string.Equals(claim.Status, PurchaseWarrantyClaimStatuses.Draft, StringComparison.OrdinalIgnoreCase))
            throw new PurchasingConflictException("Chỉ gửi phiếu ở trạng thái nháp.");
        claim.Status = PurchaseWarrantyClaimStatuses.Submitted;
        claim.SubmittedAt = DateTime.UtcNow;
        claim.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        return ToClaimDto(claim);
    }

    public async Task<PurchaseWarrantyClaimDto?> CloseClaimAsync(
        Guid claimId,
        ClosePurchaseWarrantyClaimRequest request,
        CancellationToken ct = default)
    {
        var claim = await db.PurchaseWarrantyClaims.FirstOrDefaultAsync(c => c.Id == claimId, ct);
        if (claim is null) return null;
        if (!string.Equals(claim.Status, PurchaseWarrantyClaimStatuses.Submitted, StringComparison.OrdinalIgnoreCase))
            throw new PurchasingConflictException("Chỉ đóng phiếu đã gửi.");
        if (request.ServiceRating is < 1 or > 5)
            throw new PurchasingConflictException("Đánh giá dịch vụ BH phải từ 1 đến 5.");

        claim.ServiceRating = request.ServiceRating;
        claim.ServiceNote = string.IsNullOrWhiteSpace(request.ServiceNote)
            ? null
            : request.ServiceNote.Trim();
        claim.Status = PurchaseWarrantyClaimStatuses.Closed;
        claim.ClosedAt = DateTime.UtcNow;
        claim.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        return ToClaimDto(claim);
    }

    public async Task UpsertFromApprovedImportAsync(WarehouseTransaction import, CancellationToken ct = default)
    {
        var poCode = import.ReferenceCode?.Trim();
        if (string.IsNullOrWhiteSpace(poCode))
            return;

        var order = await db.PurchaseOrders
            .Include(o => o.Items)
            .FirstOrDefaultAsync(o => o.Code == poCode, ct);
        if (order is null) return;

        var deliveryDate = VietnamClock.Today;
        var today = deliveryDate;
        var created = false;

        foreach (var item in import.Items)
        {
            var poItem = MatchPoItem(order.Items, item);
            if (poItem is null || poItem.WarrantyMonths <= 0)
                continue;

            var qty = item.Quantity;
            if (qty <= 0) continue;

            var existing = await db.PurchaseWarranties
                .FirstOrDefaultAsync(w => w.PoItemId == poItem.Id, ct);

            if (existing is null)
            {
                var seq = await db.PurchaseWarranties.IgnoreQueryFilters().CountAsync(ct) + 1;
                existing = new PurchaseWarranty
                {
                    Code = PurchaseWarranty.GenerateCode(seq),
                    PoCode = order.Code,
                    PoItemId = poItem.Id,
                    ProjectCode = order.ProjectCode,
                    PlanCode = order.PlanCode,
                    SupplierId = order.SupplierId,
                    SupplierName = order.SupplierNameSnapshot ?? order.Supplier?.SupplierName,
                    MaterialCode = poItem.MaterialCode ?? poItem.Sku,
                    MaterialName = poItem.Name,
                    Unit = poItem.Unit ?? string.Empty,
                    Quantity = qty,
                    WarrantyMonths = poItem.WarrantyMonths,
                    AnchorType = NormalizeAnchor(poItem.WarrantyAnchorType),
                    DeliveryDate = deliveryDate,
                    ImportTicketCode = import.TicketCode,
                };
                db.PurchaseWarranties.Add(existing);
                created = true;
            }
            else
            {
                existing.Quantity = Math.Max(existing.Quantity, poItem.Received);
                existing.DeliveryDate ??= deliveryDate;
                existing.WarrantyMonths = poItem.WarrantyMonths > 0
                    ? poItem.WarrantyMonths
                    : existing.WarrantyMonths;
                existing.AnchorType = NormalizeAnchor(poItem.WarrantyAnchorType);
                existing.ImportTicketCode ??= import.TicketCode;
                existing.UpdatedAt = DateTime.UtcNow;
            }

            existing.Recalculate(today);
        }

        // Caller (import approve transaction) persists.
        _ = created;
    }

    public async Task<SyncPurchaseWarrantiesResult> SyncFromReceivedPoLinesAsync(CancellationToken ct = default)
    {
        var today = VietnamClock.Today;
        var lines = await db.PurchaseOrderItems
            .AsNoTracking()
            .Include(i => i.PurchaseOrder)
            .Where(i => i.WarrantyMonths > 0 && i.Received > 0 && i.PurchaseOrder != null)
            .ToListAsync(ct);

        var created = 0;
        var updated = 0;

        foreach (var poItem in lines)
        {
            var order = poItem.PurchaseOrder!;
            var existing = await db.PurchaseWarranties
                .FirstOrDefaultAsync(w => w.PoItemId == poItem.Id, ct);

            var deliveryDate = poItem.ExpectedDeliveryDate
                ?? DateOnly.FromDateTime(order.PlacedAt?.ToLocalTime() ?? order.CreatedAt.ToLocalTime());

            if (existing is null)
            {
                var seq = await db.PurchaseWarranties.IgnoreQueryFilters().CountAsync(ct) + 1 + created;
                existing = new PurchaseWarranty
                {
                    Code = PurchaseWarranty.GenerateCode(seq),
                    PoCode = order.Code,
                    PoItemId = poItem.Id,
                    ProjectCode = order.ProjectCode,
                    PlanCode = order.PlanCode,
                    SupplierId = order.SupplierId,
                    SupplierName = order.SupplierNameSnapshot,
                    MaterialCode = poItem.MaterialCode ?? poItem.Sku,
                    MaterialName = poItem.Name,
                    Unit = poItem.Unit ?? string.Empty,
                    Quantity = poItem.Received,
                    WarrantyMonths = poItem.WarrantyMonths,
                    AnchorType = NormalizeAnchor(poItem.WarrantyAnchorType),
                    DeliveryDate = deliveryDate,
                };
                existing.Recalculate(today);
                db.PurchaseWarranties.Add(existing);
                created++;
            }
            else
            {
                existing.Quantity = poItem.Received;
                existing.WarrantyMonths = poItem.WarrantyMonths;
                existing.AnchorType = NormalizeAnchor(poItem.WarrantyAnchorType);
                existing.DeliveryDate ??= deliveryDate;
                existing.Recalculate(today);
                existing.UpdatedAt = DateTime.UtcNow;
                updated++;
            }
        }

        if (created + updated > 0)
            await db.SaveChangesAsync(ct);

        return new SyncPurchaseWarrantiesResult(created, updated);
    }

    private static string ResolveListStatus(string status, DateOnly? end, DateOnly today)
    {
        if (string.Equals(status, PurchaseWarrantyStatuses.Void, StringComparison.OrdinalIgnoreCase))
            return status;
        if (end is DateOnly e && e < today)
            return PurchaseWarrantyStatuses.Expired;
        return status;
    }

    private static string NormalizeAnchor(string? anchor) =>
        PurchaseWarrantyAnchorTypes.Allowed.Contains(anchor ?? "")
            ? anchor!.Trim().ToLowerInvariant()
            : PurchaseWarrantyAnchorTypes.Delivery;

    private static PurchaseOrderItem? MatchPoItem(
        IEnumerable<PurchaseOrderItem> items,
        WarehouseTransactionItem importLine)
    {
        const string prefix = PurchaseOrderReceiptService.PoItemNotePrefix;
        if (!string.IsNullOrWhiteSpace(importLine.Notes)
            && importLine.Notes.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
            && int.TryParse(importLine.Notes[prefix.Length..], out var poItemId))
        {
            return items.FirstOrDefault(i => i.Id == poItemId);
        }

        var code = importLine.MaterialCode?.Trim() ?? "";
        if (!string.IsNullOrEmpty(code))
        {
            return items.FirstOrDefault(i =>
                (!string.IsNullOrWhiteSpace(i.MaterialCode)
                    && string.Equals(i.MaterialCode.Trim(), code, StringComparison.OrdinalIgnoreCase))
                || (!string.IsNullOrWhiteSpace(i.Sku)
                    && string.Equals(i.Sku.Trim(), code, StringComparison.OrdinalIgnoreCase)));
        }

        return null;
    }

    private async Task<PurchaseWarrantyDetailDto> ToDetailDtoAsync(PurchaseWarranty w, CancellationToken ct)
    {
        var evidence = await LoadEvidenceAsync(w, ct);
        return new PurchaseWarrantyDetailDto(
            w.Id, w.Code, w.PoCode, w.PoItemId, w.ProjectCode, w.PlanCode,
            w.SupplierId, w.SupplierName, w.MaterialCode, w.MaterialName, w.Unit,
            w.Quantity, w.WarrantyMonths, w.AnchorType, w.DeliveryDate, w.AcceptanceDate,
            w.WarrantyEndDate, w.Status, w.ImportTicketCode, w.CreatedAt,
            w.Claims
                .Where(c => c.DeletedAt == null)
                .OrderByDescending(c => c.CreatedAt)
                .Select(ToClaimDto)
                .ToList(),
            evidence);
    }

    private async Task<IReadOnlyList<PurchaseWarrantyEvidenceDto>> LoadEvidenceAsync(
        PurchaseWarranty w,
        CancellationToken ct)
    {
        var tickets = await ResolveEvidenceTicketCodesAsync(w, ct);
        if (tickets.Count == 0) return [];

        var rows = await db.WarehouseTransactionAttachments.AsNoTracking()
            .Where(a => tickets.Contains(a.TicketCode) && (a.Kind == "nameplate" || a.Kind == "photo"))
            .OrderBy(a => a.Kind)
            .ThenBy(a => a.UploadedAt)
            .ToListAsync(ct);

        return rows
            .Select(a => new PurchaseWarrantyEvidenceDto(a.Id, a.Kind, a.FileName, a.UploadedAt))
            .ToList();
    }

    private async Task<List<string>> ResolveEvidenceTicketCodesAsync(PurchaseWarranty w, CancellationToken ct)
    {
        if (!string.IsNullOrWhiteSpace(w.ImportTicketCode))
            return [w.ImportTicketCode];

        var poCode = w.PoCode;
        if (string.IsNullOrWhiteSpace(poCode)) return [];

        var ticket = await db.WarehouseTransactions.AsNoTracking()
            .Where(t => t.TransactionType == TransactionType.Import
                        && t.ReferenceCode == poCode
                        && (t.ApprovalStatus == ApprovalStatus.Approved
                            || t.WarehouseApprovalStatus == WarehouseKeeperApprovalStatus.Approved))
            .OrderByDescending(t => t.ApprovedAt ?? t.UpdatedAt)
            .Select(t => t.TicketCode)
            .FirstOrDefaultAsync(ct);

        return string.IsNullOrWhiteSpace(ticket) ? [] : [ticket];
    }

    private static bool IsWarrantyEvidenceKind(string kind) =>
        string.Equals(kind, "nameplate", StringComparison.OrdinalIgnoreCase)
        || string.Equals(kind, "photo", StringComparison.OrdinalIgnoreCase);

    private static PurchaseWarrantyClaimDto ToClaimDto(PurchaseWarrantyClaim c) => new(
        c.Id, c.TicketCode, c.WarrantyId, c.Status, c.Reason, c.Note,
        c.CreatedById, c.CreatedByName, c.SubmittedAt, c.ClosedAt, c.CreatedAt,
        c.ServiceRating, c.ServiceNote, c.Kind, c.DefectType, c.Severity);

    private static string NormalizeKind(string? kind)
    {
        var value = string.IsNullOrWhiteSpace(kind)
            ? PurchaseWarrantyClaimKinds.Warranty
            : kind.Trim().ToLowerInvariant();
        if (!PurchaseWarrantyClaimKinds.Allowed.Contains(value))
            throw new PurchasingConflictException("Loại phiếu phải là warranty hoặc complaint.");
        return value;
    }

    private static string? NormalizeOptional(string? value, HashSet<string> allowed, string label)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;
        var trimmed = value.Trim().ToLowerInvariant();
        if (!allowed.Contains(trimmed))
            throw new PurchasingConflictException($"{label} không hợp lệ.");
        return trimmed;
    }
}
