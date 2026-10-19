using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using NamViet.ERP.Modules.Warehouse.Infrastructure.Persistence;

#nullable disable

namespace NamViet.ERP.Modules.Warehouse.Infrastructure.Persistence.Migrations;

/// <summary>
/// Placeholder migration — cây vật tư không seed tự động.
/// Nhập taxonomy trên UI (SLC → Thiết lập).
/// </summary>
[DbContext(typeof(WarehouseDbContext))]
[Migration("20260625140000_SeedMaterialCategoryTree")]
public class SeedMaterialCategoryTree : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // Intentionally empty: material category tree is optional post-deploy SQL, not auto-migration DML.
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        // No-op — material category tree is entered in the app, not via SQL seed.
    }
}
