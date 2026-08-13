using Microsoft.EntityFrameworkCore;
using NamViet.ERP.Api.Application.Dtos.Purchasing;
using NamViet.ERP.Modules.Warehouse.Application.Helpers;
using NamViet.ERP.Modules.Warehouse.Domain.Entities;

namespace NamViet.ERP.Api.Application.Services.Purchasing;

public sealed partial class PurchaseEstimateService
{
    private async Task AppendVersionAsync(
        PurchaseEstimate header,
        string? title,
        IReadOnlyList<PurchaseEstimateLineInput> lines,
        CancellationToken ct,
        string? actorName = null,
        Guid? actorId = null,
        bool persist = true)
    {
        var prev = await db.PurchaseEstimateVersions
            .Include(v => v.Lines)
            .FirstOrDefaultAsync(v => v.EstimateId == header.Id && v.VersionNumber == header.CurrentVersion, ct);

        var now = DateTime.UtcNow;
        var userId = actorId ?? Actor().Id;
        var userName = actorName ?? Actor().Name;
        var nextNo = header.CurrentVersion + 1;

        if (!string.IsNullOrWhiteSpace(title))
            header.Title = title.Trim();
        header.CurrentVersion = nextNo;
        header.UpdatedById = userId;
        header.UpdatedByName = userName;
        header.UpdatedAt = now;

        var version = NewVersion(header.Id, nextNo, userId, userName, now);
        db.PurchaseEstimateVersions.Add(version);
        await AddLinesAsync(version.Id, prev, lines, now, ct);
        if (persist)
            await db.SaveChangesAsync(ct);
    }

    private async Task<IReadOnlyList<PurchaseEstimateLineInput>> CatalogInputsAsync(CancellationToken ct)
    {
        var codes = await db.Materials.AsNoTracking()
            .Where(m => m.IsActive)
            .OrderBy(m => m.MaterialCode)
            .Select(m => m.MaterialCode)
            .ToListAsync(ct);
        if (codes.Count == 0)
            throw new PurchasingConflictException("Chưa có vật tư trong danh mục để tạo mẫu.");
        return codes.Select(c => new PurchaseEstimateLineInput(c, 0)).ToList();
    }

    private async Task AddLinesAsync(
        Guid versionId,
        PurchaseEstimateVersion? previous,
        IReadOnlyList<PurchaseEstimateLineInput> inputs,
        DateTime now,
        CancellationToken ct)
    {
        var codes = inputs
            .Select(l => l.MaterialCode.Trim())
            .Where(c => c.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        if (codes.Count == 0)
            throw new PurchasingConflictException("Chọn ít nhất một vật tư.");

        var materials = await db.Materials.AsNoTracking()
            .Where(m => codes.Contains(m.MaterialCode))
            .ToDictionaryAsync(m => m.MaterialCode, StringComparer.OrdinalIgnoreCase, ct);
        var cats = await db.MaterialCategories.AsNoTracking().ToDictionaryAsync(c => c.Id, ct);

        var prevByCode = previous?.Lines
            .GroupBy(l => l.MaterialCode, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First().NewQty, StringComparer.OrdinalIgnoreCase)
            ?? new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase);

        foreach (var input in inputs)
        {
            var code = input.MaterialCode.Trim();
            if (code.Length == 0) continue;
            if (!materials.TryGetValue(code, out var mat))
                throw new PurchasingConflictException($"Không tìm thấy vật tư {code}.");
            if (input.NewQty < 0)
                throw new PurchasingConflictException($"Số lượng mới của {code} không hợp lệ.");

            prevByCode.TryGetValue(code, out var oldQty);
            cats.TryGetValue(mat.CategoryId, out var leaf);
            var path = MaterialCategoryHelper.BuildPath(leaf, cats);
            var categoryName = path.NhomVatTu ?? path.DanhMuc ?? leaf?.Name;

            db.PurchaseEstimateLines.Add(new PurchaseEstimateLine
            {
                VersionId = versionId,
                MaterialCode = mat.MaterialCode,
                MaterialName = mat.MaterialName,
                CategoryName = categoryName,
                Unit = mat.Unit,
                OldQty = oldQty,
                NewQty = input.NewQty,
                CreatedAt = now,
                UpdatedAt = now,
            });
        }
    }
}
