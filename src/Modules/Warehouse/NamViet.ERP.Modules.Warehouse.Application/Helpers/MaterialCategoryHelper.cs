using NamViet.ERP.Modules.Warehouse.Application.DTOs;
using NamViet.ERP.Modules.Warehouse.Domain.Entities;

namespace NamViet.ERP.Modules.Warehouse.Application.Helpers;

public static class MaterialCategoryHelper
{
    public static MaterialCategoryPathDto BuildPath(
        MaterialCategory? leaf,
        IReadOnlyDictionary<int, MaterialCategory> byId)
    {
        if (leaf is null)
            return new(null, null, null, null, null);

        var levels = new string?[5];
        var current = leaf;
        while (current is not null)
        {
            if (current.Level is >= 1 and <= 5)
                levels[current.Level - 1] = current.Name.Trim();

            current = current.ParentId is int parentId && byId.TryGetValue(parentId, out var parent)
                ? parent
                : null;
        }

        return new(levels[0], levels[1], levels[2], levels[3], levels[4]);
    }

    public static bool MatchesPathFilter(
        MaterialCategoryPathDto path,
        string? danhMuc,
        string? nhomVatTu,
        string? loaiVatTu,
        string? phanLoai,
        string? kieu)
    {
        if (!string.IsNullOrWhiteSpace(danhMuc) && !EqualsName(path.DanhMuc, danhMuc)) return false;
        if (!string.IsNullOrWhiteSpace(nhomVatTu) && !EqualsName(path.NhomVatTu, nhomVatTu)) return false;
        if (!string.IsNullOrWhiteSpace(loaiVatTu) && !EqualsName(path.LoaiVatTu, loaiVatTu)) return false;
        if (!string.IsNullOrWhiteSpace(phanLoai) && !EqualsName(path.PhanLoai, phanLoai)) return false;
        if (!string.IsNullOrWhiteSpace(kieu) && !EqualsName(path.Kieu, kieu)) return false;
        return true;
    }

    public static HashSet<int> CollectCategoryIdsMatchingPathFilter(
        IReadOnlyList<MaterialCategory> all,
        string? danhMuc,
        string? nhomVatTu,
        string? loaiVatTu,
        string? phanLoai,
        string? kieu)
    {
        if (string.IsNullOrWhiteSpace(danhMuc) && string.IsNullOrWhiteSpace(nhomVatTu) &&
            string.IsNullOrWhiteSpace(loaiVatTu) && string.IsNullOrWhiteSpace(phanLoai) &&
            string.IsNullOrWhiteSpace(kieu))
            return [];

        var byId = all.ToDictionary(c => c.Id);
        return all
            .Where(c => MatchesPathFilter(BuildPath(c, byId), danhMuc, nhomVatTu, loaiVatTu, phanLoai, kieu))
            .Select(c => c.Id)
            .ToHashSet();
    }

    private static bool EqualsName(string? actual, string expected) =>
        string.Equals(actual?.Trim(), expected.Trim(), StringComparison.OrdinalIgnoreCase);

    public static HashSet<int> CollectSelfAndDescendantIds(
        int rootId,
        IReadOnlyList<MaterialCategory> all)
    {
        var childrenByParent = all
            .Where(c => c.ParentId.HasValue)
            .GroupBy(c => c.ParentId!.Value)
            .ToDictionary(g => g.Key, g => g.Select(x => x.Id).ToList());

        var result = new HashSet<int> { rootId };
        var queue = new Queue<int>();
        queue.Enqueue(rootId);

        while (queue.Count > 0)
        {
            var id = queue.Dequeue();
            if (!childrenByParent.TryGetValue(id, out var children))
                continue;

            foreach (var childId in children)
            {
                if (result.Add(childId))
                    queue.Enqueue(childId);
            }
        }

        return result;
    }
}
