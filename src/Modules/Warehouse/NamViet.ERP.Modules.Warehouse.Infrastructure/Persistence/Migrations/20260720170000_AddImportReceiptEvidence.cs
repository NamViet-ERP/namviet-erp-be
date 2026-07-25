using System;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using NamViet.ERP.Modules.Warehouse.Infrastructure.Persistence;

#nullable disable

namespace NamViet.ERP.Modules.Warehouse.Infrastructure.Persistence.Migrations;

[DbContext(typeof(WarehouseDbContext))]
[Migration("20260720170000_AddImportReceiptEvidence")]
public class AddImportReceiptEvidence : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "ReceiptEvaluation",
            schema: "warehouse",
            table: "warehouse_transactions",
            type: "character varying(2000)",
            maxLength: 2000,
            nullable: true);

        migrationBuilder.CreateTable(
            name: "warehouse_transaction_attachments",
            schema: "warehouse",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                TicketCode = table.Column<string>(type: "character varying(50)", nullable: false),
                Kind = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                StorageKey = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                FileName = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                UploadedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_warehouse_transaction_attachments", x => x.Id);
                table.ForeignKey(
                    name: "FK_warehouse_transaction_attachments_warehouse_transactions_TicketCode",
                    column: x => x.TicketCode,
                    principalSchema: "warehouse",
                    principalTable: "warehouse_transactions",
                    principalColumn: "TicketCode",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "IX_warehouse_transaction_attachments_TicketCode",
            schema: "warehouse",
            table: "warehouse_transaction_attachments",
            column: "TicketCode");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "warehouse_transaction_attachments",
            schema: "warehouse");

        migrationBuilder.DropColumn(
            name: "ReceiptEvaluation",
            schema: "warehouse",
            table: "warehouse_transactions");
    }
}
