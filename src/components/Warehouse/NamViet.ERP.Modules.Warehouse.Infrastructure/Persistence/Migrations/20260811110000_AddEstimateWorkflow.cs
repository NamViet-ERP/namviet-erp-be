using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using NamViet.ERP.Modules.Warehouse.Infrastructure.Persistence;

#nullable disable

namespace NamViet.ERP.Modules.Warehouse.Infrastructure.Persistence.Migrations;

[DbContext(typeof(WarehouseDbContext))]
[Migration("20260811110000_AddEstimateWorkflow")]
public class AddEstimateWorkflow : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            ALTER TABLE warehouse.purchase_estimates
                ADD COLUMN IF NOT EXISTS "Status" character varying(30) NOT NULL DEFAULT 'draft';
            ALTER TABLE warehouse.purchase_estimates
                ADD COLUMN IF NOT EXISTS "ShareToken" uuid;
            ALTER TABLE warehouse.purchase_estimates
                ADD COLUMN IF NOT EXISTS "PreparerName" character varying(200);
            ALTER TABLE warehouse.purchase_estimates
                ADD COLUMN IF NOT EXISTS "PreparerDate" timestamp with time zone;
            ALTER TABLE warehouse.purchase_estimates
                ADD COLUMN IF NOT EXISTS "ReviewedById" uuid;
            ALTER TABLE warehouse.purchase_estimates
                ADD COLUMN IF NOT EXISTS "ReviewedByName" character varying(200);
            ALTER TABLE warehouse.purchase_estimates
                ADD COLUMN IF NOT EXISTS "ReviewedAt" timestamp with time zone;
            ALTER TABLE warehouse.purchase_estimates
                ADD COLUMN IF NOT EXISTS "ConvertedPlanCode" character varying(30);

            CREATE UNIQUE INDEX IF NOT EXISTS "IX_purchase_estimates_ShareToken"
                ON warehouse.purchase_estimates ("ShareToken")
                WHERE "ShareToken" IS NOT NULL AND "DeletedAt" IS NULL;

            ALTER TABLE warehouse.purchase_estimate_lines
                ADD COLUMN IF NOT EXISTS "CategoryName" character varying(200);

            CREATE TABLE IF NOT EXISTS warehouse.purchase_estimate_proposals (
                "Id" uuid PRIMARY KEY,
                "EstimateId" uuid NOT NULL REFERENCES warehouse.purchase_estimates ("Id") ON DELETE CASCADE,
                "Name" character varying(255) NOT NULL,
                "Spec" character varying(500),
                "Unit" character varying(50),
                "Qty" numeric(18,4) NOT NULL DEFAULT 0,
                "Note" character varying(1000),
                "CreatedAt" timestamp with time zone NOT NULL,
                "UpdatedAt" timestamp with time zone NOT NULL,
                "DeletedAt" timestamp with time zone
            );

            CREATE TABLE IF NOT EXISTS warehouse.purchase_estimate_approvals (
                "Id" uuid PRIMARY KEY,
                "EstimateId" uuid NOT NULL REFERENCES warehouse.purchase_estimates ("Id") ON DELETE CASCADE,
                "Level" integer NOT NULL,
                "Name" character varying(200) NOT NULL,
                "Status" character varying(20) NOT NULL,
                "ActionById" uuid,
                "ActionByName" character varying(200),
                "ActionAt" timestamp with time zone,
                "Note" character varying(1000),
                "CreatedAt" timestamp with time zone NOT NULL,
                "UpdatedAt" timestamp with time zone NOT NULL,
                "DeletedAt" timestamp with time zone
            );
            CREATE UNIQUE INDEX IF NOT EXISTS "IX_purchase_estimate_approvals_Estimate_Level"
                ON warehouse.purchase_estimate_approvals ("EstimateId", "Level");
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            DROP TABLE IF EXISTS warehouse.purchase_estimate_approvals;
            DROP TABLE IF EXISTS warehouse.purchase_estimate_proposals;
            DROP INDEX IF EXISTS warehouse."IX_purchase_estimates_ShareToken";
            ALTER TABLE warehouse.purchase_estimate_lines DROP COLUMN IF EXISTS "CategoryName";
            ALTER TABLE warehouse.purchase_estimates
                DROP COLUMN IF EXISTS "Status",
                DROP COLUMN IF EXISTS "ShareToken",
                DROP COLUMN IF EXISTS "PreparerName",
                DROP COLUMN IF EXISTS "PreparerDate",
                DROP COLUMN IF EXISTS "ReviewedById",
                DROP COLUMN IF EXISTS "ReviewedByName",
                DROP COLUMN IF EXISTS "ReviewedAt",
                DROP COLUMN IF EXISTS "ConvertedPlanCode";
            """);
    }
}
