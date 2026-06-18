using Microsoft.EntityFrameworkCore;
using NamViet.ERP.Modules.Warehouse.Domain.Entities;
using NamViet.ERP.Modules.Warehouse.Infrastructure.Persistence;

namespace NamViet.ERP.Modules.Warehouse.Infrastructure.Helpers;

public static class TransactionItemHelper
{
    public static void ApplySnapshot(WarehouseTransactionItem item, Material material)
    {
        item.MaterialNameSnapshot = material.MaterialName;
        item.UnitSnapshot = material.Unit;
    }

    public static void CopySnapshots(WarehouseTransactionItem source, WarehouseTransactionItem target)
    {
        target.MaterialNameSnapshot = source.MaterialNameSnapshot;
        target.UnitSnapshot = source.UnitSnapshot;
    }

    public static string ResolveName(WarehouseTransactionItem item) =>
        !string.IsNullOrWhiteSpace(item.MaterialNameSnapshot)
            ? item.MaterialNameSnapshot
            : item.Material?.MaterialName ?? item.MaterialCode;

    public static string ResolveUnit(WarehouseTransactionItem item) =>
        !string.IsNullOrWhiteSpace(item.UnitSnapshot)
            ? item.UnitSnapshot
            : item.Material?.Unit ?? string.Empty;

    public static async Task<Dictionary<string, Material>> LoadMaterialsAsync(
        WarehouseDbContext db, IEnumerable<string> materialCodes, CancellationToken ct = default)
    {
        var codes = materialCodes.Distinct().ToList();
        if (codes.Count == 0) return new Dictionary<string, Material>();

        return await db.Materials
            .Where(m => codes.Contains(m.MaterialCode))
            .ToDictionaryAsync(m => m.MaterialCode, ct);
    }

    public static async Task ApplySnapshotsAsync(
        WarehouseDbContext db, IEnumerable<WarehouseTransactionItem> items, CancellationToken ct = default)
    {
        var list = items.ToList();
        if (list.Count == 0) return;

        var materials = await LoadMaterialsAsync(db, list.Select(i => i.MaterialCode), ct);
        foreach (var item in list)
        {
            if (materials.TryGetValue(item.MaterialCode, out var material))
                ApplySnapshot(item, material);
        }
    }

    public static void EnsureActiveMaterials(
        IEnumerable<string> materialCodes, IReadOnlyDictionary<string, Material> materials)
    {
        foreach (var code in materialCodes.Distinct())
        {
            if (!materials.TryGetValue(code, out var material))
                throw new InvalidOperationException($"Vật tư '{code}' không tồn tại.");
            if (!material.IsActive)
                throw new InvalidOperationException($"Vật tư '{code}' đã ngừng sử dụng.");
        }
    }
}
