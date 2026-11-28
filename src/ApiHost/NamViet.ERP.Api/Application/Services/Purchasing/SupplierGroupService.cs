using Microsoft.EntityFrameworkCore;
using NamViet.ERP.Api.Application.Dtos.Purchasing;
using NamViet.ERP.Modules.Warehouse.Domain.Entities;
using NamViet.ERP.Modules.Warehouse.Infrastructure.Persistence;
using NamViet.ERP.SharedKernel.Common;

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
        (page, pageSize) = Paging.Normalize(page, pageSize, maxPageSize: 500);

        var allGroups = await db.SupplierGroups.AsNoTracking()
            .Select(g => new GroupRow(g.Id, g.Name, g.Description, g.ParentGroupId))
            .ToListAsync(ct);

        var activeGroupIds = allGroups.Select(g => g.Id).ToHashSet();
        // Supplier != null loại NCC đã xóa mềm (query filter biến join thành NULL).
        var memberships = await db.SupplierGroupMembers.AsNoTracking()
            .Where(m => m.Supplier != null)
            .Select(m => new { m.SupplierGroupId, m.SupplierId })
            .ToListAsync(ct);

        var groupById = allGroups.ToDictionary(g => g.Id);
        var childrenByParent = allGroups
            .Where(g => g.ParentGroupId != null && groupById.ContainsKey(g.ParentGroupId.Value))
            .GroupBy(g => g.ParentGroupId!.Value)
            .ToDictionary(
                g => g.Key,
                g => g.OrderBy(c => c.Name, StringComparer.OrdinalIgnoreCase).ToList());

        var directSupplierIdsByGroup = memberships
            .Where(m => activeGroupIds.Contains(m.SupplierGroupId))
            .GroupBy(m => m.SupplierGroupId)
            .ToDictionary(g => g.Key, g => g.Select(x => x.SupplierId).ToHashSet());

        SupplierGroupDto BuildDto(GroupRow g)
        {
            if (g.ParentGroupId is int pid && groupById.TryGetValue(pid, out var parent))
            {
                var childSupplierCount = directSupplierIdsByGroup.TryGetValue(g.Id, out var direct)
                    ? direct.Count
                    : 0;
                return new SupplierGroupDto(
                    g.Id, g.Name, g.Description, childSupplierCount, pid, parent.Name, 0);
            }

            var children = childrenByParent.GetValueOrDefault(g.Id);
            var childCount = children?.Count ?? 0;
            int supplierCount;
            if (childCount == 0)
            {
                supplierCount = directSupplierIdsByGroup.TryGetValue(g.Id, out var direct)
                    ? direct.Count
                    : 0;
            }
            else
            {
                var union = directSupplierIdsByGroup.TryGetValue(g.Id, out var direct)
                    ? new HashSet<int>(direct)
                    : [];
                foreach (var child in children!)
                {
                    if (directSupplierIdsByGroup.TryGetValue(child.Id, out var childSuppliers))
                        union.UnionWith(childSuppliers);
                }
                supplierCount = union.Count;
            }

            return new SupplierGroupDto(
                g.Id, g.Name, g.Description, supplierCount, g.ParentGroupId, null, childCount);
        }

        var q = string.IsNullOrWhiteSpace(search) ? null : search.Trim();
        var roots = allGroups
            .Where(g => g.ParentGroupId == null)
            .OrderBy(g => g.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
        var orphans = allGroups
            .Where(g => g.ParentGroupId != null && !groupById.ContainsKey(g.ParentGroupId.Value))
            .OrderBy(g => g.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var orderedDtos = new List<SupplierGroupDto>(allGroups.Count);
        foreach (var root in roots)
        {
            var children = childrenByParent.GetValueOrDefault(root.Id) ?? [];
            if (q is null || MatchesSearch(root, q))
            {
                orderedDtos.Add(BuildDto(root));
                foreach (var child in children)
                    orderedDtos.Add(BuildDto(child));
                continue;
            }

            var matchingChildren = children.Where(c => MatchesSearch(c, q)).ToList();
            if (matchingChildren.Count > 0)
            {
                orderedDtos.Add(BuildDto(root));
                foreach (var child in matchingChildren)
                    orderedDtos.Add(BuildDto(child));
            }
        }

        foreach (var orphan in orphans)
        {
            if (q is null || MatchesSearch(orphan, q))
                orderedDtos.Add(BuildDto(orphan));
        }

        var total = orderedDtos.Count;
        var items = orderedDtos
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToList();

        return new SupplierGroupPagedResponse(
            items, total, page, pageSize, (int)Math.Ceiling(total / (double)pageSize));
    }

    public async Task<SupplierGroupDto> GetAsync(int id, CancellationToken ct = default)
    {
        var group = await db.SupplierGroups.AsNoTracking()
            .Where(g => g.Id == id)
            .Select(g => new
            {
                g.Id,
                g.Name,
                g.Description,
                g.ParentGroupId,
                ParentGroupName = g.Parent != null ? g.Parent.Name : null,
            })
            .FirstOrDefaultAsync(ct)
            ?? throw new KeyNotFoundException($"Không tìm thấy nhóm NCC #{id}.");

        if (group.ParentGroupId is null)
        {
            var childIds = await db.SupplierGroups.AsNoTracking()
                .Where(g => g.ParentGroupId == id)
                .Select(g => g.Id)
                .ToListAsync(ct);

            var targetGroupIds = childIds.Append(id).Distinct().ToList();
            var supplierCount = await db.SupplierGroupMembers.AsNoTracking()
                .Where(m => targetGroupIds.Contains(m.SupplierGroupId) && m.Supplier != null)
                .Select(m => m.SupplierId)
                .Distinct()
                .CountAsync(ct);

            return new SupplierGroupDto(
                group.Id,
                group.Name,
                group.Description,
                supplierCount,
                null,
                null,
                childIds.Count);
        }

        var directSupplierCount = await db.SupplierGroupMembers.AsNoTracking()
            .Where(m => m.SupplierGroupId == id && m.Supplier != null)
            .Select(m => m.SupplierId)
            .Distinct()
            .CountAsync(ct);

        return new SupplierGroupDto(
            group.Id,
            group.Name,
            group.Description,
            directSupplierCount,
            group.ParentGroupId,
            group.ParentGroupName,
            0);
    }

    public async Task<SupplierGroupDto> CreateAsync(
        UpsertSupplierGroupRequest request, CancellationToken ct = default)
    {
        var name = NormalizeName(request.Name);
        var parentId = request.ParentGroupId;
        string? parentGroupName = null;

        List<NormalizedSubGroupItem> normalizedSubGroups = [];
        if (request.SubGroups is { Count: > 0 })
        {
            if (parentId != null)
                throw new PurchasingConflictException("Chỉ Nhóm lớn mới có thể chứa mục nhỏ.");

            normalizedSubGroups = NormalizeSubGroups(request.SubGroups, validateUniqueIds: false);
        }

        if (parentId is int pid)
        {
            var parent = await db.SupplierGroups.AsNoTracking()
                .FirstOrDefaultAsync(g => g.Id == pid, ct);
            if (parent is null)
                throw new PurchasingConflictException("Nhóm lớn không tồn tại.");
            if (parent.ParentGroupId != null)
                throw new PurchasingConflictException(
                    "Chỉ hỗ trợ tối đa 2 cấp (Nhóm lớn → Mục nhỏ). Không thể tạo mục con cho một mục nhỏ.");
            parentGroupName = parent.Name;
        }

        var normalizedLower = name.ToLowerInvariant();
        if (await db.SupplierGroups.AnyAsync(
                g => g.ParentGroupId == parentId && g.Name.ToLower() == normalizedLower, ct))
        {
            throw new PurchasingConflictException($"Nhóm '{name}' đã tồn tại trong cấp này.");
        }

        await using var tx = db.Database.IsRelational()
            ? await db.Database.BeginTransactionAsync(ct)
            : null;

        var group = new SupplierGroup
        {
            Name = name,
            Description = Trim(request.Description),
            ParentGroupId = parentId,
        };
        db.SupplierGroups.Add(group);
        await db.SaveChangesAsync(ct);

        if (normalizedSubGroups.Count > 0)
        {
            db.SupplierGroups.AddRange(normalizedSubGroups.Select(sub => new SupplierGroup
            {
                Name = sub.Name,
                Description = sub.Description,
                ParentGroupId = group.Id,
            }));
            await db.SaveChangesAsync(ct);
        }

        if (tx is not null)
            await tx.CommitAsync(ct);

        return new SupplierGroupDto(
            group.Id,
            group.Name,
            group.Description,
            0,
            group.ParentGroupId,
            parentGroupName,
            normalizedSubGroups.Count);
    }

    public async Task<SupplierGroupDto> UpdateAsync(
        int id, UpsertSupplierGroupRequest request, CancellationToken ct = default)
    {
        var group = await db.SupplierGroups.FirstOrDefaultAsync(g => g.Id == id, ct)
            ?? throw new KeyNotFoundException($"Không tìm thấy nhóm NCC #{id}.");

        var name = NormalizeName(request.Name);
        var parentId = request.ParentGroupId;

        if (parentId == id)
            throw new PurchasingConflictException("Không thể chọn chính nhóm này làm nhóm cha.");

        if (request.SubGroups is not null && parentId != null)
            throw new PurchasingConflictException("Chỉ Nhóm lớn mới có thể chứa mục nhỏ.");

        if (parentId is int pid)
        {
            var parent = await db.SupplierGroups.AsNoTracking()
                .FirstOrDefaultAsync(g => g.Id == pid, ct);
            if (parent is null)
                throw new PurchasingConflictException("Nhóm lớn không tồn tại.");
            if (parent.ParentGroupId != null)
                throw new PurchasingConflictException(
                    "Chỉ hỗ trợ tối đa 2 cấp (Nhóm lớn → Mục nhỏ). Không thể tạo mục con cho một mục nhỏ.");
        }

        if (parentId != null && await db.SupplierGroups.AnyAsync(g => g.ParentGroupId == id, ct))
        {
            throw new PurchasingConflictException(
                "Nhóm lớn này đang có mục nhỏ bên trong nên không thể chuyển thành mục nhỏ của nhóm khác.");
        }

        var normalizedLower = name.ToLowerInvariant();
        if (await db.SupplierGroups.AnyAsync(
                g => g.Id != id && g.ParentGroupId == parentId && g.Name.ToLower() == normalizedLower, ct))
        {
            throw new PurchasingConflictException($"Nhóm '{name}' đã tồn tại trong cấp này.");
        }

        if (request.SubGroups is null)
        {
            group.Name = name;
            group.Description = Trim(request.Description);
            group.ParentGroupId = parentId;
            group.UpdatedAt = DateTime.UtcNow;
            await db.SaveChangesAsync(ct);

            return await GetAsync(id, ct);
        }

        var normalizedItems = NormalizeSubGroups(request.SubGroups, validateUniqueIds: true);
        var submittedIds = normalizedItems
            .Where(i => i.Id.HasValue)
            .Select(i => i.Id!.Value)
            .ToHashSet();

        var existingChildren = await db.SupplierGroups
            .Where(g => g.ParentGroupId == id)
            .ToListAsync(ct);
        var existingById = existingChildren.ToDictionary(c => c.Id);

        foreach (var item in normalizedItems)
        {
            if (item.Id is int existingId && !existingById.ContainsKey(existingId))
                throw new PurchasingConflictException("Mục nhỏ không thuộc nhóm lớn này hoặc đã bị xóa.");
        }

        var untouchedLowerNames = existingChildren
            .Where(c => !submittedIds.Contains(c.Id))
            .Select(c => c.Name.ToLowerInvariant())
            .ToHashSet(StringComparer.Ordinal);

        foreach (var item in normalizedItems)
        {
            if (untouchedLowerNames.Contains(item.LowerName))
                throw new PurchasingConflictException($"Mục nhỏ '{item.Name}' đã tồn tại trong nhóm '{group.Name}'.");
        }

        var existingUpdates = normalizedItems
            .Where(i => i.Id.HasValue)
            .Select(i => (Entity: existingById[i.Id!.Value], Item: i))
            .ToList();
        var newItems = normalizedItems
            .Where(i => !i.Id.HasValue)
            .ToList();

        var nameChangedUpdates = existingUpdates
            .Where(x => !string.Equals(x.Entity.Name, x.Item.Name, StringComparison.Ordinal))
            .ToList();
        var otherUpdatedCurrentLowers = existingUpdates
            .Select(x => (x.Entity.Id, Lower: x.Entity.Name.ToLowerInvariant()))
            .ToList();

        var needsTwoPhaseRename = nameChangedUpdates.Any(x =>
            otherUpdatedCurrentLowers.Any(o => o.Id != x.Entity.Id && o.Lower == x.Item.LowerName));
        var newItemClaimsRenamedOldName = newItems.Any(n =>
            nameChangedUpdates.Any(u => string.Equals(u.Entity.Name, n.Name, StringComparison.OrdinalIgnoreCase)));

        var now = DateTime.UtcNow;

        await using var tx = db.Database.IsRelational()
            ? await db.Database.BeginTransactionAsync(ct)
            : null;

        if (needsTwoPhaseRename)
        {
            foreach (var x in nameChangedUpdates)
            {
                x.Entity.Name = $"__tmp_swap_{x.Entity.Id}_{Guid.NewGuid():N}";
                x.Entity.UpdatedAt = now;
            }
            await db.SaveChangesAsync(ct);
        }

        group.Name = name;
        group.Description = Trim(request.Description);
        group.ParentGroupId = parentId;
        group.UpdatedAt = now;

        foreach (var x in existingUpdates)
        {
            x.Entity.Name = x.Item.Name;
            x.Entity.Description = x.Item.Description;
            x.Entity.UpdatedAt = now;
        }

        if (!needsTwoPhaseRename && newItemClaimsRenamedOldName)
        {
            await db.SaveChangesAsync(ct);
        }

        if (newItems.Count > 0)
        {
            db.SupplierGroups.AddRange(newItems.Select(sub => new SupplierGroup
            {
                Name = sub.Name,
                Description = sub.Description,
                ParentGroupId = id,
            }));
        }

        await db.SaveChangesAsync(ct);

        if (tx is not null)
            await tx.CommitAsync(ct);

        return await GetAsync(id, ct);
    }

    public async Task DeleteAsync(int id, CancellationToken ct = default)
    {
        var group = await db.SupplierGroups.FirstOrDefaultAsync(g => g.Id == id, ct)
            ?? throw new KeyNotFoundException($"Không tìm thấy nhóm NCC #{id}.");

        var childCount = await db.SupplierGroups.CountAsync(g => g.ParentGroupId == id, ct);
        if (childCount > 0)
        {
            throw new PurchasingConflictException(
                $"Không thể xóa nhóm lớn '{group.Name}' vì đang có {childCount} mục nhỏ. Vui lòng xóa hoặc chuyển các mục nhỏ trước.");
        }

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

        var rows = await db.SupplierGroupMembers.AsNoTracking()
            .Where(m => m.SupplierId == supplierId && m.Group != null)
            .Select(m => new SupplierGroupRefDto(
                m.SupplierGroupId,
                m.Group!.Name,
                m.Group.ParentGroupId,
                m.Group.Parent != null ? m.Group.Parent.Name : null))
            .ToListAsync(ct);

        return rows
            .OrderBy(r => r.ParentGroupName ?? r.Name, StringComparer.OrdinalIgnoreCase)
            .ThenBy(r => r.ParentGroupName is null ? 0 : 1)
            .ThenBy(r => r.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public async Task<IReadOnlyList<SupplierGroupRefDto>> SetGroupsOfSupplierAsync(
        int supplierId, SetSupplierGroupsRequest request, CancellationToken ct = default)
    {
        await EnsureSupplierExistsAsync(supplierId, ct);

        var wanted = (request.GroupIds ?? []).Distinct().ToList();
        if (wanted.Count > 0)
        {
            var selectedGroups = await db.SupplierGroups.AsNoTracking()
                .Where(g => wanted.Contains(g.Id))
                .Select(g => new { g.Id, g.ParentGroupId })
                .ToListAsync(ct);

            if (selectedGroups.Count != wanted.Count)
                throw new PurchasingConflictException("Danh sách chứa nhóm NCC không tồn tại.");

            // Leaf-only overlap normalization: khi chọn cả Nhóm lớn P và >= 1 Mục nhỏ của P thì bỏ P trần.
            var parentsWithSelectedChildren = selectedGroups
                .Where(g => g.ParentGroupId != null)
                .Select(g => g.ParentGroupId!.Value)
                .ToHashSet();

            if (parentsWithSelectedChildren.Count > 0)
            {
                wanted = wanted
                    .Where(id => !parentsWithSelectedChildren.Contains(id))
                    .ToList();
            }
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

    private sealed record GroupRow(int Id, string Name, string? Description, int? ParentGroupId);

    private sealed record NormalizedSubGroupItem(int? Id, string Name, string? Description, string LowerName);

    private static List<NormalizedSubGroupItem> NormalizeSubGroups(
        IReadOnlyList<UpsertSupplierSubGroupItem> items, bool validateUniqueIds)
    {
        var normalized = new List<NormalizedSubGroupItem>(items.Count);
        var seenNames = new HashSet<string>(StringComparer.Ordinal);
        var seenIds = validateUniqueIds ? new HashSet<int>() : null;

        foreach (var item in items)
        {
            if (seenIds is not null && item?.Id is int submittedId && !seenIds.Add(submittedId))
                throw new PurchasingConflictException("Danh sách mục nhỏ có ID bị trùng.");

            var itemName = NormalizeName(item?.Name);
            var itemLower = itemName.ToLowerInvariant();
            if (!seenNames.Add(itemLower))
                throw new PurchasingConflictException($"Mục nhỏ '{itemName}' bị trùng trong danh sách.");

            normalized.Add(new NormalizedSubGroupItem(
                item?.Id,
                itemName,
                Trim(item?.Description),
                itemLower));
        }

        return normalized;
    }

    private static bool MatchesSearch(GroupRow group, string query) =>
        group.Name.Contains(query, StringComparison.OrdinalIgnoreCase)
        || (group.Description != null && group.Description.Contains(query, StringComparison.OrdinalIgnoreCase));

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
