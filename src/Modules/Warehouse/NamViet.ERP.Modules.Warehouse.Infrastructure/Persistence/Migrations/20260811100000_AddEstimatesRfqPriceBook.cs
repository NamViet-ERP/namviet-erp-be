using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using NamViet.ERP.Modules.Warehouse.Infrastructure.Persistence;

#nullable disable

namespace NamViet.ERP.Modules.Warehouse.Infrastructure.Persistence.Migrations;

[DbContext(typeof(WarehouseDbContext))]
[Migration("20260811100000_AddEstimatesRfqPriceBook")]
public class AddEstimatesRfqPriceBook : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            ALTER TABLE warehouse.materials
                ADD COLUMN IF NOT EXISTS "CeilingPrice" numeric(18,2);
            ALTER TABLE warehouse.materials
                ADD COLUMN IF NOT EXISTS "AbcClass" character varying(1);

            CREATE TABLE IF NOT EXISTS warehouse.purchase_estimates (
                "Id" uuid PRIMARY KEY,
                "Code" character varying(30) NOT NULL,
                "ProjectCode" character varying(50) NOT NULL,
                "Title" character varying(500) NOT NULL,
                "CurrentVersion" integer NOT NULL DEFAULT 1,
                "CreatedById" uuid NOT NULL,
                "CreatedByName" character varying(200) NOT NULL,
                "UpdatedById" uuid NOT NULL,
                "UpdatedByName" character varying(200) NOT NULL,
                "CreatedAt" timestamp with time zone NOT NULL,
                "UpdatedAt" timestamp with time zone NOT NULL,
                "DeletedAt" timestamp with time zone
            );
            CREATE UNIQUE INDEX IF NOT EXISTS "IX_purchase_estimates_Code"
                ON warehouse.purchase_estimates ("Code") WHERE "DeletedAt" IS NULL;
            CREATE INDEX IF NOT EXISTS "IX_purchase_estimates_ProjectCode"
                ON warehouse.purchase_estimates ("ProjectCode");

            CREATE TABLE IF NOT EXISTS warehouse.purchase_estimate_versions (
                "Id" uuid PRIMARY KEY,
                "EstimateId" uuid NOT NULL REFERENCES warehouse.purchase_estimates ("Id") ON DELETE CASCADE,
                "VersionNumber" integer NOT NULL,
                "UpdatedById" uuid NOT NULL,
                "UpdatedByName" character varying(200) NOT NULL,
                "CreatedAt" timestamp with time zone NOT NULL,
                "UpdatedAt" timestamp with time zone NOT NULL,
                "DeletedAt" timestamp with time zone
            );
            CREATE UNIQUE INDEX IF NOT EXISTS "IX_purchase_estimate_versions_Estimate_Ver"
                ON warehouse.purchase_estimate_versions ("EstimateId", "VersionNumber");

            CREATE TABLE IF NOT EXISTS warehouse.purchase_estimate_lines (
                "Id" uuid PRIMARY KEY,
                "VersionId" uuid NOT NULL REFERENCES warehouse.purchase_estimate_versions ("Id") ON DELETE CASCADE,
                "MaterialCode" character varying(100) NOT NULL,
                "MaterialName" character varying(255) NOT NULL,
                "Unit" character varying(50),
                "OldQty" numeric(18,4) NOT NULL DEFAULT 0,
                "NewQty" numeric(18,4) NOT NULL DEFAULT 0,
                "CreatedAt" timestamp with time zone NOT NULL,
                "UpdatedAt" timestamp with time zone NOT NULL,
                "DeletedAt" timestamp with time zone
            );
            CREATE INDEX IF NOT EXISTS "IX_purchase_estimate_lines_Version_Material"
                ON warehouse.purchase_estimate_lines ("VersionId", "MaterialCode");

            CREATE TABLE IF NOT EXISTS warehouse.purchase_rfq_quotes (
                "Id" uuid PRIMARY KEY,
                "MaterialCode" character varying(100) NOT NULL,
                "MaterialName" character varying(255) NOT NULL,
                "SupplierId" integer NOT NULL,
                "SupplierName" character varying(255) NOT NULL,
                "ProjectCode" character varying(50),
                "QuoteDate" date NOT NULL,
                "UnitPrice" numeric(18,2) NOT NULL,
                "ValidUntil" date,
                "LeadTimeDays" integer,
                "PaymentTerm" character varying(200),
                "Status" character varying(20) NOT NULL,
                "PoRef" character varying(50),
                "Note" character varying(2000),
                "CreatedAt" timestamp with time zone NOT NULL,
                "UpdatedAt" timestamp with time zone NOT NULL,
                "DeletedAt" timestamp with time zone
            );
            CREATE INDEX IF NOT EXISTS "IX_purchase_rfq_quotes_QuoteDate"
                ON warehouse.purchase_rfq_quotes ("QuoteDate");
            CREATE INDEX IF NOT EXISTS "IX_purchase_rfq_quotes_SupplierId"
                ON warehouse.purchase_rfq_quotes ("SupplierId");
            CREATE INDEX IF NOT EXISTS "IX_purchase_rfq_quotes_MaterialCode"
                ON warehouse.purchase_rfq_quotes ("MaterialCode");

            CREATE TABLE IF NOT EXISTS warehouse.purchase_sell_prices (
                "Id" uuid PRIMARY KEY,
                "MaterialCode" character varying(100) NOT NULL,
                "MaterialName" character varying(255) NOT NULL,
                "ProjectCode" character varying(50),
                "SellPrice" numeric(18,2) NOT NULL,
                "EffectiveDate" date NOT NULL,
                "CreatedByName" character varying(200) NOT NULL,
                "Note" character varying(2000),
                "CreatedAt" timestamp with time zone NOT NULL,
                "UpdatedAt" timestamp with time zone NOT NULL,
                "DeletedAt" timestamp with time zone
            );
            CREATE INDEX IF NOT EXISTS "IX_purchase_sell_prices_Material_Project_Date"
                ON warehouse.purchase_sell_prices ("MaterialCode", "ProjectCode", "EffectiveDate");
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            DROP TABLE IF EXISTS warehouse.purchase_estimate_lines;
            DROP TABLE IF EXISTS warehouse.purchase_estimate_versions;
            DROP TABLE IF EXISTS warehouse.purchase_estimates;
            DROP TABLE IF EXISTS warehouse.purchase_rfq_quotes;
            DROP TABLE IF EXISTS warehouse.purchase_sell_prices;
            ALTER TABLE warehouse.materials DROP COLUMN IF EXISTS "CeilingPrice";
            ALTER TABLE warehouse.materials DROP COLUMN IF EXISTS "AbcClass";
            """);
    }
}
