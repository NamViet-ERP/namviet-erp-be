using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using NamViet.ERP.Modules.Warehouse.Infrastructure.Persistence;

#nullable disable

namespace NamViet.ERP.Modules.Warehouse.Infrastructure.Persistence.Migrations;

[DbContext(typeof(WarehouseDbContext))]
[Migration("20260721080000_AddPurchaseDefects")]
public class AddPurchaseDefects : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<decimal>(
            name: "Defected",
            schema: "warehouse",
            table: "purchase_order_items",
            type: "numeric(18,4)",
            nullable: false,
            defaultValue: 0m);

        migrationBuilder.CreateTable(
            name: "purchase_defects",
            schema: "warehouse",
            columns: table => new
            {
                TicketCode = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                PoCode = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                PlanCode = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                ProjectCode = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                SupplierName = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                Reason = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                Note = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                CreatedById = table.Column<Guid>(type: "uuid", nullable: false),
                CreatedByName = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                ApprovedById = table.Column<Guid>(type: "uuid", nullable: true),
                ApprovedByName = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                ApprovedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_purchase_defects", x => x.TicketCode);
                table.ForeignKey(
                    name: "FK_purchase_defects_purchase_orders_PoCode",
                    column: x => x.PoCode,
                    principalSchema: "warehouse",
                    principalTable: "purchase_orders",
                    principalColumn: "Code",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "purchase_defect_items",
            schema: "warehouse",
            columns: table => new
            {
                Id = table.Column<int>(type: "integer", nullable: false)
                    .Annotation("Npgsql:ValueGenerationStrategy", Npgsql.EntityFrameworkCore.PostgreSQL.Metadata.NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                TicketCode = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                PoItemId = table.Column<int>(type: "integer", nullable: false),
                MaterialCode = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                Name = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                Unit = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                Quantity = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                LineReason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_purchase_defect_items", x => x.Id);
                table.ForeignKey(
                    name: "FK_purchase_defect_items_purchase_defects_TicketCode",
                    column: x => x.TicketCode,
                    principalSchema: "warehouse",
                    principalTable: "purchase_defects",
                    principalColumn: "TicketCode",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "purchase_defect_attachments",
            schema: "warehouse",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                TicketCode = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                Kind = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                StorageKey = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                FileName = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                UploadedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_purchase_defect_attachments", x => x.Id);
                table.ForeignKey(
                    name: "FK_purchase_defect_attachments_purchase_defects_TicketCode",
                    column: x => x.TicketCode,
                    principalSchema: "warehouse",
                    principalTable: "purchase_defects",
                    principalColumn: "TicketCode",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "IX_purchase_defects_Id",
            schema: "warehouse",
            table: "purchase_defects",
            column: "Id",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_purchase_defects_PoCode",
            schema: "warehouse",
            table: "purchase_defects",
            column: "PoCode");

        migrationBuilder.CreateIndex(
            name: "IX_purchase_defects_Status",
            schema: "warehouse",
            table: "purchase_defects",
            column: "Status");

        migrationBuilder.CreateIndex(
            name: "IX_purchase_defect_items_PoItemId",
            schema: "warehouse",
            table: "purchase_defect_items",
            column: "PoItemId");

        migrationBuilder.CreateIndex(
            name: "IX_purchase_defect_items_TicketCode",
            schema: "warehouse",
            table: "purchase_defect_items",
            column: "TicketCode");

        migrationBuilder.CreateIndex(
            name: "IX_purchase_defect_attachments_TicketCode",
            schema: "warehouse",
            table: "purchase_defect_attachments",
            column: "TicketCode");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "purchase_defect_attachments",
            schema: "warehouse");

        migrationBuilder.DropTable(
            name: "purchase_defect_items",
            schema: "warehouse");

        migrationBuilder.DropTable(
            name: "purchase_defects",
            schema: "warehouse");

        migrationBuilder.DropColumn(
            name: "Defected",
            schema: "warehouse",
            table: "purchase_order_items");
    }
}
