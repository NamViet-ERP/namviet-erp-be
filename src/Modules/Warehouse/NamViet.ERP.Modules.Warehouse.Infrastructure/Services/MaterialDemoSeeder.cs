using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NamViet.ERP.Modules.Warehouse.Domain.Entities;
using NamViet.ERP.Modules.Warehouse.Infrastructure.Persistence;

namespace NamViet.ERP.Modules.Warehouse.Infrastructure.Services;

/// <summary>20 vật tư demo — đủ mã, tên, danh mục (cây Excel), quy cách, TCCL.</summary>
internal static class MaterialDemoSeeder
{
    private sealed record MaterialSeedRow(
        string Code,
        string Name,
        string CategoryCode,
        string Unit,
        decimal UnitPrice,
        string TechnicalSpec,
        string QualityStandard,
        bool IsConsumable = false);

    private static readonly MaterialSeedRow[] DemoMaterials =
    [
        new("TE-PLT-012-SA516", "Thép tấm 12mm SA516 Gr.70", "CAT-F29FE3853BDE", "Tấm", 18_500_000m,
            "1500×6000×12mm, cán nóng", "ASTM A516 Gr.70"),
        new("TE-PLT-008-SS400", "Thép tấm 8mm SS400", "CAT-DC3CE73897FC", "Tấm", 12_500_000m,
            "1500×6000×8mm", "JIS G3101 SS400"),
        new("OG-DN100-SCH40-HOT", "Ống thép đúc DN100 Sch40 cán nóng", "CAT-C90364AA151F", "Mét", 850_000m,
            "DN100, Sch40, cán nóng", "ASTM A106 Gr.B"),
        new("OG-DN80-SCH40-COLD", "Ống thép đúc DN80 Sch40 cán nguội", "CAT-FFC736B52368", "Mét", 720_000m,
            "DN80, Sch40, cán nguội", "ASTM A53 Gr.B"),
        new("HP-V100x50x5", "Thép hình V100×50×5", "CAT-722536774109", "Mét", 680_000m,
            "100×50×5mm", "JIS G3101 SS400"),
        new("HP-I200x100x8", "Thép hình I200×100×8", "CAT-134D6FFD6176", "Mét", 1_150_000m,
            "I200×100×8mm", "GB/T 706-2008"),
        new("IX-304-2B-3T", "Inox tấm 304 2B dày 3mm", "CAT-6E4454A1B693", "Tấm", 4_200_000m,
            "1219×2438×3mm, mặt 2B", "ASTM A240 TP304"),
        new("PK-CO90-D50-DUC", "Co 90° đúc DN50", "CAT-EB76839F495E", "Cái", 185_000m,
            "DN50, góc 90°, đúc", "ANSI B16.9"),
        new("PK-TEE-D80-HAN", "Tê hàn DN80", "CAT-05D87267C853", "Cái", 320_000m,
            "DN80, hàn", "ASME B16.9"),
        new("VN-CAU-MB-DN100", "Van cầu mặt bích DN100 PN16", "CAT-ED49F8513660", "Cái", 8_500_000m,
            "DN100, PN16, mặt bích", "API 600"),
        new("VN-AT-FL-DN25", "Van an toàn full lift DN25", "CAT-4136A39D3981", "Cái", 4_200_000m,
            "DN25, full lift, lò hơi", "EN ISO 4126"),
        new("LC-Y-HOI-DN50", "Lọc Y hơi DN50", "CAT-5117DE574163", "Cái", 2_100_000m,
            "DN50, lọc hơi bão hòa", "PN40"),
        new("MNK-SC-7.5", "Máy nén khí trục vít 7.5HP", "CAT-B6B794A10D6C", "Cái", 45_000_000m,
            "7.5HP, 8bar, 0.8m³/phút", "ISO 8573-1"),
        new("MNK-SC-15", "Máy nén khí trục vít 15HP", "CAT-B6B794A10D6C", "Cái", 78_000_000m,
            "15HP, 8bar, 1.6m³/phút", "ISO 8573-1"),
        new("LLK-10", "Lọc khí công nghiệp 10μm", "CAT-5117DE574163", "Cái", 3_500_000m,
            "10μm, DN100", "ISO 8573-1 Class 1"),
        new("DK-OIL-20L", "Dầu máy nén khí 20L", "CAT-EA833F17EBB0", "Thùng", 2_800_000m,
            "20L, synthetic", "ISO 8573-1", true),
        new("QH-KT-421", "Que hàn điện KT-421 Φ3.2", "CAT-0BE6FDCA382C", "Kg", 120_000m,
            "Φ3.2mm", "AWS A5.1 E6013", true),
        new("BM-CN-15KW", "Bơm ly tâm trục đứng 15kW", "CAT-436E10B686C7", "Cái", 28_500_000m,
            "Q=120m³/h, H=32m", "ISO 5199"),
        new("BO-ONG-50-COBAC", "Bông ống cách nhiệt DN50 có bạc", "CAT-00E684D78F32", "Mét", 95_000m,
            "DN50, dày 50mm, có bạc", "ASTM C547"),
        new("BLK-M12x50-88", "Bulong M12×50 cấp 8.8", "CAT-CFFBDB48F760", "Cái", 3_500m,
            "M12×50, cấp 8.8", "DIN 933"),
    ];

    public static async Task EnsureDemoMaterialsAsync(
        WarehouseDbContext db, ILogger? logger = null, CancellationToken ct = default)
    {
        var categoryIds = await db.MaterialCategories
            .AsNoTracking()
            .ToDictionaryAsync(c => c.Code, c => c.Id, ct);

        var supplier = await db.Suppliers.FirstOrDefaultAsync(s => s.SupplierCode == "NCC-DEMO", ct);
        if (supplier is null)
        {
            supplier = new Supplier
            {
                SupplierCode = "NCC-DEMO",
                SupplierName = "Nhà cung cấp demo Nam Việt",
                Rating = "A"
            };
            db.Suppliers.Add(supplier);
            await db.SaveChangesAsync(ct);
        }

        var touched = 0;
        foreach (var row in DemoMaterials)
        {
            if (!categoryIds.TryGetValue(row.CategoryCode, out var categoryId))
            {
                logger?.LogWarning("Skip material {Code}: category {Cat} not found", row.Code, row.CategoryCode);
                continue;
            }

            var material = await db.Materials
                .FirstOrDefaultAsync(m => m.MaterialCode == row.Code, ct);

            if (material is null)
            {
                material = new Material { MaterialCode = row.Code };
                db.Materials.Add(material);
            }
            else if (material.DeletedAt is not null)
            {
                material.DeletedAt = null;
            }

            material.MaterialName = row.Name;
            material.CategoryId = categoryId;
            material.Unit = row.Unit;
            material.TechnicalSpec = row.TechnicalSpec;
            material.QualityStandard = row.QualityStandard;
            material.IsConsumable = row.IsConsumable;
            material.UpdatedAt = DateTime.UtcNow;
            touched++;

            var hasPrice = await db.PriceBooks
                .AnyAsync(p => p.MaterialCode == row.Code && p.SupplierId == supplier.Id, ct);
            if (!hasPrice)
            {
                db.PriceBooks.Add(new PriceBook
                {
                    MaterialCode = row.Code,
                    SupplierId = supplier.Id,
                    UnitPrice = row.UnitPrice,
                    PriceDate = DateTime.UtcNow
                });
            }
        }

        if (touched > 0)
        {
            await db.SaveChangesAsync(ct);
            logger?.LogInformation("Ensured {Count} demo materials with full fields.", touched);
        }
    }
}
