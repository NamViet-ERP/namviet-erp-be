using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using NamViet.ERP.Modules.Warehouse.Infrastructure.Persistence;

#nullable disable

namespace NamViet.ERP.Modules.Warehouse.Infrastructure.Persistence.Migrations;

[DbContext(typeof(WarehouseDbContext))]
[Migration("20260619120000_AddTransactionItemSupplier")]
public class AddTransactionItemSupplier : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<int>(
            name: "SupplierId",
            schema: "warehouse",
            table: "warehouse_transaction_items",
            type: "integer",
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "SupplierName",
            schema: "warehouse",
            table: "warehouse_transaction_items",
            type: "character varying(255)",
            maxLength: 255,
            nullable: true);

        migrationBuilder.CreateIndex(
            name: "IX_warehouse_transaction_items_SupplierId",
            schema: "warehouse",
            table: "warehouse_transaction_items",
            column: "SupplierId");

        migrationBuilder.AddForeignKey(
            name: "FK_warehouse_transaction_items_suppliers_SupplierId",
            schema: "warehouse",
            table: "warehouse_transaction_items",
            column: "SupplierId",
            principalSchema: "warehouse",
            principalTable: "suppliers",
            principalColumn: "Id",
            onDelete: ReferentialAction.SetNull);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropForeignKey(
            name: "FK_warehouse_transaction_items_suppliers_SupplierId",
            schema: "warehouse",
            table: "warehouse_transaction_items");

        migrationBuilder.DropIndex(
            name: "IX_warehouse_transaction_items_SupplierId",
            schema: "warehouse",
            table: "warehouse_transaction_items");

        migrationBuilder.DropColumn(
            name: "SupplierId",
            schema: "warehouse",
            table: "warehouse_transaction_items");

        migrationBuilder.DropColumn(
            name: "SupplierName",
            schema: "warehouse",
            table: "warehouse_transaction_items");
    }
}
