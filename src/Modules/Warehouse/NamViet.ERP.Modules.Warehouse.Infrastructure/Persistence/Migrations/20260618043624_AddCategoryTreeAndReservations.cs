using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace NamViet.ERP.Modules.Warehouse.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddCategoryTreeAndReservations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "material_categories",
                schema: "warehouse",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    Level = table.Column<int>(type: "integer", nullable: false),
                    ParentId = table.Column<int>(type: "integer", nullable: true),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_material_categories", x => x.Id);
                    table.ForeignKey(
                        name: "FK_material_categories_material_categories_ParentId",
                        column: x => x.ParentId,
                        principalSchema: "warehouse",
                        principalTable: "material_categories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_material_categories_Code",
                schema: "warehouse",
                table: "material_categories",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_material_categories_ParentId",
                schema: "warehouse",
                table: "material_categories",
                column: "ParentId");

            migrationBuilder.Sql("""
                INSERT INTO warehouse.material_categories ("Code", "Name", "Level", "ParentId", "SortOrder", "IsActive", "CreatedAt", "UpdatedAt")
                VALUES ('UNCATEGORIZED', 'Chưa phân loại', 1, NULL, 0, TRUE, NOW(), NOW());
                """);

            migrationBuilder.AddColumn<int>(
                name: "CategoryId",
                schema: "warehouse",
                table: "materials",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsConsumable",
                schema: "warehouse",
                table: "materials",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.Sql("""
                INSERT INTO warehouse.material_categories ("Code", "Name", "Level", "ParentId", "SortOrder", "IsActive", "CreatedAt", "UpdatedAt")
                SELECT
                    'LEGACY-' || LEFT(REPLACE(md5("MaterialCategory"), '-', ''), 12),
                    "MaterialCategory",
                    2,
                    (SELECT "Id" FROM warehouse.material_categories WHERE "Code" = 'UNCATEGORIZED' LIMIT 1),
                    0,
                    TRUE,
                    NOW(),
                    NOW()
                FROM warehouse.materials
                WHERE "MaterialCategory" IS NOT NULL AND TRIM("MaterialCategory") <> ''
                GROUP BY "MaterialCategory";

                UPDATE warehouse.materials m
                SET "CategoryId" = c."Id"
                FROM warehouse.material_categories c
                WHERE c."Name" = m."MaterialCategory" AND c."Level" = 2;

                UPDATE warehouse.materials
                SET "CategoryId" = (SELECT "Id" FROM warehouse.material_categories WHERE "Code" = 'UNCATEGORIZED' LIMIT 1)
                WHERE "CategoryId" IS NULL;

                UPDATE warehouse.materials
                SET "IsConsumable" = TRUE
                WHERE "MaterialCategory" ILIKE '%tiêu hao%' OR "MaterialCategory" ILIKE '%tieu hao%';
                """);

            migrationBuilder.DropColumn(
                name: "MaterialCategory",
                schema: "warehouse",
                table: "materials");

            migrationBuilder.AlterColumn<int>(
                name: "CategoryId",
                schema: "warehouse",
                table: "materials",
                type: "integer",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "integer",
                oldNullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LinkedTicketCode",
                schema: "warehouse",
                table: "warehouse_transactions",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "ReservedQty",
                schema: "warehouse",
                table: "project_inventory",
                type: "numeric(18,4)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.CreateTable(
                name: "inventory_reservations",
                schema: "warehouse",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    WarehouseId = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    ProjectCode = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    MaterialCode = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    SourceTicketCode = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    ReservedQty = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    ExternalQty = table.Column<decimal>(type: "numeric(18,4)", nullable: false, defaultValue: 0m),
                    FulfilledQty = table.Column<decimal>(type: "numeric(18,4)", nullable: false, defaultValue: 0m),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    FulfilledAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_inventory_reservations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_inventory_reservations_materials_MaterialCode",
                        column: x => x.MaterialCode,
                        principalSchema: "warehouse",
                        principalTable: "materials",
                        principalColumn: "MaterialCode",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_inventory_reservations_projects_ProjectCode",
                        column: x => x.ProjectCode,
                        principalSchema: "warehouse",
                        principalTable: "projects",
                        principalColumn: "ProjectCode",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_inventory_reservations_warehouse_transactions_SourceTicketC~",
                        column: x => x.SourceTicketCode,
                        principalSchema: "warehouse",
                        principalTable: "warehouse_transactions",
                        principalColumn: "TicketCode",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_inventory_reservations_warehouses_WarehouseId",
                        column: x => x.WarehouseId,
                        principalSchema: "warehouse",
                        principalTable: "warehouses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_warehouse_transactions_LinkedTicketCode",
                schema: "warehouse",
                table: "warehouse_transactions",
                column: "LinkedTicketCode");

            migrationBuilder.CreateIndex(
                name: "IX_materials_CategoryId",
                schema: "warehouse",
                table: "materials",
                column: "CategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_inventory_reservations_MaterialCode",
                schema: "warehouse",
                table: "inventory_reservations",
                column: "MaterialCode");

            migrationBuilder.CreateIndex(
                name: "IX_inventory_reservations_ProjectCode_MaterialCode",
                schema: "warehouse",
                table: "inventory_reservations",
                columns: new[] { "ProjectCode", "MaterialCode" });

            migrationBuilder.CreateIndex(
                name: "IX_inventory_reservations_SourceTicketCode",
                schema: "warehouse",
                table: "inventory_reservations",
                column: "SourceTicketCode");

            migrationBuilder.CreateIndex(
                name: "IX_inventory_reservations_WarehouseId_MaterialCode_Status",
                schema: "warehouse",
                table: "inventory_reservations",
                columns: new[] { "WarehouseId", "MaterialCode", "Status" });

            migrationBuilder.AddForeignKey(
                name: "FK_materials_material_categories_CategoryId",
                schema: "warehouse",
                table: "materials",
                column: "CategoryId",
                principalSchema: "warehouse",
                principalTable: "material_categories",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_warehouse_transactions_warehouse_transactions_LinkedTicketC~",
                schema: "warehouse",
                table: "warehouse_transactions",
                column: "LinkedTicketCode",
                principalSchema: "warehouse",
                principalTable: "warehouse_transactions",
                principalColumn: "TicketCode",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.Sql("""
                INSERT INTO warehouse.warehouses ("Id", "Code", "Name", "Group", "Address", "Branch", "Manager", "Status", "CreatedAt", "UpdatedAt")
                SELECT 'wh-general', 'KHO-TONG', 'Kho tổng Nam Việt', 'Kho tổng', 'Kho tổng tập trung', 'HCM', 'Phan Văn Vỹ', 'active', NOW(), NOW()
                WHERE NOT EXISTS (SELECT 1 FROM warehouse.warehouses WHERE "Id" = 'wh-general');

                INSERT INTO warehouse.wms_settings ("Key", "Value", "CreatedAt", "UpdatedAt")
                SELECT 'general_warehouse_id', 'wh-general', NOW(), NOW()
                WHERE NOT EXISTS (SELECT 1 FROM warehouse.wms_settings WHERE "Key" = 'general_warehouse_id');
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_materials_material_categories_CategoryId",
                schema: "warehouse",
                table: "materials");

            migrationBuilder.DropForeignKey(
                name: "FK_warehouse_transactions_warehouse_transactions_LinkedTicketC~",
                schema: "warehouse",
                table: "warehouse_transactions");

            migrationBuilder.DropTable(
                name: "inventory_reservations",
                schema: "warehouse");

            migrationBuilder.DropTable(
                name: "material_categories",
                schema: "warehouse");

            migrationBuilder.DropIndex(
                name: "IX_warehouse_transactions_LinkedTicketCode",
                schema: "warehouse",
                table: "warehouse_transactions");

            migrationBuilder.DropIndex(
                name: "IX_materials_CategoryId",
                schema: "warehouse",
                table: "materials");

            migrationBuilder.DropColumn(
                name: "LinkedTicketCode",
                schema: "warehouse",
                table: "warehouse_transactions");

            migrationBuilder.DropColumn(
                name: "ReservedQty",
                schema: "warehouse",
                table: "project_inventory");

            migrationBuilder.DropColumn(
                name: "CategoryId",
                schema: "warehouse",
                table: "materials");

            migrationBuilder.DropColumn(
                name: "IsConsumable",
                schema: "warehouse",
                table: "materials");

            migrationBuilder.AddColumn<string>(
                name: "MaterialCategory",
                schema: "warehouse",
                table: "materials",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);
        }
    }
}
