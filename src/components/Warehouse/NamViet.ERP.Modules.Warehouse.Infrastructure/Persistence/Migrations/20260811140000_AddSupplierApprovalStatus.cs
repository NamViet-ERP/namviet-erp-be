using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using NamViet.ERP.Modules.Warehouse.Infrastructure.Persistence;

#nullable disable

namespace NamViet.ERP.Modules.Warehouse.Infrastructure.Persistence.Migrations;

[DbContext(typeof(WarehouseDbContext))]
[Migration("20260811140000_AddSupplierApprovalStatus")]
public class AddSupplierApprovalStatus : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            ALTER TABLE warehouse.suppliers
                ADD COLUMN IF NOT EXISTS "ApprovalStatus" character varying(20) NOT NULL DEFAULT 'approved';

            ALTER TABLE warehouse.suppliers
                ALTER COLUMN "ApprovalStatus" SET DEFAULT 'new';

            UPDATE warehouse.suppliers
            SET "Rating" = 'D'
            WHERE "DeletedAt" IS NULL
              AND ("Rating" IS NULL OR "Rating" = '' OR "Rating" = '—');

            ALTER TABLE warehouse.suppliers
                ALTER COLUMN "Rating" SET DEFAULT 'D';

            CREATE INDEX IF NOT EXISTS "IX_suppliers_ApprovalStatus"
                ON warehouse.suppliers ("ApprovalStatus");
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            DROP INDEX IF EXISTS warehouse."IX_suppliers_ApprovalStatus";
            ALTER TABLE warehouse.suppliers DROP COLUMN IF EXISTS "ApprovalStatus";
            ALTER TABLE warehouse.suppliers ALTER COLUMN "Rating" SET DEFAULT '—';
            """);
    }
}
