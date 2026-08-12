using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using NamViet.ERP.Modules.Warehouse.Infrastructure.Persistence;

#nullable disable

namespace NamViet.ERP.Modules.Warehouse.Infrastructure.Persistence.Migrations;

[DbContext(typeof(WarehouseDbContext))]
[Migration("20260811150000_AddPurchaseItemEstimatePrices")]
public class AddPurchaseItemEstimatePrices : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            CREATE TABLE IF NOT EXISTS warehouse.purchase_item_estimate_prices (
                "Id" uuid NOT NULL,
                "MaterialCode" character varying(100) NOT NULL,
                "MaterialName" character varying(255) NOT NULL,
                "ProjectCode" character varying(50),
                "EstimatePrice" numeric(18,2) NOT NULL,
                "EffectiveDate" date NOT NULL,
                "CreatedByName" character varying(200) NOT NULL,
                "Note" character varying(2000),
                "CreatedAt" timestamp with time zone NOT NULL,
                "UpdatedAt" timestamp with time zone NOT NULL,
                "DeletedAt" timestamp with time zone,
                CONSTRAINT "PK_purchase_item_estimate_prices" PRIMARY KEY ("Id")
            );

            CREATE INDEX IF NOT EXISTS "IX_purchase_item_estimate_prices_MaterialCode_ProjectCode_EffectiveDate"
                ON warehouse.purchase_item_estimate_prices ("MaterialCode", "ProjectCode", "EffectiveDate");
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            DROP TABLE IF EXISTS warehouse.purchase_item_estimate_prices;
            """);
    }
}
