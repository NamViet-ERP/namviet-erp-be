using Microsoft.EntityFrameworkCore;
using NamViet.ERP.Api.Application.Dtos.Purchasing;
using NamViet.ERP.Api.Application.Mappers.Purchasing;
using NamViet.ERP.Api.Application.Services.Shared;
using NamViet.ERP.Api.Services.Storage;
using NamViet.ERP.Modules.Warehouse.Domain.Entities;
using NamViet.ERP.Modules.Warehouse.Infrastructure.Persistence;
using NamViet.ERP.SharedKernel.Interfaces;
using System.Text.Json;
using NamViet.ERP.SharedKernel.Common;

namespace NamViet.ERP.Api.Application.Services.Purchasing;

public sealed partial class PurchaseOrderService
{
    public async Task<IReadOnlyList<PurchaseOrderInstallmentDocumentDto>?> ListInstallmentDocumentsAsync(
        string code, int stageNumber, CancellationToken ct = default)
    {
        var installment = await FindInstallmentAsync(code, stageNumber, asTracking: false, ct);
        if (installment is null) return null;
        return installment.Documents
            .OrderByDescending(d => d.UploadedAt)
            .Select(ToInstallmentDocumentDto)
            .ToList();
    }

    public async Task<PurchaseOrderInstallmentDocumentDto?> UploadInstallmentDocumentAsync(
        string code,
        int stageNumber,
        string docType,
        string fileName,
        string contentType,
        Stream content,
        CancellationToken ct = default)
    {
        var installment = await FindInstallmentAsync(code, stageNumber, asTracking: true, ct);
        if (installment is null) return null;
        EnsureInstallmentEditable(installment);

        var normalizedDocType = NormalizeInstallmentDocType(docType);
        var required = DeserializeRequiredDocTypes(installment.RequiredDocTypes);
        if (required.Count > 0
            && !required.Contains(normalizedDocType, StringComparer.OrdinalIgnoreCase))
            throw new PurchasingConflictException(
                $"Loại chứng từ '{normalizedDocType}' không thuộc danh sách bắt buộc của đợt {stageNumber}.");
        EnsureAllowedExtension(fileName);
        var ext = Path.GetExtension(fileName);
        var relative =
            $"purchasing/orders/{installment.PoCode}/installments/{stageNumber}/{normalizedDocType}/{Guid.NewGuid():N}{ext}";
        var stored = await fileStorage.SaveAsync(content, relative, contentType, ct);
        var document = new PurchaseOrderInstallmentDocument
        {
            InstallmentId = installment.Id,
            DocType = normalizedDocType,
            FileName = Path.GetFileName(fileName),
            ContentType = string.IsNullOrWhiteSpace(contentType)
                ? "application/octet-stream"
                : contentType.Trim(),
            StoragePath = stored.StorageKey,
            UploadedAt = DateTime.UtcNow,
            UploadedByName = currentUser.IsAuthenticated ? currentUser.Name : null,
        };
        installment.Documents.Add(document);
        installment.PurchaseOrder.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        return ToInstallmentDocumentDto(document);
    }

    public async Task<PurchaseOrderInstallmentDocumentDto?> GetInstallmentDocumentAsync(
        string code, int stageNumber, int documentId, CancellationToken ct = default)
    {
        var poCode = code.Trim();
        var document = await db.PurchaseOrderInstallmentDocuments.AsNoTracking()
            .Include(d => d.Installment)
            .FirstOrDefaultAsync(d =>
                d.Id == documentId &&
                d.Installment.PoCode == poCode &&
                d.Installment.StageNumber == stageNumber, ct);
        return document is null ? null : ToInstallmentDocumentDto(document);
    }

    public async Task<bool> DeleteInstallmentDocumentAsync(
        string code, int stageNumber, int documentId, CancellationToken ct = default)
    {
        var installment = await FindInstallmentAsync(code, stageNumber, asTracking: true, ct);
        if (installment is null) return false;
        EnsureInstallmentEditable(installment);
        var document = installment.Documents.FirstOrDefault(d => d.Id == documentId);
        if (document is null) return false;

        db.PurchaseOrderInstallmentDocuments.Remove(document);
        installment.PurchaseOrder.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<PurchaseOrderInstallmentDto?> CompleteInstallmentAsync(
        string code, int stageNumber, CancellationToken ct = default)
    {
        var poCode = code.Trim();
        var installments = await db.PurchaseOrderInstallments
            .Include(i => i.Documents)
            .Include(i => i.PurchaseOrder)
            .Where(i => i.PoCode == poCode)
            .OrderBy(i => i.StageNumber)
            .ToListAsync(ct);
        var installment = installments.FirstOrDefault(i => i.StageNumber == stageNumber);
        if (installment is null) return null;
        EnsureInstallmentEditable(installment);

        var required = DeserializeRequiredDocTypes(installment.RequiredDocTypes);
        var uploaded = installment.Documents
            .Select(d => d.DocType)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var missing = required.Where(t => !uploaded.Contains(t)).ToList();
        if (missing.Count > 0)
            throw new PurchasingConflictException(
                $"Chưa đủ tài liệu bắt buộc cho đợt {stageNumber}: {string.Join(", ", missing)}.");

        installment.Status = PurchaseOrderInstallmentStatuses.Completed;
        var next = installments.FirstOrDefault(i => i.StageNumber == stageNumber + 1);
        if (next is not null)
            next.Status = PurchaseOrderInstallmentStatuses.Open;
        installment.PurchaseOrder.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        return PurchasingMapper.ToInstallmentDto(installment);
    }
}
