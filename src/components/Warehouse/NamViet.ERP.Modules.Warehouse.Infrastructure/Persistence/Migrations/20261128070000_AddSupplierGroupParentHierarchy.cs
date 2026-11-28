using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using NamViet.ERP.Modules.Warehouse.Infrastructure.Persistence;

#nullable disable

namespace NamViet.ERP.Modules.Warehouse.Infrastructure.Persistence.Migrations;

[DbContext(typeof(WarehouseDbContext))]
[Migration("20261128070000_AddSupplierGroupParentHierarchy")]
public partial class AddSupplierGroupParentHierarchy : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            ALTER TABLE warehouse.supplier_groups
                ADD COLUMN IF NOT EXISTS "ParentGroupId" integer NULL;

            DO $$
            BEGIN
                IF NOT EXISTS (
                    SELECT 1
                    FROM pg_constraint
                    WHERE conname = 'FK_supplier_groups_supplier_groups_ParentGroupId'
                      AND conrelid = 'warehouse.supplier_groups'::regclass
                ) THEN
                    ALTER TABLE warehouse.supplier_groups
                        ADD CONSTRAINT "FK_supplier_groups_supplier_groups_ParentGroupId"
                        FOREIGN KEY ("ParentGroupId") REFERENCES warehouse.supplier_groups ("Id") ON DELETE RESTRICT;
                END IF;
            END $$;

            DROP INDEX IF EXISTS warehouse."IX_supplier_groups_Name";

            CREATE UNIQUE INDEX IF NOT EXISTS "IX_supplier_groups_Root_Name"
                ON warehouse.supplier_groups ("Name")
                WHERE "ParentGroupId" IS NULL AND "DeletedAt" IS NULL;

            CREATE UNIQUE INDEX IF NOT EXISTS "IX_supplier_groups_ParentGroupId_Name"
                ON warehouse.supplier_groups ("ParentGroupId", "Name")
                WHERE "ParentGroupId" IS NOT NULL AND "DeletedAt" IS NULL;

            CREATE INDEX IF NOT EXISTS "IX_supplier_groups_ParentGroupId"
                ON warehouse.supplier_groups ("ParentGroupId");
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            DROP INDEX IF EXISTS warehouse."IX_supplier_groups_ParentGroupId_Name";
            DROP INDEX IF EXISTS warehouse."IX_supplier_groups_Root_Name";
            DROP INDEX IF EXISTS warehouse."IX_supplier_groups_ParentGroupId";

            ALTER TABLE warehouse.supplier_groups
                DROP CONSTRAINT IF EXISTS "FK_supplier_groups_supplier_groups_ParentGroupId";

            ALTER TABLE warehouse.supplier_groups
                DROP COLUMN IF EXISTS "ParentGroupId";

            CREATE UNIQUE INDEX IF NOT EXISTS "IX_supplier_groups_Name"
                ON warehouse.supplier_groups ("Name")
                WHERE "DeletedAt" IS NULL;
            """);
    }
}
