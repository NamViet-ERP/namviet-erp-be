using Microsoft.EntityFrameworkCore;
using NamViet.ERP.Api.Application.Dtos.Purchasing;
using NamViet.ERP.Modules.Warehouse.Domain.Entities;
using NamViet.ERP.Modules.Warehouse.Infrastructure.Persistence;

namespace NamViet.ERP.Api.Application.Services.Purchasing;

public interface ISupplierContactService
{
    Task<SupplierContactPagedResponse> ListAsync(
        int? supplierId, string? search, int page, int pageSize, CancellationToken ct = default);

    Task<SupplierContactDto> CreateAsync(UpsertSupplierContactRequest request, CancellationToken ct = default);
    Task<SupplierContactDto> UpdateAsync(int id, UpsertSupplierContactRequest request, CancellationToken ct = default);
    Task DeleteAsync(int id, CancellationToken ct = default);
}

public sealed class SupplierContactService(WarehouseDbContext db) : ISupplierContactService
{
    public async Task<SupplierContactPagedResponse> ListAsync(
        int? supplierId, string? search, int page, int pageSize, CancellationToken ct = default)
    {
        page = Math.Max(page, 1);
        pageSize = Math.Clamp(pageSize, 1, 200);

        var query = db.SupplierContacts.AsNoTracking().Where(c => c.Supplier != null);
        if (supplierId is int sid)
            query = query.Where(c => c.SupplierId == sid);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var q = search.Trim();
            query = query.Where(c =>
                EF.Functions.ILike(c.FullName, $"%{q}%")
                || (c.Phone != null && EF.Functions.ILike(c.Phone, $"%{q}%"))
                || (c.Email != null && EF.Functions.ILike(c.Email, $"%{q}%"))
                || (c.Position != null && EF.Functions.ILike(c.Position, $"%{q}%"))
                || EF.Functions.ILike(c.Supplier!.SupplierName, $"%{q}%"));
        }

        var total = await query.CountAsync(ct);
        var items = await query
            .OrderBy(c => c.Supplier!.SupplierName)
            .ThenByDescending(c => c.IsPrimary)
            .ThenBy(c => c.FullName)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(c => new SupplierContactDto(
                c.Id, c.SupplierId, c.Supplier!.SupplierName, c.FullName,
                c.Position, c.Phone, c.Email, c.Note, c.IsPrimary))
            .ToListAsync(ct);

        return new SupplierContactPagedResponse(
            items, total, page, pageSize, (int)Math.Ceiling(total / (double)pageSize));
    }

    public async Task<SupplierContactDto> CreateAsync(
        UpsertSupplierContactRequest request, CancellationToken ct = default)
    {
        var supplier = await db.Suppliers.FirstOrDefaultAsync(s => s.Id == request.SupplierId, ct)
            ?? throw new KeyNotFoundException($"Không tìm thấy nhà cung cấp #{request.SupplierId}.");

        var contact = new SupplierContact
        {
            SupplierId = supplier.Id,
            FullName = RequireName(request.FullName),
            Position = Trim(request.Position),
            Phone = Trim(request.Phone),
            Email = ValidateEmail(request.Email),
            Note = Trim(request.Note),
            IsPrimary = request.IsPrimary,
        };

        db.SupplierContacts.Add(contact);
        if (contact.IsPrimary)
            await ClearOtherPrimaryAsync(supplier.Id, contactIdToKeep: null, ct);

        await db.SaveChangesAsync(ct);
        return ToDto(contact, supplier.SupplierName);
    }

    public async Task<SupplierContactDto> UpdateAsync(
        int id, UpsertSupplierContactRequest request, CancellationToken ct = default)
    {
        var contact = await db.SupplierContacts.FirstOrDefaultAsync(c => c.Id == id, ct)
            ?? throw new KeyNotFoundException($"Không tìm thấy liên hệ NCC #{id}.");

        var supplier = await db.Suppliers.FirstOrDefaultAsync(s => s.Id == request.SupplierId, ct)
            ?? throw new KeyNotFoundException($"Không tìm thấy nhà cung cấp #{request.SupplierId}.");

        contact.SupplierId = supplier.Id;
        contact.FullName = RequireName(request.FullName);
        contact.Position = Trim(request.Position);
        contact.Phone = Trim(request.Phone);
        contact.Email = ValidateEmail(request.Email);
        contact.Note = Trim(request.Note);
        contact.IsPrimary = request.IsPrimary;
        contact.UpdatedAt = DateTime.UtcNow;

        if (contact.IsPrimary)
            await ClearOtherPrimaryAsync(supplier.Id, contact.Id, ct);

        await db.SaveChangesAsync(ct);
        return ToDto(contact, supplier.SupplierName);
    }

    public async Task DeleteAsync(int id, CancellationToken ct = default)
    {
        var contact = await db.SupplierContacts.FirstOrDefaultAsync(c => c.Id == id, ct)
            ?? throw new KeyNotFoundException($"Không tìm thấy liên hệ NCC #{id}.");

        contact.DeletedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
    }

    private async Task ClearOtherPrimaryAsync(int supplierId, int? contactIdToKeep, CancellationToken ct)
    {
        var others = await db.SupplierContacts
            .Where(c => c.SupplierId == supplierId && c.IsPrimary
                        && (contactIdToKeep == null || c.Id != contactIdToKeep))
            .ToListAsync(ct);

        foreach (var other in others)
        {
            other.IsPrimary = false;
            other.UpdatedAt = DateTime.UtcNow;
        }
    }

    private static SupplierContactDto ToDto(SupplierContact c, string supplierName) =>
        new(c.Id, c.SupplierId, supplierName, c.FullName,
            c.Position, c.Phone, c.Email, c.Note, c.IsPrimary);

    private static string RequireName(string? name)
    {
        var value = (name ?? string.Empty).Trim();
        if (value.Length == 0)
            throw new PurchasingConflictException("Tên người liên hệ là bắt buộc.");
        if (value.Length > 255)
            throw new PurchasingConflictException("Tên người liên hệ tối đa 255 ký tự.");
        return value;
    }

    private static string? ValidateEmail(string? email)
    {
        var value = Trim(email);
        if (value is null)
            return null;
        if (!value.Contains('@') || value.StartsWith('@') || value.EndsWith('@'))
            throw new PurchasingConflictException($"Email '{value}' không hợp lệ.");
        return value;
    }

    private static string? Trim(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
