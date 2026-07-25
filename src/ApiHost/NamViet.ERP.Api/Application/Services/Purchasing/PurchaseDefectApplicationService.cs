using Microsoft.EntityFrameworkCore;
using NamViet.ERP.Api.Application.Dtos.Purchasing;
using NamViet.ERP.Api.Services.Storage;
using NamViet.ERP.Modules.Warehouse.Domain.Entities;
using NamViet.ERP.Modules.Warehouse.Domain.Enums;
using NamViet.ERP.Modules.Warehouse.Infrastructure.Persistence;
using NamViet.ERP.SharedKernel.Interfaces;

namespace NamViet.ERP.Api.Application.Services.Purchasing;

public interface IPurchaseDefectApplicationService
{
    Task<List<PurchaseDefectDto>> ListAsync(CancellationToken ct = default);
    Task<PurchaseDefectDto?> GetByIdAsync(string id, CancellationToken ct = default);
    Task<PoDefectPreviewDto> PreviewFromPoAsync(string poCode, CancellationToken ct = default);
    Task<PurchaseDefectDto> CreateFromPoAsync(string poCode, CreateDefectFromPoRequest request, CancellationToken ct = default);
    Task<PurchaseDefectDto?> SubmitAsync(string id, CancellationToken ct = default);
    Task<PurchaseDefectDto?> ApproveAsync(string id, CancellationToken ct = default);
    Task<PurchaseDefectDto?> UpdateReasonAsync(string id, string? reason, CancellationToken ct = default);
    Task<PurchaseDefectAttachmentDto?> UploadAttachmentAsync(
        string id, string kind, string fileName, string contentType, Stream content, CancellationToken ct = default);
    Task<PurchaseDefectAttachmentDto?> GetAttachmentAsync(
        string id, Guid attachmentId, CancellationToken ct = default);
    Task<bool> DeleteAttachmentAsync(string id, Guid attachmentId, CancellationToken ct = default);
    Task<bool> DeleteAsync(string id, CancellationToken ct = default);
}

public sealed class PurchaseDefectApplicationService(
    WarehouseDbContext db,
    ICurrentUser currentUser,
    IFileStorage fileStorage) : IPurchaseDefectApplicationService
{
    public const string PoItemNotePrefix = PurchaseOrderReceiptService.PoItemNotePrefix;

    public async Task<List<PurchaseDefectDto>> ListAsync(CancellationToken ct = default)
    {
        var rows = await DefectQuery()
            .OrderByDescending(d => d.CreatedAt)
            .ToListAsync(ct);
        return rows.Select(ToDto).ToList();
    }

    public async Task<PurchaseDefectDto?> GetByIdAsync(string id, CancellationToken ct = default)
    {
        var d = await FindAsync(id, ct);
        return d is null ? null : ToDto(d);
    }

    public async Task<PoDefectPreviewDto> PreviewFromPoAsync(string poCode, CancellationToken ct = default)
    {
        var order = await LoadPlacedOrderAsync(poCode, ct);
        var pendingNk = await GetPendingNkQtyByPoItemAsync(order.Code, null, ct);
        var pendingHl = await GetPendingDefectQtyByPoItemAsync(order.Code, null, ct);

        var lines = order.Items
            .OrderBy(i => i.Id)
            .Select(line =>
            {
                pendingNk.TryGetValue(line.Id, out var nkQty);
                pendingHl.TryGetValue(line.Id, out var hlQty);
                var available = Math.Max(0, line.Ordered - line.Received - line.Defected - nkQty - hlQty);
                return new PoDefectLinePreviewDto(
                    PoItemId: line.Id,
                    Name: line.Name,
                    MaterialCode: line.MaterialCode ?? line.Sku,
                    Unit: line.Unit ?? "",
                    Ordered: line.Ordered,
                    Received: line.Received,
                    Defected: line.Defected,
                    ReservedPendingNk: nkQty,
                    ReservedPendingDefect: hlQty,
                    Available: available,
                    UnitPrice: line.PoPrice);
            })
            .Where(l => l.Available > 0)
            .ToList();

        return new PoDefectPreviewDto(
            PoCode: order.Code,
            PlanCode: order.PlanCode,
            ProjectCode: order.ProjectCode ?? "",
            SupplierName: order.SupplierNameSnapshot ?? order.Supplier?.SupplierName ?? "",
            Lines: lines);
    }

    public async Task<PurchaseDefectDto> CreateFromPoAsync(
        string poCode,
        CreateDefectFromPoRequest request,
        CancellationToken ct = default)
    {
        if (request.Items is null || request.Items.Count == 0)
            throw new PurchasingConflictException("Chọn ít nhất một dòng hàng lỗi.");

        var order = await LoadPlacedOrderAsync(poCode, ct);
        var pendingNk = await GetPendingNkQtyByPoItemAsync(order.Code, null, ct);
        var pendingHl = await GetPendingDefectQtyByPoItemAsync(order.Code, null, ct);

        var selectionMap = request.Items
            .GroupBy(s => s.PoItemId)
            .ToDictionary(g => g.Key, g => g.First());

        var missing = selectionMap.Keys.Where(id => order.Items.All(i => i.Id != id)).ToList();
        if (missing.Count > 0)
            throw new PurchasingConflictException("Dòng PO không hợp lệ: " + string.Join(", ", missing));

        var defectItems = new List<PurchaseDefectItem>();
        foreach (var line in order.Items.Where(i => selectionMap.ContainsKey(i.Id)).OrderBy(i => i.Id))
        {
            var sel = selectionMap[line.Id];
            pendingNk.TryGetValue(line.Id, out var nkQty);
            pendingHl.TryGetValue(line.Id, out var hlQty);
            var available = Math.Max(0, line.Ordered - line.Received - line.Defected - nkQty - hlQty);
            if (sel.Qty <= 0)
                throw new PurchasingConflictException($"SL lỗi không hợp lệ cho '{line.Name}'.");
            if (sel.Qty > available)
                throw new PurchasingConflictException(
                    $"'{line.Name}': chỉ còn {available:0.##} (đã trừ NK/HL chờ duyệt), không ghi lỗi {sel.Qty:0.##}.");

            defectItems.Add(new PurchaseDefectItem
            {
                PoItemId = line.Id,
                MaterialCode = line.MaterialCode ?? line.Sku,
                Name = line.Name,
                Unit = line.Unit ?? "",
                Quantity = sel.Qty,
                LineReason = string.IsNullOrWhiteSpace(sel.LineReason) ? null : sel.LineReason.Trim(),
            });
        }

        var count = await db.PurchaseDefects.IgnoreQueryFilters().CountAsync(ct);
        var ticket = PurchaseDefect.GenerateTicketCode(count + 1);
        while (await db.PurchaseDefects.IgnoreQueryFilters().AnyAsync(d => d.TicketCode == ticket, ct))
        {
            count++;
            ticket = PurchaseDefect.GenerateTicketCode(count + 1);
        }

        var defect = new PurchaseDefect
        {
            TicketCode = ticket,
            PoCode = order.Code,
            PlanCode = order.PlanCode,
            ProjectCode = order.ProjectCode ?? "",
            SupplierName = order.SupplierNameSnapshot ?? order.Supplier?.SupplierName,
            Status = PurchaseDefectStatuses.Draft,
            Reason = string.IsNullOrWhiteSpace(request.Reason) ? null : request.Reason.Trim(),
            Note = string.IsNullOrWhiteSpace(request.Note) ? null : request.Note.Trim(),
            CreatedById = currentUser.Id,
            CreatedByName = currentUser.Name ?? "System",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            Items = defectItems,
        };

        foreach (var item in defectItems)
            item.TicketCode = ticket;

        db.PurchaseDefects.Add(defect); 
        await db.SaveChangesAsync(ct);
        return ToDto(await FindAsync(ticket, ct) ?? defect);
    }

    public async Task<PurchaseDefectDto?> SubmitAsync(string id, CancellationToken ct = default)
    {
        var d = await FindAsync(id, ct);
        if (d is null) return null;
        if (!string.Equals(d.Status, PurchaseDefectStatuses.Draft, StringComparison.OrdinalIgnoreCase))
            throw new PurchasingConflictException("Chỉ gửi duyệt phiếu HL ở trạng thái nháp.");
        if (d.Items.Count == 0)
            throw new PurchasingConflictException("Phiếu HL không có dòng hàng.");

        await EnsureWithinAvailabilityAsync(d, ct);
        d.Status = PurchaseDefectStatuses.Pending;
        d.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        return ToDto(d);
    }

    public async Task<PurchaseDefectDto?> ApproveAsync(string id, CancellationToken ct = default)
    {
        var d = await FindTrackedAsync(id, ct);
        if (d is null) return null;
        if (!string.Equals(d.Status, PurchaseDefectStatuses.Pending, StringComparison.OrdinalIgnoreCase))
            throw new PurchasingConflictException("Chỉ duyệt phiếu HL đang chờ duyệt.");
        if (d.Items.Any(i => string.IsNullOrWhiteSpace(i.LineReason)))
            throw new PurchasingConflictException("Cần lý do lỗi trên từng dòng trước khi duyệt.");
        if (!d.Attachments.Any(a => string.Equals(a.Kind, "photo", StringComparison.OrdinalIgnoreCase)))
            throw new PurchasingConflictException("Cần ít nhất một ảnh hàng lỗi trước khi duyệt.");

        await EnsureWithinAvailabilityAsync(d, ct);

        var order = await db.PurchaseOrders
            .Include(o => o.Items)
            .FirstOrDefaultAsync(o => o.Code == d.PoCode, ct)
            ?? throw new PurchasingConflictException($"Không tìm thấy PO '{d.PoCode}'.");

        var planLineIds = order.Items
            .Where(i => i.PlanLineId is int)
            .Select(i => i.PlanLineId!.Value)
            .Distinct()
            .ToList();
        var planLines = planLineIds.Count == 0
            ? new Dictionary<int, PurchasePlanLine>()
            : await db.PurchasePlanLines
                .Where(l => planLineIds.Contains(l.Id))
                .ToDictionaryAsync(l => l.Id, ct);

        foreach (var item in d.Items)
        {
            var poItem = order.Items.FirstOrDefault(i => i.Id == item.PoItemId)
                ?? throw new PurchasingConflictException($"Dòng PO #{item.PoItemId} không còn trên {d.PoCode}.");

            var remaining = poItem.Ordered - poItem.Received - poItem.Defected;
            if (item.Quantity > remaining)
                throw new PurchasingConflictException(
                    $"'{poItem.Name}': còn {remaining:0.##} để ghi lỗi, phiếu yêu cầu {item.Quantity:0.##}.");

            poItem.Defected += item.Quantity;

            if (poItem.PlanLineId is int planLineId && planLines.TryGetValue(planLineId, out var planLine))
            {
                planLine.DefectQty += item.Quantity;
                planLine.PurchaseStatus = ResolvePurchaseStatus(planLine);
            }
        }

        order.DeliveryStatus = ResolveDeliveryStatus(order.Items);
        order.UpdatedAt = DateTime.UtcNow;

        d.Status = PurchaseDefectStatuses.Approved;
        d.ApprovedById = currentUser.Id;
        d.ApprovedByName = currentUser.Name ?? "System";
        d.ApprovedAt = DateTime.UtcNow;
        d.UpdatedAt = DateTime.UtcNow;

        await db.SaveChangesAsync(ct);
        return ToDto(d);
    }

    public async Task<PurchaseDefectDto?> UpdateReasonAsync(string id, string? reason, CancellationToken ct = default)
    {
        var d = await FindAsync(id, ct);
        if (d is null) return null;
        if (string.Equals(d.Status, PurchaseDefectStatuses.Approved, StringComparison.OrdinalIgnoreCase))
            throw new PurchasingConflictException("Phiếu HL đã duyệt — không sửa lý do.");

        d.Reason = string.IsNullOrWhiteSpace(reason) ? null : reason.Trim();
        d.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        return ToDto(d);
    }

    public async Task<PurchaseDefectAttachmentDto?> UploadAttachmentAsync(
        string id, string kind, string fileName, string contentType, Stream content, CancellationToken ct = default)
    {
        var d = await FindAsync(id, ct);
        if (d is null) return null;
        if (string.Equals(d.Status, PurchaseDefectStatuses.Approved, StringComparison.OrdinalIgnoreCase))
            throw new PurchasingConflictException("Phiếu HL đã duyệt — không thêm file.");

        var normalizedKind = NormalizeAttachmentKind(kind);
        EnsureAllowedExtension(fileName);

        var ext = Path.GetExtension(fileName);
        var relative = $"wms/defects/{d.TicketCode}/{normalizedKind}/{Guid.NewGuid():N}{ext}";
        var stored = await fileStorage.SaveAsync(content, relative, contentType, ct);

        var attachment = new PurchaseDefectAttachment
        {
            TicketCode = d.TicketCode,
            Kind = normalizedKind,
            StorageKey = stored.StorageKey,
            FileName = Path.GetFileName(fileName),
            UploadedAt = DateTime.UtcNow,
        };
        db.PurchaseDefectAttachments.Add(attachment);
        d.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);

        return new PurchaseDefectAttachmentDto(attachment.Id, attachment.Kind, attachment.FileName, attachment.StorageKey, attachment.UploadedAt);
    }

    public async Task<PurchaseDefectAttachmentDto?> GetAttachmentAsync(
        string id, Guid attachmentId, CancellationToken ct = default)
    {
        var d = await FindAsync(id, ct);
        if (d is null) return null;
        var att = d.Attachments.FirstOrDefault(a => a.Id == attachmentId);
        if (att is null) return null;
        return new PurchaseDefectAttachmentDto(att.Id, att.Kind, att.FileName, att.StorageKey, att.UploadedAt);
    }

    public async Task<bool> DeleteAttachmentAsync(string id, Guid attachmentId, CancellationToken ct = default)
    {
        var d = await FindAsync(id, ct);
        if (d is null) return false;
        if (string.Equals(d.Status, PurchaseDefectStatuses.Approved, StringComparison.OrdinalIgnoreCase))
            throw new PurchasingConflictException("Phiếu HL đã duyệt — không xóa file.");

        var att = d.Attachments.FirstOrDefault(a => a.Id == attachmentId);
        if (att is null) return false;
        db.PurchaseDefectAttachments.Remove(att);
        d.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<bool> DeleteAsync(string id, CancellationToken ct = default)
    {
        var d = await FindAsync(id, ct);
        if (d is null) return false;
        if (!string.Equals(d.Status, PurchaseDefectStatuses.Draft, StringComparison.OrdinalIgnoreCase))
            throw new PurchasingConflictException("Chỉ xóa được phiếu HL nháp.");

        d.DeletedAt = DateTime.UtcNow;
        d.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        return true;
    }

    private async Task EnsureWithinAvailabilityAsync(PurchaseDefect defect, CancellationToken ct)
    {
        var order = await db.PurchaseOrders
            .AsNoTracking()
            .Include(o => o.Items)
            .FirstOrDefaultAsync(o => o.Code == defect.PoCode, ct)
            ?? throw new PurchasingConflictException($"Không tìm thấy PO '{defect.PoCode}'.");

        var pendingNk = await GetPendingNkQtyByPoItemAsync(defect.PoCode, null, ct);
        var pendingHl = await GetPendingDefectQtyByPoItemAsync(defect.PoCode, defect.TicketCode, ct);

        foreach (var item in defect.Items)
        {
            var poItem = order.Items.FirstOrDefault(i => i.Id == item.PoItemId)
                ?? throw new PurchasingConflictException($"Dòng PO #{item.PoItemId} không hợp lệ.");
            pendingNk.TryGetValue(poItem.Id, out var nkQty);
            pendingHl.TryGetValue(poItem.Id, out var hlQty);
            var available = Math.Max(0, poItem.Ordered - poItem.Received - poItem.Defected - nkQty - hlQty);
            if (item.Quantity > available)
                throw new PurchasingConflictException(
                    $"'{poItem.Name}': chỉ còn {available:0.##} (đã trừ NK/HL chờ duyệt), phiếu yêu cầu {item.Quantity:0.##}.");
        }
    }

    private async Task<PurchaseOrder> LoadPlacedOrderAsync(string poCode, CancellationToken ct)
    {
        var code = poCode.Trim();
        var order = await db.PurchaseOrders
            .AsNoTracking()
            .Include(o => o.Items)
            .Include(o => o.Supplier)
            .FirstOrDefaultAsync(o => o.Code == code, ct)
            ?? throw new PurchasingConflictException($"Không tìm thấy PO '{code}'.");

        if (!string.Equals(order.OrderPlacementStatus, PurchaseOrderPlacementStatuses.Placed, StringComparison.OrdinalIgnoreCase))
            throw new PurchasingConflictException("Chỉ tạo phiếu hàng lỗi từ PO đã đặt hàng (placed).");

        if (string.IsNullOrWhiteSpace(order.ProjectCode))
            throw new PurchasingConflictException("PO thiếu mã dự án.");

        return order;
    }

    private async Task<Dictionary<int, decimal>> GetPendingNkQtyByPoItemAsync(
        string poCode, string? excludeTicketCode, CancellationToken ct)
    {
        var pendingImports = await db.WarehouseTransactions
            .AsNoTracking()
            .Include(t => t.Items)
            .Where(t =>
                t.TransactionType == TransactionType.Import
                && t.ApprovalStatus == ApprovalStatus.Pending
                && t.ReferenceCode == poCode
                && (excludeTicketCode == null || t.TicketCode != excludeTicketCode))
            .ToListAsync(ct);

        var map = new Dictionary<int, decimal>();
        foreach (var nk in pendingImports)
        {
            foreach (var line in nk.Items)
            {
                if (string.IsNullOrWhiteSpace(line.Notes)
                    || !line.Notes.StartsWith(PoItemNotePrefix, StringComparison.OrdinalIgnoreCase)
                    || !int.TryParse(line.Notes[PoItemNotePrefix.Length..], out var poItemId))
                    continue;
                map[poItemId] = map.GetValueOrDefault(poItemId) + line.Quantity;
            }
        }
        return map;
    }

    private async Task<Dictionary<int, decimal>> GetPendingDefectQtyByPoItemAsync(
        string poCode, string? excludeTicketCode, CancellationToken ct)
    {
        var pending = await db.PurchaseDefects
            .AsNoTracking()
            .Include(d => d.Items)
            .Where(d =>
                d.PoCode == poCode
                && d.Status == PurchaseDefectStatuses.Pending
                && (excludeTicketCode == null || d.TicketCode != excludeTicketCode))
            .ToListAsync(ct);

        var map = new Dictionary<int, decimal>();
        foreach (var hl in pending)
        {
            foreach (var line in hl.Items)
                map[line.PoItemId] = map.GetValueOrDefault(line.PoItemId) + line.Quantity;
        }
        return map;
    }

    private IQueryable<PurchaseDefect> DefectQuery() =>
        db.PurchaseDefects
            .Include(d => d.Items)
            .Include(d => d.Attachments);

    private Task<PurchaseDefect?> FindAsync(string id, CancellationToken ct) =>
        DefectQuery().FirstOrDefaultAsync(d => d.TicketCode == id || d.Id.ToString() == id, ct);

    private Task<PurchaseDefect?> FindTrackedAsync(string id, CancellationToken ct) =>
        db.PurchaseDefects
            .Include(d => d.Items)
            .Include(d => d.Attachments)
            .FirstOrDefaultAsync(d => d.TicketCode == id || d.Id.ToString() == id, ct);

    private static PurchaseDefectDto ToDto(PurchaseDefect d) => new(
        Id: d.TicketCode,
        TicketCode: d.TicketCode,
        PoCode: d.PoCode,
        PlanCode: d.PlanCode,
        ProjectCode: d.ProjectCode,
        SupplierName: d.SupplierName,
        Status: d.Status,
        Reason: d.Reason,
        Note: d.Note,
        CreatedByName: d.CreatedByName,
        CreatedAt: d.CreatedAt,
        ApprovedByName: d.ApprovedByName,
        ApprovedAt: d.ApprovedAt,
        Items: d.Items.OrderBy(i => i.Id).Select(i => new PurchaseDefectItemDto(
            i.Id, i.PoItemId, i.MaterialCode, i.Name, i.Unit, i.Quantity, i.LineReason)).ToList(),
        Attachments: d.Attachments.OrderBy(a => a.UploadedAt).Select(a => new PurchaseDefectAttachmentDto(
            a.Id, a.Kind, a.FileName, a.StorageKey, a.UploadedAt)).ToList());

    private static string ResolveDeliveryStatus(IEnumerable<PurchaseOrderItem> items)
    {
        var list = items.ToList();
        if (list.Count == 0 || list.All(i => i.Received <= 0 && i.Defected <= 0))
            return PurchaseOrderDeliveryStatuses.None;
        if (list.All(i => i.Received + i.Defected >= i.Ordered))
            return PurchaseOrderDeliveryStatuses.Complete;
        return PurchaseOrderDeliveryStatuses.Partial;
    }

    private static string ResolvePurchaseStatus(PurchasePlanLine line)
    {
        var closed = line.ReceivedQty + line.DefectQty;
        if (closed <= 0 && line.OrderedQty > 0)
            return PurchaseLineStatuses.Ordered;
        if (closed > 0 && closed < line.Quantity)
            return PurchaseLineStatuses.PartialDelivery;
        if (closed >= line.Quantity)
            return PurchaseLineStatuses.Delivered;
        return line.PurchaseStatus ?? PurchaseLineStatuses.NotPurchased;
    }

    private static string NormalizeAttachmentKind(string kind)
    {
        var k = (kind ?? "").Trim().ToLowerInvariant();
        return k is "photo" or "document" ? k : "photo";
    }

    private static void EnsureAllowedExtension(string fileName)
    {
        var ext = Path.GetExtension(fileName).ToLowerInvariant();
        string[] allowed = [".jpg", ".jpeg", ".png", ".webp", ".gif", ".pdf", ".doc", ".docx", ".xls", ".xlsx"];
        if (!allowed.Contains(ext))
            throw new PurchasingConflictException($"Định dạng file không hỗ trợ: {ext}");
    }
}
