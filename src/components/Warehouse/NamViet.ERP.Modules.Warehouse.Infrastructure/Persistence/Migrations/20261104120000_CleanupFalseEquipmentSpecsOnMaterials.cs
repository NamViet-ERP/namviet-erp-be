using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using NamViet.ERP.Modules.Warehouse.Infrastructure.Persistence;

#nullable disable

namespace NamViet.ERP.Modules.Warehouse.Infrastructure.Persistence.Migrations;

[DbContext(typeof(WarehouseDbContext))]
[Migration("20261104120000_CleanupFalseEquipmentSpecsOnMaterials")]
public class CleanupFalseEquipmentSpecsOnMaterials : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(@"
            UPDATE warehouse.materials
            SET ""GeneralTechnicalSpec"" = NULL
            WHERE ""GeneralTechnicalSpec"" IS NOT NULL
              AND (""GeneralTechnicalSpec"" LIKE '%kW%' OR ""GeneralTechnicalSpec"" LIKE '%Pa%' OR ""GeneralTechnicalSpec"" LIKE '%m³/h%')
              AND (""MaterialCode"" LIKE 'BÔ-%' OR ""MaterialCode"" LIKE 'BH-%' OR ""MaterialCode"" LIKE 'TH-%' 
                   OR ""MaterialCode"" LIKE 'IN-%' OR ""MaterialCode"" LIKE 'ONG-%' OR ""MaterialCode"" LIKE 'PK-%' 
                   OR ""MaterialCode"" LIKE 'LB-%' OR ""MaterialCode"" LIKE 'VT-%')
              AND ""GeneralTechnicalSpec"" NOT LIKE '%Động cơ%'
              AND ""GeneralTechnicalSpec"" NOT LIKE '%Quạt%'
              AND ""GeneralTechnicalSpec"" NOT LIKE '%Bơm%';
        ");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
    }
}
