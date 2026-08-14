using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using NamViet.ERP.Modules.Warehouse.Infrastructure.Persistence;

#nullable disable

namespace NamViet.ERP.Modules.Warehouse.Infrastructure.Persistence.Migrations;

[DbContext(typeof(WarehouseDbContext))]
[Migration("20260814110000_AddPurchasePaymentPaidBy")]
public class AddPurchasePaymentPaidBy : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            ALTER TABLE warehouse.purchase_payments
                ADD COLUMN IF NOT EXISTS "PaidById" uuid;
            ALTER TABLE warehouse.purchase_payments
                ADD COLUMN IF NOT EXISTS "PaidByName" character varying(200);
            ALTER TABLE warehouse.purchase_payments
                ADD COLUMN IF NOT EXISTS "PaidAt" timestamp with time zone;
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            ALTER TABLE warehouse.purchase_payments DROP COLUMN IF EXISTS "PaidAt";
            ALTER TABLE warehouse.purchase_payments DROP COLUMN IF EXISTS "PaidByName";
            ALTER TABLE warehouse.purchase_payments DROP COLUMN IF EXISTS "PaidById";
            """);
    }
}
