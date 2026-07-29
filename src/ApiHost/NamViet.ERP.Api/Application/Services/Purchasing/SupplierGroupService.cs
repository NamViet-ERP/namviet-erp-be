using Microsoft.EntityFrameworkCore;
using NamViet.ERP.Api.Application.Dtos.Purchasing;
using NamViet.ERP.Modules.Warehouse.Domain.Entities;
using NamViet.ERP.Modules.Warehouse.Infrastructure.Persistence;

namespace NamViet.ERP.Api.Application.Services.Purchasing;

public interface ISupplierGroupService
{
    Task<SupplierGroupPagedResponse> ListAsync(
        string? search, int page, int pageSize, CancellationToken ct = default);

    Task<SupplierGroupDto> GetAsync(int id, CancellationToken ct = default);
    Task<SupplierGroupDto> CreateAsync(UpsertSupplierGroupRequest request, CancellationToken ct = default);
    Task<SupplierGroupDto> UpdateAsync(int id, UpsertSupplierGroupRequest request, CancellationToken ct = default);
    Task DeleteAsync(int id, CancellationToken ct = default);

    Task<IReadOnlyList<SupplierGroupMemberDto>> ListMembersAsync(int groupId, CancellationToken ct = default);

    Task<IReadOnlyList<SupplierGroupMemberDto>> SetMembersAsync(
        int groupId, SetSupplierGroupMembersRequest request, CancellationToken ct = default);

    Task<IReadOnlyList<SupplierGroupRefDto>> ListGroupsOfSupplierAsync(
        int supplierId, CancellationToken ct = default);

    Task<IReadOnlyList<SupplierGroupRefDto>> SetGroupsOfSupplierAsync(
        int supplierId, SetSupplierGroupsRequest request, CancellationToken ct = default);
}

public sealed class SupplierGroupService(WarehouseDbContext db) : ISupplierGroupService
{
    public async Task<SupplierGroupPagedResponse> ListAsync(
        string? search, int page, int pageSize, CancellationToken ct = default)
    {
        page = Math.Max(page, 1);
        pageSize = Math.Clamp(pageSize, 1, 200);

        var query = db.SupplierGroups.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(search))
        {
            var q = search.Trim();
            query = query.Where(g =>
                EF.Functions.ILike(g.Name, $"%{q}%")
                || (g.Description != null && EF.Functions.ILike(g.Description, $"%{q}%")));
        }

        var total = await query.CountAsync(ct);
        var items = await query
            .OrderBy(g => g.Name)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            // Supplier != null loại NCC đã xóa mềm (query filter biến join thành NULL).
            .Select(g => new SupplierGroupDto(
                g.Id, g.Name, g.Description, g.Members.Count(m => m.Supplier != null)))
            .ToListAsync(ct);

        return new SupplierGroupPagedResponse(
            items, total, page, pageSize, (int)Math.Ceiling(total / (double)pageSize));
    }

    public async Task<SupplierGroupDto> GetAsync(int id, CancellationToken ct = default) =>
        await db.SupplierGroups.AsNoTracking()
            .Where(g => g.Id == id)
            .Select(g => new SupplierGroupDto(
                g.Id, g.Name, g.Description, g.Members.Count(m => m.Supplier != null)))
            .FirstOrDefaultAsync(ct)
        ?? throw new KeyNotFoundException($"Không tìm thấy nhóm NCC #{id}.");

    public async Task<SupplierGroupDto> CreateAsync(
        UpsertSupplierGroupRequest request, CancellationToken ct = default)
    {
        var name = NormalizeName(request.Name);
        if (await db.SupplierGroups.AnyAsync(g => g.Name == name, ct))
            throw new PurchasingConflictException($"Nhóm NCC '{name}' đã tồn tại.");

        var group = new SupplierGroup { Name = name, Description = Trim(request.Description) };
        db.SupplierGroups.Add(group);
        await db.SaveChangesAsync(ct);

        return new SupplierGroupDto(group.Id, group.Name, group.Description, 0);
    }

    public async Task<SupplierGroupDto> UpdateAsync(
        int id, UpsertSupplierGroupRequest request, CancellationToken ct = default)
    {
        var group = await db.SupplierGroups.FirstOrDefaultAsync(g => g.Id == id, ct)
            ?? throw new KeyNotFoundException($"Không tìm thấy nhóm NCC #{id}.");

        var name = NormalizeName(request.Name);
        if (await db.SupplierGroups.AnyAsync(g => g.Id != id && g.Name == name, ct))
            throw new PurchasingConflictException($"Nhóm NCC '{name}' đã tồn tại.");

        group.Name = name;
        group.Description = Trim(request.Description);
        group.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);

        var count = await db.SupplierGroupMembers
            .CountAsync(m => m.SupplierGroupId == id && m.Supplier != null, ct);
        return new SupplierGroupDto(group.Id, group.Name, group.Description, count);
    }

    public async Task DeleteAsync(int id, CancellationToken ct = default)
    {
        var group = await db.SupplierGroups.FirstOrDefaultAsync(g => g.Id == id, ct)
            ?? throw new KeyNotFoundException($"Không tìm thấy nhóm NCC #{id}.");

        // Xóa mềm nhóm; bỏ luôn liên kết thành viên để không còn đếm vào NCC.
        var members = await db.SupplierGroupMembers.Where(m => m.SupplierGroupId == id).ToListAsync(ct);
        db.SupplierGroupMembers.RemoveRange(members);
        group.DeletedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<SupplierGroupMemberDto>> ListMembersAsync(
        int groupId, CancellationToken ct = default)
    {
        await EnsureGroupExistsAsync(groupId, ct);

        return await db.SupplierGroupMembers.AsNoTracking()
            .Where(m => m.SupplierGroupId == groupId && m.Supplier != null)
            .OrderBy(m => m.Supplier!.SupplierName)
            .Select(m => new SupplierGroupMemberDto(
                m.SupplierId, m.Supplier!.SupplierCode, m.Supplier!.SupplierName))
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<SupplierGroupMemberDto>> SetMembersAsync(
        int groupId, SetSupplierGroupMembersRequest request, CancellationToken ct = default)
    {
        await EnsureGroupExistsAsync(groupId, ct);

        var wanted = (request.SupplierIds ?? []).Distinct().ToList();
        if (wanted.Count > 0)
        {
            var found = await db.Suppliers.CountAsync(s => wanted.Contains(s.Id), ct);
            if (found != wanted.Count)
                throw new PurchasingConflictException("Danh sách chứa nhà cung cấp không tồn tại.");
        }

        var current = await db.SupplierGroupMembers
            .Where(m => m.SupplierGroupId == groupId)
            .ToListAsync(ct);

        db.SupplierGroupMembers.RemoveRange(current.Where(m => !wanted.Contains(m.SupplierId)));
        var currentIds = current.Select(m => m.SupplierId).ToHashSet();
        db.SupplierGroupMembers.AddRange(wanted
            .Where(id => !currentIds.Contains(id))
            .Select(id => new SupplierGroupMember { SupplierGroupId = groupId, SupplierId = id }));

        await db.SaveChangesAsync(ct);
        return await ListMembersAsync(groupId, ct);
    }

    public async Task<IReadOnlyList<SupplierGroupRefDto>> ListGroupsOfSupplierAsync(
        int supplierId, CancellationToken ct = default)
    {
        await EnsureSupplierExistsAsync(supplierId, ct);

        return await db.SupplierGroupMembers.AsNoTracking()
            .Where(m => m.SupplierId == supplierId && m.Group != null)
            .OrderBy(m => m.Group!.Name)
            .Select(m => new SupplierGroupRefDto(m.SupplierGroupId, m.Group!.Name))
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<SupplierGroupRefDto>> SetGroupsOfSupplierAsync(
        int supplierId, SetSupplierGroupsRequest request, CancellationToken ct = default)
    {
        await EnsureSupplierExistsAsync(supplierId, ct);

        var wanted = (request.GroupIds ?? []).Distinct().ToList();
        if (wanted.Count > 0)
        {
            var found = await db.SupplierGroups.CountAsync(g => wanted.Contains(g.Id), ct);
            if (found != wanted.Count)
                throw new PurchasingConflictException("Danh sách chứa nhóm NCC không tồn tại.");
        }

        var current = await db.SupplierGroupMembers
            .Where(m => m.SupplierId == supplierId)
            .ToListAsync(ct);

        db.SupplierGroupMembers.RemoveRange(current.Where(m => !wanted.Contains(m.SupplierGroupId)));
        var currentIds = current.Select(m => m.SupplierGroupId).ToHashSet();
        db.SupplierGroupMembers.AddRange(wanted
            .Where(id => !currentIds.Contains(id))
            .Select(id => new SupplierGroupMember { SupplierGroupId = id, SupplierId = supplierId }));

        await db.SaveChangesAsync(ct);
        return await ListGroupsOfSupplierAsync(supplierId, ct);
    }

    private async Task EnsureGroupExistsAsync(int groupId, CancellationToken ct)
    {
        if (!await db.SupplierGroups.AnyAsync(g => g.Id == groupId, ct))
            throw new KeyNotFoundException($"Không tìm thấy nhóm NCC #{groupId}.");
    }

    private async Task EnsureSupplierExistsAsync(int supplierId, CancellationToken ct)
    {
        if (!await db.Suppliers.AnyAsync(s => s.Id == supplierId, ct))
            throw new KeyNotFoundException($"Không tìm thấy nhà cung cấp #{supplierId}.");
    }

    private static string NormalizeName(string? name)
    {
        var value = (name ?? string.Empty).Trim();
        if (value.Length == 0)
            throw new PurchasingConflictException("Tên nhóm NCC là bắt buộc.");
        if (value.Length > 255)
            throw new PurchasingConflictException("Tên nhóm NCC tối đa 255 ký tự.");
        return value;
    }

    private static string? Trim(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
