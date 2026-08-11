using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using NamViet.ERP.Modules.Warehouse.Infrastructure.Persistence;

#nullable disable

namespace NamViet.ERP.Modules.Warehouse.Infrastructure.Persistence.Migrations;

[DbContext(typeof(WarehouseDbContext))]
[Migration("20260810120000_AddPurchaseContracts")]
public class AddPurchaseContracts : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            CREATE TABLE IF NOT EXISTS warehouse.purchase_contracts (
                "Id" uuid PRIMARY KEY,
                "Code" character varying(30) NOT NULL,
                "ContractNo" character varying(100) NOT NULL,
                "Title" character varying(500) NOT NULL,
                "PoCode" character varying(50),
                "ProjectCode" character varying(50),
                "SupplierId" integer,
                "SupplierName" character varying(255),
                "Status" character varying(30) NOT NULL DEFAULT 'draft',
                "SignedDate" date,
                "EffectiveFrom" date,
                "EffectiveTo" date,
                "Amount" numeric(18,2),
                "Note" character varying(2000),
                "FileName" character varying(255),
                "StorageKey" character varying(500),
                "UploadedAt" timestamp with time zone,
                "UploadedByName" character varying(200),
                "CreatedAt" timestamp with time zone NOT NULL DEFAULT (NOW()),
                "UpdatedAt" timestamp with time zone NOT NULL DEFAULT (NOW()),
                "DeletedAt" timestamp with time zone
            );

            CREATE UNIQUE INDEX IF NOT EXISTS "IX_purchase_contracts_Code"
                ON warehouse.purchase_contracts ("Code")
                WHERE "DeletedAt" IS NULL;

            CREATE INDEX IF NOT EXISTS "IX_purchase_contracts_ProjectCode"
                ON warehouse.purchase_contracts ("ProjectCode");

            CREATE INDEX IF NOT EXISTS "IX_purchase_contracts_SupplierId"
                ON warehouse.purchase_contracts ("SupplierId");

            CREATE INDEX IF NOT EXISTS "IX_purchase_contracts_PoCode"
                ON warehouse.purchase_contracts ("PoCode");

            CREATE INDEX IF NOT EXISTS "IX_purchase_contracts_Status"
                ON warehouse.purchase_contracts ("Status");
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            DROP TABLE IF EXISTS warehouse.purchase_contracts;
            """);
    }
}
