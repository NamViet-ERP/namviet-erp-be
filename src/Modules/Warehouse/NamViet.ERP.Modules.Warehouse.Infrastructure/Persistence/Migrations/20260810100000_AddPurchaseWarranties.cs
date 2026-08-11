using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using NamViet.ERP.Modules.Warehouse.Infrastructure.Persistence;

#nullable disable

namespace NamViet.ERP.Modules.Warehouse.Infrastructure.Persistence.Migrations;

[DbContext(typeof(WarehouseDbContext))]
[Migration("20260810100000_AddPurchaseWarranties")]
public class AddPurchaseWarranties : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            ALTER TABLE warehouse.purchase_order_items
                ADD COLUMN IF NOT EXISTS "WarrantyMonths" integer NOT NULL DEFAULT 0;
            ALTER TABLE warehouse.purchase_order_items
                ADD COLUMN IF NOT EXISTS "WarrantyAnchorType" character varying(20) NOT NULL DEFAULT 'delivery';

            CREATE TABLE IF NOT EXISTS warehouse.purchase_warranties (
                "Id" uuid PRIMARY KEY,
                "Code" character varying(30) NOT NULL,
                "PoCode" character varying(50) NOT NULL,
                "PoItemId" integer NOT NULL,
                "ProjectCode" character varying(50),
                "PlanCode" character varying(50),
                "SupplierId" integer,
                "SupplierName" character varying(255),
                "MaterialCode" character varying(100),
                "MaterialName" character varying(500) NOT NULL,
                "Unit" character varying(50) NOT NULL DEFAULT '',
                "Quantity" numeric(18,4) NOT NULL DEFAULT 0,
                "WarrantyMonths" integer NOT NULL DEFAULT 0,
                "AnchorType" character varying(20) NOT NULL DEFAULT 'delivery',
                "DeliveryDate" date,
                "AcceptanceDate" date,
                "WarrantyEndDate" date,
                "Status" character varying(30) NOT NULL DEFAULT 'pending_anchor',
                "ImportTicketCode" character varying(50),
                "CreatedAt" timestamp with time zone NOT NULL DEFAULT (NOW()),
                "UpdatedAt" timestamp with time zone NOT NULL DEFAULT (NOW()),
                "DeletedAt" timestamp with time zone
            );

            CREATE UNIQUE INDEX IF NOT EXISTS "IX_purchase_warranties_Code"
                ON warehouse.purchase_warranties ("Code")
                WHERE "DeletedAt" IS NULL;

            CREATE UNIQUE INDEX IF NOT EXISTS "IX_purchase_warranties_PoItemId"
                ON warehouse.purchase_warranties ("PoItemId")
                WHERE "DeletedAt" IS NULL;

            CREATE INDEX IF NOT EXISTS "IX_purchase_warranties_PoCode"
                ON warehouse.purchase_warranties ("PoCode");

            CREATE INDEX IF NOT EXISTS "IX_purchase_warranties_ProjectCode"
                ON warehouse.purchase_warranties ("ProjectCode");

            CREATE INDEX IF NOT EXISTS "IX_purchase_warranties_SupplierId"
                ON warehouse.purchase_warranties ("SupplierId");

            CREATE INDEX IF NOT EXISTS "IX_purchase_warranties_MaterialCode"
                ON warehouse.purchase_warranties ("MaterialCode");

            CREATE INDEX IF NOT EXISTS "IX_purchase_warranties_Status_EndDate"
                ON warehouse.purchase_warranties ("Status", "WarrantyEndDate");

            CREATE TABLE IF NOT EXISTS warehouse.purchase_warranty_claims (
                "Id" uuid PRIMARY KEY,
                "TicketCode" character varying(30) NOT NULL,
                "WarrantyId" uuid NOT NULL,
                "Status" character varying(30) NOT NULL DEFAULT 'draft',
                "Reason" character varying(2000) NOT NULL DEFAULT '',
                "Note" character varying(2000),
                "CreatedById" uuid NOT NULL,
                "CreatedByName" character varying(200) NOT NULL DEFAULT '',
                "SubmittedAt" timestamp with time zone,
                "ClosedAt" timestamp with time zone,
                "CreatedAt" timestamp with time zone NOT NULL DEFAULT (NOW()),
                "UpdatedAt" timestamp with time zone NOT NULL DEFAULT (NOW()),
                "DeletedAt" timestamp with time zone,
                CONSTRAINT "FK_purchase_warranty_claims_warranties_WarrantyId"
                    FOREIGN KEY ("WarrantyId") REFERENCES warehouse.purchase_warranties ("Id") ON DELETE CASCADE
            );

            CREATE UNIQUE INDEX IF NOT EXISTS "IX_purchase_warranty_claims_TicketCode"
                ON warehouse.purchase_warranty_claims ("TicketCode")
                WHERE "DeletedAt" IS NULL;

            CREATE INDEX IF NOT EXISTS "IX_purchase_warranty_claims_WarrantyId"
                ON warehouse.purchase_warranty_claims ("WarrantyId");
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            DROP TABLE IF EXISTS warehouse.purchase_warranty_claims;
            DROP TABLE IF EXISTS warehouse.purchase_warranties;
            ALTER TABLE warehouse.purchase_order_items DROP COLUMN IF EXISTS "WarrantyAnchorType";
            ALTER TABLE warehouse.purchase_order_items DROP COLUMN IF EXISTS "WarrantyMonths";
            """);
    }
}
