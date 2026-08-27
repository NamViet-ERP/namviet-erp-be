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
    public async Task<IReadOnlyList<PurchaseOrderAttachmentDto>?> ListAttachmentsAsync(
        string code, CancellationToken ct = default)
    {
        var poCode = code.Trim();
        var exists = await db.PurchaseOrders.AsNoTracking().AnyAsync(o => o.Code == poCode, ct);
        if (!exists) return null;

        var rows = await db.PurchaseOrderAttachments.AsNoTracking()
            .Where(a => a.PoCode == poCode)
            .OrderByDescending(a => a.UploadedAt)
            .ToListAsync(ct);

        return rows.Select(ToAttachmentDto).ToList();
    }

    public async Task<PurchaseOrderAttachmentDto?> UploadAttachmentAsync(
        string code,
        string docType,
        string fileName,
        string contentType,
        Stream content,
        CancellationToken ct = default)
    {
        var poCode = code.Trim();
        var order = await db.PurchaseOrders.FirstOrDefaultAsync(o => o.Code == poCode, ct);
        if (order is null) return null;

        var normalizedDocType = NormalizeDocType(docType);
        EnsureAllowedExtension(fileName);

        var ext = Path.GetExtension(fileName);
        var relative = $"purchasing/orders/{poCode}/{normalizedDocType}/{Guid.NewGuid():N}{ext}";
        var stored = await fileStorage.SaveAsync(content, relative, contentType, ct);

        var attachment = new PurchaseOrderAttachment
        {
            PoCode = poCode,
            DocType = normalizedDocType,
            StorageKey = stored.StorageKey,
            FileName = Path.GetFileName(fileName),
            UploadedAt = DateTime.UtcNow,
            UploadedByName = currentUser.IsAuthenticated ? currentUser.Name : null,
        };
        db.PurchaseOrderAttachments.Add(attachment);
        order.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);

        return ToAttachmentDto(attachment);
    }

    public async Task<PurchaseOrderAttachmentDto?> GetAttachmentAsync(
        string code, Guid attachmentId, CancellationToken ct = default)
    {
        var poCode = code.Trim();
        var att = await db.PurchaseOrderAttachments.AsNoTracking()
            .FirstOrDefaultAsync(a => a.PoCode == poCode && a.Id == attachmentId, ct);
        return att is null ? null : ToAttachmentDto(att);
    }

    public async Task<bool> DeleteAttachmentAsync(string code, Guid attachmentId, CancellationToken ct = default)
    {
        var poCode = code.Trim();
        var order = await db.PurchaseOrders.FirstOrDefaultAsync(o => o.Code == poCode, ct);
        if (order is null) return false;

        var att = await db.PurchaseOrderAttachments
            .FirstOrDefaultAsync(a => a.PoCode == poCode && a.Id == attachmentId, ct);
        if (att is null) return false;

        db.PurchaseOrderAttachments.Remove(att);
        order.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        return true;
    }
}
