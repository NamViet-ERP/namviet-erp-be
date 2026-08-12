using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using NamViet.ERP.Modules.Warehouse.Infrastructure.Persistence;

#nullable disable

namespace NamViet.ERP.Modules.Warehouse.Infrastructure.Persistence.Migrations;

[DbContext(typeof(WarehouseDbContext))]
[Migration("20260812180000_AddSupplierContactRole")]
public class AddSupplierContactRole : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            ALTER TABLE warehouse.supplier_contacts
                ADD COLUMN IF NOT EXISTS "Role" character varying(20) NOT NULL DEFAULT 'secondary';

            UPDATE warehouse.supplier_contacts
            SET "Role" = CASE WHEN "IsPrimary" THEN 'primary' ELSE 'secondary' END;

            -- Keep IsPrimary mirrored for any legacy reader.
            UPDATE warehouse.supplier_contacts
            SET "IsPrimary" = ("Role" = 'primary');

            CREATE INDEX IF NOT EXISTS "IX_supplier_contacts_SupplierId_Role"
                ON warehouse.supplier_contacts ("SupplierId", "Role")
                WHERE "DeletedAt" IS NULL;
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            DROP INDEX IF EXISTS warehouse."IX_supplier_contacts_SupplierId_Role";
            ALTER TABLE warehouse.supplier_contacts DROP COLUMN IF EXISTS "Role";
            """);
    }
}
