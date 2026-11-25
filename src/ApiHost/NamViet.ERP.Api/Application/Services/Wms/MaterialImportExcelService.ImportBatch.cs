using Microsoft.EntityFrameworkCore;
using NamViet.ERP.Api.Application.Dtos.Wms;
using NamViet.ERP.Modules.Warehouse.Domain.Entities;
using NamViet.ERP.SharedKernel.Common;

namespace NamViet.ERP.Api.Application.Services.Wms;

public sealed partial class MaterialImportExcelService
{
    public async Task<MaterialImportResultDto> ImportBatchAsync(
        MaterialImportBatchRequest request, CancellationToken ct = default)
    {
        if (request.Rows.Count == 0)
            throw new DomainRuleException("Không có dòng dữ liệu nào để import.");

        var validRows = request.Rows.Where(r => r.IsValid).ToList();
        if (validRows.Count == 0)
            throw new DomainRuleException("Không có dòng dữ liệu hợp lệ nào để import.");

        var createdCount = 0;
        var updatedCount = 0;
        var skippedCount = 0;
        var importedCodes = new List<string>();
        var errors = new List<string>();

        await using var tx = await db.Database.BeginTransactionAsync(ct);
        try
        {
            // 1. Đồng bộ Đơn vị tính mới
            var existingUnits = await db.MaterialUnits.AsNoTracking()
                .ToDictionaryAsync(u => u.Name, u => u, StringComparer.OrdinalIgnoreCase, ct);
            var requiredUnits = validRows.Select(r => r.Unit.Trim())
                .Where(u => !string.IsNullOrEmpty(u))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            foreach (var unitName in requiredUnits)
            {
                if (!existingUnits.ContainsKey(unitName))
                {
                    var newUnit = new MaterialUnit { Name = unitName, IsActive = true };
                    db.MaterialUnits.Add(newUnit);
                    existingUnits[unitName] = newUnit;
                }
            }
            await db.SaveChangesAsync(ct);

            // 2. Đồng bộ Xuất xứ mới
            var existingOrigins = await db.MaterialOrigins.AsNoTracking()
                .ToDictionaryAsync(o => o.Name, o => o, StringComparer.OrdinalIgnoreCase, ct);
            var requiredOrigins = validRows.Select(r => r.Origin?.Trim())
                .Where(o => !string.IsNullOrEmpty(o))
                .Select(o => o!)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            foreach (var originName in requiredOrigins)
            {
                if (!existingOrigins.ContainsKey(originName))
                {
                    var newOrigin = new MaterialOrigin { Name = originName, IsActive = true };
                    db.MaterialOrigins.Add(newOrigin);
                    existingOrigins[originName] = newOrigin;
                }
            }
            await db.SaveChangesAsync(ct);

            // 3. Xử lý Cây Danh mục (Tự tạo nhánh nếu chưa có)
            var allCategories = await db.MaterialCategories.ToListAsync(ct);
            var categoryCache = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

            // Helper tìm hoặc tạo node danh mục
            async Task<int> EnsureCategoryHierarchyAsync(string l1, string l2, string? l3, string? l4, string? l5)
            {
                var key = $"{l1}|{l2}|{l3}|{l4}|{l5}";
                if (categoryCache.TryGetValue(key, out var cachedId)) return cachedId;

                // Level 1: Danh mục
                var c1 = allCategories.FirstOrDefault(c => c.Level == 1 && c.ParentId == null && string.Equals(c.Name.Trim(), l1.Trim(), StringComparison.OrdinalIgnoreCase));
                if (c1 is null)
                {
                    c1 = new MaterialCategory
                    {
                        Name = l1.Trim(),
                        Code = GenerateCategoryCode("CAT1", l1),
                        Level = 1,
                        ParentId = null,
                        SortOrder = allCategories.Count(c => c.Level == 1) + 1,
                        IsActive = true
                    };
                    db.MaterialCategories.Add(c1);
                    await db.SaveChangesAsync(ct);
                    allCategories.Add(c1);
                }

                // Level 2: Nhóm vật tư
                var c2 = allCategories.FirstOrDefault(c => c.Level == 2 && c.ParentId == c1.Id && string.Equals(c.Name.Trim(), l2.Trim(), StringComparison.OrdinalIgnoreCase));
                if (c2 is null)
                {
                    c2 = new MaterialCategory
                    {
                        Name = l2.Trim(),
                        Code = GenerateCategoryCode("CAT2", l2),
                        Level = 2,
                        ParentId = c1.Id,
                        SortOrder = allCategories.Count(c => c.ParentId == c1.Id) + 1,
                        IsActive = true
                    };
                    db.MaterialCategories.Add(c2);
                    await db.SaveChangesAsync(ct);
                    allCategories.Add(c2);
                }

                var currentLeaf = c2;

                // Level 3 (nếu có)
                if (!string.IsNullOrWhiteSpace(l3) && l3.Trim() != "-")
                {
                    var c3 = allCategories.FirstOrDefault(c => c.Level == 3 && c.ParentId == c2.Id && string.Equals(c.Name.Trim(), l3.Trim(), StringComparison.OrdinalIgnoreCase));
                    if (c3 is null)
                    {
                        c3 = new MaterialCategory
                        {
                            Name = l3.Trim(),
                            Code = GenerateCategoryCode("CAT3", l3),
                            Level = 3,
                            ParentId = c2.Id,
                            SortOrder = allCategories.Count(c => c.ParentId == c2.Id) + 1,
                            IsActive = true
                        };
                        db.MaterialCategories.Add(c3);
                        await db.SaveChangesAsync(ct);
                        allCategories.Add(c3);
                    }
                    currentLeaf = c3;

                    // Level 4 (nếu có)
                    if (!string.IsNullOrWhiteSpace(l4) && l4.Trim() != "-")
                    {
                        var c4 = allCategories.FirstOrDefault(c => c.Level == 4 && c.ParentId == c3.Id && string.Equals(c.Name.Trim(), l4.Trim(), StringComparison.OrdinalIgnoreCase));
                        if (c4 is null)
                        {
                            c4 = new MaterialCategory
                            {
                                Name = l4.Trim(),
                                Code = GenerateCategoryCode("CAT4", l4),
                                Level = 4,
                                ParentId = c3.Id,
                                SortOrder = allCategories.Count(c => c.ParentId == c3.Id) + 1,
                                IsActive = true
                            };
                            db.MaterialCategories.Add(c4);
                            await db.SaveChangesAsync(ct);
                            allCategories.Add(c4);
                        }
                        currentLeaf = c4;

                        // Level 5 (nếu có)
                        if (!string.IsNullOrWhiteSpace(l5) && l5.Trim() != "-")
                        {
                            var c5 = allCategories.FirstOrDefault(c => c.Level == 5 && c.ParentId == c4.Id && string.Equals(c.Name.Trim(), l5.Trim(), StringComparison.OrdinalIgnoreCase));
                            if (c5 is null)
                            {
                                c5 = new MaterialCategory
                                {
                                    Name = l5.Trim(),
                                    Code = GenerateCategoryCode("CAT5", l5),
                                    Level = 5,
                                    ParentId = c4.Id,
                                    SortOrder = allCategories.Count(c => c.ParentId == c4.Id) + 1,
                                    IsActive = true
                                };
                                db.MaterialCategories.Add(c5);
                                await db.SaveChangesAsync(ct);
                                allCategories.Add(c5);
                            }
                            currentLeaf = c5;
                        }
                    }
                }

                categoryCache[key] = currentLeaf.Id;
                return currentLeaf.Id;
            }

            // 4. Lấy tất cả vật tư hiện tại theo danh sách mã
            var codes = validRows.Select(r => r.MaterialCode.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
            var existingMaterials = await db.Materials.IgnoreQueryFilters()
                .Where(m => codes.Contains(m.MaterialCode))
                .ToDictionaryAsync(m => m.MaterialCode, StringComparer.OrdinalIgnoreCase, ct);

            foreach (var row in validRows)
            {
                var code = row.MaterialCode.Trim();
                var leafCatId = await EnsureCategoryHierarchyAsync(
                    row.DanhMuc ?? "Vật tư chung",
                    row.NhomVatTu ?? "Tổng hợp",
                    row.LoaiVatTu,
                    row.PhanLoai,
                    row.Kieu);

                var isEq = IsEquipmentCategoryOrItem(row.DanhMuc, row.NhomVatTu, row.LoaiVatTu, row.MaterialName, code);
                var rowGenSpec = string.IsNullOrWhiteSpace(row.GeneralTechnicalSpec) ? null : row.GeneralTechnicalSpec.Trim();
                if (!isEq && rowGenSpec != null && (rowGenSpec.Contains("kW", StringComparison.OrdinalIgnoreCase) || rowGenSpec.Contains("Pa", StringComparison.OrdinalIgnoreCase) || rowGenSpec.Contains("m³/h", StringComparison.OrdinalIgnoreCase)))
                {
                    rowGenSpec = null;
                }

                if (existingMaterials.TryGetValue(code, out var existing))
                {
                    if (request.OverwriteExisting)
                    {
                        existing.MaterialName = row.MaterialName.Trim();
                        existing.CategoryId = leafCatId;
                        existing.Unit = row.Unit.Trim();
                        existing.Origin = string.IsNullOrWhiteSpace(row.Origin) ? null : row.Origin.Trim();
                        existing.TechnicalSpec = string.IsNullOrWhiteSpace(row.TechnicalSpec) ? null : row.TechnicalSpec.Trim();
                        existing.GeneralTechnicalSpec = rowGenSpec;
                        existing.QualityStandard = string.IsNullOrWhiteSpace(row.QualityStandard) ? null : row.QualityStandard.Trim();
                        existing.CeilingPrice = row.CeilingPrice is > 0 ? row.CeilingPrice : null;
                        existing.AbcClass = NormalizeAbc(row.AbcClass);
                        existing.IsConsumable = row.IsConsumable;
                        existing.IsActive = true;
                        existing.DeletedAt = null;
                        existing.UpdatedAt = DateTime.UtcNow;

                        updatedCount++;
                        importedCodes.Add(code);
                    }
                    else
                    {
                        skippedCount++;
                    }
                }
                else
                {
                    var newMaterial = new Material
                    {
                        MaterialCode = code,
                        MaterialName = row.MaterialName.Trim(),
                        CategoryId = leafCatId,
                        Unit = row.Unit.Trim(),
                        Origin = string.IsNullOrWhiteSpace(row.Origin) ? null : row.Origin.Trim(),
                        TechnicalSpec = string.IsNullOrWhiteSpace(row.TechnicalSpec) ? null : row.TechnicalSpec.Trim(),
                        GeneralTechnicalSpec = rowGenSpec,
                        QualityStandard = string.IsNullOrWhiteSpace(row.QualityStandard) ? null : row.QualityStandard.Trim(),
                        CeilingPrice = row.CeilingPrice is > 0 ? row.CeilingPrice : null,
                        AbcClass = NormalizeAbc(row.AbcClass),
                        IsConsumable = row.IsConsumable,
                        IsActive = true
                    };
                    db.Materials.Add(newMaterial);
                    existingMaterials[code] = newMaterial;
                    createdCount++;
                    importedCodes.Add(code);
                }
            }

            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);

            return new MaterialImportResultDto(
                createdCount,
                updatedCount,
                skippedCount,
                createdCount + updatedCount,
                importedCodes,
                errors);
        }
        catch (Exception ex)
        {
            await tx.RollbackAsync(ct);
            throw new DomainRuleException($"Lỗi trong quá trình import dữ liệu: {ex.Message}");
        }
    }

    private static string GenerateCategoryCode(string prefix, string name)
    {
        var slug = NormalizeHeader(name);
        if (slug.Length > 10) slug = slug[..10];
        if (string.IsNullOrEmpty(slug)) slug = "CAT";
        var uid = Guid.NewGuid().ToString("N")[..4].ToUpperInvariant();
        return $"{prefix}-{slug.ToUpperInvariant()}-{uid}";
    }
}
