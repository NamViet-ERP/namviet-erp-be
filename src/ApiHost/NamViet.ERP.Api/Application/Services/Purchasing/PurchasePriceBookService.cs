using Microsoft.EntityFrameworkCore;
using NamViet.ERP.Api.Application.Dtos.Purchasing;
using NamViet.ERP.Modules.Warehouse.Domain.Entities;
using NamViet.ERP.Modules.Warehouse.Domain.Enums;
using NamViet.ERP.Modules.Warehouse.Infrastructure.Persistence;
using NamViet.ERP.SharedKernel.Interfaces;

namespace NamViet.ERP.Api.Application.Services.Purchasing;

public interface IPurchasePriceBookService
{
    Task<IReadOnlyList<PriceBookItemDto>> ListItemsAsync(
        string? search, int? categoryId, DateOnly? fromDate, DateOnly? toDate, CancellationToken ct = default);

    Task<PriceBookItemDetailDto?> GetItemAsync(string materialCode, CancellationToken ct = default);

    Task<PriceBookMatrixDto> MatrixAsync(int categoryId, CancellationToken ct = default);

    Task<IReadOnlyList<PriceBookCategoryRowDto>> CategoriesAsync(CancellationToken ct = default);

    Task<IReadOnlyList<PriceBookMarginRowDto>> MarginsAsync(CancellationToken ct = default);

    Task<PurchaseSellPriceDto> UpsertSellPriceAsync(
        UpsertPurchaseSellPriceRequest request, CancellationToken ct = default);

    Task<IReadOnlyList<PriceBookEstimateRowDto>> EstimatesAsync(CancellationToken ct = default);

    Task<PurchaseItemEstimatePriceDto> UpsertEstimatePriceAsync(
        UpsertPurchaseItemEstimatePriceRequest request, CancellationToken ct = default);
}

public sealed class PurchasePriceBookService(
    WarehouseDbContext db,
    ICurrentUser currentUser) : IPurchasePriceBookService
{
    public async Task<IReadOnlyList<PriceBookItemDto>> ListItemsAsync(
        string? search, int? categoryId, DateOnly? fromDate, DateOnly? toDate, CancellationToken ct = default)
    {
        var categoryIds = categoryId is > 0
            ? await DescendantCategoryIdsAsync(categoryId.Value, ct)
            : null;

        var materialsQuery = db.Materials.AsNoTracking().Where(m => m.IsActive);
        if (categoryIds is not null)
            materialsQuery = materialsQuery.Where(m => categoryIds.Contains(m.CategoryId));
        if (!string.IsNullOrWhiteSpace(search))
        {
            var q = search.Trim().ToLowerInvariant();
            materialsQuery = materialsQuery.Where(m =>
                m.MaterialCode.ToLower().Contains(q) || m.MaterialName.ToLower().Contains(q));
        }

        var materials = await materialsQuery
            .OrderBy(m => m.MaterialCode)
            .Take(500)
            .ToListAsync(ct);
        var cats = await db.MaterialCategories.AsNoTracking().ToDictionaryAsync(c => c.Id, ct);
        var codes = materials.Select(m => m.MaterialCode).ToList();
        var history = await LoadPoHistoryAsync(codes, ct);
        var estimates = await LoadLatestEstimatesAsync(codes, toDate, ct);

        return materials.Select(m =>
        {
            cats.TryGetValue(m.CategoryId, out var cat);
            history.TryGetValue(m.MaterialCode, out var hist);
            estimates.TryGetValue(m.MaterialCode, out var estimate);
            return ToItemDto(m, cat?.Name, hist, estimate, fromDate, toDate);
        }).ToList();
    }

    public async Task<PriceBookItemDetailDto?> GetItemAsync(string materialCode, CancellationToken ct = default)
    {
        var code = materialCode.Trim();
        var material = await db.Materials.AsNoTracking().FirstOrDefaultAsync(m => m.MaterialCode == code, ct);
        if (material is null) return null;
        var cat = await db.MaterialCategories.AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == material.CategoryId, ct);
        var histMap = await LoadPoHistoryAsync([code], ct);
        histMap.TryGetValue(code, out var poHist);
        var estimates = await LoadLatestEstimatesAsync([code], null, ct);
        estimates.TryGetValue(code, out var estimate);
        var item = ToItemDto(material, cat?.Name, poHist, estimate, null, null);

        var history = (poHist ?? [])
            .Select(p => new PriceBookHistoryRowDto(
                p.Date, p.Kind, p.Status, p.SupplierId, p.SupplierName, p.UnitPrice, p.PoRef, p.ProjectCode))
            .ToList();
        var sells = await db.PurchaseSellPrices.AsNoTracking()
            .Where(s => s.MaterialCode == code)
            .ToListAsync(ct);
        foreach (var s in sells)
        {
            history.Add(new PriceBookHistoryRowDto(
                s.EffectiveDate, "sell", null, null, null, s.SellPrice, null, s.ProjectCode));
        }

        var estimateRows = await db.PurchaseItemEstimatePrices.AsNoTracking()
            .Where(s => s.MaterialCode == code)
            .ToListAsync(ct);
        foreach (var e in estimateRows)
        {
            history.Add(new PriceBookHistoryRowDto(
                e.EffectiveDate, "estimate", null, null, null, e.EstimatePrice, null, e.ProjectCode));
        }

        return new PriceBookItemDetailDto(
            item,
            history.OrderByDescending(h => h.Date).ThenByDescending(h => h.UnitPrice).ToList());
    }

    public async Task<PriceBookMatrixDto> MatrixAsync(int categoryId, CancellationToken ct = default)
    {
        var cat = await db.MaterialCategories.AsNoTracking().FirstOrDefaultAsync(c => c.Id == categoryId, ct)
            ?? throw new PurchasingConflictException("Nhóm hàng không tồn tại.");
        var categoryIds = await DescendantCategoryIdsAsync(categoryId, ct);
        var materials = await db.Materials.AsNoTracking()
            .Where(m => m.IsActive && categoryIds.Contains(m.CategoryId))
            .OrderBy(m => m.MaterialCode)
            .Take(80)
            .ToListAsync(ct);
        var codes = materials.Select(m => m.MaterialCode).ToList();
        var histMap = await LoadPoHistoryAsync(codes, ct);
        var quotes = codes.Count == 0
            ? []
            : await db.PurchaseRfqQuotes.AsNoTracking()
                .Where(q => codes.Contains(q.MaterialCode) && q.SupplierId > 0)
                .Select(q => new { q.MaterialCode, q.SupplierId, q.SupplierName, q.UnitPrice, q.QuoteDate })
                .ToListAsync(ct);

        var suppliers = histMap.Values.SelectMany(h => h)
            .Where(h => h.SupplierId is > 0)
            .Select(h => (Id: h.SupplierId!.Value, Name: h.SupplierName ?? $"NCC #{h.SupplierId}"))
            .Concat(quotes.Select(q => (Id: q.SupplierId, Name: q.SupplierName)))
            .GroupBy(s => s.Id)
            .Select(g => g.First())
            .OrderBy(s => s.Name)
            .ToList();

        // Chỉ hiện NCC đã có giá trong nhóm này — không đổ cả danh mục NCC.

        var rows = materials.Select(m =>
        {
            histMap.TryGetValue(m.MaterialCode, out var hist);
            var poPoints = (hist ?? [])
                .Where(h => h.SupplierId is > 0)
                .Select(h => (Sid: h.SupplierId!.Value, h.Date, h.UnitPrice));
            var quotePoints = quotes
                .Where(q => q.MaterialCode == m.MaterialCode)
                .Select(q => (Sid: q.SupplierId, Date: q.QuoteDate, q.UnitPrice));
            var latestBySup = poPoints.Concat(quotePoints)
                .GroupBy(x => x.Sid)
                .ToDictionary(g => g.Key, g => g.OrderByDescending(x => x.Date).First().UnitPrice);
            var min = latestBySup.Count == 0 ? (decimal?)null : latestBySup.Values.Min();
            var cells = suppliers.Select(s =>
            {
                latestBySup.TryGetValue(s.Id, out var price);
                return new PriceBookMatrixCellDto(s.Id, s.Name, latestBySup.ContainsKey(s.Id) ? price : null,
                    min is not null && latestBySup.ContainsKey(s.Id) && price == min);
            }).ToList();
            return new PriceBookMatrixRowDto(m.MaterialCode, m.MaterialName, cells);
        }).ToList();

        return new PriceBookMatrixDto(cat.Id, cat.Name, rows);
    }

    public async Task<IReadOnlyList<PriceBookCategoryRowDto>> CategoriesAsync(CancellationToken ct = default)
    {
        var roots = await db.MaterialCategories.AsNoTracking()
            .Where(c => c.ParentId == null && c.Code != "UNCATEGORIZED")
            .OrderBy(c => c.Code)
            .ToListAsync(ct);
        var allCats = await db.MaterialCategories.AsNoTracking().ToListAsync(ct);
        var materials = await db.Materials.AsNoTracking()
            .Select(m => new { m.MaterialCode, m.CategoryId })
            .ToListAsync(ct);
        var history = await LoadPoHistoryAsync(materials.Select(m => m.MaterialCode).ToList(), ct);
        var grand = history.Values.SelectMany(h => h).Where(IsPurchaseKind).Sum(h => h.LineTotal);
        var rows = new List<PriceBookCategoryRowDto>();

        foreach (var root in roots)
        {
            var ids = CollectDescendants(root.Id, allCats);
            var codes = materials.Where(m => ids.Contains(m.CategoryId)).Select(m => m.MaterialCode).ToHashSet();
            var hist = history.Where(kv => codes.Contains(kv.Key)).SelectMany(kv => kv.Value)
                .Where(IsPurchaseKind).ToList();
            var spend = hist.Sum(h => h.LineTotal);
            rows.Add(new PriceBookCategoryRowDto(
                root.Id, root.Code, root.Name,
                codes.Count,
                hist.Select(h => h.SupplierId).Distinct().Count(),
                hist.Count,
                spend,
                grand <= 0 ? 0 : Math.Round(spend / grand * 100m, 1)));
        }

        return rows;
    }

    public async Task<IReadOnlyList<PriceBookMarginRowDto>> MarginsAsync(CancellationToken ct = default)
    {
        var sells = await db.PurchaseSellPrices.AsNoTracking().ToListAsync(ct);
        var latest = sells
            .GroupBy(s => $"{s.MaterialCode}|{s.ProjectCode ?? ""}")
            .Select(g => g.OrderByDescending(x => x.EffectiveDate).ThenByDescending(x => x.UpdatedAt).First())
            .ToList();
        var histMap = await LoadPoHistoryAsync(latest.Select(s => s.MaterialCode).Distinct().ToList(), ct);

        return latest.Select(s =>
        {
            histMap.TryGetValue(s.MaterialCode, out var hist);
            var purchase = (hist ?? []).Where(IsPurchaseKind).ToList();
            var prices = purchase.Select(h => h.UnitPrice).ToList();
            var avg = prices.Count == 0 ? (decimal?)null : Math.Round(prices.Average(), 2);
            var latestPo = purchase.Count == 0 ? (decimal?)null : purchase[^1].UnitPrice;
            var margin = latestPo is null ? (decimal?)null : s.SellPrice - latestPo;
            var pct = margin is null || s.SellPrice == 0
                ? (decimal?)null
                : Math.Round(margin.Value / s.SellPrice * 100m, 1);
            return new PriceBookMarginRowDto(
                s.MaterialCode, s.MaterialName, s.ProjectCode, s.SellPrice, s.EffectiveDate,
                avg, latestPo, margin, pct,
                sells.Count(x => x.MaterialCode == s.MaterialCode));
        }).ToList();
    }

    public async Task<PurchaseSellPriceDto> UpsertSellPriceAsync(
        UpsertPurchaseSellPriceRequest request, CancellationToken ct = default)
    {
        var code = request.MaterialCode.Trim();
        var mat = await db.Materials.AsNoTracking().FirstOrDefaultAsync(m => m.MaterialCode == code, ct)
            ?? throw new PurchasingConflictException($"Không tìm thấy vật tư {code}.");
        if (request.SellPrice <= 0)
            throw new PurchasingConflictException("Giá bán phải lớn hơn 0.");

        var now = DateTime.UtcNow;
        var projectCode = string.IsNullOrWhiteSpace(request.ProjectCode) ? null : request.ProjectCode.Trim();
        var actor = string.IsNullOrWhiteSpace(currentUser.Name) ? currentUser.Email : currentUser.Name;
        var existing = await db.PurchaseSellPrices.FirstOrDefaultAsync(s =>
            s.MaterialCode == mat.MaterialCode
            && (s.ProjectCode ?? "") == (projectCode ?? "")
            && s.EffectiveDate == request.EffectiveDate, ct);

        if (existing is null)
        {
            existing = new PurchaseSellPrice
            {
                MaterialCode = mat.MaterialCode,
                MaterialName = mat.MaterialName,
                ProjectCode = projectCode,
                SellPrice = request.SellPrice,
                EffectiveDate = request.EffectiveDate,
                CreatedByName = actor,
                Note = string.IsNullOrWhiteSpace(request.Note) ? null : request.Note.Trim(),
                CreatedAt = now,
                UpdatedAt = now,
            };
            db.PurchaseSellPrices.Add(existing);
        }
        else
        {
            existing.MaterialName = mat.MaterialName;
            existing.SellPrice = request.SellPrice;
            existing.CreatedByName = actor;
            existing.Note = string.IsNullOrWhiteSpace(request.Note) ? null : request.Note.Trim();
            existing.UpdatedAt = now;
        }

        await db.SaveChangesAsync(ct);
        return new PurchaseSellPriceDto(
            existing.Id, existing.MaterialCode, existing.MaterialName, existing.ProjectCode,
            existing.SellPrice, existing.EffectiveDate, existing.CreatedByName, existing.Note);
    }

    public async Task<IReadOnlyList<PriceBookEstimateRowDto>> EstimatesAsync(CancellationToken ct = default)
    {
        var rows = await db.PurchaseItemEstimatePrices.AsNoTracking().ToListAsync(ct);
        var latest = rows
            .GroupBy(s => $"{s.MaterialCode}|{s.ProjectCode ?? ""}")
            .Select(g => g.OrderByDescending(x => x.EffectiveDate).ThenByDescending(x => x.UpdatedAt).First())
            .ToList();
        var histMap = await LoadPoHistoryAsync(latest.Select(s => s.MaterialCode).Distinct().ToList(), ct);

        return latest.Select(s =>
        {
            histMap.TryGetValue(s.MaterialCode, out var hist);
            var purchase = (hist ?? []).Where(h => h.Kind == "po").ToList();
            var latestPo = purchase.Count == 0 ? (decimal?)null : purchase[^1].UnitPrice;
            var diff = latestPo is null ? (decimal?)null : latestPo - s.EstimatePrice;
            var diffPct = diff is null || s.EstimatePrice == 0
                ? (decimal?)null
                : Math.Round(diff.Value / s.EstimatePrice * 100m, 1);
            var marginPct = diffPct is null ? null : -diffPct;
            return new PriceBookEstimateRowDto(
                s.MaterialCode, s.MaterialName, s.ProjectCode, s.EstimatePrice, s.EffectiveDate,
                latestPo, diff, diffPct, marginPct,
                rows.Count(x => x.MaterialCode == s.MaterialCode));
        }).ToList();
    }

    public async Task<PurchaseItemEstimatePriceDto> UpsertEstimatePriceAsync(
        UpsertPurchaseItemEstimatePriceRequest request, CancellationToken ct = default)
    {
        var code = request.MaterialCode.Trim();
        var mat = await db.Materials.AsNoTracking().FirstOrDefaultAsync(m => m.MaterialCode == code, ct)
            ?? throw new PurchasingConflictException($"Không tìm thấy vật tư {code}.");
        if (request.EstimatePrice <= 0)
            throw new PurchasingConflictException("Giá dự toán phải lớn hơn 0.");

        var now = DateTime.UtcNow;
        var projectCode = string.IsNullOrWhiteSpace(request.ProjectCode) ? null : request.ProjectCode.Trim();
        var actor = string.IsNullOrWhiteSpace(currentUser.Name) ? currentUser.Email : currentUser.Name;
        var existing = await db.PurchaseItemEstimatePrices.FirstOrDefaultAsync(s =>
            s.MaterialCode == mat.MaterialCode
            && (s.ProjectCode ?? "") == (projectCode ?? "")
            && s.EffectiveDate == request.EffectiveDate, ct);

        if (existing is null)
        {
            existing = new PurchaseItemEstimatePrice
            {
                MaterialCode = mat.MaterialCode,
                MaterialName = mat.MaterialName,
                ProjectCode = projectCode,
                EstimatePrice = request.EstimatePrice,
                EffectiveDate = request.EffectiveDate,
                CreatedByName = actor,
                Note = string.IsNullOrWhiteSpace(request.Note) ? null : request.Note.Trim(),
                CreatedAt = now,
                UpdatedAt = now,
            };
            db.PurchaseItemEstimatePrices.Add(existing);
        }
        else
        {
            existing.MaterialName = mat.MaterialName;
            existing.EstimatePrice = request.EstimatePrice;
            existing.CreatedByName = actor;
            existing.Note = string.IsNullOrWhiteSpace(request.Note) ? null : request.Note.Trim();
            existing.UpdatedAt = now;
        }

        await db.SaveChangesAsync(ct);
        return new PurchaseItemEstimatePriceDto(
            existing.Id, existing.MaterialCode, existing.MaterialName, existing.ProjectCode,
            existing.EstimatePrice, existing.EffectiveDate, existing.CreatedByName, existing.Note);
    }

    private async Task<Dictionary<string, List<PoPricePoint>>> LoadPoHistoryAsync(
        IReadOnlyList<string> codes, CancellationToken ct)
    {
        if (codes.Count == 0) return new Dictionary<string, List<PoPricePoint>>(StringComparer.OrdinalIgnoreCase);

        var poRows = await (
            from item in db.PurchaseOrderItems.AsNoTracking()
            join po in db.PurchaseOrders.AsNoTracking() on item.PoCode equals po.Code
            where item.MaterialCode != null
                  && codes.Contains(item.MaterialCode)
                  && item.PoPrice > 0
            select new PoPricePoint(
                item.MaterialCode!,
                po.OrderDate,
                item.PoPrice,
                po.SupplierId,
                po.SupplierNameSnapshot,
                po.Code,
                po.ProjectCode,
                item.Ordered * item.PoPrice,
                "po",
                null)
        ).ToListAsync(ct);

        var livePlanStatuses = new[]
        {
            PurchasePlanStatuses.Approved,
            PurchasePlanStatuses.Closed,
            PurchasePlanStatuses.ActiveLegacy,
        };
        var planRows = await (
            from line in db.PurchasePlanLines.AsNoTracking()
            join plan in db.PurchasePlans.AsNoTracking() on line.PlanCode equals plan.Code
            where line.MaterialCode != null
                  && codes.Contains(line.MaterialCode)
                  && line.PurchaseUnitPrice > 0
                  && livePlanStatuses.Contains(plan.Status)
            select new
            {
                line.MaterialCode,
                Date = line.OrderedDate ?? line.ActualReceiptDate,
                Price = line.PurchaseUnitPrice!.Value,
                line.PurchaseSupplierName,
                line.SelectedSupplierName,
                line.PlanCode,
                plan.ProjectCode,
                plan.UpdatedAt,
                Qty = line.OrderedQty > 0 ? line.OrderedQty : line.Quantity,
            }
        ).ToListAsync(ct);

        var quoteRows = await db.PurchaseRfqQuotes.AsNoTracking()
            .Where(q => codes.Contains(q.MaterialCode) && q.UnitPrice > 0)
            .Select(q => new
            {
                q.MaterialCode,
                q.QuoteDate,
                q.UnitPrice,
                q.SupplierId,
                q.SupplierName,
                q.PoRef,
                q.ProjectCode,
                q.Status,
            })
            .ToListAsync(ct);

        var importRows = await (
            from item in db.WarehouseTransactionItems.AsNoTracking()
            join tx in db.WarehouseTransactions.AsNoTracking() on item.TicketCode equals tx.TicketCode
            where codes.Contains(item.MaterialCode)
                  && item.UnitPrice > 0
                  && tx.TransactionType == TransactionType.Import
                  && tx.ApprovalStatus == ApprovalStatus.Approved
                  && tx.DeletedAt == null
            select new
            {
                item.MaterialCode,
                Date = tx.ApprovedAt ?? tx.CreatedAt,
                item.UnitPrice,
                SupplierId = item.SupplierId ?? tx.SupplierId,
                SupplierName = item.SupplierName ?? tx.SupplierName,
                tx.TicketCode,
                tx.ProjectCode,
                Qty = item.Quantity,
            }
        ).ToListAsync(ct);

        var supplierNames = planRows
            .Select(r => r.PurchaseSupplierName)
            .Concat(planRows.Select(r => r.SelectedSupplierName))
            .Where(n => !string.IsNullOrWhiteSpace(n))
            .Select(n => n!.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        var suppliers = supplierNames.Count == 0
            ? new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
            : await db.Suppliers.AsNoTracking()
                .Where(s => supplierNames.Contains(s.SupplierName))
                .ToDictionaryAsync(s => s.SupplierName, s => s.Id, StringComparer.OrdinalIgnoreCase, ct);

        int? ResolveSupplier(string? name)
        {
            if (string.IsNullOrWhiteSpace(name)) return null;
            return suppliers.TryGetValue(name.Trim(), out var id) ? id : null;
        }

        var merged = new List<PoPricePoint>(poRows.Count + planRows.Count + quoteRows.Count + importRows.Count);
        merged.AddRange(poRows);
        foreach (var r in planRows)
        {
            var date = r.Date ?? DateOnly.FromDateTime(DateTime.SpecifyKind(r.UpdatedAt, DateTimeKind.Utc));
            var supplierName = r.PurchaseSupplierName ?? r.SelectedSupplierName;
            merged.Add(new PoPricePoint(
                r.MaterialCode!,
                date,
                r.Price,
                ResolveSupplier(supplierName),
                supplierName,
                r.PlanCode,
                r.ProjectCode,
                r.Qty * r.Price,
                "plan"));
        }
        foreach (var q in quoteRows)
        {
            merged.Add(new PoPricePoint(
                q.MaterialCode,
                q.QuoteDate,
                q.UnitPrice,
                q.SupplierId > 0 ? q.SupplierId : null,
                q.SupplierName,
                q.PoRef ?? "",
                q.ProjectCode,
                q.UnitPrice,
                "quote",
                q.Status));
        }
        foreach (var n in importRows)
        {
            merged.Add(new PoPricePoint(
                n.MaterialCode,
                DateOnly.FromDateTime(DateTime.SpecifyKind(n.Date, DateTimeKind.Utc)),
                n.UnitPrice,
                n.SupplierId is > 0 ? n.SupplierId : null,
                n.SupplierName,
                n.TicketCode,
                n.ProjectCode,
                n.Qty * n.UnitPrice,
                "import"));
        }

        return merged
            .GroupBy(r => r.MaterialCode, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.OrderBy(x => x.Date).ToList(), StringComparer.OrdinalIgnoreCase);
    }

    private async Task<HashSet<int>> DescendantCategoryIdsAsync(int rootId, CancellationToken ct)
    {
        var all = await db.MaterialCategories.AsNoTracking().Select(c => new { c.Id, c.ParentId }).ToListAsync(ct);
        return CollectDescendants(rootId, all.Select(c => new MaterialCategory { Id = c.Id, ParentId = c.ParentId }));
    }

    private static HashSet<int> CollectDescendants(int rootId, IEnumerable<MaterialCategory> all)
    {
        var byParent = all.GroupBy(c => c.ParentId ?? 0).ToDictionary(g => g.Key, g => g.Select(x => x.Id).ToList());
        var set = new HashSet<int> { rootId };
        var stack = new Stack<int>();
        stack.Push(rootId);
        while (stack.Count > 0)
        {
            var id = stack.Pop();
            if (!byParent.TryGetValue(id, out var kids)) continue;
            foreach (var kid in kids)
            {
                if (set.Add(kid)) stack.Push(kid);
            }
        }
        return set;
    }

    private static bool IsPurchaseKind(PoPricePoint point) => point.Kind is "po" or "plan" or "import";

    private async Task<Dictionary<string, decimal>> LoadLatestEstimatesAsync(
        IReadOnlyList<string> codes, DateOnly? asOf, CancellationToken ct)
    {
        if (codes.Count == 0) return new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase);

        var rows = await db.PurchaseItemEstimatePrices.AsNoTracking()
            .Where(e => codes.Contains(e.MaterialCode))
            .ToListAsync(ct);

        return rows
            .Where(e => asOf is null || e.EffectiveDate <= asOf)
            .GroupBy(e => e.MaterialCode, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                g => g.Key,
                g => g.OrderByDescending(x => x.EffectiveDate).ThenByDescending(x => x.UpdatedAt).First().EstimatePrice,
                StringComparer.OrdinalIgnoreCase);
    }

    private static PriceBookItemDto ToItemDto(
        Material m,
        string? categoryName,
        List<PoPricePoint>? hist,
        decimal? estimatePrice,
        DateOnly? fromDate,
        DateOnly? toDate)
    {
        var purchase = (hist ?? [])
            .Where(h => h.Kind == "po")
            .Where(h => fromDate is null || h.Date >= fromDate)
            .Where(h => toDate is null || h.Date <= toDate)
            .ToList();
        if (purchase.Count == 0)
        {
            return new PriceBookItemDto(
                m.MaterialCode, m.MaterialName, m.CategoryId, categoryName, m.CeilingPrice, m.AbcClass,
                m.TechnicalSpec, m.GeneralTechnicalSpec,
                null, null, null, null, null, null, 0, null, false,
                estimatePrice, null, null, 0);
        }

        var latest = purchase[^1];
        var prev = purchase.Count > 1 ? purchase[^2] : null;
        decimal? trend = prev is null || prev.UnitPrice == 0
            ? 0
            : Math.Round((latest.UnitPrice - prev.UnitPrice) / prev.UnitPrice * 100m, 1);
        var over = m.CeilingPrice is > 0 && latest.UnitPrice > m.CeilingPrice;
        decimal? diff = estimatePrice is null ? null : latest.UnitPrice - estimatePrice;
        decimal? diffPct = diff is null || estimatePrice is null or 0
            ? null
            : Math.Round(diff.Value / estimatePrice.Value * 100m, 1);
        return new PriceBookItemDto(
            m.MaterialCode, m.MaterialName, m.CategoryId, categoryName, m.CeilingPrice, m.AbcClass,
            m.TechnicalSpec, m.GeneralTechnicalSpec,
            latest.UnitPrice, latest.Date, latest.SupplierName,
            purchase.Min(h => h.UnitPrice), purchase.Max(h => h.UnitPrice),
            Math.Round(purchase.Average(h => h.UnitPrice), 2),
            purchase.Select(h => h.SupplierId).Distinct().Count(),
            trend, over, estimatePrice, diff, diffPct, purchase.Count);
    }

    private sealed record PoPricePoint(
        string MaterialCode,
        DateOnly Date,
        decimal UnitPrice,
        int? SupplierId,
        string? SupplierName,
        string PoRef,
        string? ProjectCode,
        decimal LineTotal,
        string Kind,
        string? Status = null);
}
