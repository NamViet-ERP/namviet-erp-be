using Microsoft.EntityFrameworkCore;
using NamViet.ERP.Api.Application.Dtos.Purchasing;
using NamViet.ERP.Modules.Warehouse.Domain.Entities;
using NamViet.ERP.Modules.Warehouse.Infrastructure.Persistence;

namespace NamViet.ERP.Api.Application.Services.Purchasing;

public interface ISupplierMaterialRiskService
{
    Task<IReadOnlyList<SupplierMaterialRiskDto>> LookupAsync(
        SupplierMaterialRiskLookupRequest request,
        CancellationToken ct = default);
}

public sealed class SupplierMaterialRiskService(WarehouseDbContext db) : ISupplierMaterialRiskService
{
    public const int DefectThreshold = 5;
    public const int DefectWindowMonths = 3;
    public const int WarrantyClaimThreshold = 2;

    public async Task<IReadOnlyList<SupplierMaterialRiskDto>> LookupAsync(
        SupplierMaterialRiskLookupRequest request,
        CancellationToken ct = default)
    {
        var rawPairs = (request.Pairs ?? [])
            .Where(p => !string.IsNullOrWhiteSpace(p.MaterialCode))
            .Select(p => new
            {
                SupplierId = p.SupplierId is > 0 ? p.SupplierId : null,
                SupplierName = NormalizeName(p.SupplierName),
                MaterialCode = p.MaterialCode.Trim(),
            })
            .Where(p => p.SupplierId is not null || !string.IsNullOrEmpty(p.SupplierName))
            .GroupBy(p => PairKey(p.SupplierId, p.SupplierName, p.MaterialCode), StringComparer.Ordinal)
            .Select(g => g.First())
            .Take(100)
            .ToList();

        if (rawPairs.Count == 0)
            return [];

        var nameKeys = rawPairs
            .Where(p => p.SupplierId is null && !string.IsNullOrEmpty(p.SupplierName))
            .Select(p => p.SupplierName!)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var suppliersByName = new Dictionary<string, (int Id, string Name)>(StringComparer.OrdinalIgnoreCase);
        if (nameKeys.Count > 0)
        {
            var lowered = nameKeys.Select(n => n.ToLowerInvariant()).ToList();
            var found = await db.Suppliers.AsNoTracking()
                .Where(s => lowered.Contains(s.SupplierName.ToLower()))
                .Select(s => new { s.Id, s.SupplierName })
                .ToListAsync(ct);
            foreach (var s in found)
                suppliersByName.TryAdd(s.SupplierName, (s.Id, s.SupplierName));
        }

        var resolved = rawPairs.Select(p =>
        {
            int? supplierId = p.SupplierId;
            string? supplierName = p.SupplierName;
            if (supplierId is null
                && !string.IsNullOrEmpty(supplierName)
                && suppliersByName.TryGetValue(supplierName, out var hit))
            {
                supplierId = hit.Id;
                supplierName = hit.Name;
            }

            return new ResolvedPair(supplierId, supplierName, p.MaterialCode);
        }).ToList();

        var materialCodes = resolved
            .Select(p => p.MaterialCode)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Select(c => c.ToLowerInvariant())
            .ToList();

        var since = DateTime.UtcNow.AddMonths(-DefectWindowMonths);

        var defectRows = await (
            from d in db.PurchaseDefects.AsNoTracking()
            join i in db.PurchaseDefectItems.AsNoTracking() on d.TicketCode equals i.TicketCode
            join po in db.PurchaseOrders.AsNoTracking() on d.PoCode equals po.Code into poJoin
            from po in poJoin.DefaultIfEmpty()
            where d.CreatedAt >= since
                  && i.MaterialCode != null
                  && materialCodes.Contains(i.MaterialCode.ToLower())
                  && d.Status != PurchaseDefectStatuses.Draft
                  && d.Status != PurchaseDefectStatuses.Rejected
            select new
            {
                d.TicketCode,
                MaterialCode = i.MaterialCode!,
                SupplierId = po != null ? po.SupplierId : null,
                SupplierName = d.SupplierName
                    ?? (po != null ? po.SupplierNameSnapshot : null),
            }
        ).ToListAsync(ct);

        var claimRows = await (
            from c in db.PurchaseWarrantyClaims.AsNoTracking()
            join w in db.PurchaseWarranties.AsNoTracking() on c.WarrantyId equals w.Id
            where w.MaterialCode != null
                  && materialCodes.Contains(w.MaterialCode.ToLower())
                  && (c.Status == PurchaseWarrantyClaimStatuses.Submitted
                      || c.Status == PurchaseWarrantyClaimStatuses.Closed)
            select new
            {
                c.Id,
                MaterialCode = w.MaterialCode!,
                w.SupplierId,
                w.SupplierName,
            }
        ).ToListAsync(ct);

        var results = new List<SupplierMaterialRiskDto>(resolved.Count);
        foreach (var pair in resolved)
        {
            var defectCount = defectRows
                .Where(r => MaterialEquals(r.MaterialCode, pair.MaterialCode)
                            && SupplierMatches(pair, r.SupplierId, r.SupplierName))
                .Select(r => r.TicketCode)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Count();

            var claimCount = claimRows
                .Where(r => MaterialEquals(r.MaterialCode, pair.MaterialCode)
                            && SupplierMatches(pair, r.SupplierId, r.SupplierName))
                .Select(r => r.Id)
                .Distinct()
                .Count();

            var reasons = new List<string>();
            if (defectCount >= DefectThreshold)
            {
                reasons.Add(
                    $"Có {defectCount} phiếu hàng lỗi trong {DefectWindowMonths} tháng gần đây (ngưỡng {DefectThreshold}).");
            }

            if (claimCount >= WarrantyClaimThreshold)
            {
                reasons.Add(
                    $"Có {claimCount} phiếu bảo hành (ngưỡng {WarrantyClaimThreshold}).");
            }

            results.Add(new SupplierMaterialRiskDto(
                pair.SupplierId,
                pair.SupplierName,
                pair.MaterialCode,
                defectCount,
                claimCount,
                reasons.Count > 0,
                reasons));
        }

        return results;
    }

    private static string PairKey(int? supplierId, string? supplierName, string materialCode) =>
        $"{supplierId?.ToString() ?? ""}|{supplierName?.ToUpperInvariant() ?? ""}|{materialCode.ToUpperInvariant()}";

    private static string? NormalizeName(string? name) =>
        string.IsNullOrWhiteSpace(name) ? null : name.Trim();

    private static bool MaterialEquals(string a, string b) =>
        string.Equals(a, b, StringComparison.OrdinalIgnoreCase);

    private static bool SupplierMatches(ResolvedPair pair, int? rowSupplierId, string? rowSupplierName)
    {
        if (pair.SupplierId is not null && rowSupplierId is not null && pair.SupplierId == rowSupplierId)
            return true;

        if (!string.IsNullOrEmpty(pair.SupplierName)
            && !string.IsNullOrEmpty(rowSupplierName)
            && string.Equals(pair.SupplierName, rowSupplierName, StringComparison.OrdinalIgnoreCase))
            return true;

        return false;
    }

    private sealed record ResolvedPair(int? SupplierId, string? SupplierName, string MaterialCode);
}
