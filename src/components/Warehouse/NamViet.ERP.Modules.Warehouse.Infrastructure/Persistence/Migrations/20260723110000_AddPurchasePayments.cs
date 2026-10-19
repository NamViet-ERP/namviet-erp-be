using System;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using NamViet.ERP.Modules.Warehouse.Infrastructure.Persistence;

#nullable disable

namespace NamViet.ERP.Modules.Warehouse.Infrastructure.Persistence.Migrations;

[DbContext(typeof(WarehouseDbContext))]
[Migration("20260723110000_AddPurchasePayments")]
public class AddPurchasePayments : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "purchase_payments",
            schema: "warehouse",
            columns: table => new
            {
                Code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                PoCode = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                SupplierId = table.Column<int>(type: "integer", nullable: true),
                SupplierNameSnapshot = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                InvoiceNo = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                Amount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                DueDate = table.Column<DateOnly>(type: "date", nullable: false),
                Status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                Note = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                CreatedById = table.Column<Guid>(type: "uuid", nullable: true),
                CreatedByName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_purchase_payments", x => x.Code);
                table.ForeignKey(
                    name: "FK_purchase_payments_purchase_orders_PoCode",
                    column: x => x.PoCode,
                    principalSchema: "warehouse",
                    principalTable: "purchase_orders",
                    principalColumn: "Code",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_purchase_payments_suppliers_SupplierId",
                    column: x => x.SupplierId,
                    principalSchema: "warehouse",
                    principalTable: "suppliers",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.SetNull);
            });

        migrationBuilder.CreateIndex(
            name: "IX_purchase_payments_PoCode",
            schema: "warehouse",
            table: "purchase_payments",
            column: "PoCode",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_purchase_payments_Status",
            schema: "warehouse",
            table: "purchase_payments",
            column: "Status");

        migrationBuilder.CreateIndex(
            name: "IX_purchase_payments_DueDate",
            schema: "warehouse",
            table: "purchase_payments",
            column: "DueDate");

        migrationBuilder.CreateIndex(
            name: "IX_purchase_payments_SupplierId",
            schema: "warehouse",
            table: "purchase_payments",
            column: "SupplierId");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "purchase_payments",
            schema: "warehouse");
    }
}
