using Microsoft.EntityFrameworkCore;
using NamViet.ERP.Api.Application.Dtos.Purchasing;
using NamViet.ERP.Api.Services.Storage;
using NamViet.ERP.Modules.Warehouse.Domain.Entities;
using NamViet.ERP.Modules.Warehouse.Infrastructure.Persistence;
using NamViet.ERP.SharedKernel.Interfaces;
using NamViet.ERP.SharedKernel.Time;

namespace NamViet.ERP.Api.Application.Services.Purchasing;

public interface IPurchaseContractService
{
    Task<PurchaseContractPagedResponse> ListAsync(
        int page,
        int pageSize,
        string? projectCode,
        int? supplierId,
        string? poCode,
        string? status,
        string? search,
        CancellationToken ct = default);

    Task<PurchaseContractDto?> GetByIdAsync(Guid id, CancellationToken ct = default);

    Task<PurchaseContractDto> CreateAsync(CreatePurchaseContractRequest request, CancellationToken ct = default);

    Task<PurchaseContractDto?> UpdateAsync(Guid id, UpdatePurchaseContractRequest request, CancellationToken ct = default);

    Task<PurchaseContractDto?> UploadAsync(
        Guid id,
        string fileName,
        string contentType,
        Stream content,
        CancellationToken ct = default);

    Task<(PurchaseContractDto Dto, string StorageKey)?> GetForDownloadAsync(Guid id, CancellationToken ct = default);

    Task<bool> SoftDeleteAsync(Guid id, CancellationToken ct = default);

    /// <summary>Import file HĐ từ đính kèm PO (docType=contract), idempotent theo PoCode+StorageKey.</summary>
    Task<ImportPurchaseContractsFromPoResult> ImportFromPoAttachmentsAsync(CancellationToken ct = default);
}

public sealed class PurchaseContractService(
    WarehouseDbContext db,
    IFileStorage fileStorage,
    ICurrentUser currentUser) : IPurchaseContractService
{
    private static readonly string[] AllowedExtensions =
    [
        ".jpg", ".jpeg", ".png", ".webp", ".gif",
        ".pdf",
        ".doc", ".docx", ".rtf",
        ".xls", ".xlsx", ".xlsm", ".xlsb", ".csv",
        ".ppt", ".pptx",
        ".txt",
    ];

    public async Task<PurchaseContractPagedResponse> ListAsync(
        int page,
        int pageSize,
        string? projectCode,
        int? supplierId,
        string? poCode,
        string? status,
        string? search,
        CancellationToken ct = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var today = VietnamClock.Today;
        var query = db.PurchaseContracts.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(projectCode))
            query = query.Where(c => c.ProjectCode == projectCode.Trim());

        if (supplierId is > 0)
            query = query.Where(c => c.SupplierId == supplierId);

        if (!string.IsNullOrWhiteSpace(poCode))
            query = query.Where(c => c.PoCode == poCode.Trim());

        if (!string.IsNullOrWhiteSpace(status))
            query = query.Where(c => c.Status.ToLower() == status.Trim().ToLower());

        if (!string.IsNullOrWhiteSpace(search))
        {
            var q = search.Trim().ToLowerInvariant();
            query = query.Where(c =>
                c.Code.ToLower().Contains(q)
                || c.ContractNo.ToLower().Contains(q)
                || c.Title.ToLower().Contains(q)
                || (c.SupplierName != null && c.SupplierName.ToLower().Contains(q))
                || (c.PoCode != null && c.PoCode.ToLower().Contains(q)));
        }

        var total = await query.CountAsync(ct);
        var totalPages = total == 0 ? 0 : (int)Math.Ceiling(total / (double)pageSize);

        var rows = await query
            .OrderByDescending(c => c.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        foreach (var row in rows)
            row.RefreshStatus(today);

        return new PurchaseContractPagedResponse(
            rows.Select(ToDto).ToList(),
            total,
            page,
            pageSize,
            totalPages);
    }

    public async Task<PurchaseContractDto?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var row = await db.PurchaseContracts.AsNoTracking().FirstOrDefaultAsync(c => c.Id == id, ct);
        if (row is null) return null;
        row.RefreshStatus(VietnamClock.Today);
        return ToDto(row);
    }

    public async Task<PurchaseContractDto> CreateAsync(
        CreatePurchaseContractRequest request,
        CancellationToken ct = default)
    {
        var contractNo = (request.ContractNo ?? string.Empty).Trim();
        var title = (request.Title ?? string.Empty).Trim();
        if (string.IsNullOrEmpty(contractNo))
            throw new PurchasingConflictException("Số hợp đồng là bắt buộc.");
        if (string.IsNullOrEmpty(title))
            throw new PurchasingConflictException("Tiêu đề hợp đồng là bắt buộc.");

        var (supplierId, supplierName, projectCode, poCode) = await ResolveLinksAsync(
            request.SupplierId,
            request.ProjectCode,
            request.PoCode,
            ct);

        var status = NormalizeStatus(request.Status) ?? PurchaseContractStatuses.Draft;
        var seq = await db.PurchaseContracts.IgnoreQueryFilters().CountAsync(ct) + 1;
        var entity = new PurchaseContract
        {
            Code = PurchaseContract.GenerateCode(seq),
            ContractNo = contractNo,
            Title = title,
            PoCode = poCode,
            ProjectCode = projectCode,
            SupplierId = supplierId,
            SupplierName = supplierName,
            Status = status,
            SignedDate = request.SignedDate,
            EffectiveFrom = request.EffectiveFrom,
            EffectiveTo = request.EffectiveTo,
            Amount = request.Amount is > 0 ? request.Amount : null,
            Note = string.IsNullOrWhiteSpace(request.Note) ? null : request.Note.Trim(),
        };
        entity.RefreshStatus(VietnamClock.Today);

        db.PurchaseContracts.Add(entity);
        await db.SaveChangesAsync(ct);
        return ToDto(entity);
    }

    public async Task<PurchaseContractDto?> UpdateAsync(
        Guid id,
        UpdatePurchaseContractRequest request,
        CancellationToken ct = default)
    {
        var entity = await db.PurchaseContracts.FirstOrDefaultAsync(c => c.Id == id, ct);
        if (entity is null) return null;

        if (request.ContractNo is not null)
        {
            var contractNo = request.ContractNo.Trim();
            if (string.IsNullOrEmpty(contractNo))
                throw new PurchasingConflictException("Số hợp đồng là bắt buộc.");
            entity.ContractNo = contractNo;
        }

        if (request.Title is not null)
        {
            var title = request.Title.Trim();
            if (string.IsNullOrEmpty(title))
                throw new PurchasingConflictException("Tiêu đề hợp đồng là bắt buộc.");
            entity.Title = title;
        }

        if (request.SupplierId is not null || request.ProjectCode is not null || request.PoCode is not null)
        {
            var (supplierId, supplierName, projectCode, poCode) = await ResolveLinksAsync(
                request.SupplierId ?? entity.SupplierId,
                request.ProjectCode ?? entity.ProjectCode,
                request.PoCode ?? entity.PoCode,
                ct);
            entity.SupplierId = supplierId;
            entity.SupplierName = supplierName;
            entity.ProjectCode = projectCode;
            entity.PoCode = poCode;
        }

        if (request.Status is not null)
            entity.Status = NormalizeStatus(request.Status)
                ?? throw new PurchasingConflictException("Trạng thái hợp đồng không hợp lệ.");

        if (request.SignedDate is not null) entity.SignedDate = request.SignedDate;
        if (request.EffectiveFrom is not null) entity.EffectiveFrom = request.EffectiveFrom;
        if (request.EffectiveTo is not null) entity.EffectiveTo = request.EffectiveTo;
        if (request.Amount is not null)
            entity.Amount = request.Amount > 0 ? request.Amount : null;
        if (request.Note is not null)
            entity.Note = string.IsNullOrWhiteSpace(request.Note) ? null : request.Note.Trim();

        entity.RefreshStatus(VietnamClock.Today);
        entity.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        return ToDto(entity);
    }

    public async Task<PurchaseContractDto?> UploadAsync(
        Guid id,
        string fileName,
        string contentType,
        Stream content,
        CancellationToken ct = default)
    {
        var entity = await db.PurchaseContracts.FirstOrDefaultAsync(c => c.Id == id, ct);
        if (entity is null) return null;

        EnsureAllowedExtension(fileName);
        var ext = Path.GetExtension(fileName);
        var relative = $"purchasing/contracts/{entity.Code}/{Guid.NewGuid():N}{ext}";
        var stored = await fileStorage.SaveAsync(content, relative, contentType, ct);

        entity.FileName = Path.GetFileName(fileName);
        entity.StorageKey = stored.StorageKey;
        entity.UploadedAt = DateTime.UtcNow;
        entity.UploadedByName = currentUser.IsAuthenticated ? currentUser.Name : null;
        entity.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        return ToDto(entity);
    }

    public async Task<(PurchaseContractDto Dto, string StorageKey)?> GetForDownloadAsync(
        Guid id,
        CancellationToken ct = default)
    {
        var row = await db.PurchaseContracts.AsNoTracking().FirstOrDefaultAsync(c => c.Id == id, ct);
        if (row is null || string.IsNullOrWhiteSpace(row.StorageKey) || string.IsNullOrWhiteSpace(row.FileName))
            return null;
        return (ToDto(row), row.StorageKey);
    }

    public async Task<bool> SoftDeleteAsync(Guid id, CancellationToken ct = default)
    {
        var entity = await db.PurchaseContracts.FirstOrDefaultAsync(c => c.Id == id, ct);
        if (entity is null) return false;
        entity.DeletedAt = DateTime.UtcNow;
        entity.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<ImportPurchaseContractsFromPoResult> ImportFromPoAttachmentsAsync(
        CancellationToken ct = default)
    {
        var attachments = await db.PurchaseOrderAttachments.AsNoTracking()
            .Where(a => a.DocType.ToLower() == PurchaseOrderAttachmentDocTypes.Contract)
            .OrderBy(a => a.UploadedAt)
            .ToListAsync(ct);

        if (attachments.Count == 0)
            return new ImportPurchaseContractsFromPoResult(0, 0);

        var existingKeys = await db.PurchaseContracts.AsNoTracking()
            .Where(c => c.PoCode != null && c.StorageKey != null)
            .Select(c => new { c.PoCode, c.StorageKey })
            .ToListAsync(ct);
        var existing = existingKeys
            .Select(x => $"{x.PoCode}|{x.StorageKey}")
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var poCodes = attachments.Select(a => a.PoCode).Distinct().ToList();
        var orders = await db.PurchaseOrders.AsNoTracking()
            .Where(o => poCodes.Contains(o.Code))
            .ToDictionaryAsync(o => o.Code, ct);

        var imported = 0;
        var skipped = 0;
        var seqBase = await db.PurchaseContracts.IgnoreQueryFilters().CountAsync(ct);

        foreach (var att in attachments)
        {
            var key = $"{att.PoCode}|{att.StorageKey}";
            if (existing.Contains(key))
            {
                skipped++;
                continue;
            }

            orders.TryGetValue(att.PoCode, out var order);
            var title = string.IsNullOrWhiteSpace(att.FileName)
                ? $"Hợp đồng {att.PoCode}"
                : Path.GetFileNameWithoutExtension(att.FileName);
            if (string.IsNullOrWhiteSpace(title))
                title = $"Hợp đồng {att.PoCode}";

            seqBase++;
            var entity = new PurchaseContract
            {
                Code = PurchaseContract.GenerateCode(seqBase),
                ContractNo = $"{att.PoCode}-HD",
                Title = title.Length > 500 ? title[..500] : title,
                PoCode = att.PoCode,
                ProjectCode = order?.ProjectCode,
                SupplierId = order?.SupplierId,
                SupplierName = order?.SupplierNameSnapshot,
                Status = PurchaseContractStatuses.Active,
                FileName = att.FileName,
                StorageKey = att.StorageKey,
                UploadedAt = att.UploadedAt,
                UploadedByName = att.UploadedByName,
                Note = "Nhập từ đính kèm PO (docType=contract).",
            };
            entity.RefreshStatus(VietnamClock.Today);
            db.PurchaseContracts.Add(entity);
            existing.Add(key);
            imported++;
        }

        if (imported > 0)
            await db.SaveChangesAsync(ct);

        return new ImportPurchaseContractsFromPoResult(imported, skipped);
    }

    private async Task<(int? SupplierId, string? SupplierName, string? ProjectCode, string? PoCode)> ResolveLinksAsync(
        int? supplierId,
        string? projectCode,
        string? poCode,
        CancellationToken ct)
    {
        string? resolvedPo = string.IsNullOrWhiteSpace(poCode) ? null : poCode.Trim();
        string? resolvedProject = string.IsNullOrWhiteSpace(projectCode) ? null : projectCode.Trim();
        int? resolvedSupplierId = supplierId is > 0 ? supplierId : null;
        string? resolvedSupplierName = null;

        if (resolvedPo is not null)
        {
            var order = await db.PurchaseOrders.AsNoTracking()
                .FirstOrDefaultAsync(o => o.Code == resolvedPo, ct)
                ?? throw new PurchasingConflictException($"Không tìm thấy PO {resolvedPo}.");
            resolvedProject ??= order.ProjectCode;
            resolvedSupplierId ??= order.SupplierId;
            resolvedSupplierName = order.SupplierNameSnapshot;
        }

        if (resolvedSupplierId is not null)
        {
            var supplier = await db.Suppliers.AsNoTracking()
                .FirstOrDefaultAsync(s => s.Id == resolvedSupplierId.Value, ct)
                ?? throw new PurchasingConflictException("Không tìm thấy nhà cung cấp.");
            resolvedSupplierName = supplier.SupplierName;
        }

        if (resolvedProject is not null)
        {
            var exists = await db.Projects.AsNoTracking().AnyAsync(p => p.ProjectCode == resolvedProject, ct);
            if (!exists)
                throw new PurchasingConflictException($"Không tìm thấy dự án {resolvedProject}.");
        }

        return (resolvedSupplierId, resolvedSupplierName, resolvedProject, resolvedPo);
    }

    private static string? NormalizeStatus(string? status)
    {
        if (string.IsNullOrWhiteSpace(status)) return null;
        var t = status.Trim().ToLowerInvariant();
        return PurchaseContractStatuses.Allowed.Contains(t) ? t : null;
    }

    private static void EnsureAllowedExtension(string fileName)
    {
        var ext = Path.GetExtension(fileName).ToLowerInvariant();
        if (!AllowedExtensions.Contains(ext))
            throw new PurchasingConflictException(
                $"Định dạng file không hỗ trợ: {ext}. Cho phép ảnh, PDF và file Microsoft Office.");
    }

    private static PurchaseContractDto ToDto(PurchaseContract c) => new(
        c.Id,
        c.Code,
        c.ContractNo,
        c.Title,
        c.PoCode,
        c.ProjectCode,
        c.SupplierId,
        c.SupplierName,
        c.Status,
        c.SignedDate,
        c.EffectiveFrom,
        c.EffectiveTo,
        c.Amount,
        c.Note,
        c.FileName,
        !string.IsNullOrWhiteSpace(c.StorageKey),
        c.UploadedAt,
        c.UploadedByName,
        c.CreatedAt);
}
