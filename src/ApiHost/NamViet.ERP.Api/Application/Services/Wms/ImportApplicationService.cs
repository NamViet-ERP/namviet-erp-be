using Microsoft.EntityFrameworkCore;
using NamViet.ERP.Api.Application.Dtos.Purchasing;
using NamViet.ERP.Api.Application.Dtos.Wms;
using NamViet.ERP.Api.Application.Mappers.Wms;
using NamViet.ERP.Api.Application.Services.Purchasing;
using NamViet.ERP.Api.Application.Services.Shared;
using NamViet.ERP.Modules.Warehouse.Domain.Entities;
using NamViet.ERP.Modules.Warehouse.Domain.Enums;
using NamViet.ERP.Modules.Warehouse.Domain.Services;
using NamViet.ERP.Modules.Warehouse.Infrastructure.Helpers;
using NamViet.ERP.Modules.Warehouse.Infrastructure.Persistence;
using NamViet.ERP.SharedKernel.Authorization;
using NamViet.ERP.SharedKernel.Interfaces;
using NamViet.ERP.Api.Services.Storage;
using NamViet.ERP.SharedKernel.Common;

namespace NamViet.ERP.Api.Application.Services.Wms;

public interface IImportApplicationService
{
    Task<List<ImportDto>> ListAsync(DateOnly? dateFrom = null, DateOnly? dateTo = null, CancellationToken ct = default);
    Task<ImportDto?> GetByIdAsync(string id, CancellationToken ct = default);
    Task<ImportDto> CreateAsync(CreateImportDto dto, CancellationToken ct = default);
    Task<ImportDto?> UpdateAsync(string id, CreateImportDto dto, CancellationToken ct = default);
    Task<ImportDto?> ApproveAsync(
        string id,
        ApproveImportRequest? request = null,
        CancellationToken ct = default);
    Task<ImportDto?> SubmitAsync(string id, CancellationToken ct = default);
    Task<bool> DeleteAsync(string id, CancellationToken ct = default);
    Task<ImportDto> CreateDraftFromPurchaseOrderAsync(
        string poCode,
        CreateImportFromPoRequest? request = null,
        CancellationToken ct = default);
    Task<PoImportPreviewDto> PreviewImportFromPurchaseOrderAsync(
        string poCode,
        string? excludeTicketCode = null,
        CancellationToken ct = default);
    Task<ImportDto?> UpdateReceiptEvaluationAsync(
        string id,
        string? evaluation,
        CancellationToken ct = default);
    Task<ImportAttachmentDto?> UploadAttachmentAsync(
        string id,
        string kind,
        string fileName,
        string contentType,
        Stream content,
        CancellationToken ct = default);
    Task<ImportAttachmentDto?> GetAttachmentAsync(
        string id,
        Guid attachmentId,
        CancellationToken ct = default);
    Task<bool> DeleteAttachmentAsync(
        string id,
        Guid attachmentId,
        CancellationToken ct = default);
}
public sealed class ImportApplicationService(
    WarehouseDbContext db,
    IInventoryDomainService inventory,
    IProjectBudgetService projectBudget,
    IStocktakeFreezeGuard stocktakeFreeze,
    ICurrentUser currentUser,
    IPurchaseOrderReceiptService poReceipt,
    IPurchaseDefectApplicationService defects,
    ISupplierDeliveryEvaluationService deliveryEvaluations,
    IFileStorage fileStorage,
    IActivityEventWriter activity) : IImportApplicationService
{
    public async Task<List<ImportDto>> ListAsync(DateOnly? dateFrom = null, DateOnly? dateTo = null, CancellationToken ct = default)
    {
        await inventory.HealPendingDirectExportsAsync(ct);
        var items = await ImportQuery()
            .WhereCreatedBetween(dateFrom, dateTo)
            .OrderByDescending(t => t.CreatedAt)
            .ToListAsync(ct);
        return items.Select(WmsMapper.ToImport).ToList();
    }

    public async Task<ImportDto?> GetByIdAsync(string id, CancellationToken ct = default)
    {
        var t = await FindImportAsync(id, ct);
        return t is null ? null : WmsMapper.ToImport(t);
    }

    public async Task<ImportDto> CreateAsync(CreateImportDto dto, CancellationToken ct = default)
    {
        await stocktakeFreeze.EnsureProjectsNotFrozenAsync([dto.Project], ct);

        if (dto.Items.Any(i => i.UnitPrice <= 0))
            throw new DomainRuleException("Đơn giá phải lớn hơn 0.");

        var approvalStatus = WmsMapper.FromClientApprovalStatus(dto.Status);

        return await SequentialCodeHelper.ExecuteWithUniqueRetryAsync(async () =>
        {
            db.ChangeTracker.Clear();

            await using var tx = await db.Database.BeginTransactionAsync(ct);
            await TicketCodeAllocator.AcquireTypeLockAsync(db, TransactionType.Import, ct);
            await stocktakeFreeze.EnsureProjectsNotFrozenAsync([dto.Project], ct);

            var ticketCode = string.IsNullOrWhiteSpace(dto.VoucherCode)
                ? await TicketCodeAllocator.NextAsync(db, TransactionType.Import, ct)
                : dto.VoucherCode;

            var transaction = BuildTransaction(dto, ticketCode, approvalStatus);
            transaction.WarehouseId = await ResolveImportWarehouseIdAsync(dto, ct);
            await ValidateAndSnapshotItemsAsync(transaction.Items, ct);
            if (PurchaseOrderReceiptService.SettlesPurchaseOrder(transaction))
                await poReceipt.EnsureImportLinesWithinPoAvailabilityAsync(transaction, ct);

            db.WarehouseTransactions.Add(transaction);

            if (approvalStatus is ApprovalStatus.Draft or ApprovalStatus.Pending)
                await CreateDraftReservationsAsync(transaction, ct);
            if (approvalStatus == ApprovalStatus.Pending)
                await inventory.EnsurePendingDirectExportAsync(transaction, ct);
            else if (approvalStatus == ApprovalStatus.Approved)
            {
                await inventory.ApplyApprovedImportAsync(transaction, ct);
                if (PurchaseOrderReceiptService.SettlesPurchaseOrder(transaction))
                    await poReceipt.ApplyApprovedImportReceiptAsync(transaction, ct);
            }

            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);

            await WritePurchasingImportActivityAsync(
                transaction,
                ActivityActions.ImportCreated,
                $"Tạo phiếu nhập hàng [{transaction.TicketCode}]",
                ct);

            await db.Entry(transaction).Collection(t => t.Items).LoadAsync(ct);
            foreach (var item in transaction.Items)
                await db.Entry(item).Reference(i => i.Material).LoadAsync(ct);
            return WmsMapper.ToImport(transaction);
        }, ct: ct);
    }

    public async Task<ImportDto?> UpdateAsync(string id, CreateImportDto dto, CancellationToken ct = default)
    {
        var t = await FindImportAsync(id, ct);
        if (t is null) return null;
        if (t.ApprovalStatus == ApprovalStatus.Approved)
            throw new DomainRuleException("Phiếu đã duyệt, không thể chỉnh sửa.");

        await stocktakeFreeze.EnsureProjectsNotFrozenAsync([dto.Project], ct);

        await using var tx = await db.Database.BeginTransactionAsync(ct);

        if (HasPlannedInventory(t.ApprovalStatus))
        {
            await inventory.CancelReservationsAsync(t.TicketCode, ct);
            await inventory.CancelProjectImportPlannedAsync(t, ct);
        }

        t.WarehouseId = await ResolveImportWarehouseIdAsync(dto, ct);
        t.ReferenceCode = dto.ReferenceCode;
        if (!string.IsNullOrWhiteSpace(dto.PlanCode))
            t.PurchasePlanCode = dto.PlanCode.Trim();
        t.SupplierName = ResolveHeaderSupplier(dto);
        t.WarehouseKeeper = dto.WarehouseKeeper;
        t.WarehouseApprovalStatus = WmsMapper.FromClientKeeperStatus(dto.WarehouseApproval);
        t.IsDirectProcurement = dto.IsDirectProcurement ?? false;
        t.BeneficiaryProjectCode = dto.Project;
        t.Notes = dto.Note;
        var nextStatus = WmsMapper.FromClientApprovalStatus(dto.Status);
        if (nextStatus == ApprovalStatus.Approved)
            throw new DomainRuleException("Không thể duyệt phiếu qua cập nhật. Hãy dùng thao tác Duyệt kho.");
        t.ApprovalStatus = nextStatus;
        t.UpdatedAt = DateTime.UtcNow;

        var preservePrices = !PriceVisibility.CanViewPrices(currentUser);
        var previousPrices = preservePrices
            ? t.Items
                .GroupBy(i => $"{i.MaterialCode}|{i.Notes ?? ""}", StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.First().UnitPrice, StringComparer.OrdinalIgnoreCase)
            : null;

        if (!preservePrices && dto.Items.Any(i => i.UnitPrice <= 0))
            throw new DomainRuleException("Đơn giá phải lớn hơn 0.");

        db.WarehouseTransactionItems.RemoveRange(t.Items);
        t.Items = dto.Items.Select(i =>
        {
            var materialCode = string.IsNullOrEmpty(i.Sku) ? i.ProductId : i.Sku;
            var key = $"{materialCode}|{i.Notes ?? ""}";
            decimal unitPrice = i.UnitPrice;
            if (preservePrices
                && previousPrices is not null
                && previousPrices.TryGetValue(key, out var kept))
            {
                unitPrice = kept;
            }
            if (unitPrice <= 0)
                throw new DomainRuleException(
                    $"Dòng {materialCode}: thiếu đơn giá đã lưu — liên hệ cung ứng cập nhật phiếu.");
            return new WarehouseTransactionItem
            {
                TicketCode = t.TicketCode,
                MaterialCode = materialCode,
                Quantity = i.Qty,
                PlannedQuantity = i.Qty,
                UnitPrice = unitPrice,
                SupplierId = i.SupplierId,
                SupplierName = ResolveLineSupplierName(i, dto.Supplier),
                Notes = i.Notes,
            };
        }).ToList();
        await ValidateAndSnapshotItemsAsync(t.Items, ct);
        if (PurchaseOrderReceiptService.SettlesPurchaseOrder(t))
            await poReceipt.EnsureImportLinesWithinPoAvailabilityAsync(t, ct);

        if (HasPlannedInventory(t.ApprovalStatus))
            await CreateDraftReservationsAsync(t, ct);
        await inventory.EnsurePendingDirectExportAsync(t, ct);

        await stocktakeFreeze.EnsureProjectsNotFrozenAsync([dto.Project], ct);
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return WmsMapper.ToImport(t);
    }

    public async Task<ImportDto?> ApproveAsync(
        string id,
        ApproveImportRequest? request = null,
        CancellationToken ct = default)
    {
        var confirmOverBudget = request?.ConfirmOverBudget ?? false;
        var t = await FindImportAsync(id, ct);
        if (t is null) return null;
        if (t.ApprovalStatus is ApprovalStatus.Approved or ApprovalStatus.Rejected)
            throw new DomainRuleException(
                t.ApprovalStatus == ApprovalStatus.Approved
                    ? "Phiếu đã duyệt, không thể duyệt lại."
                    : "Phiếu đã từ chối, không thể duyệt lại.");
        if (t.ApprovalStatus != ApprovalStatus.Pending)
            throw new DomainRuleException("Chỉ phiếu đã chốt dự kiến (Pending) mới được duyệt kho.");

        await stocktakeFreeze.EnsureProjectsNotFrozenAsync([t.BeneficiaryProjectCode], ct);
        EnsureApprovalEvidence(t);

        ApplyReceiveSettlement(t, request);
        if (PurchaseOrderReceiptService.SettlesPurchaseOrder(t))
            await poReceipt.EnsureImportLinesWithinPoAvailabilityAsync(t, ct, forApproveSettlement: true);

        await using var tx = await db.Database.BeginTransactionAsync(ct);

        if (HasPlannedInventory(t.ApprovalStatus))
            await inventory.CancelProjectImportPlannedAsync(t, ct);

        foreach (var item in t.Items)
        {
            var planned = item.PlannedQuantity > 0 ? item.PlannedQuantity : item.Quantity;
            item.PlannedQuantity = planned;
            item.Quantity = item.AcceptedQty ?? planned;
        }

        await projectBudget.EnsureImportApprovalWithinBudgetAsync(t, confirmOverBudget, ct);

        t.ApprovalStatus = ApprovalStatus.Approved;
        t.WarehouseApprovalStatus = WarehouseKeeperApprovalStatus.Approved;
        t.ApprovedById = currentUser.Id;
        t.ApprovedByName = currentUser.Name;
        t.ApprovedAt = DateTime.UtcNow;
        t.UpdatedAt = DateTime.UtcNow;

        await inventory.ApplyApprovedImportAsync(t, ct);

        var defectTicket = await defects.CreateAndApproveFromImportAsync(t, request?.DefectReason, ct);
        if (!string.IsNullOrWhiteSpace(defectTicket))
            t.LinkedDefectTicketCode = defectTicket;

        if (PurchaseOrderReceiptService.SettlesPurchaseOrder(t))
        {
            await poReceipt.ApplyApprovedImportReceiptAsync(t, ct);
            await deliveryEvaluations.EnsurePendingFromApprovedImportAsync(t, ct);
        }
        await stocktakeFreeze.EnsureProjectsNotFrozenAsync([t.BeneficiaryProjectCode], ct);
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);

        await WritePurchasingImportActivityAsync(
            t,
            ActivityActions.ImportApproved,
            $"Cập nhật trạng thái duyệt kho phiếu nhập hàng [{t.TicketCode}]",
            ct);

        return WmsMapper.ToImport(t);
    }

    /// <summary>
    /// Gán Đạt / Lỗi / Thiếu trên từng dòng. Mặc định (không gửi Lines) = toàn bộ đạt.
    /// </summary>
    private static void ApplyReceiveSettlement(WarehouseTransaction t, ApproveImportRequest? request)
    {
        foreach (var item in t.Items)
        {
            if (item.PlannedQuantity <= 0)
                item.PlannedQuantity = item.Quantity;
        }

        var lines = request?.Lines;
        if (lines is null || lines.Count == 0)
        {
            foreach (var item in t.Items)
            {
                item.AcceptedQty = item.PlannedQuantity;
                item.DefectQty = 0;
                item.ShortageQty = 0;
                item.DefectLineReason = null;
                item.ShortageLineReason = null;
            }
            t.ShortageReport = null;
            return;
        }

        if (lines.Count != t.Items.Count)
            throw new DomainRuleException("Phải gửi đủ mọi dòng kiểm nhận (Đạt / Lỗi / Thiếu).");

        var byId = new Dictionary<int, ApproveImportLineSettlementRequest>();
        foreach (var line in lines)
        {
            if (!byId.TryAdd(line.LineId, line))
                throw new DomainRuleException("Dòng kiểm nhận bị trùng.");
        }

        decimal totalDefect = 0;
        decimal totalShortage = 0;

        foreach (var item in t.Items)
        {
            if (!byId.TryGetValue(item.Id, out var settle))
                throw new DomainRuleException($"Thiếu kiểm nhận cho dòng '{item.MaterialCode}'.");

            var planned = item.PlannedQuantity > 0 ? item.PlannedQuantity : item.Quantity;
            if (settle.AcceptedQty < 0 || settle.DefectQty < 0 || settle.ShortageQty < 0)
                throw new DomainRuleException($"'{item.MaterialCode}': số lượng không được âm.");
            if (Math.Abs(settle.AcceptedQty + settle.DefectQty + settle.ShortageQty - planned) > 0.0001m)
                throw new DomainRuleException(
                    $"'{item.MaterialCode}': Đạt + Lỗi + Thiếu phải bằng SL dự kiến ({planned:0.##}).");

            if (settle.DefectQty > 0 && string.IsNullOrWhiteSpace(settle.DefectLineReason))
                throw new DomainRuleException($"'{item.MaterialCode}': hàng lỗi cần lý do trên dòng.");
            if (settle.ShortageQty > 0 && string.IsNullOrWhiteSpace(settle.ShortageLineReason))
                throw new DomainRuleException($"'{item.MaterialCode}': hàng thiếu cần lý do trên dòng.");

            item.AcceptedQty = settle.AcceptedQty;
            item.DefectQty = settle.DefectQty;
            item.ShortageQty = settle.ShortageQty;
            item.DefectLineReason = string.IsNullOrWhiteSpace(settle.DefectLineReason)
                ? null
                : settle.DefectLineReason.Trim();
            item.ShortageLineReason = string.IsNullOrWhiteSpace(settle.ShortageLineReason)
                ? null
                : settle.ShortageLineReason.Trim();

            totalDefect += settle.DefectQty;
            totalShortage += settle.ShortageQty;
        }

        if (totalShortage > 0)
        {
            if (string.IsNullOrWhiteSpace(request?.ShortageReport))
                throw new DomainRuleException("Có hàng thiếu — bắt buộc nhập báo cáo thiếu hàng.");
            t.ShortageReport = request!.ShortageReport.Trim();
        }
        else
        {
            t.ShortageReport = string.IsNullOrWhiteSpace(request?.ShortageReport)
                ? null
                : request.ShortageReport.Trim();
        }

        if (totalDefect > 0)
        {
            var hasDedicated = t.Attachments.Any(a =>
                string.Equals(a.Kind, "defect_photo", StringComparison.OrdinalIgnoreCase));
            if (!hasDedicated)
                throw new DomainRuleException(
                    "Có hàng lỗi — cần ít nhất 1 ảnh hàng lỗi (mục Ảnh hàng lỗi).");
            if (string.IsNullOrWhiteSpace(t.ReferenceCode))
                throw new DomainRuleException("Hàng lỗi chỉ hỗ trợ phiếu nhập gắn PO.");
        }
    }

    public async Task<ImportDto?> UpdateReceiptEvaluationAsync(
        string id,
        string? evaluation,
        CancellationToken ct = default)
    {
        var t = await FindImportAsync(id, ct);
        if (t is null) return null;
        if (t.ApprovalStatus == ApprovalStatus.Approved)
            throw new DomainRuleException("Phiếu đã duyệt, không thể cập nhật đánh giá.");
        t.ReceiptEvaluation = string.IsNullOrWhiteSpace(evaluation) ? null : evaluation.Trim();
        t.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        return WmsMapper.ToImport(t);
    }

    public async Task<ImportAttachmentDto?> UploadAttachmentAsync(
        string id,
        string kind,
        string fileName,
        string contentType,
        Stream content,
        CancellationToken ct = default)
    {
        var t = await FindImportAsync(id, ct);
        if (t is null) return null;
        if (t.ApprovalStatus == ApprovalStatus.Approved)
            throw new DomainRuleException("Phiếu đã duyệt, không thể thêm file.");

        var normalizedKind = NormalizeAttachmentKind(kind);
        var safeFileName = Path.GetFileName(fileName).Trim();
        var ext = Path.GetExtension(safeFileName).ToLowerInvariant();
        EnsureAllowedExtension(ext, normalizedKind);

        var relative = $"wms/imports/{t.TicketCode}/{normalizedKind}/{Guid.NewGuid():N}{ext}";
        var stored = await fileStorage.SaveAsync(content, relative, contentType, ct);

        var attachment = new WarehouseTransactionAttachment
        {
            Id = Guid.NewGuid(),
            TicketCode = t.TicketCode,
            Kind = normalizedKind,
            FileName = safeFileName,
            StorageKey = stored.StorageKey,
            UploadedAt = DateTime.UtcNow,
        };
        db.WarehouseTransactionAttachments.Add(attachment);
        t.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        return new ImportAttachmentDto(
            attachment.Id,
            attachment.Kind,
            attachment.FileName,
            attachment.StorageKey,
            WmsMapper.FormatCreatedAt(attachment.UploadedAt));
    }

    public async Task<ImportAttachmentDto?> GetAttachmentAsync(
        string id,
        Guid attachmentId,
        CancellationToken ct = default)
    {
        var t = await FindImportAsync(id, ct);
        if (t is null) return null;
        var attachment = t.Attachments.FirstOrDefault(a => a.Id == attachmentId);
        if (attachment is null) return null;
        return new ImportAttachmentDto(
            attachment.Id,
            attachment.Kind,
            attachment.FileName,
            attachment.StorageKey,
            WmsMapper.FormatCreatedAt(attachment.UploadedAt));
    }

    public async Task<bool> DeleteAttachmentAsync(
        string id,
        Guid attachmentId,
        CancellationToken ct = default)
    {
        var t = await FindImportAsync(id, ct);
        if (t is null) return false;
        if (t.ApprovalStatus == ApprovalStatus.Approved)
            throw new DomainRuleException("Phiếu đã duyệt, không thể xóa file.");
        var attachment = t.Attachments.FirstOrDefault(a => a.Id == attachmentId);
        if (attachment is null) return false;
        db.WarehouseTransactionAttachments.Remove(attachment);
        t.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<PoImportPreviewDto> PreviewImportFromPurchaseOrderAsync(
        string poCode,
        string? excludeTicketCode = null,
        CancellationToken ct = default) =>
        await poReceipt.PreviewImportFromPoAsync(poCode, excludeTicketCode, ct);

    public async Task<ImportDto> CreateDraftFromPurchaseOrderAsync(
        string poCode,
        CreateImportFromPoRequest? request = null,
        CancellationToken ct = default)
    {
        var issueFromGeneral = request?.IssueFromGeneralWarehouse ?? false;
        var isDirect = request?.IsDirectProcurement ?? false;
        if (issueFromGeneral && !isDirect)
            throw new DomainRuleException("Cấp từ kho trung tâm chỉ dùng cho xuất thẳng công trường.");
        if (issueFromGeneral && (request?.Items is null || request.Items.Count == 0))
            throw new DomainRuleException("Chọn ít nhất một dòng vật tư khi cấp từ kho trung tâm.");

        var dto = await poReceipt.BuildImportDraftFromPoAsync(
            poCode,
            request?.Items,
            isDirect,
            ignorePoAvailability: issueFromGeneral,
            ct);
        if (issueFromGeneral)
            dto = dto with { IssueFromGeneralWarehouse = true, IsDirectProcurement = true };
        return await CreateAsync(dto, ct);
    }

    public async Task<ImportDto?> SubmitAsync(string id, CancellationToken ct = default)
    {
        var t = await FindImportAsync(id, ct);
        if (t is null) return null;
        if (t.ApprovalStatus != ApprovalStatus.Draft)
            throw new DomainRuleException("Chỉ phiếu nháp mới được PM chốt.");

        await stocktakeFreeze.EnsureProjectsNotFrozenAsync([t.BeneficiaryProjectCode], ct);
        if (PurchaseOrderReceiptService.SettlesPurchaseOrder(t))
            await poReceipt.EnsureImportLinesWithinPoAvailabilityAsync(t, ct);

        t.ApprovalStatus = ApprovalStatus.Pending;
        t.UpdatedAt = DateTime.UtcNow;
        await inventory.EnsurePendingDirectExportAsync(t, ct);
        await stocktakeFreeze.EnsureProjectsNotFrozenAsync([t.BeneficiaryProjectCode], ct);
        await db.SaveChangesAsync(ct);

        await activity.WriteAsync(new ActivityEventWriteRequest(
            ActivityModules.Wms,
            ActivityActions.ImportSubmitted,
            $"Chốt dự kiến phiếu nhập [{t.TicketCode}]",
            ActivityCategories.Import,
            EntityType: "warehouse_import",
            EntityCode: t.TicketCode), ct);

        return WmsMapper.ToImport(t);
    }

    public async Task<bool> DeleteAsync(string id, CancellationToken ct = default)
    {
        var t = await FindImportAsync(id, ct);
        if (t is null) return false;
        if (t.ApprovalStatus != ApprovalStatus.Draft)
            throw new DomainRuleException(
                t.ApprovalStatus == ApprovalStatus.Approved
                    ? "Phiếu đã duyệt, không thể xóa."
                    : "Chỉ phiếu nháp mới được xóa. Phiếu chờ duyệt không thể xóa.");

        await using var tx = await db.Database.BeginTransactionAsync(ct);
        if (HasPlannedInventory(t.ApprovalStatus))
        {
            await inventory.CancelReservationsAsync(t.TicketCode, ct);
            await inventory.CancelProjectImportPlannedAsync(t, ct);
        }
        if (!string.IsNullOrWhiteSpace(t.LinkedTicketCode))
        {
            var linkedExport = await db.WarehouseTransactions
                .FirstOrDefaultAsync(
                    x => x.TransactionType == TransactionType.Export
                         && x.TicketCode == t.LinkedTicketCode
                         && x.ApprovalStatus != ApprovalStatus.Approved,
                    ct);
            if (linkedExport is not null)
                db.WarehouseTransactions.Remove(linkedExport);
        }
        db.WarehouseTransactions.Remove(t);
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return true;
    }

    private IQueryable<WarehouseTransaction> ImportQuery() =>
        db.WarehouseTransactions
            .Include(t => t.Items).ThenInclude(i => i.Material)
            .Include(t => t.Attachments)
            .Include(t => t.Supplier)
            .Where(t => t.TransactionType == TransactionType.Import);

    private static void EnsureApprovalEvidence(WarehouseTransaction t)
    {
        var hasEvaluation = !string.IsNullOrWhiteSpace(t.ReceiptEvaluation);
        var hasPhoto = t.Attachments.Any(a => string.Equals(a.Kind, "photo", StringComparison.OrdinalIgnoreCase));
        var hasDocument = t.Attachments.Any(a => string.Equals(a.Kind, "document", StringComparison.OrdinalIgnoreCase));

        if (!hasEvaluation)
            throw new DomainRuleException(
                "Thiếu đánh giá nhận hàng. Nhập đánh giá rồi lưu (hoặc bấm Duyệt kho để tự lưu).");
        if (!hasPhoto)
            throw new DomainRuleException(
                "Thiếu ảnh nhận hàng. Cần ít nhất 1 ảnh nhận hàng.");
        if (!hasDocument)
            throw new DomainRuleException(
                "Thiếu file chứng từ. Cần ít nhất 1 file chứng từ (PDF/Word/Excel).");
    }

    private static string NormalizeAttachmentKind(string? kind)
    {
        var k = kind?.Trim().ToLowerInvariant();
        return k switch
        {
            "photo" => "photo",
            "nameplate" => "nameplate",
            "defect_photo" => "defect_photo",
            _ => "document",
        };
    }

    private static void EnsureAllowedExtension(string ext, string kind)
    {
        if (kind is "photo" or "nameplate" or "defect_photo")
        {
            if (ext is not (".jpg" or ".jpeg" or ".png" or ".webp"))
                throw new DomainRuleException("Ảnh chỉ chấp nhận JPG, PNG, WEBP.");
            return;
        }

        if (ext is not (".pdf" or ".doc" or ".docx" or ".xls" or ".xlsx" or ".jpg" or ".jpeg" or ".png" or ".webp"))
            throw new DomainRuleException("File chứng từ không hợp lệ.");
    }

    private static bool HasPlannedInventory(ApprovalStatus status) =>
        status is ApprovalStatus.Draft or ApprovalStatus.Pending;

    private async Task<string> ResolveImportWarehouseIdAsync(CreateImportDto dto, CancellationToken ct)
    {
        if (dto.IssueFromGeneralWarehouse)
        {
            if (!(dto.IsDirectProcurement ?? false))
                throw new DomainRuleException("Cấp từ kho trung tâm chỉ dùng cho xuất thẳng công trường.");
            return await inventory.GetGeneralWarehouseIdAsync(ct);
        }

        return dto.WarehouseId ?? "";
    }

    private async Task CreateDraftReservationsAsync(WarehouseTransaction transaction, CancellationToken ct)
    {
        if (transaction.IsDirectProcurement || string.IsNullOrWhiteSpace(transaction.BeneficiaryProjectCode))
            return;

        await inventory.CreateProjectImportPlannedAsync(transaction, ct);
    }

    private WarehouseTransaction BuildTransaction(
        CreateImportDto dto, string ticketCode, ApprovalStatus approvalStatus) =>
        new()
        {
            Id = Guid.NewGuid(),
            TicketCode = ticketCode,
            TransactionType = TransactionType.Import,
            WarehouseId = dto.WarehouseId,
            ReferenceCode = dto.ReferenceCode,
            PurchasePlanCode = string.IsNullOrWhiteSpace(dto.PlanCode) ? null : dto.PlanCode.Trim(),
            SupplierId = ResolveHeaderSupplierId(dto),
            SupplierName = ResolveHeaderSupplier(dto),
            WarehouseKeeper = dto.WarehouseKeeper,
            WarehouseApprovalStatus = WmsMapper.FromClientKeeperStatus(dto.WarehouseApproval),
            IsDirectProcurement = dto.IsDirectProcurement ?? false,
            BeneficiaryProjectCode = dto.Project,
            Notes = dto.Note,
            CreatedById = currentUser.Id,
            CreatedByName = string.IsNullOrEmpty(dto.CreatedBy) ? currentUser.Name : dto.CreatedBy,
            ApprovalStatus = approvalStatus,
            Items = dto.Items.Select(i => new WarehouseTransactionItem
            {
                MaterialCode = string.IsNullOrEmpty(i.Sku) ? i.ProductId : i.Sku,
                Quantity = i.Qty,
                PlannedQuantity = i.Qty,
                UnitPrice = i.UnitPrice,
                SupplierId = i.SupplierId,
                SupplierName = ResolveLineSupplierName(i, dto.Supplier),
                Notes = i.Notes,
            }).ToList()
        };

    private static string ResolveLineSupplierName(ImportItemDto item, string? headerSupplier)
    {
        if (!string.IsNullOrWhiteSpace(item.Supplier)) return item.Supplier.Trim();
        return headerSupplier?.Trim() ?? "";
    }

    private static int? ResolveHeaderSupplierId(CreateImportDto dto)
    {
        var lineIds = dto.Items
            .Where(i => i.SupplierId is int)
            .Select(i => i.SupplierId!.Value)
            .Distinct()
            .ToList();
        return lineIds.Count == 1 ? lineIds[0] : null;
    }

    private static string ResolveHeaderSupplier(CreateImportDto dto)
    {
        var lineNames = dto.Items
            .Select(i => ResolveLineSupplierName(i, null))
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (lineNames.Count == 0)
            return dto.Supplier?.Trim() ?? "";

        if (lineNames.Count == 1)
            return lineNames[0];

        return "Nhiều NCC";
    }

    private async Task ValidateAndSnapshotItemsAsync(
        IEnumerable<WarehouseTransactionItem> items, CancellationToken ct)
    {
        var list = items.ToList();
        var materials = await TransactionItemHelper.LoadMaterialsAsync(
            db, list.Select(i => i.MaterialCode), ct);
        TransactionItemHelper.EnsureActiveMaterials(list.Select(i => i.MaterialCode), materials);
        await TransactionItemHelper.ApplySnapshotsAsync(db, list, ct);
    }

    private async Task WritePurchasingImportActivityAsync(
        WarehouseTransaction t,
        string action,
        string message,
        CancellationToken ct)
    {
        var isPurchasingLinked = !string.IsNullOrWhiteSpace(t.PurchasePlanCode)
            || (!string.IsNullOrWhiteSpace(t.ReferenceCode)
                && t.ReferenceCode.StartsWith("PO-", StringComparison.OrdinalIgnoreCase));
        if (!isPurchasingLinked)
            return;

        await activity.WriteAsync(new ActivityEventWriteRequest(
            ActivityModules.Purchasing,
            action,
            message,
            ActivityCategories.Import,
            EntityType: "warehouse_import",
            EntityCode: t.TicketCode), ct);
    }

    private async Task<WarehouseTransaction?> FindImportAsync(string id, CancellationToken ct)
    {
        if (Guid.TryParse(id, out var guid))
            return await ImportQuery().FirstOrDefaultAsync(t => t.Id == guid, ct);
        return await ImportQuery().FirstOrDefaultAsync(t => t.TicketCode == id, ct);
    }
}
