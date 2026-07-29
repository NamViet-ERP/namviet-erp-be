using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NamViet.ERP.Modules.Warehouse.Infrastructure.Persistence.Migrations;

/// <summary>
/// DDL-only: bổ sung trường quản trị NCC và chỉ số đánh giá theo kỳ.
/// </summary>
[DbContext(typeof(WarehouseDbContext))]
[Migration("20260727093000_AddSupplierGovernanceFields")]
public class AddSupplierGovernanceFields : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            ALTER TABLE warehouse.suppliers
                ADD COLUMN IF NOT EXISTS "PaymentTerms" character varying(500) NULL;

            ALTER TABLE warehouse.supplier_evaluations
                ADD COLUMN IF NOT EXISTS "OnTimeDeliveryRate" numeric(5,2) NOT NULL DEFAULT 0;

            ALTER TABLE warehouse.supplier_evaluations
                ADD COLUMN IF NOT EXISTS "DefectRate" numeric(5,2) NOT NULL DEFAULT 0;

            ALTER TABLE warehouse.supplier_evaluations
                ADD COLUMN IF NOT EXISTS "WarrantyCompletionRate" numeric(5,2) NOT NULL DEFAULT 0;
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            ALTER TABLE warehouse.supplier_evaluations DROP COLUMN IF EXISTS "WarrantyCompletionRate";
            ALTER TABLE warehouse.supplier_evaluations DROP COLUMN IF EXISTS "DefectRate";
            ALTER TABLE warehouse.supplier_evaluations DROP COLUMN IF EXISTS "OnTimeDeliveryRate";
            ALTER TABLE warehouse.suppliers DROP COLUMN IF EXISTS "PaymentTerms";
            """);
    }
}
