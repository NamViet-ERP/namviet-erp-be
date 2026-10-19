using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using NamViet.ERP.Modules.Warehouse.Infrastructure.Persistence;

#nullable disable

namespace NamViet.ERP.Modules.Warehouse.Infrastructure.Persistence.Migrations;

[DbContext(typeof(WarehouseDbContext))]
[Migration("20260728140000_ExtendPurchaseDefectsWarranty")]
public class ExtendPurchaseDefectsWarranty : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropForeignKey(
            name: "FK_purchase_defects_purchase_orders_PoCode",
            schema: "warehouse",
            table: "purchase_defects");

        migrationBuilder.AlterColumn<string>(
            name: "PoCode",
            schema: "warehouse",
            table: "purchase_defects",
            type: "character varying(50)",
            maxLength: 50,
            nullable: true,
            oldClrType: typeof(string),
            oldType: "character varying(50)",
            oldMaxLength: 50);

        migrationBuilder.AddColumn<string>(
            name: "SourceType",
            schema: "warehouse",
            table: "purchase_defects",
            type: "character varying(20)",
            maxLength: 20,
            nullable: false,
            defaultValue: "po");

        migrationBuilder.AddColumn<string>(
            name: "CauseSource",
            schema: "warehouse",
            table: "purchase_defects",
            type: "character varying(30)",
            maxLength: 30,
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "SupplierFeedback",
            schema: "warehouse",
            table: "purchase_defects",
            type: "character varying(4000)",
            maxLength: 4000,
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "HandlingMethod",
            schema: "warehouse",
            table: "purchase_defects",
            type: "character varying(40)",
            maxLength: 40,
            nullable: true);

        migrationBuilder.AddColumn<Guid>(
            name: "ProcessedById",
            schema: "warehouse",
            table: "purchase_defects",
            type: "uuid",
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "ProcessedByName",
            schema: "warehouse",
            table: "purchase_defects",
            type: "character varying(255)",
            maxLength: 255,
            nullable: true);

        migrationBuilder.AddColumn<DateTime>(
            name: "ProcessedAt",
            schema: "warehouse",
            table: "purchase_defects",
            type: "timestamp with time zone",
            nullable: true);

        migrationBuilder.CreateIndex(
            name: "IX_purchase_defects_SourceType",
            schema: "warehouse",
            table: "purchase_defects",
            column: "SourceType");

        migrationBuilder.AddForeignKey(
            name: "FK_purchase_defects_purchase_orders_PoCode",
            schema: "warehouse",
            table: "purchase_defects",
            column: "PoCode",
            principalSchema: "warehouse",
            principalTable: "purchase_orders",
            principalColumn: "Code",
            onDelete: ReferentialAction.Restrict);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropForeignKey(
            name: "FK_purchase_defects_purchase_orders_PoCode",
            schema: "warehouse",
            table: "purchase_defects");

        migrationBuilder.DropIndex(
            name: "IX_purchase_defects_SourceType",
            schema: "warehouse",
            table: "purchase_defects");

        migrationBuilder.DropColumn(name: "SourceType", schema: "warehouse", table: "purchase_defects");
        migrationBuilder.DropColumn(name: "CauseSource", schema: "warehouse", table: "purchase_defects");
        migrationBuilder.DropColumn(name: "SupplierFeedback", schema: "warehouse", table: "purchase_defects");
        migrationBuilder.DropColumn(name: "HandlingMethod", schema: "warehouse", table: "purchase_defects");
        migrationBuilder.DropColumn(name: "ProcessedById", schema: "warehouse", table: "purchase_defects");
        migrationBuilder.DropColumn(name: "ProcessedByName", schema: "warehouse", table: "purchase_defects");
        migrationBuilder.DropColumn(name: "ProcessedAt", schema: "warehouse", table: "purchase_defects");

        migrationBuilder.Sql("""
            UPDATE warehouse.purchase_defects SET "PoCode" = '' WHERE "PoCode" IS NULL;
            """);

        migrationBuilder.AlterColumn<string>(
            name: "PoCode",
            schema: "warehouse",
            table: "purchase_defects",
            type: "character varying(50)",
            maxLength: 50,
            nullable: false,
            defaultValue: "",
            oldClrType: typeof(string),
            oldType: "character varying(50)",
            oldMaxLength: 50,
            oldNullable: true);

        migrationBuilder.AddForeignKey(
            name: "FK_purchase_defects_purchase_orders_PoCode",
            schema: "warehouse",
            table: "purchase_defects",
            column: "PoCode",
            principalSchema: "warehouse",
            principalTable: "purchase_orders",
            principalColumn: "Code",
            onDelete: ReferentialAction.Restrict);
    }
}
