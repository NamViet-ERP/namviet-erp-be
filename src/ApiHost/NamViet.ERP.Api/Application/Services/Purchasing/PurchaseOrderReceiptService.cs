using Microsoft.EntityFrameworkCore;
using NamViet.ERP.Api.Application.Dtos.Purchasing;
using NamViet.ERP.Api.Application.Dtos.Wms;
using NamViet.ERP.Modules.Warehouse.Domain.Entities;
using NamViet.ERP.Modules.Warehouse.Domain.Enums;
using NamViet.ERP.Modules.Warehouse.Infrastructure.Persistence;

namespace NamViet.ERP.Api.Application.Services.Purchasing;

public interface IPurchaseOrderReceiptService
{
    /// <summary>
    /// After warehouse import is approved: bump PO.Received / plan.ReceivedQty and DeliveryStatus
    /// when ReferenceCode matches a placed PO.
    /// </summary>
    Task ApplyApprovedImportReceiptAsync(WarehouseTransaction import, CancellationToken ct = default);

    /// <summary>Preview remaining PO lines (Ordered − Received − qty on pending NK).</summary>
    Task<PoImportPreviewDto> PreviewImportFromPoAsync(
        string poCode,
        string? excludeTicketCode = null,
        CancellationToken ct = default);

    /// <summary>
    /// Build CreateImportDto from selected PO lines (or all open lines when selections null).
    /// </summary>
    Task<CreateImportDto> BuildImportDraftFromPoAsync(
        string poCode,
        IReadOnlyList<CreateImportFromPoLineRequest>? selections = null,
        CancellationToken ct = default);

    /// <summary>
    /// Ensures import lines linked to a PO do not exceed available qty
    /// (excludes draft NK; includes pending NK).
    /// </summary>
    Task EnsureImportLinesWithinPoAvailabilityAsync(
        WarehouseTransaction import,
        CancellationToken ct = default);
}

public sealed class PurchaseOrderReceiptService(WarehouseDbContext db) : IPurchaseOrderReceiptService
{
    public const string PoItemNotePrefix = "poItem:";

    public async Task ApplyApprovedImportReceiptAsync(WarehouseTransaction import, CancellationToken ct = default)
    {
        var poCode = import.ReferenceCode?.Trim();
        if (string.IsNullOrWhiteSpace(poCode))
            return;

        var order = await db.PurchaseOrders
            .Include(o => o.Items)
            .FirstOrDefaultAsync(o => o.Code == poCode, ct);
        if (order is null)
            return;

        if (!string.Equals(order.OrderPlacementStatus, PurchaseOrderPlacementStatuses.Placed, StringComparison.OrdinalIgnoreCase))
            return;

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

        var anyApplied = false;
        foreach (var item in import.Items)
        {
            var poItem = MatchPoItem(order.Items, item);
            if (poItem is null)
                continue;

            var remaining = poItem.Ordered - poItem.Received;
            if (remaining <= 0)
                continue;

            var receiveQty = Math.Min(item.Quantity, remaining);
            if (receiveQty <= 0)
                continue;

            poItem.Received += receiveQty;
            anyApplied = true;

            if (poItem.PlanLineId is int planLineId && planLines.TryGetValue(planLineId, out var planLine))
            {
                planLine.ReceivedQty += receiveQty;
                planLine.ActualReceiptDate ??= DateOnly.FromDateTime(DateTime.UtcNow);
                planLine.PurchaseStatus = ResolvePurchaseStatus(planLine);
            }
        }

        if (!anyApplied)
            return;

        order.DeliveryStatus = ResolveDeliveryStatus(order.Items);
        order.UpdatedAt = DateTime.UtcNow;
    }

    public async Task<PoImportPreviewDto> PreviewImportFromPoAsync(
        string poCode,
        string? excludeTicketCode = null,
        CancellationToken ct = default)
    {
        var order = await LoadPlacedOrderAsync(poCode, ct);
        var reserved = await GetPendingReservedQtyByPoItemAsync(order.Code, excludeTicketCode, ct);
        var pendingDefect = await GetPendingDefectQtyByPoItemAsync(order.Code, null, ct);

        var lines = order.Items
            .OrderBy(i => i.Id)
            .Select(line =>
            {
                reserved.TryGetValue(line.Id, out var pendingQty);
                pendingDefect.TryGetValue(line.Id, out var defectPending);
                var available = Math.Max(0, line.Ordered - line.Received - line.Defected - pendingQty - defectPending);
                return new
                {
                    Line = line,
                    Available = available,
                    PendingQty = pendingQty,
                };
            })
            .Where(x => x.Available > 0 || x.Line.Ordered > x.Line.Received + x.Line.Defected)
            .Select(x => new PoImportLinePreviewDto(
                PoItemId: x.Line.Id,
                Name: x.Line.Name,
                MaterialCode: x.Line.MaterialCode ?? x.Line.Sku,
                Unit: x.Line.Unit ?? "",
                Ordered: x.Line.Ordered,
                Received: x.Line.Received,
                ReservedPending: x.PendingQty,
                Available: x.Available,
                UnitPrice: x.Line.PoPrice))
            .ToList();

        return new PoImportPreviewDto(
            PoCode: order.Code,
            PlanCode: order.PlanCode,
            ProjectCode: order.ProjectCode ?? "",
            SupplierName: order.SupplierNameSnapshot ?? order.Supplier?.SupplierName ?? "",
            Lines: lines.Where(l => l.Available > 0).ToList());
    }

    public async Task<CreateImportDto> BuildImportDraftFromPoAsync(
        string poCode,
        IReadOnlyList<CreateImportFromPoLineRequest>? selections = null,
        CancellationToken ct = default)
    {
        var order = await LoadPlacedOrderAsync(poCode, ct);
        var reserved = await GetPendingReservedQtyByPoItemAsync(order.Code, excludeTicketCode: null, ct);
        var pendingDefect = await GetPendingDefectQtyByPoItemAsync(order.Code, null, ct);

        var selectionMap = selections is { Count: > 0 }
            ? selections
                .GroupBy(s => s.PoItemId)
                .ToDictionary(g => g.Key, g => g.Sum(x => x.Qty))
            : null;

        IEnumerable<PurchaseOrderItem> candidateItems = order.Items.OrderBy(i => i.Id);
        if (selectionMap is not null)
        {
            var missing = selectionMap.Keys.Where(id => order.Items.All(i => i.Id != id)).ToList();
            if (missing.Count > 0)
                throw new PurchasingConflictException(
                    "Dòng PO không hợp lệ: " + string.Join(", ", missing));
            candidateItems = order.Items.Where(i => selectionMap.ContainsKey(i.Id)).OrderBy(i => i.Id);
        }

        var materials = await db.Materials.AsNoTracking().Where(m => m.IsActive).ToListAsync(ct);
        var byCode = materials
            .Where(m => !string.IsNullOrWhiteSpace(m.MaterialCode))
            .ToDictionary(m => m.MaterialCode.Trim(), m => m, StringComparer.OrdinalIgnoreCase);
        var byName = materials
            .GroupBy(m => NormalizeName(m.MaterialName), StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.Ordinal);

        var unresolved = new List<string>();
        var importItems = new List<ImportItemDto>();

        foreach (var line in candidateItems)
        {
            reserved.TryGetValue(line.Id, out var pendingQty);
            pendingDefect.TryGetValue(line.Id, out var defectPending);
            var available = Math.Max(0, line.Ordered - line.Received - line.Defected - pendingQty - defectPending);
            if (available <= 0)
            {
                if (selectionMap is not null)
                    throw new PurchasingConflictException(
                        $"Mặt hàng '{line.Name}' không còn SL để nhập (đã nhận / lỗi / đang chờ duyệt).");
                continue;
            }

            var qty = selectionMap is not null ? selectionMap[line.Id] : available;
            if (qty <= 0)
                throw new PurchasingConflictException($"SL nhập không hợp lệ cho '{line.Name}'.");
            if (qty > available)
                throw new PurchasingConflictException(
                    $"'{line.Name}': chỉ còn {available:0.##} (đã trừ NK chờ duyệt), không nhập được {qty:0.##}.");

            Material? material = null;
            if (!string.IsNullOrWhiteSpace(line.MaterialCode) && byCode.TryGetValue(line.MaterialCode.Trim(), out var byMatCode))
                material = byMatCode;
            else if (!string.IsNullOrWhiteSpace(line.Sku) && byCode.TryGetValue(line.Sku.Trim(), out var bySku))
                material = bySku;
            else if (byName.TryGetValue(NormalizeName(line.Name), out var byMatName))
                material = byMatName;

            if (material is null)
            {
                unresolved.Add(line.Name);
                continue;
            }

            importItems.Add(new ImportItemDto(
                ProductId: material.MaterialCode,
                Sku: material.MaterialCode,
                Name: material.MaterialName,
                Qty: qty,
                Unit: string.IsNullOrWhiteSpace(line.Unit) ? (material.Unit ?? "") : line.Unit,
                UnitPrice: line.PoPrice,
                Supplier: order.SupplierNameSnapshot ?? order.Supplier?.SupplierName,
                SupplierId: order.SupplierId,
                Notes: $"{PoItemNotePrefix}{line.Id}"));
        }

        if (unresolved.Count > 0)
            throw new PurchasingConflictException(
                "Không khớp vật tư kho cho: " + string.Join("; ", unresolved) +
                ". Kiểm tra MaterialCode trên KHMH/PO (hoặc tạo vật tư trùng mã/tên) rồi thử lại.");

        if (importItems.Count == 0)
            throw new PurchasingConflictException("Không có dòng nhập hợp lệ từ PO.");

        var total = Math.Round(importItems.Sum(i => i.Qty * i.UnitPrice), 2);
        var supplierName = order.SupplierNameSnapshot ?? order.Supplier?.SupplierName ?? "";
        var planNote = string.IsNullOrWhiteSpace(order.PlanCode)
            ? $"Nhập kho từ PO {order.Code}"
            : $"Nhập kho từ PO {order.Code} | KHMH {order.PlanCode}";

        return new CreateImportDto(
            VoucherCode: "",
            Date: DateTime.Now.ToString("dd/MM/yyyy"),
            Supplier: supplierName,
            ReferenceCode: order.Code,
            TotalValue: total,
            Status: "draft",
            WarehouseApproval: "pending",
            ApprovedBy: "",
            WarehouseKeeper: "",
            Items: importItems,
            CreatedBy: null,
            Note: planNote,
            WarehouseId: "",
            IsDirectProcurement: false,
            Project: order.ProjectCode,
            PlanCode: order.PlanCode);
    }

    public async Task EnsureImportLinesWithinPoAvailabilityAsync(
        WarehouseTransaction import,
        CancellationToken ct = default)
    {
        var poCode = import.ReferenceCode?.Trim();
        if (string.IsNullOrWhiteSpace(poCode))
            return;

        var hasPoLinks = import.Items.Any(i =>
            !string.IsNullOrWhiteSpace(i.Notes)
            && i.Notes.StartsWith(PoItemNotePrefix, StringComparison.OrdinalIgnoreCase));
        if (!hasPoLinks)
            return;

        var order = await db.PurchaseOrders
            .AsNoTracking()
            .Include(o => o.Items)
            .FirstOrDefaultAsync(o => o.Code == poCode, ct);
        if (order is null)
            return;

        var reserved = await GetPendingReservedQtyByPoItemAsync(poCode, import.TicketCode, ct);
        var pendingDefect = await GetPendingDefectQtyByPoItemAsync(poCode, null, ct);

        foreach (var item in import.Items)
        {
            var poItem = MatchPoItem(order.Items, item);
            if (poItem is null)
                continue;

            reserved.TryGetValue(poItem.Id, out var pendingQty);
            pendingDefect.TryGetValue(poItem.Id, out var defectPending);
            var available = Math.Max(0, poItem.Ordered - poItem.Received - poItem.Defected - pendingQty - defectPending);
            if (item.Quantity > available)
                throw new PurchasingConflictException(
                    $"'{poItem.Name}': chỉ còn {available:0.##} để nhập (đã trừ NK/HL chờ duyệt), phiếu đang yêu cầu {item.Quantity:0.##}.");
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
            throw new PurchasingConflictException("Chỉ tạo phiếu nhập từ PO đã đặt hàng (placed).");

        if (string.IsNullOrWhiteSpace(order.ProjectCode))
            throw new PurchasingConflictException("PO thiếu mã dự án — không thể tạo phiếu nhập.");

        return order;
    }

    /// <summary>
    /// Qty already locked on pending (submitted) NK lines for this PO, keyed by PO item id.
    /// Draft NK does not reserve. Approved qty is already in PO.Received.
    /// </summary>
    private async Task<Dictionary<int, decimal>> GetPendingReservedQtyByPoItemAsync(
        string poCode,
        string? excludeTicketCode,
        CancellationToken ct)
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
        string poCode,
        string? excludeTicketCode,
        CancellationToken ct)
    {
        var pending = await db.PurchaseDefects
            .AsNoTracking()
            .Include(d => d.Items)
            .Where(d =>
                d.PoCode == poCode
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

    private static PurchaseOrderItem? MatchPoItem(
        IEnumerable<PurchaseOrderItem> items,
        WarehouseTransactionItem importLine)
    {
        if (!string.IsNullOrWhiteSpace(importLine.Notes)
            && importLine.Notes.StartsWith(PoItemNotePrefix, StringComparison.OrdinalIgnoreCase)
            && int.TryParse(importLine.Notes[PoItemNotePrefix.Length..], out var poItemId))
        {
            return items.FirstOrDefault(i => i.Id == poItemId);
        }

        var code = importLine.MaterialCode?.Trim() ?? "";
        if (!string.IsNullOrEmpty(code))
        {
            var byCode = items.FirstOrDefault(i =>
                (!string.IsNullOrWhiteSpace(i.MaterialCode)
                    && string.Equals(i.MaterialCode.Trim(), code, StringComparison.OrdinalIgnoreCase))
                || (!string.IsNullOrWhiteSpace(i.Sku)
                    && string.Equals(i.Sku.Trim(), code, StringComparison.OrdinalIgnoreCase)));
            if (byCode is not null)
                return byCode;
        }

        var name = NormalizeName(importLine.MaterialNameSnapshot);
        if (string.IsNullOrEmpty(name))
            return null;

        return items.FirstOrDefault(i => NormalizeName(i.Name) == name);
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
        if (closed >= line.Quantity && line.Quantity > 0)
            return PurchaseLineStatuses.Delivered;
        return line.PurchaseStatus ?? PurchaseLineStatuses.NotPurchased;
    }

    private static string NormalizeName(string? name) =>
        (name ?? string.Empty).Trim().ToLowerInvariant();
}
