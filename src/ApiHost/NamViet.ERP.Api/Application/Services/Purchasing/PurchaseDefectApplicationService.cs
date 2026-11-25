using Microsoft.EntityFrameworkCore;
using NamViet.ERP.Api.Application.Dtos.Purchasing;
using NamViet.ERP.Api.Services.Storage;
using NamViet.ERP.Modules.Warehouse.Domain.Entities;
using NamViet.ERP.Modules.Warehouse.Domain.Services;
using NamViet.ERP.Modules.Warehouse.Infrastructure.Persistence;
using NamViet.ERP.SharedKernel.Common;
using NamViet.ERP.SharedKernel.Interfaces;

namespace NamViet.ERP.Api.Application.Services.Purchasing;

public interface IPurchaseDefectApplicationService
{
    Task<List<PurchaseDefectDto>> ListAsync(CancellationToken ct = default);
    Task<PurchaseDefectDto?> GetByIdAsync(string id, CancellationToken ct = default);
    Task<IReadOnlyList<PoDefectEligiblePurchaseOrderDto>> ListEligiblePurchaseOrdersAsync(CancellationToken ct = default);
    Task<PoDefectPreviewDto> PreviewFromPoAsync(string poCode, CancellationToken ct = default);
    Task<PurchaseDefectDto> CreateFromPoAsync(string poCode, CreateDefectFromPoRequest request, CancellationToken ct = default);
    Task<StockDefectPreviewDto> PreviewFromStockAsync(string projectCode, CancellationToken ct = default);
    Task<PurchaseDefectDto> CreateFromStockAsync(CreateDefectFromStockRequest request, CancellationToken ct = default);
    /// <summary>
    /// Tạo + duyệt HL từ kiểm nhận NK (đã set DefectQty trên dòng). Trả mã HL hoặc null nếu không có lỗi.
    /// </summary>
    Task<string?> CreateAndApproveFromImportAsync(
        WarehouseTransaction import,
        string? defectReason,
        CancellationToken ct = default);
    Task<PurchaseDefectDto?> SubmitAsync(string id, CancellationToken ct = default);
    Task<PurchaseDefectDto?> ApproveAsync(string id, CancellationToken ct = default);
    Task<PurchaseDefectDto?> UpdateReasonAsync(string id, string? reason, CancellationToken ct = default);
    Task<PurchaseDefectDto?> UpdateItemsAsync(string id, UpdateDefectItemsRequest request, CancellationToken ct = default);
    Task<PurchaseDefectDto?> CancelAsync(string id, CancellationToken ct = default);
    Task<PurchaseDefectDto?> UpdateProcessingAsync(string id, UpdateDefectProcessingRequest request, CancellationToken ct = default);
    Task<PurchaseDefectAttachmentDto?> UploadAttachmentAsync(
        string id, string kind, string fileName, string contentType, Stream content, CancellationToken ct = default);
    Task<PurchaseDefectAttachmentDto?> GetAttachmentAsync(
        string id, Guid attachmentId, CancellationToken ct = default);
    Task<bool> DeleteAttachmentAsync(string id, Guid attachmentId, CancellationToken ct = default);
    Task<bool> DeleteAsync(string id, CancellationToken ct = default);
}

public sealed partial class PurchaseDefectApplicationService(
    WarehouseDbContext db,
    ICurrentUser currentUser,
    IFileStorage fileStorage,
    IInventoryDomainService inventory) : IPurchaseDefectApplicationService
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

    public async Task<IReadOnlyList<PoDefectEligiblePurchaseOrderDto>> ListEligiblePurchaseOrdersAsync(
        CancellationToken ct = default)
    {
        var orders = await db.PurchaseOrders
            .AsNoTracking()
            .Include(o => o.Items)
            .Include(o => o.Supplier)
            .Where(o =>
                o.OrderPlacementStatus == PurchaseOrderPlacementStatuses.Placed
                && o.ProjectCode != null
                && o.ProjectCode != "")
            .OrderByDescending(o => o.CreatedAt)
            .ToListAsync(ct);

        if (orders.Count == 0)
            return [];

        var poCodes = orders.Select(o => o.Code).ToList();
        var pendingNkByPoItem = await GetPendingNkQtyByPoItemsAsync(poCodes, ct);
        var pendingHlByPoItem = await GetPendingDefectQtyByPoItemsAsync(poCodes, ct);

        var result = new List<PoDefectEligiblePurchaseOrderDto>();
        foreach (var order in orders)
        {
            decimal availableTotal = 0;
            foreach (var line in order.Items)
            {
                pendingNkByPoItem.TryGetValue(line.Id, out var nkQty);
                pendingHlByPoItem.TryGetValue(line.Id, out var hlQty);
                availableTotal += Math.Max(0, line.Ordered - line.Received - line.Defected - nkQty - hlQty);
            }

            if (availableTotal <= 0)
                continue;

            result.Add(new PoDefectEligiblePurchaseOrderDto(
                PoCode: order.Code,
                PlanCode: order.PlanCode,
                ProjectCode: order.ProjectCode ?? "",
                SupplierName: order.SupplierNameSnapshot ?? order.Supplier?.SupplierName ?? "",
                AvailableQty: availableTotal));
        }

        return result;
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
                    $"'{line.Name}': trên PO chỉ còn {available:0.##} để ghi lỗi (đã trừ NK/HL đang chờ). Không thể ghi {sel.Qty:0.##}.");

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

        var ticket = await NextTicketCodeAsync(ct);
        var defect = new PurchaseDefect
        {
            TicketCode = ticket,
            SourceType = PurchaseDefectSourceTypes.Po,
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

    public async Task<StockDefectPreviewDto> PreviewFromStockAsync(string projectCode, CancellationToken ct = default)
    {
        var code = projectCode.Trim();
        if (string.IsNullOrWhiteSpace(code))
            throw new PurchasingConflictException("Chọn kho dự án.");

        var projectExists = await db.Projects.AsNoTracking()
            .AnyAsync(p => p.ProjectCode == code && p.DeletedAt == null, ct);
        if (!projectExists)
            throw new PurchasingConflictException($"Không tìm thấy kho dự án '{code}'.");

        var pendingByMaterial = await inventory.GetPendingStockDefectQtyByMaterialAsync(code, null, ct);

        var rows = await db.ProjectInventories
            .AsNoTracking()
            .Include(pi => pi.Material)
            .Where(pi => pi.ProjectCode == code && pi.DeletedAt == null)
            .ToListAsync(ct);

        var lines = rows
            .Select(pi =>
            {
                var onHand = pi.ActualInventoryQty;
                var availableBase = pi.AvailableQty;
                pendingByMaterial.TryGetValue(pi.MaterialCode, out var pendingHl);
                var available = Math.Max(0, availableBase - pendingHl);
                return new StockDefectLinePreviewDto(
                    MaterialCode: pi.MaterialCode,
                    Name: pi.Material?.MaterialName ?? pi.MaterialCode,
                    Unit: pi.Material?.Unit ?? "",
                    OnHand: onHand,
                    ReservedPendingDefect: pendingHl,
                    Available: available);
            })
            .Where(l => l.Available > 0)
            .OrderBy(l => l.Name)
            .ToList();

        return new StockDefectPreviewDto(ProjectCode: code, Lines: lines);
    }

    public async Task<PurchaseDefectDto> CreateFromStockAsync(
        CreateDefectFromStockRequest request,
        CancellationToken ct = default)
    {
        if (request.Items is null || request.Items.Count == 0)
            throw new PurchasingConflictException("Chọn ít nhất một dòng hàng lỗi.");

        var projectCode = (request.ProjectCode ?? "").Trim();
        if (string.IsNullOrWhiteSpace(projectCode))
            throw new PurchasingConflictException("Chọn kho dự án.");

        var projectExists = await db.Projects.AsNoTracking()
            .AnyAsync(p => p.ProjectCode == projectCode && p.DeletedAt == null, ct);
        if (!projectExists)
            throw new PurchasingConflictException($"Không tìm thấy kho dự án '{projectCode}'.");

        var selectionMap = request.Items
            .Where(i => !string.IsNullOrWhiteSpace(i.MaterialCode))
            .GroupBy(i => i.MaterialCode.Trim(), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

        if (selectionMap.Count == 0)
            throw new PurchasingConflictException("Chọn ít nhất một dòng hàng lỗi.");

        var codes = selectionMap.Keys.ToList();
        var inventories = await db.ProjectInventories
            .AsNoTracking()
            .Include(pi => pi.Material)
            .Where(pi => pi.ProjectCode == projectCode && codes.Contains(pi.MaterialCode) && pi.DeletedAt == null)
            .ToListAsync(ct);

        var pendingByMaterial = await inventory.GetPendingStockDefectQtyByMaterialAsync(projectCode, null, ct);
        var defectItems = new List<PurchaseDefectItem>();

        foreach (var code in codes)
        {
            var sel = selectionMap[code];
            var inv = inventories.FirstOrDefault(i =>
                string.Equals(i.MaterialCode, code, StringComparison.OrdinalIgnoreCase));
            if (inv is null)
                throw new PurchasingConflictException($"Vật tư '{code}' không có tồn tại kho dự án.");

            pendingByMaterial.TryGetValue(inv.MaterialCode, out var pendingHl);
            var available = Math.Max(0, inv.AvailableQty - pendingHl);
            if (sel.Qty <= 0)
                throw new PurchasingConflictException($"SL lỗi không hợp lệ cho '{inv.Material?.MaterialName ?? code}'.");
            if (sel.Qty > available)
                throw new PurchasingConflictException(
                    $"'{inv.Material?.MaterialName ?? code}': chỉ còn {available:0.##} khả dụng, không ghi lỗi {sel.Qty:0.##}.");

            defectItems.Add(new PurchaseDefectItem
            {
                PoItemId = 0,
                MaterialCode = inv.MaterialCode,
                Name = inv.Material?.MaterialName ?? inv.MaterialCode,
                Unit = inv.Material?.Unit ?? "",
                Quantity = sel.Qty,
                LineReason = string.IsNullOrWhiteSpace(sel.LineReason) ? null : sel.LineReason.Trim(),
            });
        }

        var ticket = await NextTicketCodeAsync(ct);
        var defect = new PurchaseDefect
        {
            TicketCode = ticket,
            SourceType = PurchaseDefectSourceTypes.Stock,
            PoCode = null,
            PlanCode = "",
            ProjectCode = projectCode,
            SupplierName = null,
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

    private async Task<string> NextTicketCodeAsync(CancellationToken ct)
    {
        var codes = await db.PurchaseDefects.IgnoreQueryFilters()
            .Select(d => d.TicketCode)
            .ToListAsync(ct);
        var seq = SequentialCodeHelper.NextSequence(codes, "HL-");
        var ticket = PurchaseDefect.GenerateTicketCode(seq);
        while (await db.PurchaseDefects.IgnoreQueryFilters().AnyAsync(d => d.TicketCode == ticket, ct))
        {
            seq++;
            ticket = PurchaseDefect.GenerateTicketCode(seq);
        }
        return ticket;
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
        SourceType: string.IsNullOrWhiteSpace(d.SourceType) ? PurchaseDefectSourceTypes.Po : d.SourceType,
        PoCode: d.PoCode,
        PlanCode: d.PlanCode,
        ProjectCode: d.ProjectCode,
        SupplierName: d.SupplierName,
        Status: d.Status,
        ProcessingPhase: d.ResolveProcessingPhase(),
        Reason: d.Reason,
        Note: d.Note,
        CauseSource: d.CauseSource,
        SupplierFeedback: d.SupplierFeedback,
        HandlingMethod: d.HandlingMethod,
        CreatedByName: d.CreatedByName,
        CreatedAt: d.CreatedAt,
        ApprovedByName: d.ApprovedByName,
        ApprovedAt: d.ApprovedAt,
        ProcessedByName: d.ProcessedByName,
        ProcessedAt: d.ProcessedAt,
        Items: d.Items.OrderBy(i => i.Id).Select(i => new PurchaseDefectItemDto(
            i.Id, i.PoItemId, i.MaterialCode, i.Name, i.Unit, i.Quantity, i.LineReason)).ToList(),
        Attachments: d.Attachments.OrderBy(a => a.UploadedAt).Select(a => new PurchaseDefectAttachmentDto(
            a.Id, a.Kind, a.FileName, a.StorageKey, a.UploadedAt)).ToList(),
        ImportTicketCode: d.ImportTicketCode);

    private static string NormalizeAttachmentKind(string kind)
    {
        var k = (kind ?? "").Trim().ToLowerInvariant();
        return k is "photo" or "document" ? k : "photo";
    }

    private static void EnsureAllowedExtension(string fileName) =>
        FileUploadValidator.EnsureAllowedExtension(
            fileName,
            FileUploadValidator.DefectAttachmentExtensions,
            msg => new PurchasingConflictException(msg));
}
