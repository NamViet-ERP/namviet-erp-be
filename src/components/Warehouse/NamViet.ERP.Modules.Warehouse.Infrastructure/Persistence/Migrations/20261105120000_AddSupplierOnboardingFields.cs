using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using NamViet.ERP.Modules.Warehouse.Infrastructure.Persistence;

#nullable disable

namespace NamViet.ERP.Modules.Warehouse.Infrastructure.Persistence.Migrations;

[DbContext(typeof(WarehouseDbContext))]
[Migration("20261105120000_AddSupplierOnboardingFields")]
public partial class AddSupplierOnboardingFields : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            ALTER TABLE warehouse.suppliers ADD COLUMN IF NOT EXISTS "SupplierType" character varying(50) NULL;
            ALTER TABLE warehouse.suppliers ADD COLUMN IF NOT EXISTS "Scope" character varying(50) NULL;
            ALTER TABLE warehouse.suppliers ADD COLUMN IF NOT EXISTS "Website" character varying(255) NULL;
        """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            ALTER TABLE warehouse.suppliers DROP COLUMN IF EXISTS "SupplierType";
            ALTER TABLE warehouse.suppliers DROP COLUMN IF EXISTS "Scope";
            ALTER TABLE warehouse.suppliers DROP COLUMN IF EXISTS "Website";
        """);
    }
}
