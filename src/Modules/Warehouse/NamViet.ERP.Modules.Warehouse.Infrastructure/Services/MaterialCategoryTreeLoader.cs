using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NamViet.ERP.Modules.Warehouse.Domain;
using NamViet.ERP.Modules.Warehouse.Domain.Entities;
using NamViet.ERP.Modules.Warehouse.Infrastructure.Persistence;

namespace NamViet.ERP.Modules.Warehouse.Infrastructure.Services;

internal static class MaterialCategoryTreeLoader
{
    private sealed record CategorySeedItem(
        string Code, string Name, int Level, string? ParentCode, int SortOrder, bool IsActive);

    /// <summary>Idempotent: đảm bảo cây Excel được nạp kể cả DB kho đã tồn tại từ trước.</summary>
    public static async Task EnsureTreeSeededAsync(
        WarehouseDbContext db, ILogger? logger = null, CancellationToken ct = default)
    {
        var hasExcelRoots = await db.MaterialCategories
            .AnyAsync(c => c.Level == 1 && c.Code != MaterialCategoryCodes.Uncategorized, ct);
        if (hasExcelRoots)
            return;

        logger?.LogInformation("Seeding material category tree from JSON...");

        if (!await db.MaterialCategories.AnyAsync(c => c.Code == MaterialCategoryCodes.Uncategorized, ct))
        {
            db.MaterialCategories.Add(new MaterialCategory
            {
                Code = MaterialCategoryCodes.Uncategorized,
                Name = "Chưa phân loại",
                Level = 1,
                SortOrder = 0
            });
            await db.SaveChangesAsync(ct);
        }

        await AppendTreeFromJsonAsync(db, ct);
        logger?.LogInformation("Material category tree seeded.");
    }

    public static async Task<Dictionary<string, int>> SeedFromJsonAsync(
        WarehouseDbContext db, CancellationToken ct = default)
    {
        await EnsureTreeSeededAsync(db, ct: ct);
        return await db.MaterialCategories
            .AsNoTracking()
            .ToDictionaryAsync(c => c.Code, c => c.Id, ct);
    }

    private static async Task AppendTreeFromJsonAsync(WarehouseDbContext db, CancellationToken ct)
    {
        var jsonPath = ResolveJsonPath();
        if (jsonPath is null)
            return;

        var json = await File.ReadAllTextAsync(jsonPath, ct);
        var items = JsonSerializer.Deserialize<List<CategorySeedItem>>(json,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? [];

        var existingCodes = await db.MaterialCategories
            .AsNoTracking()
            .Select(c => c.Code)
            .ToListAsync(ct);
        var codeToId = await db.MaterialCategories
            .AsNoTracking()
            .ToDictionaryAsync(c => c.Code, c => c.Id, ct);

        foreach (var level in Enumerable.Range(1, 5))
        {
            var levelItems = items
                .Where(i => i.Level == level && !existingCodes.Contains(i.Code))
                .OrderBy(i => i.SortOrder)
                .ToList();

            foreach (var item in levelItems)
            {
                int? parentId = null;
                if (!string.IsNullOrEmpty(item.ParentCode) &&
                    codeToId.TryGetValue(item.ParentCode, out var pid))
                    parentId = pid;

                db.MaterialCategories.Add(new MaterialCategory
                {
                    Code = item.Code,
                    Name = item.Name.Trim(),
                    Level = item.Level,
                    ParentId = parentId,
                    SortOrder = item.SortOrder,
                    IsActive = item.IsActive
                });
            }

            if (levelItems.Count > 0)
            {
                await db.SaveChangesAsync(ct);
                foreach (var item in levelItems)
                {
                    var entity = await db.MaterialCategories
                        .AsNoTracking()
                        .FirstAsync(c => c.Code == item.Code, ct);
                    codeToId[item.Code] = entity.Id;
                    existingCodes.Add(item.Code);
                }
            }
        }
    }

    private static string? ResolveJsonPath()
    {
        var candidates = new[]
        {
            Path.Combine(AppContext.BaseDirectory, "Data", "material-categories-tree.json"),
            Path.Combine(AppContext.BaseDirectory, "material-categories-tree.json"),
        };

        foreach (var path in candidates)
            if (File.Exists(path)) return path;

        var devPath = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory,
            "..", "..", "..", "..", "Data", "material-categories-tree.json"));
        return File.Exists(devPath) ? devPath : null;
    }
}
