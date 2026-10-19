using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace NamViet.ERP.Modules.Warehouse.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class SchemaIntegrityFix : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "misa_inventory_snapshots",
                schema: "warehouse");

            migrationBuilder.DropColumn(
                name: "FeStatus",
                schema: "warehouse",
                table: "warehouse_transactions");

            migrationBuilder.Sql("""
                UPDATE warehouse.warehouse_transactions
                SET "WarehouseApprovalStatus" = 'Pending'
                WHERE "WarehouseApprovalStatus" IS NULL OR "WarehouseApprovalStatus" = '';
                """);

            migrationBuilder.Sql("""
                UPDATE warehouse.warehouse_transactions
                SET "WarehouseApprovalStatus" = INITCAP("WarehouseApprovalStatus")
                WHERE "WarehouseApprovalStatus" IS NOT NULL;
                """);

            migrationBuilder.RenameColumn(
                name: "ProductId",
                schema: "warehouse",
                table: "stocktake_items",
                newName: "MaterialCode");

            migrationBuilder.RenameColumn(
                name: "ProductId",
                schema: "warehouse",
                table: "conversion_items",
                newName: "MaterialCode");

            migrationBuilder.RenameColumn(
                name: "ProductId",
                schema: "warehouse",
                table: "adjustment_items",
                newName: "MaterialCode");

            migrationBuilder.AddColumn<DateOnly>(
                name: "OrderDate",
                schema: "warehouse",
                table: "purchase_orders",
                type: "date",
                nullable: false,
                defaultValue: new DateOnly(2026, 1, 1));

            migrationBuilder.AddColumn<string>(
                name: "ProjectCode",
                schema: "warehouse",
                table: "purchase_orders",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "SupplierId",
                schema: "warehouse",
                table: "purchase_orders",
                type: "integer",
                nullable: true);

            migrationBuilder.Sql("""
                UPDATE warehouse.purchase_orders po
                SET "ProjectCode" = po."Project"
                WHERE po."Project" IN (SELECT "ProjectCode" FROM warehouse.projects);
                """);

            migrationBuilder.Sql("""
                UPDATE warehouse.purchase_orders po
                SET "SupplierId" = s."Id"
                FROM warehouse.suppliers s
                WHERE po."Supplier" IN (s."SupplierName", s."SupplierCode");
                """);

            migrationBuilder.DropColumn(
                name: "Date",
                schema: "warehouse",
                table: "purchase_orders");

            migrationBuilder.DropColumn(
                name: "Project",
                schema: "warehouse",
                table: "purchase_orders");

            migrationBuilder.DropColumn(
                name: "Supplier",
                schema: "warehouse",
                table: "purchase_orders");

            migrationBuilder.AddColumn<DateOnly>(
                name: "ExpectedDate",
                schema: "warehouse",
                table: "expected_imports",
                type: "date",
                nullable: false,
                defaultValue: new DateOnly(2026, 1, 1));

            migrationBuilder.AddColumn<string>(
                name: "MaterialCode",
                schema: "warehouse",
                table: "expected_imports",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "ProjectCode",
                schema: "warehouse",
                table: "expected_imports",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "");

            migrationBuilder.Sql("""
                UPDATE warehouse.expected_imports
                SET "MaterialCode" = "Sku", "ProjectCode" = "Project"
                WHERE "MaterialCode" = '' OR "ProjectCode" = '';
                """);

            migrationBuilder.DropColumn(
                name: "Date",
                schema: "warehouse",
                table: "expected_imports");

            migrationBuilder.DropColumn(
                name: "Project",
                schema: "warehouse",
                table: "expected_imports");

            migrationBuilder.DropColumn(
                name: "Sku",
                schema: "warehouse",
                table: "expected_imports");

            migrationBuilder.AlterColumn<string>(
                name: "WarehouseApprovalStatus",
                schema: "warehouse",
                table: "warehouse_transactions",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "Pending",
                oldClrType: typeof(string),
                oldType: "character varying(30)",
                oldMaxLength: 30,
                oldNullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MaterialCode",
                schema: "warehouse",
                table: "purchase_order_items",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "");

            migrationBuilder.Sql("""
                UPDATE warehouse.purchase_order_items SET "MaterialCode" = "Sku" WHERE "MaterialCode" = '';
                """);

            migrationBuilder.CreateIndex(
                name: "IX_stocktake_items_MaterialCode",
                schema: "warehouse",
                table: "stocktake_items",
                column: "MaterialCode");

            migrationBuilder.CreateIndex(
                name: "IX_purchase_orders_ProjectCode",
                schema: "warehouse",
                table: "purchase_orders",
                column: "ProjectCode");

            migrationBuilder.CreateIndex(
                name: "IX_purchase_orders_SupplierId",
                schema: "warehouse",
                table: "purchase_orders",
                column: "SupplierId");

            migrationBuilder.CreateIndex(
                name: "IX_purchase_order_items_MaterialCode",
                schema: "warehouse",
                table: "purchase_order_items",
                column: "MaterialCode");

            migrationBuilder.CreateIndex(
                name: "IX_expected_imports_MaterialCode",
                schema: "warehouse",
                table: "expected_imports",
                column: "MaterialCode");

            migrationBuilder.CreateIndex(
                name: "IX_expected_imports_ProjectCode",
                schema: "warehouse",
                table: "expected_imports",
                column: "ProjectCode");

            migrationBuilder.CreateIndex(
                name: "IX_conversion_items_MaterialCode",
                schema: "warehouse",
                table: "conversion_items",
                column: "MaterialCode");

            migrationBuilder.CreateIndex(
                name: "IX_adjustment_items_MaterialCode",
                schema: "warehouse",
                table: "adjustment_items",
                column: "MaterialCode");

            migrationBuilder.AddForeignKey(
                name: "FK_adjustment_items_materials_MaterialCode",
                schema: "warehouse",
                table: "adjustment_items",
                column: "MaterialCode",
                principalSchema: "warehouse",
                principalTable: "materials",
                principalColumn: "MaterialCode",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_conversion_items_materials_MaterialCode",
                schema: "warehouse",
                table: "conversion_items",
                column: "MaterialCode",
                principalSchema: "warehouse",
                principalTable: "materials",
                principalColumn: "MaterialCode",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_expected_imports_materials_MaterialCode",
                schema: "warehouse",
                table: "expected_imports",
                column: "MaterialCode",
                principalSchema: "warehouse",
                principalTable: "materials",
                principalColumn: "MaterialCode",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_expected_imports_projects_ProjectCode",
                schema: "warehouse",
                table: "expected_imports",
                column: "ProjectCode",
                principalSchema: "warehouse",
                principalTable: "projects",
                principalColumn: "ProjectCode",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_purchase_order_items_materials_MaterialCode",
                schema: "warehouse",
                table: "purchase_order_items",
                column: "MaterialCode",
                principalSchema: "warehouse",
                principalTable: "materials",
                principalColumn: "MaterialCode",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_purchase_orders_projects_ProjectCode",
                schema: "warehouse",
                table: "purchase_orders",
                column: "ProjectCode",
                principalSchema: "warehouse",
                principalTable: "projects",
                principalColumn: "ProjectCode",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_purchase_orders_suppliers_SupplierId",
                schema: "warehouse",
                table: "purchase_orders",
                column: "SupplierId",
                principalSchema: "warehouse",
                principalTable: "suppliers",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_stocktake_items_materials_MaterialCode",
                schema: "warehouse",
                table: "stocktake_items",
                column: "MaterialCode",
                principalSchema: "warehouse",
                principalTable: "materials",
                principalColumn: "MaterialCode",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_adjustment_items_materials_MaterialCode",
                schema: "warehouse",
                table: "adjustment_items");

            migrationBuilder.DropForeignKey(
                name: "FK_conversion_items_materials_MaterialCode",
                schema: "warehouse",
                table: "conversion_items");

            migrationBuilder.DropForeignKey(
                name: "FK_expected_imports_materials_MaterialCode",
                schema: "warehouse",
                table: "expected_imports");

            migrationBuilder.DropForeignKey(
                name: "FK_expected_imports_projects_ProjectCode",
                schema: "warehouse",
                table: "expected_imports");

            migrationBuilder.DropForeignKey(
                name: "FK_purchase_order_items_materials_MaterialCode",
                schema: "warehouse",
                table: "purchase_order_items");

            migrationBuilder.DropForeignKey(
                name: "FK_purchase_orders_projects_ProjectCode",
                schema: "warehouse",
                table: "purchase_orders");

            migrationBuilder.DropForeignKey(
                name: "FK_purchase_orders_suppliers_SupplierId",
                schema: "warehouse",
                table: "purchase_orders");

            migrationBuilder.DropForeignKey(
                name: "FK_stocktake_items_materials_MaterialCode",
                schema: "warehouse",
                table: "stocktake_items");

            migrationBuilder.DropIndex(
                name: "IX_stocktake_items_MaterialCode",
                schema: "warehouse",
                table: "stocktake_items");

            migrationBuilder.DropIndex(
                name: "IX_purchase_orders_ProjectCode",
                schema: "warehouse",
                table: "purchase_orders");

            migrationBuilder.DropIndex(
                name: "IX_purchase_orders_SupplierId",
                schema: "warehouse",
                table: "purchase_orders");

            migrationBuilder.DropIndex(
                name: "IX_purchase_order_items_MaterialCode",
                schema: "warehouse",
                table: "purchase_order_items");

            migrationBuilder.DropIndex(
                name: "IX_expected_imports_MaterialCode",
                schema: "warehouse",
                table: "expected_imports");

            migrationBuilder.DropIndex(
                name: "IX_expected_imports_ProjectCode",
                schema: "warehouse",
                table: "expected_imports");

            migrationBuilder.DropIndex(
                name: "IX_conversion_items_MaterialCode",
                schema: "warehouse",
                table: "conversion_items");

            migrationBuilder.DropIndex(
                name: "IX_adjustment_items_MaterialCode",
                schema: "warehouse",
                table: "adjustment_items");

            migrationBuilder.DropColumn(
                name: "MaterialCode",
                schema: "warehouse",
                table: "stocktake_items");

            migrationBuilder.DropColumn(
                name: "OrderDate",
                schema: "warehouse",
                table: "purchase_orders");

            migrationBuilder.DropColumn(
                name: "ProjectCode",
                schema: "warehouse",
                table: "purchase_orders");

            migrationBuilder.DropColumn(
                name: "SupplierId",
                schema: "warehouse",
                table: "purchase_orders");

            migrationBuilder.DropColumn(
                name: "MaterialCode",
                schema: "warehouse",
                table: "purchase_order_items");

            migrationBuilder.DropColumn(
                name: "ExpectedDate",
                schema: "warehouse",
                table: "expected_imports");

            migrationBuilder.DropColumn(
                name: "MaterialCode",
                schema: "warehouse",
                table: "expected_imports");

            migrationBuilder.DropColumn(
                name: "ProjectCode",
                schema: "warehouse",
                table: "expected_imports");

            migrationBuilder.DropColumn(
                name: "MaterialCode",
                schema: "warehouse",
                table: "conversion_items");

            migrationBuilder.DropColumn(
                name: "MaterialCode",
                schema: "warehouse",
                table: "adjustment_items");

            migrationBuilder.AlterColumn<string>(
                name: "WarehouseApprovalStatus",
                schema: "warehouse",
                table: "warehouse_transactions",
                type: "character varying(30)",
                maxLength: 30,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(20)",
                oldMaxLength: 20,
                oldDefaultValue: "Pending");

            migrationBuilder.AddColumn<string>(
                name: "FeStatus",
                schema: "warehouse",
                table: "warehouse_transactions",
                type: "character varying(30)",
                maxLength: 30,
                nullable: false,
                defaultValue: "pending");

            migrationBuilder.AddColumn<string>(
                name: "ProductId",
                schema: "warehouse",
                table: "stocktake_items",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Date",
                schema: "warehouse",
                table: "purchase_orders",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Project",
                schema: "warehouse",
                table: "purchase_orders",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Supplier",
                schema: "warehouse",
                table: "purchase_orders",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Date",
                schema: "warehouse",
                table: "expected_imports",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Project",
                schema: "warehouse",
                table: "expected_imports",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Sku",
                schema: "warehouse",
                table: "expected_imports",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "ProductId",
                schema: "warehouse",
                table: "conversion_items",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "ProductId",
                schema: "warehouse",
                table: "adjustment_items",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateTable(
                name: "misa_inventory_snapshots",
                schema: "warehouse",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    MisaQty = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    Sku = table.Column<string>(type: "text", nullable: false),
                    SnapshotAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Unit = table.Column<string>(type: "text", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    WmsQty = table.Column<decimal>(type: "numeric(18,4)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_misa_inventory_snapshots", x => x.Id);
                });
        }
    }
}
