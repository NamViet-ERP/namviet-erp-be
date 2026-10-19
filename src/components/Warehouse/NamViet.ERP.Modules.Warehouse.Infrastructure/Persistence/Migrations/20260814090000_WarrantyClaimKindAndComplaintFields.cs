using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using NamViet.ERP.Modules.Warehouse.Infrastructure.Persistence;

#nullable disable

namespace NamViet.ERP.Modules.Warehouse.Infrastructure.Persistence.Migrations;

[DbContext(typeof(WarehouseDbContext))]
[Migration("20260814090000_WarrantyClaimKindAndComplaintFields")]
public class WarrantyClaimKindAndComplaintFields : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            ALTER TABLE warehouse.purchase_warranty_claims
                ADD COLUMN IF NOT EXISTS "Kind" character varying(20) NOT NULL DEFAULT 'warranty';
            ALTER TABLE warehouse.purchase_warranty_claims
                ADD COLUMN IF NOT EXISTS "DefectType" character varying(20);
            ALTER TABLE warehouse.purchase_warranty_claims
                ADD COLUMN IF NOT EXISTS "Severity" character varying(20);

            CREATE INDEX IF NOT EXISTS "IX_purchase_warranty_claims_Kind"
                ON warehouse.purchase_warranty_claims ("Kind");
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            DROP INDEX IF EXISTS warehouse."IX_purchase_warranty_claims_Kind";
            ALTER TABLE warehouse.purchase_warranty_claims DROP COLUMN IF EXISTS "Severity";
            ALTER TABLE warehouse.purchase_warranty_claims DROP COLUMN IF EXISTS "DefectType";
            ALTER TABLE warehouse.purchase_warranty_claims DROP COLUMN IF EXISTS "Kind";
            """);
    }
}
