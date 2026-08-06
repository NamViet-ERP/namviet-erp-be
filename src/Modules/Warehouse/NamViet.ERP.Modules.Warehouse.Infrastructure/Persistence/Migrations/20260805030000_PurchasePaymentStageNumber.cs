using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using NamViet.ERP.Modules.Warehouse.Infrastructure.Persistence;

#nullable disable

namespace NamViet.ERP.Modules.Warehouse.Infrastructure.Persistence.Migrations;

[DbContext(typeof(WarehouseDbContext))]
[Migration("20260805030000_PurchasePaymentStageNumber")]
public class PurchasePaymentStageNumber : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "IX_purchase_payments_PoCode",
            schema: "warehouse",
            table: "purchase_payments");

        migrationBuilder.AddColumn<int>(
            name: "StageNumber",
            schema: "warehouse",
            table: "purchase_payments",
            type: "integer",
            nullable: false,
            defaultValue: 1);

        migrationBuilder.CreateIndex(
            name: "IX_purchase_payments_PoCode",
            schema: "warehouse",
            table: "purchase_payments",
            column: "PoCode");

        migrationBuilder.CreateIndex(
            name: "IX_purchase_payments_PoCode_StageNumber",
            schema: "warehouse",
            table: "purchase_payments",
            columns: new[] { "PoCode", "StageNumber" },
            unique: true,
            filter: "\"DeletedAt\" IS NULL AND lower(\"Status\") <> 'cancelled'");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "IX_purchase_payments_PoCode_StageNumber",
            schema: "warehouse",
            table: "purchase_payments");

        migrationBuilder.DropIndex(
            name: "IX_purchase_payments_PoCode",
            schema: "warehouse",
            table: "purchase_payments");

        migrationBuilder.DropColumn(
            name: "StageNumber",
            schema: "warehouse",
            table: "purchase_payments");

        migrationBuilder.CreateIndex(
            name: "IX_purchase_payments_PoCode",
            schema: "warehouse",
            table: "purchase_payments",
            column: "PoCode",
            unique: true);
    }
}
