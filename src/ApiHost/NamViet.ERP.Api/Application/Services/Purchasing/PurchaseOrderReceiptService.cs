using Microsoft.EntityFrameworkCore;
using NamViet.ERP.Api.Application.Dtos.Purchasing;
using NamViet.ERP.Api.Application.Dtos.Wms;
using NamViet.ERP.Modules.Warehouse.Domain;
using NamViet.ERP.Modules.Warehouse.Domain.Entities;
using NamViet.ERP.Modules.Warehouse.Domain.Enums;
using NamViet.ERP.Modules.Warehouse.Infrastructure.Persistence;

namespace NamViet.ERP.Api.Application.Services.Purchasing;

public interface IPurchaseOrderReceiptService
{
    /// <summary>
    /// After warehouse import is approved: set PO.Received / plan.ReceivedQty and DeliveryStatus
    /// from the sum of approved NK lines (idempotent; does not require PO to be placed).
    /// </summary>
    Task ApplyApprovedImportReceiptAsync(WarehouseTransaction import, CancellationToken ct = default);

    /// <summary>
    /// Heal PO.Received / DeliveryStatus from approved NK even when Apply was skipped historically.
    /// </summary>
    Task ReconcileReceivedFromApprovedImportsAsync(string poCode, CancellationToken ct = default);

    /// <summary>
    /// Preview remaining PO lines: Ordered − max(PO.Received, approved NK) − Defected − pending NK − pending HL.
    /// </summary>
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
        bool isDirectProcurement = false,
        bool ignorePoAvailability = false,
        CancellationToken ct = default);

    /// <summary>
    /// Ensures import lines linked to a PO do not exceed remaining qty.
    /// Matches by poItem: notes, material code, or name. Draft NK does not reserve.
    /// When <paramref name="forApproveSettlement"/> is true, compares against
    /// AcceptedQty + DefectQty (SL thực tế + lỗi) instead of Planned Quantity.
    /// </summary>
    Task EnsureImportLinesWithinPoAvailabilityAsync(
        WarehouseTransaction import,
        CancellationToken ct = default,
        bool forApproveSettlement = false);
}

public sealed class PurchaseOrderReceiptService(
    WarehouseDbContext db,
    IPurchaseWarrantyService warranties) : IPurchaseOrderReceiptService
{
    public const string PoItemNotePrefix = "poItem:";

    public static bool IsActualReceipt(WarehouseTransaction import) =>
        import.ApprovalStatus == ApprovalStatus.Approved
        || import.WarehouseApprovalStatus == WarehouseKeeperApprovalStatus.Approved;

    /// <summary>
    /// Xuất thẳng lấy từ kho (WarehouseId) không quyết toán PO.Received / Còn lại.
    /// Nguồn kho dự án (WarehouseId trống) vẫn quyết toán như cũ.
    /// </summary>
    public static bool IsIssueFromGeneralWarehouse(WarehouseTransaction import) =>
        import.IsDirectProcurement && !string.IsNullOrWhiteSpace(import.WarehouseId);

    public static bool SettlesPurchaseOrder(WarehouseTransaction import) =>
        !IsIssueFromGeneralWarehouse(import);

    public async Task ApplyApprovedImportReceiptAsync(WarehouseTransaction import, CancellationToken ct = default)
    {
        if (IsIssueFromGeneralWarehouse(import))
            return;

        var poCode = import.ReferenceCode?.Trim();
        if (string.IsNullOrWhiteSpace(poCode))
            return;

        await ReconcileReceivedFromApprovedImportsAsync(poCode, import, ct);
        await warranties.UpsertFromApprovedImportAsync(import, ct);
    }

    public Task ReconcileReceivedFromApprovedImportsAsync(string poCode, CancellationToken ct = default) =>
        ReconcileReceivedFromApprovedImportsAsync(poCode, extraImport: null, ct);

    public async Task<PoImportPreviewDto> PreviewImportFromPoAsync(
        string poCode,
        string? excludeTicketCode = null,
        CancellationToken ct = default)
    {
        await ReconcileReceivedFromApprovedImportsAsync(poCode.Trim(), extraImport: null, ct);
        await db.SaveChangesAsync(ct);

        var order = await LoadPlacedOrderAsync(poCode, ct);
        var consumption = await GetConsumptionByPoItemAsync(order, excludeTicketCode, ct);
        var generalAvailableByCode = await LoadGeneralAvailableByMaterialAsync(ct);

        var activeMaterialCodes = await db.Materials.AsNoTracking()
            .Where(m => m.IsActive && !string.IsNullOrWhiteSpace(m.MaterialCode))
            .Select(m => m.MaterialCode.Trim())
            .ToListAsync(ct);
        var activeMaterialSet = new HashSet<string>(activeMaterialCodes, StringComparer.OrdinalIgnoreCase);

        var lines = order.Items
            .Where(line =>
            {
                var materialCode = (line.MaterialCode ?? line.Sku)?.Trim();
                if (string.IsNullOrWhiteSpace(materialCode) || !activeMaterialSet.Contains(materialCode))
                    return false;
                if (line.PlanLine?.LineType is PurchasePlanLineTypes.Service or PurchasePlanLineTypes.Expense)
                    return false;
                return true;
            })
            .OrderBy(i => i.Id)
            .Select(line =>
            {
                var row = consumption[line.Id];
                var materialCode = line.MaterialCode ?? line.Sku;
                var generalAvailable = LookupGeneralAvailable(generalAvailableByCode, materialCode);
                return new PoImportLinePreviewDto(
                    PoItemId: line.Id,
                    Name: line.Name,
                    MaterialCode: materialCode,
                    Unit: line.Unit ?? "",
                    Ordered: line.Ordered,
                    Received: row.EffectiveReceived,
                    ReservedPending: row.PendingQty,
                    Available: row.Available,
                    UnitPrice: line.PoPrice,
                    GeneralAvailableQty: generalAvailable);
            })
            .ToList();

        return new PoImportPreviewDto(
            PoCode: order.Code,
            PlanCode: order.PlanCode,
            ProjectCode: order.ProjectCode ?? "",
            SupplierName: order.SupplierNameSnapshot ?? order.Supplier?.SupplierName ?? "",
            Lines: lines);
    }

    public async Task<CreateImportDto> BuildImportDraftFromPoAsync(
        string poCode,
        IReadOnlyList<CreateImportFromPoLineRequest>? selections = null,
        bool isDirectProcurement = false,
        bool ignorePoAvailability = false,
        CancellationToken ct = default)
    {
        await ReconcileReceivedFromApprovedImportsAsync(poCode.Trim(), extraImport: null, ct);
        await db.SaveChangesAsync(ct);

        var order = await LoadPlacedOrderAsync(poCode, ct);
        var consumption = await GetConsumptionByPoItemAsync(order, excludeTicketCode: null, ct);

        var selectionMap = selections is { Count: > 0 }
            ? selections
                .GroupBy(s => s.PoItemId)
                .ToDictionary(g => g.Key, g => g.Sum(x => x.Qty))
            : null;

        IEnumerable<PurchaseOrderItem> candidateItems = order.Items
            .Where(i => (i.PlanLine == null || i.PlanLine.LineType == PurchasePlanLineTypes.Material)
                     && !string.IsNullOrWhiteSpace(i.MaterialCode ?? i.Sku))
            .OrderBy(i => i.Id);
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
            var available = consumption[line.Id].Available;
            if (!ignorePoAvailability && available <= 0)
            {
                if (selectionMap is not null)
                    throw new PurchasingConflictException(
                        $"Mặt hàng '{line.Name}' không còn số lượng để nhập trên PO (đã nhận đủ hoặc đang bị giữ bởi NK/HL chờ duyệt).");
                continue;
            }

            var qty = selectionMap is not null ? selectionMap[line.Id] : available;
            if (qty <= 0)
            {
                if (ignorePoAvailability && selectionMap is null)
                    continue;
                throw new PurchasingConflictException($"Số lượng nhập không hợp lệ cho '{line.Name}'.");
            }
            if (!ignorePoAvailability && qty > available)
                throw new PurchasingConflictException(
                    $"'{line.Name}': trên PO chỉ còn {available:0.##} để nhập (đã trừ NK đã duyệt và NK/HL đang chờ). Không thể nhập {qty:0.##}.");

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
            throw new PurchasingConflictException(
                "PO đã nhập đủ (phiếu thực tế / đang chờ duyệt) - không còn SL để tạo phiếu nhập.");

        var total = Math.Round(importItems.Sum(i => i.Qty * i.UnitPrice), 2);
        var supplierName = order.SupplierNameSnapshot ?? order.Supplier?.SupplierName ?? "";
        var planNote = isDirectProcurement
            ? (string.IsNullOrWhiteSpace(order.PlanCode)
                ? $"Xuất thẳng công trường từ PO {order.Code}"
                : $"Xuất thẳng công trường từ PO {order.Code} | KHMH {order.PlanCode}")
            : (string.IsNullOrWhiteSpace(order.PlanCode)
                ? $"Nhập kho từ PO {order.Code}"
                : $"Nhập kho từ PO {order.Code} | KHMH {order.PlanCode}");

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
            IsDirectProcurement: isDirectProcurement,
            Project: order.ProjectCode,
            PlanCode: order.PlanCode);
    }

    public async Task EnsureImportLinesWithinPoAvailabilityAsync(
        WarehouseTransaction import,
        CancellationToken ct = default,
        bool forApproveSettlement = false)
    {
        if (IsIssueFromGeneralWarehouse(import))
            return;

        var poCode = import.ReferenceCode?.Trim();
        if (string.IsNullOrWhiteSpace(poCode))
            return;

        var order = await db.PurchaseOrders
            .AsNoTracking()
            .Include(o => o.Items)
            .FirstOrDefaultAsync(o => o.Code == poCode, ct);
        if (order is null)
            return;

        var consumption = await GetConsumptionByPoItemAsync(order, import.TicketCode, ct);

        foreach (var item in import.Items)
        {
            var poItem = MatchPoItem(order.Items, item);
            if (poItem is null)
                continue;

            var available = consumption[poItem.Id].Available;
            // Duyệt: chỉ SL thực tế + lỗi chiếm phần còn lại trên PO (thiếu không nhập kho).
            // Tạo/chốt: so với SL trên phiếu (Quantity / dự kiến).
            var requested = forApproveSettlement
                ? (item.AcceptedQty ?? 0) + (item.DefectQty ?? 0)
                : item.Quantity;

            if (requested <= 0)
                continue;

            if (requested > available)
            {
                if (forApproveSettlement)
                {
                    throw new PurchasingConflictException(
                        $"'{poItem.Name}': trên PO chỉ còn {available:0.##} để nhập kho (đã trừ NK đã duyệt và NK/HL đang chờ). " +
                        $"Phiếu này đang duyệt SL thực tế + lỗi = {requested:0.##}.");
                }

                throw new PurchasingConflictException(
                    $"'{poItem.Name}': trên PO chỉ còn {available:0.##} để nhập (đã trừ NK đã duyệt và NK/HL đang chờ). " +
                    $"Phiếu đang ghi SL = {requested:0.##}.");
            }
        }
    }

    private async Task ReconcileReceivedFromApprovedImportsAsync(
        string poCode,
        WarehouseTransaction? extraImport,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(poCode))
            return;

        var order = await db.PurchaseOrders
            .Include(o => o.Items)
            .FirstOrDefaultAsync(o => o.Code == poCode, ct);
        if (order is null)
            return;

        var imports = await db.WarehouseTransactions
            .Include(t => t.Items)
            .Where(t => t.TransactionType == TransactionType.Import && t.ReferenceCode == poCode)
            .ToListAsync(ct);

        if (extraImport is not null
            && imports.All(t => !string.Equals(t.TicketCode, extraImport.TicketCode, StringComparison.OrdinalIgnoreCase)))
        {
            imports.Add(extraImport);
        }

        var approvedQty = SumQtyByPoItem(order, imports.Where(IsActualReceipt).Where(SettlesPurchaseOrder));

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

        var anyChange = false;
        foreach (var poItem in order.Items)
        {
            var fromNk = approvedQty.GetValueOrDefault(poItem.Id);
            var nextReceived = Math.Max(poItem.Received, fromNk);
            var delta = nextReceived - poItem.Received;
            if (delta <= 0)
                continue;

            poItem.Received = nextReceived;
            anyChange = true;

            if (poItem.PlanLineId is int planLineId && planLines.TryGetValue(planLineId, out var planLine))
            {
                planLine.ReceivedQty += delta;
                planLine.ActualReceiptDate ??= DateOnly.FromDateTime(DateTime.UtcNow);
                planLine.PurchaseStatus = ResolvePurchaseStatus(planLine);
            }
        }

        var nextStatus = ResolveDeliveryStatus(order.Items);
        if (anyChange || !string.Equals(order.DeliveryStatus, nextStatus, StringComparison.OrdinalIgnoreCase))
        {
            order.DeliveryStatus = nextStatus;
            order.UpdatedAt = DateTime.UtcNow;
        }
    }

    private async Task<PurchaseOrder> LoadPlacedOrderAsync(string poCode, CancellationToken ct)
    {
        var code = poCode.Trim();
        var order = await db.PurchaseOrders
            .AsNoTracking()
            .Include(o => o.Items)
                .ThenInclude(i => i.PlanLine)
            .Include(o => o.Supplier)
            .FirstOrDefaultAsync(o => o.Code == code, ct)
            ?? throw new PurchasingConflictException($"Không tìm thấy PO '{code}'.");

        if (!string.Equals(order.OrderPlacementStatus, PurchaseOrderPlacementStatuses.Placed, StringComparison.OrdinalIgnoreCase))
            throw new PurchasingConflictException("Chỉ tạo phiếu nhập từ PO đã đặt hàng (placed).");

        if (string.IsNullOrWhiteSpace(order.ProjectCode))
            throw new PurchasingConflictException("PO thiếu mã dự án - không thể tạo phiếu nhập.");

        return order;
    }

    private async Task<Dictionary<int, PoItemConsumption>> GetConsumptionByPoItemAsync(
        PurchaseOrder order,
        string? excludeTicketCode,
        CancellationToken ct)
    {
        var imports = await db.WarehouseTransactions
            .AsNoTracking()
            .Include(t => t.Items)
            .Where(t =>
                t.TransactionType == TransactionType.Import
                && t.ReferenceCode == order.Code
                && (excludeTicketCode == null || t.TicketCode != excludeTicketCode))
            .ToListAsync(ct);

        var approved = SumQtyByPoItem(order, imports.Where(IsActualReceipt).Where(SettlesPurchaseOrder));
        var pending = SumQtyByPoItem(
            order,
            imports.Where(t =>
                t.ApprovalStatus == ApprovalStatus.Pending
                && !IsActualReceipt(t)
                && SettlesPurchaseOrder(t)));
        var pendingDefect = await GetPendingDefectQtyByPoItemAsync(order.Code, null, ct);

        var map = new Dictionary<int, PoItemConsumption>();
        foreach (var line in order.Items)
        {
            var approvedQty = approved.GetValueOrDefault(line.Id);
            var pendingQty = pending.GetValueOrDefault(line.Id);
            var defectPending = pendingDefect.GetValueOrDefault(line.Id);
            var effectiveReceived = Math.Max(line.Received, approvedQty);
            var available = Math.Max(
                0,
                line.Ordered - effectiveReceived - line.Defected - pendingQty - defectPending);
            map[line.Id] = new PoItemConsumption(
                approvedQty,
                pendingQty,
                effectiveReceived,
                available);
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

    private static Dictionary<int, decimal> SumQtyByPoItem(
        PurchaseOrder order,
        IEnumerable<WarehouseTransaction> imports)
    {
        var map = new Dictionary<int, decimal>();
        foreach (var import in imports)
        {
            foreach (var item in import.Items)
            {
                var poItem = MatchPoItem(order.Items, item);
                if (poItem is null)
                    continue;
                map[poItem.Id] = map.GetValueOrDefault(poItem.Id) + item.Quantity;
            }
        }

        return map;
    }

    public static PurchaseOrderItem? MatchPoItem(
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

    private async Task<Dictionary<string, decimal>> LoadGeneralAvailableByMaterialAsync(CancellationToken ct)
    {
        var setting = await db.WmsSettings.AsNoTracking()
            .FirstOrDefaultAsync(s => s.Key == WmsSettingsKeys.GeneralWarehouseId, ct);
        var generalWarehouseId = setting?.Value?.Trim().Trim('"');
        if (string.IsNullOrWhiteSpace(generalWarehouseId))
            generalWarehouseId = WmsWarehouseIds.General;

        var inventories = await db.WarehouseInventories.AsNoTracking()
            .Where(i => i.WarehouseId == generalWarehouseId)
            .Select(i => new { i.MaterialCode, i.Qty })
            .ToListAsync(ct);
        var actualByCode = inventories
            .Where(i => !string.IsNullOrWhiteSpace(i.MaterialCode))
            .GroupBy(i => i.MaterialCode.Trim(), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.Sum(x => x.Qty), StringComparer.OrdinalIgnoreCase);

        var reservations = await db.InventoryReservations.AsNoTracking()
            .Where(r => r.WarehouseId == generalWarehouseId && r.Status == ReservationStatus.Holding)
            .Select(r => new { r.MaterialCode, r.ReservedQty })
            .ToListAsync(ct);
        var reservedByCode = reservations
            .Where(r => !string.IsNullOrWhiteSpace(r.MaterialCode))
            .GroupBy(r => r.MaterialCode.Trim(), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.Sum(x => x.ReservedQty), StringComparer.OrdinalIgnoreCase);

        var available = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase);
        foreach (var (code, actual) in actualByCode)
        {
            reservedByCode.TryGetValue(code, out var reserved);
            available[code] = Math.Max(0m, actual - reserved);
        }

        return available;
    }

    private static decimal LookupGeneralAvailable(
        IReadOnlyDictionary<string, decimal> byCode,
        string? materialCode)
    {
        var code = materialCode?.Trim();
        if (string.IsNullOrWhiteSpace(code))
            return 0m;
        return byCode.TryGetValue(code, out var qty) ? qty : 0m;
    }

    private static string NormalizeName(string? name) =>
        (name ?? string.Empty).Trim().ToLowerInvariant();

    private readonly record struct PoItemConsumption(
        decimal ApprovedQty,
        decimal PendingQty,
        decimal EffectiveReceived,
        decimal Available);
}
