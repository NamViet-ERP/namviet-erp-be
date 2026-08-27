using Microsoft.EntityFrameworkCore;
using NamViet.ERP.Api.Application.Dtos.Purchasing;
using NamViet.ERP.Modules.Warehouse.Infrastructure.Persistence;
using NamViet.ERP.SharedKernel.Common;

namespace NamViet.ERP.Api.Application.Services.Purchasing;

public interface IPurchaseDocumentService
{
    Task<PurchaseDocumentPagedResponse> ListAsync(
        int page,
        int pageSize,
        string? poCode,
        string? projectCode,
        int? supplierId,
        string? docType,
        string? search,
        CancellationToken ct = default);
}

public sealed class PurchaseDocumentService(WarehouseDbContext db) : IPurchaseDocumentService
{
    public async Task<PurchaseDocumentPagedResponse> ListAsync(
        int page,
        int pageSize,
        string? poCode,
        string? projectCode,
        int? supplierId,
        string? docType,
        string? search,
        CancellationToken ct = default)
    {
        (page, pageSize) = Paging.Normalize(page, pageSize, 100);

        var query =
            from a in db.PurchaseOrderAttachments.AsNoTracking()
            join o in db.PurchaseOrders.AsNoTracking() on a.PoCode equals o.Code
            select new { a, o };

        if (!string.IsNullOrWhiteSpace(poCode))
        {
            var code = poCode.Trim();
            query = query.Where(x => x.a.PoCode == code);
        }

        if (!string.IsNullOrWhiteSpace(projectCode))
        {
            var project = projectCode.Trim();
            query = query.Where(x => x.o.ProjectCode == project);
        }

        if (supplierId is > 0)
            query = query.Where(x => x.o.SupplierId == supplierId);

        if (!string.IsNullOrWhiteSpace(docType))
        {
            var type = docType.Trim().ToLowerInvariant();
            if (type is "iv") type = "invoice";
            query = query.Where(x => x.a.DocType.ToLower() == type);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var q = search.Trim().ToLowerInvariant();
            query = query.Where(x =>
                x.a.FileName.ToLower().Contains(q)
                || x.a.PoCode.ToLower().Contains(q)
                || (x.o.SupplierNameSnapshot != null && x.o.SupplierNameSnapshot.ToLower().Contains(q)));
        }

        var total = await query.CountAsync(ct);
        var totalPages = total == 0 ? 0 : (int)Math.Ceiling(total / (double)pageSize);

        var items = await query
            .OrderByDescending(x => x.a.UploadedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(x => new PurchaseDocumentDto(
                x.a.Id,
                x.a.PoCode,
                x.o.ProjectCode,
                x.o.SupplierId,
                x.o.SupplierNameSnapshot,
                x.a.DocType,
                x.a.FileName,
                x.a.UploadedAt,
                x.a.UploadedByName))
            .ToListAsync(ct);

        return new PurchaseDocumentPagedResponse(items, total, page, pageSize, totalPages);
    }
}
