using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using NamViet.ERP.Modules.Warehouse.Infrastructure.Persistence;

#nullable disable

namespace NamViet.ERP.Modules.Warehouse.Infrastructure.Persistence.Migrations;

[DbContext(typeof(WarehouseDbContext))]
[Migration("20260811090000_WarrantyClaimServiceRatingAndContractImportIndex")]
public class WarrantyClaimServiceRatingAndContractImportIndex : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            ALTER TABLE warehouse.purchase_warranty_claims
                ADD COLUMN IF NOT EXISTS "ServiceRating" integer;
            ALTER TABLE warehouse.purchase_warranty_claims
                ADD COLUMN IF NOT EXISTS "ServiceNote" character varying(2000);

            CREATE UNIQUE INDEX IF NOT EXISTS "IX_purchase_contracts_PoCode_StorageKey"
                ON warehouse.purchase_contracts ("PoCode", "StorageKey")
                WHERE "DeletedAt" IS NULL AND "PoCode" IS NOT NULL AND "StorageKey" IS NOT NULL;
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            DROP INDEX IF EXISTS warehouse."IX_purchase_contracts_PoCode_StorageKey";
            ALTER TABLE warehouse.purchase_warranty_claims DROP COLUMN IF EXISTS "ServiceNote";
            ALTER TABLE warehouse.purchase_warranty_claims DROP COLUMN IF EXISTS "ServiceRating";
            """);
    }
}
