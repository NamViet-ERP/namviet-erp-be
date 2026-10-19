using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using NamViet.ERP.Modules.Warehouse.Infrastructure.Persistence;

#nullable disable

namespace NamViet.ERP.Modules.Warehouse.Infrastructure.Persistence.Migrations;

[DbContext(typeof(WarehouseDbContext))]
[Migration("20260806140000_SupplierEvalCriteriaAndBlacklist")]
public class SupplierEvalCriteriaAndBlacklist : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            -- Supplier blacklist + rating default for unrated
            ALTER TABLE warehouse.suppliers
                ADD COLUMN IF NOT EXISTS "IsBlacklisted" boolean NOT NULL DEFAULT false,
                ADD COLUMN IF NOT EXISTS "BlacklistedAt" timestamp with time zone,
                ADD COLUMN IF NOT EXISTS "BlacklistedById" uuid,
                ADD COLUMN IF NOT EXISTS "BlacklistedByName" character varying(200),
                ADD COLUMN IF NOT EXISTS "BlacklistReason" character varying(1000);

            CREATE INDEX IF NOT EXISTS "IX_suppliers_IsBlacklisted"
                ON warehouse.suppliers ("IsBlacklisted");

            UPDATE warehouse.suppliers
            SET "Rating" = '—'
            WHERE "Rating" IS NULL OR "Rating" = '';

            ALTER TABLE warehouse.suppliers
                ALTER COLUMN "Rating" SET DEFAULT '—';

            -- Delivery evaluations: 5 criteria, drop legacy rates
            ALTER TABLE warehouse.supplier_delivery_evaluations
                ADD COLUMN IF NOT EXISTS "ProjectCode" character varying(50),
                ADD COLUMN IF NOT EXISTS "Quality" numeric(4,2),
                ADD COLUMN IF NOT EXISTS "Delivery" numeric(4,2),
                ADD COLUMN IF NOT EXISTS "Price" numeric(4,2),
                ADD COLUMN IF NOT EXISTS "Service" numeric(4,2),
                ADD COLUMN IF NOT EXISTS "Compliance" numeric(4,2);

            ALTER TABLE warehouse.supplier_delivery_evaluations
                DROP COLUMN IF EXISTS "OnTimeDeliveryRate",
                DROP COLUMN IF EXISTS "DefectRate",
                DROP COLUMN IF EXISTS "WarrantyCompletionRate";

            -- Periodic KPI: criteria JSON, drop legacy rates
            ALTER TABLE warehouse.supplier_evaluations
                ADD COLUMN IF NOT EXISTS "CriteriaScores" jsonb NOT NULL DEFAULT '{}'::jsonb;

            ALTER TABLE warehouse.supplier_evaluations
                DROP COLUMN IF EXISTS "OnTimeDeliveryRate",
                DROP COLUMN IF EXISTS "DefectRate",
                DROP COLUMN IF EXISTS "WarrantyCompletionRate";
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            ALTER TABLE warehouse.supplier_evaluations
                ADD COLUMN IF NOT EXISTS "OnTimeDeliveryRate" numeric(5,2) NOT NULL DEFAULT 0,
                ADD COLUMN IF NOT EXISTS "DefectRate" numeric(5,2) NOT NULL DEFAULT 0,
                ADD COLUMN IF NOT EXISTS "WarrantyCompletionRate" numeric(5,2) NOT NULL DEFAULT 0;

            ALTER TABLE warehouse.supplier_evaluations
                DROP COLUMN IF EXISTS "CriteriaScores";

            ALTER TABLE warehouse.supplier_delivery_evaluations
                ADD COLUMN IF NOT EXISTS "OnTimeDeliveryRate" numeric(5,2),
                ADD COLUMN IF NOT EXISTS "DefectRate" numeric(5,2),
                ADD COLUMN IF NOT EXISTS "WarrantyCompletionRate" numeric(5,2);

            ALTER TABLE warehouse.supplier_delivery_evaluations
                DROP COLUMN IF EXISTS "ProjectCode",
                DROP COLUMN IF EXISTS "Quality",
                DROP COLUMN IF EXISTS "Delivery",
                DROP COLUMN IF EXISTS "Price",
                DROP COLUMN IF EXISTS "Service",
                DROP COLUMN IF EXISTS "Compliance";

            DROP INDEX IF EXISTS warehouse."IX_suppliers_IsBlacklisted";

            ALTER TABLE warehouse.suppliers
                DROP COLUMN IF EXISTS "IsBlacklisted",
                DROP COLUMN IF EXISTS "BlacklistedAt",
                DROP COLUMN IF EXISTS "BlacklistedById",
                DROP COLUMN IF EXISTS "BlacklistedByName",
                DROP COLUMN IF EXISTS "BlacklistReason";

            ALTER TABLE warehouse.suppliers
                ALTER COLUMN "Rating" SET DEFAULT 'A';
            """);
    }
}
