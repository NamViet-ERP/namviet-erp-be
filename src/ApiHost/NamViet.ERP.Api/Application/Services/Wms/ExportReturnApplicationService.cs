using Microsoft.EntityFrameworkCore;
using NamViet.ERP.Api.Application.Dtos.Wms;
using NamViet.ERP.Api.Application.Mappers.Wms;
using NamViet.ERP.Modules.Warehouse.Domain.Entities;
using NamViet.ERP.Modules.Warehouse.Domain.Enums;
using NamViet.ERP.Modules.Warehouse.Domain.Services;
using NamViet.ERP.Modules.Warehouse.Infrastructure.Helpers;
using NamViet.ERP.Modules.Warehouse.Infrastructure.Persistence;
using NamViet.ERP.SharedKernel.Interfaces;
using NamViet.ERP.SharedKernel.Common;

namespace NamViet.ERP.Api.Application.Services.Wms;

public interface IExportReturnApplicationService
{
    Task<List<ExportReturnDto>> ListAsync(DateOnly? dateFrom = null, DateOnly? dateTo = null, CancellationToken ct = default);
    Task<ExportReturnDto?> GetByIdAsync(string id, CancellationToken ct = default);
    Task<List<ExportReturnableExportDto>> ListReturnableExportsAsync(
        string projectCode, string? excludeReturnId = null, CancellationToken ct = default);
    Task<List<ExportReturnableLineDto>> GetReturnableLinesAsync(
        string sourceExportId, string? excludeReturnId = null, CancellationToken ct = default);
    Task<ExportReturnDto> CreateAsync(CreateExportReturnDto dto, CancellationToken ct = default);
    Task<ExportReturnDto?> UpdateAsync(string id, CreateExportReturnDto dto, CancellationToken ct = default);
    Task<ExportReturnDto?> ApproveAsync(string id, CancellationToken ct = default);
    Task<ExportReturnDto?> SubmitAsync(string id, CancellationToken ct = default);
    Task<bool> DeleteAsync(string id, CancellationToken ct = default);
    Task<ImportAttachmentDto?> UploadAttachmentAsync(
        string id, string kind, string fileName, string contentType, Stream content, CancellationToken ct = default);
    Task<ImportAttachmentDto?> GetAttachmentAsync(string id, Guid attachmentId, CancellationToken ct = default);
    Task<bool> DeleteAttachmentAsync(string id, Guid attachmentId, CancellationToken ct = default);
}

public sealed class ExportReturnApplicationService(
    WarehouseDbContext db,
    IInventoryDomainService inventory,
    IStocktakeFreezeGuard stocktakeFreeze,
    ICurrentUser currentUser,
    NamViet.ERP.Api.Services.Storage.IFileStorage fileStorage) : IExportReturnApplicationService
{
    private static readonly ApprovalStatus[] ActiveReturnStatuses =
    [
        ApprovalStatus.Draft,
        ApprovalStatus.Pending,
        ApprovalStatus.Approved
    ];

    public async Task<List<ExportReturnDto>> ListAsync(
        DateOnly? dateFrom = null, DateOnly? dateTo = null, CancellationToken ct = default)
    {
        var items = await Query()
            .WhereCreatedBetween(dateFrom, dateTo)
            .OrderByDescending(t => t.CreatedAt)
            .ToListAsync(ct);
        var projectNames = await GetProjectNamesAsync(items, ct);
        var sourceExports = await GetSourceExportsAsync(items, ct);
        return items.Select(t => MapReturn(t, projectNames, sourceExports)).ToList();
    }

    public async Task<ExportReturnDto?> GetByIdAsync(string id, CancellationToken ct = default)
    {
        var t = await FindAsync(id, ct);
        if (t is null) return null;
        var projectNames = await GetProjectNamesAsync([t], ct);
        var sourceExports = await GetSourceExportsAsync([t], ct);
        return MapReturn(t, projectNames, sourceExports);
    }

    public async Task<List<ExportReturnableExportDto>> ListReturnableExportsAsync(
        string projectCode, string? excludeReturnId = null, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(projectCode))
            throw new DomainRuleException("Vui lòng chọn kho dự án.");

        var exports = await db.WarehouseTransactions
            .AsNoTracking()
            .Include(t => t.Items)
            .Where(t => t.TransactionType == TransactionType.Export
                        && t.ApprovalStatus == ApprovalStatus.Approved
                        && !t.IsDirectProcurement
                        && (t.ProjectCode == projectCode || t.BeneficiaryProjectCode == projectCode))
            .OrderByDescending(t => t.CreatedAt)
            .ToListAsync(ct);

        if (exports.Count == 0)
            return [];

        var exportCodes = exports.Select(e => e.TicketCode).ToList();
        var excludeTicketCode = await ResolveExcludeReturnTicketCodeAsync(excludeReturnId, ct);
        var returnsBySource = await LoadReturnsBySourceCodesAsync(exportCodes, ct);
        var blockedSources = await LoadBlockedSourceCodesAsync(exportCodes, ct, excludeReturnId);

        var result = new List<ExportReturnableExportDto>();
        foreach (var export in exports)
        {
            if (blockedSources.Contains(export.TicketCode))
                continue;

            var sourceReturns = returnsBySource.GetValueOrDefault(export.TicketCode) ?? [];
            if (!string.IsNullOrWhiteSpace(excludeTicketCode))
            {
                sourceReturns = sourceReturns
                    .Where(r => !string.Equals(r.TicketCode, excludeTicketCode, StringComparison.OrdinalIgnoreCase))
                    .ToList();
            }

            var returnedByKey = ExportReturnLineHelper.SumReturnedQtyByKey(sourceReturns);
            var lines = ExportReturnLineHelper.BuildReturnableLines(export, returnedByKey);
            if (lines.Count == 0)
                continue;

            result.Add(new ExportReturnableExportDto(
                export.Id.ToString(),
                export.TicketCode,
                WmsMapper.FormatDate(export.CreatedAt),
                export.SoCode,
                export.Customer,
                WmsMapper.ToClientApprovalStatus(export.ApprovalStatus),
                WmsMapper.FormatApprovedAt(export.ApprovedAt),
                lines.Count,
                lines.Sum(l => l.RemainingQty)));
        }

        return result;
    }

    public async Task<List<ExportReturnableLineDto>> GetReturnableLinesAsync(
        string sourceExportId, string? excludeReturnId = null, CancellationToken ct = default)
    {
        var export = await FindSourceExportAsync(sourceExportId, ct)
            ?? throw new DomainRuleException("Không tìm thấy phiếu xuất nguồn.");

        await EnsureNoActiveReturnDraftAsync(export.TicketCode, ct, excludeReturnId);

        var excludeTicketCode = await ResolveExcludeReturnTicketCodeAsync(excludeReturnId, ct);
        var returnedByKey = ExportReturnLineHelper.SumReturnedQtyByKey(
            await LoadReturnsForSourceAsync(export.TicketCode, ct, excludeTicketCode));
        var lines = ExportReturnLineHelper.BuildReturnableLines(export, returnedByKey);
        if (lines.Count == 0)
            throw new DomainRuleException("Phiếu xuất đã được hoàn trả đủ — không còn vật tư để trả.");

        return lines.Select(line => new ExportReturnableLineDto(
            line.MaterialCode,
            line.MaterialCode,
            ResolveMaterialName(export, line.MaterialCode),
            ResolveMaterialUnit(export, line.MaterialCode),
            line.ExportedQty,
            line.ReturnedQty,
            line.RemainingQty)).ToList();
    }

    public async Task<ExportReturnDto> CreateAsync(CreateExportReturnDto dto, CancellationToken ct = default)
    {
        var export = await FindSourceExportAsync(dto.SourceExportId, ct)
            ?? throw new DomainRuleException("Không tìm thấy phiếu xuất nguồn.");

        if (export.ApprovalStatus != ApprovalStatus.Approved)
            throw new DomainRuleException("Chỉ hoàn trả theo phiếu xuất đã duyệt.");
        if (export.IsDirectProcurement)
            throw new DomainRuleException("Phiếu xuất Direct Issue không hỗ trợ hoàn trả.");

        var projectCode = export.ProjectCode ?? export.BeneficiaryProjectCode
            ?? throw new DomainRuleException("Phiếu xuất nguồn không thuộc kho dự án.");

        if (!string.IsNullOrWhiteSpace(dto.Project)
            && !string.Equals(dto.Project.Trim(), projectCode, StringComparison.OrdinalIgnoreCase))
            throw new DomainRuleException("Kho dự án không khớp với phiếu xuất nguồn.");

        await stocktakeFreeze.EnsureProjectsNotFrozenAsync([projectCode], ct);

        await EnsureNoActiveReturnDraftAsync(export.TicketCode, ct);
        await ValidateReturnItemsAsync(export, dto.Items, ct);

        var count = await db.WarehouseTransactions.CountAsync(
            t => t.TransactionType == TransactionType.ExportReturn, ct);
        var ticketCode = string.IsNullOrWhiteSpace(dto.VoucherCode)
            ? WarehouseTransaction.GenerateTicketCode(TransactionType.ExportReturn, count + 1)
            : dto.VoucherCode;

        var approvalStatus = WmsMapper.FromClientApprovalStatus(dto.Status);
        var transaction = new WarehouseTransaction
        {
            Id = Guid.NewGuid(),
            TicketCode = ticketCode,
            TransactionType = TransactionType.ExportReturn,
            ProjectCode = projectCode,
            BeneficiaryProjectCode = projectCode,
            LinkedTicketCode = export.TicketCode,
            Notes = dto.Note,
            CreatedById = currentUser.Id,
            CreatedByName = string.IsNullOrEmpty(dto.CreatedBy) ? currentUser.Name : dto.CreatedBy,
            ApprovalStatus = approvalStatus,
            Items = MapItems(dto.Items, export, ticketCode),
        };

        await ValidateAndSnapshotItemsAsync(transaction.Items, ct);

        await using var tx = await db.Database.BeginTransactionAsync(ct);
        db.WarehouseTransactions.Add(transaction);
        if (approvalStatus is ApprovalStatus.Draft or ApprovalStatus.Pending)
            await inventory.CreateProjectExportReturnPlannedAsync(transaction, ct);
        else if (approvalStatus == ApprovalStatus.Approved)
            await inventory.ApplyApprovedExportReturnAsync(transaction, ct);

        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);

        return await MapReturnResponseAsync(transaction, ct);
    }

    public async Task<ExportReturnDto?> UpdateAsync(string id, CreateExportReturnDto dto, CancellationToken ct = default)
    {
        var t = await FindAsync(id, ct);
        if (t is null) return null;
        if (t.ApprovalStatus == ApprovalStatus.Approved)
            throw new DomainRuleException("Phiếu đã duyệt, không thể chỉnh sửa.");

        var export = await FindSourceExportAsync(dto.SourceExportId, ct)
            ?? throw new DomainRuleException("Không tìm thấy phiếu xuất nguồn.");
        if (!string.Equals(t.LinkedTicketCode, export.TicketCode, StringComparison.OrdinalIgnoreCase))
            throw new DomainRuleException("Không thể đổi phiếu xuất nguồn khi sửa phiếu hoàn trả.");

        var projectCode = export.ProjectCode ?? export.BeneficiaryProjectCode
            ?? throw new DomainRuleException("Phiếu xuất nguồn không thuộc kho dự án.");
        await stocktakeFreeze.EnsureProjectsNotFrozenAsync([projectCode], ct);

        await ValidateReturnItemsAsync(export, dto.Items, ct, excludeReturnTicketCode: t.TicketCode);

        await using var tx = await db.Database.BeginTransactionAsync(ct);

        if (HasPlannedInventory(t.ApprovalStatus))
            await inventory.CancelProjectExportReturnPlannedAsync(t, ct);

        t.Notes = dto.Note;
        var nextStatus = WmsMapper.FromClientApprovalStatus(dto.Status);
        if (nextStatus == ApprovalStatus.Approved)
            throw new DomainRuleException("Không thể duyệt phiếu qua cập nhật. Hãy dùng thao tác Duyệt kho.");
        t.ApprovalStatus = nextStatus;
        t.UpdatedAt = DateTime.UtcNow;

        db.WarehouseTransactionItems.RemoveRange(t.Items);
        t.Items = MapItems(dto.Items, export, t.TicketCode);
        await ValidateAndSnapshotItemsAsync(t.Items, ct);

        if (HasPlannedInventory(t.ApprovalStatus))
            await inventory.CreateProjectExportReturnPlannedAsync(t, ct);

        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);

        return await MapReturnResponseAsync(t, ct);
    }

    public async Task<ExportReturnDto?> ApproveAsync(string id, CancellationToken ct = default)
    {
        var t = await FindAsync(id, ct);
        if (t is null) return null;
        if (t.ApprovalStatus is ApprovalStatus.Approved or ApprovalStatus.Rejected)
            throw new DomainRuleException($"Phiếu đã ở trạng thái '{t.ApprovalStatus}', không thể duyệt lại.");
        if (t.ApprovalStatus != ApprovalStatus.Pending)
            throw new DomainRuleException("Chỉ phiếu đã chốt dự kiến (Pending) mới được duyệt kho.");

        await stocktakeFreeze.EnsureProjectsNotFrozenAsync(
            [t.ProjectCode ?? t.BeneficiaryProjectCode], ct);

        var export = await db.WarehouseTransactions
            .Include(e => e.Items)
            .FirstOrDefaultAsync(e => e.TicketCode == t.LinkedTicketCode, ct)
            ?? throw new DomainRuleException("Phiếu xuất nguồn không tồn tại.");
        await ValidateReturnItemsAsync(
            export,
            t.Items,
            ct,
            excludeReturnTicketCode: t.TicketCode);

        await using var tx = await db.Database.BeginTransactionAsync(ct);
        t.ApprovalStatus = ApprovalStatus.Approved;
        t.ApprovedById = currentUser.Id;
        t.ApprovedByName = currentUser.Name;
        t.ApprovedAt = DateTime.UtcNow;
        t.UpdatedAt = DateTime.UtcNow;

        await inventory.CancelProjectExportReturnPlannedAsync(t, ct);
        await inventory.ApplyApprovedExportReturnAsync(t, ct);
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);

        return await MapReturnResponseAsync(t, ct);
    }

    public async Task<ExportReturnDto?> SubmitAsync(string id, CancellationToken ct = default)
    {
        var t = await FindAsync(id, ct);
        if (t is null) return null;
        if (t.ApprovalStatus != ApprovalStatus.Draft)
            throw new DomainRuleException("Chỉ phiếu nháp mới được PM chốt.");
        if (string.IsNullOrWhiteSpace(t.Notes) || t.Notes.Trim().Length < 3)
            throw new DomainRuleException("Vui lòng nhập lý do hoàn trả trước khi gửi.");
        if (!t.Attachments.Any(a => a.Kind == "photo"))
            throw new DomainRuleException("Vui lòng đính kèm ít nhất một ảnh hoàn trả trước khi gửi.");

        await stocktakeFreeze.EnsureProjectsNotFrozenAsync(
            [t.ProjectCode ?? t.BeneficiaryProjectCode], ct);

        t.ApprovalStatus = ApprovalStatus.Pending;
        t.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);

        return await MapReturnResponseAsync(t, ct);
    }

    public async Task<ImportAttachmentDto?> UploadAttachmentAsync(
        string id,
        string kind,
        string fileName,
        string contentType,
        Stream content,
        CancellationToken ct = default)
    {
        var t = await FindAsync(id, ct);
        if (t is null) return null;
        if (t.ApprovalStatus == ApprovalStatus.Approved)
            throw new DomainRuleException("Phiếu đã duyệt, không thể thêm file.");

        var normalizedKind = string.Equals(kind?.Trim(), "photo", StringComparison.OrdinalIgnoreCase)
            ? "photo"
            : "document";
        var safeFileName = Path.GetFileName(fileName).Trim();
        var ext = Path.GetExtension(safeFileName).ToLowerInvariant();
        EnsureAllowedAttachmentExtension(ext, normalizedKind);

        var relative = $"wms/export-returns/{t.TicketCode}/{normalizedKind}/{Guid.NewGuid():N}{ext}";
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
        string id, Guid attachmentId, CancellationToken ct = default)
    {
        var t = await FindAsync(id, ct);
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
        string id, Guid attachmentId, CancellationToken ct = default)
    {
        var t = await FindAsync(id, ct);
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

    private static void EnsureAllowedAttachmentExtension(string ext, string kind)
    {
        if (kind == "photo")
        {
            if (ext is not (".jpg" or ".jpeg" or ".png" or ".webp"))
                throw new DomainRuleException("Ảnh hoàn trả chỉ chấp nhận JPG, PNG, WEBP.");
            return;
        }

        if (ext is not (".pdf" or ".doc" or ".docx" or ".xls" or ".xlsx" or ".jpg" or ".jpeg" or ".png" or ".webp"))
            throw new DomainRuleException("File chứng từ không hợp lệ.");
    }

    public async Task<bool> DeleteAsync(string id, CancellationToken ct = default)
    {
        var t = await FindAsync(id, ct);
        if (t is null) return false;
        if (t.ApprovalStatus == ApprovalStatus.Approved)
            throw new DomainRuleException("Phiếu đã duyệt, không thể xóa.");

        await using var tx = await db.Database.BeginTransactionAsync(ct);
        if (HasPlannedInventory(t.ApprovalStatus))
            await inventory.CancelProjectExportReturnPlannedAsync(t, ct);
        db.WarehouseTransactions.Remove(t);
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return true;
    }

    private static bool HasPlannedInventory(ApprovalStatus status) =>
        status is ApprovalStatus.Draft or ApprovalStatus.Pending;

    private async Task ValidateReturnItemsAsync(
        WarehouseTransaction export,
        List<ExportReturnItemDto> items,
        CancellationToken ct,
        string? excludeReturnTicketCode = null)
    {
        await ValidateReturnQuantitiesAsync(
            export,
            items.Select(item =>
            {
                var materialCode = ExportReturnLineHelper.ResolveMaterialCode(item.Sku, item.ProductId);
                var exportLine = ResolveExportLine(export, materialCode);
                return (materialCode, item.Qty, exportLine?.ConstructionPhase);
            }),
            ct,
            excludeReturnTicketCode);
    }

    private async Task ValidateReturnItemsAsync(
        WarehouseTransaction export,
        IEnumerable<WarehouseTransactionItem> items,
        CancellationToken ct,
        string? excludeReturnTicketCode = null)
    {
        await ValidateReturnQuantitiesAsync(
            export,
            items.Select(item => (item.MaterialCode, item.Quantity, item.ConstructionPhase)),
            ct,
            excludeReturnTicketCode);
    }

    private async Task ValidateReturnQuantitiesAsync(
        WarehouseTransaction export,
        IEnumerable<(string MaterialCode, decimal Qty, string? ConstructionPhase)> items,
        CancellationToken ct,
        string? excludeReturnTicketCode = null)
    {
        var itemList = items.ToList();
        if (itemList.Count == 0)
            throw new DomainRuleException("Phiếu hoàn trả phải có ít nhất một dòng vật tư.");

        var returnedByKey = ExportReturnLineHelper.SumReturnedQtyByKey(
            await LoadReturnsForSourceAsync(export.TicketCode, ct, excludeReturnTicketCode));
        var expectedLines = ExportReturnLineHelper.BuildReturnableLines(export, returnedByKey);
        if (expectedLines.Count == 0)
            throw new DomainRuleException("Phiếu xuất đã được hoàn trả đủ.");

        var expectedByKey = expectedLines.ToDictionary(
            l => ExportReturnLineHelper.BuildKey(l.MaterialCode, l.ConstructionPhase),
            l => l.RemainingQty,
            StringComparer.OrdinalIgnoreCase);

        var requestByKey = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase);
        foreach (var item in itemList)
        {
            var materialCode = item.MaterialCode?.Trim() ?? "";
            if (string.IsNullOrWhiteSpace(materialCode))
                throw new DomainRuleException("Mỗi dòng hoàn trả phải có mã vật tư.");
            if (item.Qty <= 0)
                throw new DomainRuleException($"Số lượng hoàn trả vật tư {materialCode} phải lớn hơn 0.");

            var key = ExportReturnLineHelper.BuildKey(materialCode, item.ConstructionPhase);
            if (!expectedByKey.ContainsKey(key))
                throw new DomainRuleException(
                    $"Vật tư {materialCode} không còn trong phạm vi hoàn trả của phiếu xuất.");

            requestByKey[key] = requestByKey.GetValueOrDefault(key) + item.Qty;
        }

        foreach (var (key, actualQty) in requestByKey)
        {
            if (!expectedByKey.TryGetValue(key, out var remainingQty))
                continue;

            if (actualQty > remainingQty)
            {
                var materialCode = key.Split('\u001f')[0];
                throw new DomainRuleException(
                    $"Vật tư {materialCode}: số lượng hoàn trả {actualQty} vượt phần còn lại {remainingQty}.");
            }
        }
    }

    private static WarehouseTransactionItem? ResolveExportLine(
        WarehouseTransaction export, string materialCode) =>
        export.Items.FirstOrDefault(e =>
            string.Equals(e.MaterialCode, materialCode, StringComparison.OrdinalIgnoreCase));

    private async Task EnsureNoActiveReturnDraftAsync(
        string sourceExportCode, CancellationToken ct, string? excludeReturnId = null)
    {
        var query = db.WarehouseTransactions.Where(t =>
            t.TransactionType == TransactionType.ExportReturn
            && t.LinkedTicketCode == sourceExportCode
            && (t.ApprovalStatus == ApprovalStatus.Draft || t.ApprovalStatus == ApprovalStatus.Pending));

        if (!string.IsNullOrWhiteSpace(excludeReturnId))
        {
            if (Guid.TryParse(excludeReturnId, out var guid))
                query = query.Where(t => t.Id != guid);
            else
                query = query.Where(t => t.TicketCode != excludeReturnId);
        }

        var hasDraft = await query.AnyAsync(ct);
        if (hasDraft)
            throw new DomainRuleException(
                "Phiếu xuất đang có phiếu hoàn trả nháp/chờ duyệt — hãy xử lý trước khi tạo mới.");
    }

    private async Task<string?> ResolveExcludeReturnTicketCodeAsync(
        string? excludeReturnId, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(excludeReturnId))
            return null;

        var t = await FindAsync(excludeReturnId, ct);
        return t?.TicketCode;
    }

    private async Task<WarehouseTransaction?> FindSourceExportAsync(string id, CancellationToken ct)
    {
        if (Guid.TryParse(id, out var guid))
        {
            return await db.WarehouseTransactions
                .Include(t => t.Items).ThenInclude(i => i.Material)
                .FirstOrDefaultAsync(t => t.Id == guid && t.TransactionType == TransactionType.Export, ct);
        }

        return await db.WarehouseTransactions
            .Include(t => t.Items).ThenInclude(i => i.Material)
            .FirstOrDefaultAsync(t => t.TicketCode == id && t.TransactionType == TransactionType.Export, ct);
    }

    private async Task<List<WarehouseTransaction>> LoadReturnsForSourceAsync(
        string sourceExportCode, CancellationToken ct, string? excludeReturnTicketCode = null)
    {
        var query = db.WarehouseTransactions
            .AsNoTracking()
            .Include(t => t.Items)
            .Where(t => t.TransactionType == TransactionType.ExportReturn
                        && t.LinkedTicketCode == sourceExportCode
                        && ActiveReturnStatuses.Contains(t.ApprovalStatus));

        if (!string.IsNullOrWhiteSpace(excludeReturnTicketCode))
            query = query.Where(t => t.TicketCode != excludeReturnTicketCode);

        return await query.ToListAsync(ct);
    }

    private async Task<Dictionary<string, List<WarehouseTransaction>>> LoadReturnsBySourceCodesAsync(
        List<string> sourceExportCodes, CancellationToken ct)
    {
        var returns = await db.WarehouseTransactions
            .AsNoTracking()
            .Include(t => t.Items)
            .Where(t => t.TransactionType == TransactionType.ExportReturn
                        && t.LinkedTicketCode != null
                        && sourceExportCodes.Contains(t.LinkedTicketCode)
                        && ActiveReturnStatuses.Contains(t.ApprovalStatus))
            .ToListAsync(ct);

        return returns
            .GroupBy(t => t.LinkedTicketCode!, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.ToList(), StringComparer.OrdinalIgnoreCase);
    }

    private async Task<HashSet<string>> LoadBlockedSourceCodesAsync(
        List<string> sourceExportCodes, CancellationToken ct, string? excludeReturnId = null)
    {
        var query = db.WarehouseTransactions
            .AsNoTracking()
            .Where(t => t.TransactionType == TransactionType.ExportReturn
                        && t.LinkedTicketCode != null
                        && sourceExportCodes.Contains(t.LinkedTicketCode)
                        && (t.ApprovalStatus == ApprovalStatus.Draft
                            || t.ApprovalStatus == ApprovalStatus.Pending));

        if (!string.IsNullOrWhiteSpace(excludeReturnId))
        {
            if (Guid.TryParse(excludeReturnId, out var guid))
                query = query.Where(t => t.Id != guid);
            else
                query = query.Where(t => t.TicketCode != excludeReturnId);
        }

        var blocked = await query
            .Select(t => t.LinkedTicketCode!)
            .Distinct()
            .ToListAsync(ct);

        return blocked.ToHashSet(StringComparer.OrdinalIgnoreCase);
    }

    private static List<WarehouseTransactionItem> MapItems(
        IEnumerable<ExportReturnItemDto> items,
        WarehouseTransaction export,
        string ticketCode) =>
        items.Select(i =>
        {
            var materialCode = ExportReturnLineHelper.ResolveMaterialCode(i.Sku, i.ProductId);
            var exportLine = export.Items.FirstOrDefault(e =>
                string.Equals(e.MaterialCode, materialCode, StringComparison.OrdinalIgnoreCase));
            return new WarehouseTransactionItem
            {
                TicketCode = ticketCode,
                MaterialCode = materialCode,
                Quantity = i.Qty,
                ConstructionPhase = string.IsNullOrWhiteSpace(exportLine?.ConstructionPhase)
                    ? null
                    : exportLine.ConstructionPhase.Trim(),
            };
        }).ToList();

    private IQueryable<WarehouseTransaction> Query() =>
        db.WarehouseTransactions
            .Include(t => t.Items).ThenInclude(i => i.Material)
            .Include(t => t.Attachments)
            .Where(t => t.TransactionType == TransactionType.ExportReturn);

    private async Task<WarehouseTransaction?> FindAsync(string id, CancellationToken ct)
    {
        if (Guid.TryParse(id, out var guid))
            return await Query().FirstOrDefaultAsync(t => t.Id == guid, ct);
        return await Query().FirstOrDefaultAsync(t => t.TicketCode == id, ct);
    }

    private async Task<ExportReturnDto> MapReturnResponseAsync(WarehouseTransaction t, CancellationToken ct)
    {
        var projectNames = await GetProjectNamesAsync([t], ct);
        var sourceExports = await GetSourceExportsAsync([t], ct);
        return MapReturn(t, projectNames, sourceExports);
    }

    private static ExportReturnDto MapReturn(
        WarehouseTransaction t,
        Dictionary<string, string> projectNames,
        Dictionary<string, WarehouseTransaction> sourceExports)
    {
        var projectCode = t.ProjectCode ?? t.BeneficiaryProjectCode;
        var projectName = projectCode != null ? projectNames.GetValueOrDefault(projectCode) : null;
        var source = t.LinkedTicketCode != null
            ? sourceExports.GetValueOrDefault(t.LinkedTicketCode)
            : null;

        return WmsMapper.ToExportReturn(
            t,
            source?.Id.ToString() ?? "",
            source?.TicketCode ?? t.LinkedTicketCode ?? "",
            projectName,
            t.Attachments
                .OrderBy(a => a.UploadedAt)
                .Select(a => new ImportAttachmentDto(
                    a.Id,
                    a.Kind,
                    a.FileName,
                    a.StorageKey,
                    WmsMapper.FormatCreatedAt(a.UploadedAt)))
                .ToList());
    }

    private async Task<Dictionary<string, string>> GetProjectNamesAsync(
        List<WarehouseTransaction> items, CancellationToken ct)
    {
        var codes = items
            .SelectMany(t => new[] { t.ProjectCode, t.BeneficiaryProjectCode })
            .Where(c => !string.IsNullOrWhiteSpace(c))
            .Distinct()
            .ToList();
        return await db.Projects
            .Where(p => codes.Contains(p.ProjectCode))
            .ToDictionaryAsync(p => p.ProjectCode, p => p.ProjectName, ct);
    }

    private async Task<Dictionary<string, WarehouseTransaction>> GetSourceExportsAsync(
        List<WarehouseTransaction> items, CancellationToken ct)
    {
        var codes = items
            .Select(t => t.LinkedTicketCode)
            .Where(c => !string.IsNullOrWhiteSpace(c))
            .Distinct()
            .ToList();
        if (codes.Count == 0)
            return new Dictionary<string, WarehouseTransaction>(StringComparer.OrdinalIgnoreCase);

        var exports = await db.WarehouseTransactions
            .AsNoTracking()
            .Where(t => codes.Contains(t.TicketCode) && t.TransactionType == TransactionType.Export)
            .ToListAsync(ct);

        return exports.ToDictionary(e => e.TicketCode, e => e, StringComparer.OrdinalIgnoreCase);
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

    private static string ResolveMaterialName(WarehouseTransaction export, string materialCode)
    {
        var item = export.Items.FirstOrDefault(i =>
            string.Equals(i.MaterialCode, materialCode, StringComparison.OrdinalIgnoreCase));
        return item is null ? materialCode : TransactionItemHelper.ResolveName(item);
    }

    private static string ResolveMaterialUnit(WarehouseTransaction export, string materialCode)
    {
        var item = export.Items.FirstOrDefault(i =>
            string.Equals(i.MaterialCode, materialCode, StringComparison.OrdinalIgnoreCase));
        return item is null ? "" : TransactionItemHelper.ResolveUnit(item);
    }
}
