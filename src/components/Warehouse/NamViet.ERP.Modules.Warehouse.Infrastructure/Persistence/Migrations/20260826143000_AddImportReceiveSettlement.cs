using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using NamViet.ERP.Modules.Warehouse.Infrastructure.Persistence;

#nullable disable

namespace NamViet.ERP.Modules.Warehouse.Infrastructure.Persistence.Migrations;

[DbContext(typeof(WarehouseDbContext))]
[Migration("20260826143000_AddImportReceiveSettlement")]
public class AddImportReceiveSettlement : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            ALTER TABLE warehouse.warehouse_transaction_items
            ADD COLUMN IF NOT EXISTS "PlannedQuantity" numeric(18,4) NOT NULL DEFAULT 0;

            ALTER TABLE warehouse.warehouse_transaction_items
            ADD COLUMN IF NOT EXISTS "AcceptedQty" numeric(18,4) NULL;

            ALTER TABLE warehouse.warehouse_transaction_items
            ADD COLUMN IF NOT EXISTS "DefectQty" numeric(18,4) NULL;

            ALTER TABLE warehouse.warehouse_transaction_items
            ADD COLUMN IF NOT EXISTS "ShortageQty" numeric(18,4) NULL;

            ALTER TABLE warehouse.warehouse_transaction_items
            ADD COLUMN IF NOT EXISTS "DefectLineReason" character varying(1000) NULL;

            ALTER TABLE warehouse.warehouse_transaction_items
            ADD COLUMN IF NOT EXISTS "ShortageLineReason" character varying(1000) NULL;

            UPDATE warehouse.warehouse_transaction_items
            SET "PlannedQuantity" = "Quantity"
            WHERE "PlannedQuantity" = 0 AND "Quantity" > 0;

            ALTER TABLE warehouse.warehouse_transactions
            ADD COLUMN IF NOT EXISTS "ShortageReport" character varying(2000) NULL;

            ALTER TABLE warehouse.warehouse_transactions
            ADD COLUMN IF NOT EXISTS "LinkedDefectTicketCode" character varying(50) NULL;

            ALTER TABLE warehouse.purchase_defects
            ADD COLUMN IF NOT EXISTS "ImportTicketCode" character varying(50) NULL;

            CREATE INDEX IF NOT EXISTS "IX_purchase_defects_ImportTicketCode"
            ON warehouse.purchase_defects ("ImportTicketCode");
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            DROP INDEX IF EXISTS warehouse."IX_purchase_defects_ImportTicketCode";

            ALTER TABLE warehouse.purchase_defects
            DROP COLUMN IF EXISTS "ImportTicketCode";

            ALTER TABLE warehouse.warehouse_transactions
            DROP COLUMN IF EXISTS "LinkedDefectTicketCode";

            ALTER TABLE warehouse.warehouse_transactions
            DROP COLUMN IF EXISTS "ShortageReport";

            ALTER TABLE warehouse.warehouse_transaction_items
            DROP COLUMN IF EXISTS "ShortageLineReason";

            ALTER TABLE warehouse.warehouse_transaction_items
            DROP COLUMN IF EXISTS "DefectLineReason";

            ALTER TABLE warehouse.warehouse_transaction_items
            DROP COLUMN IF EXISTS "ShortageQty";

            ALTER TABLE warehouse.warehouse_transaction_items
            DROP COLUMN IF EXISTS "DefectQty";

            ALTER TABLE warehouse.warehouse_transaction_items
            DROP COLUMN IF EXISTS "AcceptedQty";

            ALTER TABLE warehouse.warehouse_transaction_items
            DROP COLUMN IF EXISTS "PlannedQuantity";
            """);
    }
}
