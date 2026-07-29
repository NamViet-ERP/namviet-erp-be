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
using NamViet.ERP.SharedKernel.Interfaces;
using NamViet.ERP.Api.Services.Storage;

namespace NamViet.ERP.Api.Application.Services.Wms;

public interface IImportApplicationService
{
    Task<List<ImportDto>> ListAsync(DateOnly? dateFrom = null, DateOnly? dateTo = null, CancellationToken ct = default);
    Task<ImportDto?> GetByIdAsync(string id, CancellationToken ct = default);
    Task<ImportDto> CreateAsync(CreateImportDto dto, CancellationToken ct = default);
    Task<ImportDto?> UpdateAsync(string id, CreateImportDto dto, CancellationToken ct = default);
    Task<ImportDto?> ApproveAsync(string id, bool confirmOverBudget = false, CancellationToken ct = default);
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
    IFileStorage fileStorage,
    IActivityEventWriter activity) : IImportApplicationService
{
    public async Task<List<ImportDto>> ListAsync(DateOnly? dateFrom = null, DateOnly? dateTo = null, CancellationToken ct = default)
    {
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

        var count = await db.WarehouseTransactions.CountAsync(t => t.TransactionType == TransactionType.Import, ct);
        var ticketCode = string.IsNullOrWhiteSpace(dto.VoucherCode)
            ? WarehouseTransaction.GenerateTicketCode(TransactionType.Import, count + 1)
            : dto.VoucherCode;

        var approvalStatus = WmsMapper.FromClientApprovalStatus(dto.Status);
        var transaction = BuildTransaction(dto, ticketCode, approvalStatus);
        await ValidateAndSnapshotItemsAsync(transaction.Items, ct);

        await using var tx = await db.Database.BeginTransactionAsync(ct);
        db.WarehouseTransactions.Add(transaction);

        if (approvalStatus is ApprovalStatus.Draft or ApprovalStatus.Pending)
            await CreateDraftReservationsAsync(transaction, ct);
        else if (approvalStatus == ApprovalStatus.Approved)
        {
            await inventory.ApplyApprovedImportAsync(transaction, ct);
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
    }

    public async Task<ImportDto?> UpdateAsync(string id, CreateImportDto dto, CancellationToken ct = default)
    {
        var t = await FindImportAsync(id, ct);
        if (t is null) return null;
        if (t.ApprovalStatus == ApprovalStatus.Approved)
            throw new InvalidOperationException("Phiếu đã duyệt, không thể chỉnh sửa.");

        await stocktakeFreeze.EnsureProjectsNotFrozenAsync([dto.Project], ct);

        await using var tx = await db.Database.BeginTransactionAsync(ct);

        if (HasPlannedInventory(t.ApprovalStatus))
        {
            await inventory.CancelReservationsAsync(t.TicketCode, ct);
            await inventory.CancelProjectImportPlannedAsync(t, ct);
        }

        t.WarehouseId = dto.WarehouseId;
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
            throw new InvalidOperationException("Không thể duyệt phiếu qua cập nhật. Hãy dùng thao tác Duyệt kho.");
        t.ApprovalStatus = nextStatus;
        t.UpdatedAt = DateTime.UtcNow;

        db.WarehouseTransactionItems.RemoveRange(t.Items);
        t.Items = dto.Items.Select(i => new WarehouseTransactionItem
        {
            TicketCode = t.TicketCode,
            MaterialCode = string.IsNullOrEmpty(i.Sku) ? i.ProductId : i.Sku,
            Quantity = i.Qty,
            UnitPrice = i.UnitPrice,
            SupplierId = i.SupplierId,
            SupplierName = ResolveLineSupplierName(i, dto.Supplier),
            Notes = i.Notes,
        }).ToList();
        await ValidateAndSnapshotItemsAsync(t.Items, ct);

        if (HasPlannedInventory(t.ApprovalStatus))
            await CreateDraftReservationsAsync(t, ct);

        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return WmsMapper.ToImport(t);
    }

    public async Task<ImportDto?> ApproveAsync(string id, bool confirmOverBudget = false, CancellationToken ct = default)
    {
        var t = await FindImportAsync(id, ct);
        if (t is null) return null;
        if (t.ApprovalStatus is ApprovalStatus.Approved or ApprovalStatus.Rejected)
            throw new InvalidOperationException($"Phiếu đã ở trạng thái '{t.ApprovalStatus}', không thể duyệt lại.");
        if (t.ApprovalStatus != ApprovalStatus.Pending)
            throw new InvalidOperationException("Chỉ phiếu đã chốt dự kiến (Pending) mới được duyệt kho.");

        await stocktakeFreeze.EnsureProjectsNotFrozenAsync([t.BeneficiaryProjectCode], ct);
        await poReceipt.EnsureImportLinesWithinPoAvailabilityAsync(t, ct);
        EnsureApprovalEvidence(t);

        await projectBudget.EnsureImportApprovalWithinBudgetAsync(t, confirmOverBudget, ct);

        await using var tx = await db.Database.BeginTransactionAsync(ct);
        t.ApprovalStatus = ApprovalStatus.Approved;
        t.WarehouseApprovalStatus = WarehouseKeeperApprovalStatus.Approved;
        t.ApprovedById = currentUser.Id;
        t.ApprovedByName = currentUser.Name;
        t.ApprovedAt = DateTime.UtcNow;
        t.UpdatedAt = DateTime.UtcNow;

        await inventory.ApplyApprovedImportAsync(t, ct);
        await poReceipt.ApplyApprovedImportReceiptAsync(t, ct);
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);

        await WritePurchasingImportActivityAsync(
            t,
            ActivityActions.ImportApproved,
            $"Cập nhật trạng thái duyệt kho phiếu nhập hàng [{t.TicketCode}]",
            ct);

        return WmsMapper.ToImport(t);
    }

    public async Task<ImportDto?> UpdateReceiptEvaluationAsync(
        string id,
        string? evaluation,
        CancellationToken ct = default)
    {
        var t = await FindImportAsync(id, ct);
        if (t is null) return null;
        if (t.ApprovalStatus == ApprovalStatus.Approved)
            throw new InvalidOperationException("Phiếu đã duyệt, không thể cập nhật đánh giá.");
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
            throw new InvalidOperationException("Phiếu đã duyệt, không thể thêm file.");

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
            throw new InvalidOperationException("Phiếu đã duyệt, không thể xóa file.");
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
        var dto = await poReceipt.BuildImportDraftFromPoAsync(poCode, request?.Items, ct);
        return await CreateAsync(dto, ct);
    }

    public async Task<ImportDto?> SubmitAsync(string id, CancellationToken ct = default)
    {
        var t = await FindImportAsync(id, ct);
        if (t is null) return null;
        if (t.ApprovalStatus != ApprovalStatus.Draft)
            throw new InvalidOperationException("Chỉ phiếu nháp mới được PM chốt.");

        await stocktakeFreeze.EnsureProjectsNotFrozenAsync([t.BeneficiaryProjectCode], ct);
        await poReceipt.EnsureImportLinesWithinPoAvailabilityAsync(t, ct);

        t.ApprovalStatus = ApprovalStatus.Pending;
        t.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        return WmsMapper.ToImport(t);
    }

    public async Task<bool> DeleteAsync(string id, CancellationToken ct = default)
    {
        var t = await FindImportAsync(id, ct);
        if (t is null) return false;
        if (t.ApprovalStatus != ApprovalStatus.Draft)
            throw new InvalidOperationException(
                t.ApprovalStatus == ApprovalStatus.Approved
                    ? "Phiếu đã duyệt, không thể xóa."
                    : "Chỉ phiếu nháp mới được xóa. Phiếu chờ duyệt không thể xóa.");

        await using var tx = await db.Database.BeginTransactionAsync(ct);
        if (HasPlannedInventory(t.ApprovalStatus))
        {
            await inventory.CancelReservationsAsync(t.TicketCode, ct);
            await inventory.CancelProjectImportPlannedAsync(t, ct);
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
            throw new InvalidOperationException(
                "Thiếu đánh giá nhận hàng. Nhập đánh giá rồi lưu (hoặc bấm Duyệt kho để tự lưu).");
        if (!hasPhoto)
            throw new InvalidOperationException(
                "Thiếu ảnh nhận hàng. Cần ít nhất 1 ảnh nhận hàng.");
        if (!hasDocument)
            throw new InvalidOperationException(
                "Thiếu file chứng từ. Cần ít nhất 1 file chứng từ (PDF/Word/Excel).");
    }

    private static string NormalizeAttachmentKind(string? kind) =>
        string.Equals(kind?.Trim(), "photo", StringComparison.OrdinalIgnoreCase) ? "photo" : "document";

    private static void EnsureAllowedExtension(string ext, string kind)
    {
        if (kind == "photo")
        {
            if (ext is not (".jpg" or ".jpeg" or ".png" or ".webp"))
                throw new InvalidOperationException("Ảnh nhận hàng chỉ chấp nhận JPG, PNG, WEBP.");
            return;
        }

        if (ext is not (".pdf" or ".doc" or ".docx" or ".xls" or ".xlsx" or ".jpg" or ".jpeg" or ".png" or ".webp"))
            throw new InvalidOperationException("File chứng từ không hợp lệ.");
    }

    private static bool HasPlannedInventory(ApprovalStatus status) =>
        status is ApprovalStatus.Draft or ApprovalStatus.Pending;

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
