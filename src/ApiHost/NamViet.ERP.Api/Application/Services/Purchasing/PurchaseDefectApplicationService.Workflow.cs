using Microsoft.EntityFrameworkCore;
using NamViet.ERP.Api.Application.Dtos.Purchasing;
using NamViet.ERP.Modules.Warehouse.Domain.Entities;
using NamViet.ERP.Modules.Warehouse.Domain.Enums;

namespace NamViet.ERP.Api.Application.Services.Purchasing;

public sealed partial class PurchaseDefectApplicationService
{
    public async Task<string?> CreateAndApproveFromImportAsync(
        WarehouseTransaction import,
        string? defectReason,
        CancellationToken ct = default)
    {
        var defectLines = import.Items
            .Where(i => (i.DefectQty ?? 0) > 0)
            .ToList();
        if (defectLines.Count == 0)
            return null;

        var poCode = import.ReferenceCode?.Trim();
        if (string.IsNullOrWhiteSpace(poCode))
            throw new PurchasingConflictException("Phiếu NK không gắn PO — không tạo HL từ kiểm nhận.");

        var order = await LoadPlacedOrderAsync(poCode, ct);
        // NK đã Approved trước khi gọi — không còn trong pending NK.
        var pendingNk = await GetPendingNkQtyByPoItemAsync(order.Code, null, ct);
        var pendingHl = await GetPendingDefectQtyByPoItemAsync(order.Code, null, ct);

        var defectItems = new List<PurchaseDefectItem>();
        foreach (var line in defectLines)
        {
            var poItemId = TryParsePoItemId(line.Notes);
            if (poItemId is null)
                throw new PurchasingConflictException(
                    $"Dòng '{line.MaterialCode}' thiếu liên kết PO (poItem) — không ghi hàng lỗi.");

            var poItem = order.Items.FirstOrDefault(i => i.Id == poItemId.Value)
                ?? throw new PurchasingConflictException($"Dòng PO #{poItemId} không còn trên {poCode}.");

            pendingNk.TryGetValue(poItem.Id, out var nkQty);
            pendingHl.TryGetValue(poItem.Id, out var hlQty);
            var available = Math.Max(0, poItem.Ordered - poItem.Received - poItem.Defected - nkQty - hlQty);
            var qty = line.DefectQty!.Value;
            if (qty > available)
                throw new PurchasingConflictException(
                    $"'{poItem.Name}': chỉ còn {available:0.##} để ghi lỗi, không ghi {qty:0.##}.");

            if (string.IsNullOrWhiteSpace(line.DefectLineReason))
                throw new PurchasingConflictException($"'{poItem.Name}': cần lý do hàng lỗi.");

            defectItems.Add(new PurchaseDefectItem
            {
                PoItemId = poItem.Id,
                MaterialCode = poItem.MaterialCode ?? poItem.Sku ?? line.MaterialCode,
                Name = poItem.Name,
                Unit = poItem.Unit ?? "",
                Quantity = qty,
                LineReason = line.DefectLineReason.Trim(),
            });
        }

        var photos = import.Attachments
            .Where(a => string.Equals(a.Kind, "defect_photo", StringComparison.OrdinalIgnoreCase))
            .ToList();
        if (photos.Count == 0)
            throw new PurchasingConflictException("Cần ảnh hàng lỗi trước khi tạo HL.");

        var ticket = await NextTicketCodeAsync(ct);
        var defect = new PurchaseDefect
        {
            TicketCode = ticket,
            SourceType = PurchaseDefectSourceTypes.Po,
            PoCode = order.Code,
            ImportTicketCode = import.TicketCode,
            PlanCode = order.PlanCode,
            ProjectCode = order.ProjectCode ?? import.BeneficiaryProjectCode ?? "",
            SupplierName = order.SupplierNameSnapshot ?? order.Supplier?.SupplierName ?? import.SupplierName,
            Status = PurchaseDefectStatuses.Pending,
            Reason = string.IsNullOrWhiteSpace(defectReason)
                ? $"Tạo từ kiểm nhận {import.TicketCode}"
                : defectReason.Trim(),
            Note = string.IsNullOrWhiteSpace(import.ShortageReport) ? null : $"Thiếu hàng (NK): {import.ShortageReport}",
            CreatedById = currentUser.Id,
            CreatedByName = currentUser.Name ?? "System",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            Items = defectItems,
        };

        foreach (var item in defectItems)
            item.TicketCode = ticket;

        foreach (var photo in photos)
        {
            defect.Attachments.Add(new PurchaseDefectAttachment
            {
                TicketCode = ticket,
                Kind = "photo",
                StorageKey = photo.StorageKey,
                FileName = photo.FileName,
                UploadedAt = photo.UploadedAt,
            });
        }

        db.PurchaseDefects.Add(defect);
        await db.SaveChangesAsync(ct);

        await ApplyPoDefectApprovalAsync(defect, ct);
        defect.Status = PurchaseDefectStatuses.Approved;
        defect.ApprovedById = currentUser.Id;
        defect.ApprovedByName = currentUser.Name ?? "System";
        defect.ApprovedAt = DateTime.UtcNow;
        defect.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);

        return ticket;
    }

    private static int? TryParsePoItemId(string? notes)
    {
        if (string.IsNullOrWhiteSpace(notes)) return null;
        const string prefix = PurchaseOrderReceiptService.PoItemNotePrefix;
        if (!notes.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) return null;
        return int.TryParse(notes[prefix.Length..].Trim(), out var id) ? id : null;
    }

    public async Task<PurchaseDefectDto?> SubmitAsync(string id, CancellationToken ct = default)
    {
        var d = await FindTrackedAsync(id, ct);
        if (d is null) return null;
        if (!string.Equals(d.Status, PurchaseDefectStatuses.Draft, StringComparison.OrdinalIgnoreCase))
            throw new PurchasingConflictException("Chỉ gửi duyệt phiếu HL ở trạng thái nháp.");
        if (d.Items.Count == 0)
            throw new PurchasingConflictException("Phiếu HL không có dòng hàng.");
        if (d.Items.Any(i => string.IsNullOrWhiteSpace(i.LineReason)))
            throw new PurchasingConflictException("Mỗi dòng cần lý do lỗi trước khi gửi duyệt.");
        if (!d.Attachments.Any(a => string.Equals(a.Kind, "photo", StringComparison.OrdinalIgnoreCase)))
            throw new PurchasingConflictException("Gửi duyệt cần ít nhất một ảnh hàng lỗi.");

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

        if (d.IsFromPo)
            await ApplyPoDefectApprovalAsync(d, ct);
        else
            await ApplyStockDefectApprovalAsync(d, ct);

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

    public async Task<PurchaseDefectDto?> UpdateItemsAsync(
        string id, UpdateDefectItemsRequest request, CancellationToken ct = default)
    {
        var d = await FindTrackedAsync(id, ct);
        if (d is null) return null;

        var editable =
            string.Equals(d.Status, PurchaseDefectStatuses.Draft, StringComparison.OrdinalIgnoreCase)
            || string.Equals(d.Status, PurchaseDefectStatuses.Pending, StringComparison.OrdinalIgnoreCase);
        if (!editable)
            throw new PurchasingConflictException("Chỉ sửa số lượng phiếu HL nháp hoặc chờ duyệt.");

        if (request.Items is null || request.Items.Count == 0)
            throw new PurchasingConflictException("Phiếu HL cần ít nhất một dòng.");
        if (request.Items.Count != d.Items.Count)
            throw new PurchasingConflictException("Phải gửi đủ mọi dòng của phiếu, không thêm dòng mới.");

        var incomingById = new Dictionary<int, UpdateDefectItemRequest>();
        foreach (var incoming in request.Items)
        {
            if (!incomingById.TryAdd(incoming.Id, incoming))
                throw new PurchasingConflictException("Dòng hàng bị trùng.");
        }

        foreach (var item in d.Items)
        {
            if (!incomingById.TryGetValue(item.Id, out var incoming))
                throw new PurchasingConflictException($"Thiếu số lượng cho dòng '{item.Name}'.");
            if (incoming.Quantity <= 0)
                throw new PurchasingConflictException($"'{item.Name}': số lượng phải lớn hơn 0.");
            item.Quantity = incoming.Quantity;
            if (incoming.LineReason is not null)
                item.LineReason = string.IsNullOrWhiteSpace(incoming.LineReason)
                    ? null
                    : incoming.LineReason.Trim();
        }

        if (incomingById.Keys.Any(itemId => d.Items.All(i => i.Id != itemId)))
            throw new PurchasingConflictException("Có dòng không thuộc phiếu này.");

        await EnsureWithinAvailabilityAsync(d, ct);

        d.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        return ToDto(d);
    }

    public async Task<PurchaseDefectDto?> CancelAsync(string id, CancellationToken ct = default)
    {
        var d = await FindTrackedAsync(id, ct);
        if (d is null) return null;
        if (!string.Equals(d.Status, PurchaseDefectStatuses.Pending, StringComparison.OrdinalIgnoreCase))
            throw new PurchasingConflictException("Chỉ hủy phiếu HL đang chờ duyệt.");

        d.Status = PurchaseDefectStatuses.Rejected;
        d.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        return ToDto(d);
    }

    public async Task<PurchaseDefectDto?> UpdateProcessingAsync(
        string id,
        UpdateDefectProcessingRequest request,
        CancellationToken ct = default)
    {
        var d = await FindTrackedAsync(id, ct);
        if (d is null) return null;
        if (!string.Equals(d.Status, PurchaseDefectStatuses.Approved, StringComparison.OrdinalIgnoreCase))
            throw new PurchasingConflictException("Chỉ cập nhật xử lý khi phiếu đã duyệt.");

        d.CauseSource = NormalizeCauseSource(request.CauseSource);
        d.SupplierFeedback = string.IsNullOrWhiteSpace(request.SupplierFeedback)
            ? null
            : request.SupplierFeedback.Trim();
        d.HandlingMethod = NormalizeHandlingMethod(request.HandlingMethod);

        if (d.ResolveProcessingPhase() == PurchaseDefectProcessingPhases.Processed)
        {
            d.ProcessedById = currentUser.Id;
            d.ProcessedByName = currentUser.Name ?? "System";
            d.ProcessedAt = DateTime.UtcNow;
        }
        else
        {
            d.ProcessedById = null;
            d.ProcessedByName = null;
            d.ProcessedAt = null;
        }

        d.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        return ToDto(d);
    }

    private async Task ApplyPoDefectApprovalAsync(PurchaseDefect d, CancellationToken ct)
    {
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
    }

    private async Task ApplyStockDefectApprovalAsync(PurchaseDefect d, CancellationToken ct)
    {
        foreach (var item in d.Items)
        {
            if (string.IsNullOrWhiteSpace(item.MaterialCode))
                throw new PurchasingConflictException("Dòng HL thiếu mã vật tư.");

            var inv = await db.ProjectInventories
                .FirstOrDefaultAsync(
                    pi => pi.ProjectCode == d.ProjectCode
                        && pi.MaterialCode == item.MaterialCode
                        && pi.DeletedAt == null,
                    ct)
                ?? throw new PurchasingConflictException(
                    $"Không tìm thấy tồn '{item.MaterialCode}' tại kho {d.ProjectCode}.");

            if (item.Quantity > inv.ActualInventoryQty)
                throw new PurchasingConflictException(
                    $"'{item.Name}': tồn thực tế {inv.ActualInventoryQty:0.##}, không trừ lỗi {item.Quantity:0.##}.");

            var available = inv.ActualInventoryQty - inv.PlannedExportQty;
            if (item.Quantity > available)
                throw new PurchasingConflictException(
                    $"'{item.Name}': chỉ còn {available:0.##} khả dụng (đã trừ xuất/CK dự kiến), không trừ lỗi {item.Quantity:0.##}.");

            inv.ApplyActualExport(item.Quantity);
        }
    }

    private async Task EnsureWithinAvailabilityAsync(PurchaseDefect defect, CancellationToken ct)
    {
        if (defect.IsFromStock)
        {
            var pending = await inventory.GetPendingStockDefectQtyByMaterialAsync(
                defect.ProjectCode, defect.TicketCode, ct);

            foreach (var item in defect.Items)
            {
                if (string.IsNullOrWhiteSpace(item.MaterialCode))
                    throw new PurchasingConflictException("Dòng HL thiếu mã vật tư.");

                var inv = await db.ProjectInventories
                    .AsNoTracking()
                    .FirstOrDefaultAsync(
                        pi => pi.ProjectCode == defect.ProjectCode
                            && pi.MaterialCode == item.MaterialCode
                            && pi.DeletedAt == null,
                        ct)
                    ?? throw new PurchasingConflictException(
                        $"Không tìm thấy tồn '{item.MaterialCode}' tại kho {defect.ProjectCode}.");

                pending.TryGetValue(item.MaterialCode, out var stockPendingHl);
                var available = Math.Max(0, inv.AvailableQty - stockPendingHl);
                if (item.Quantity > available)
                    throw new PurchasingConflictException(
                        $"'{item.Name}': chỉ còn {available:0.##} khả dụng, phiếu yêu cầu {item.Quantity:0.##}.");
            }
            return;
        }

        var order = await db.PurchaseOrders
            .AsNoTracking()
            .Include(o => o.Items)
            .FirstOrDefaultAsync(o => o.Code == defect.PoCode, ct)
            ?? throw new PurchasingConflictException($"Không tìm thấy PO '{defect.PoCode}'.");

        var pendingNk = await GetPendingNkQtyByPoItemAsync(defect.PoCode!, null, ct);
        var pendingHlPo = await GetPendingDefectQtyByPoItemAsync(defect.PoCode!, defect.TicketCode, ct);

        foreach (var item in defect.Items)
        {
            var poItem = order.Items.FirstOrDefault(i => i.Id == item.PoItemId)
                ?? throw new PurchasingConflictException($"Dòng PO #{item.PoItemId} không hợp lệ.");
            pendingNk.TryGetValue(poItem.Id, out var nkQty);
            pendingHlPo.TryGetValue(poItem.Id, out var hlQty);
            var available = Math.Max(0, poItem.Ordered - poItem.Received - poItem.Defected - nkQty - hlQty);
            if (item.Quantity > available)
                throw new PurchasingConflictException(
                    $"'{poItem.Name}': trên PO chỉ còn {available:0.##} để ghi lỗi (đã trừ NK/HL đang chờ). Phiếu đang ghi {item.Quantity:0.##}.");
        }
    }

    private async Task<Dictionary<int, decimal>> GetPendingNkQtyByPoItemAsync(
        string poCode, string? excludeTicketCode, CancellationToken ct)
    {
        var map = await GetPendingNkQtyByPoItemsAsync([poCode], ct, excludeTicketCode);
        return map;
    }

    private async Task<Dictionary<int, decimal>> GetPendingNkQtyByPoItemsAsync(
        IReadOnlyCollection<string> poCodes,
        CancellationToken ct,
        string? excludeTicketCode = null)
    {
        if (poCodes.Count == 0)
            return new Dictionary<int, decimal>();

        var codes = poCodes as IList<string> ?? poCodes.ToList();
        var pendingImports = await db.WarehouseTransactions
            .AsNoTracking()
            .Include(t => t.Items)
            .Where(t =>
                t.TransactionType == TransactionType.Import
                && t.ApprovalStatus == ApprovalStatus.Pending
                && t.ReferenceCode != null
                && codes.Contains(t.ReferenceCode)
                && (excludeTicketCode == null || t.TicketCode != excludeTicketCode))
            .ToListAsync(ct);

        var map = new Dictionary<int, decimal>();
        foreach (var nk in pendingImports)
        {
            if (!PurchaseOrderReceiptService.SettlesPurchaseOrder(nk))
                continue;

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
        return await GetPendingDefectQtyByPoItemsAsync([poCode], ct, excludeTicketCode);
    }

    private async Task<Dictionary<int, decimal>> GetPendingDefectQtyByPoItemsAsync(
        IReadOnlyCollection<string> poCodes,
        CancellationToken ct,
        string? excludeTicketCode = null)
    {
        if (poCodes.Count == 0)
            return new Dictionary<int, decimal>();

        var codes = poCodes as IList<string> ?? poCodes.ToList();
        var pending = await db.PurchaseDefects
            .AsNoTracking()
            .Include(d => d.Items)
            .Where(d =>
                d.PoCode != null
                && codes.Contains(d.PoCode)
                && d.SourceType == PurchaseDefectSourceTypes.Po
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

    private static string? NormalizeCauseSource(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var v = value.Trim().ToLowerInvariant();
        return v switch
        {
            PurchaseDefectCauseSources.Supplier or "ncc" => PurchaseDefectCauseSources.Supplier,
            PurchaseDefectCauseSources.Customer or "khach_hang" or "customer" => PurchaseDefectCauseSources.Customer,
            _ => throw new PurchasingConflictException("Nguyên nhân phải là NCC hoặc khách hàng."),
        };
    }

    private static string? NormalizeHandlingMethod(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var v = value.Trim().ToLowerInvariant();
        return v switch
        {
            PurchaseDefectHandlingMethods.WarrantyRepair or "bao_hanh" => PurchaseDefectHandlingMethods.WarrantyRepair,
            PurchaseDefectHandlingMethods.Replace or "doi_moi" => PurchaseDefectHandlingMethods.Replace,
            _ => throw new PurchasingConflictException("Hướng xử lý phải là bảo hành sửa chữa hoặc đổi mới."),
        };
    }
}
