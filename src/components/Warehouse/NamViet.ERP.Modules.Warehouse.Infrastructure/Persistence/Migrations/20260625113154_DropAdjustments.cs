using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace NamViet.ERP.Modules.Warehouse.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class DropAdjustments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "adjustment_items",
                schema: "warehouse");

            migrationBuilder.DropTable(
                name: "adjustments",
                schema: "warehouse");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "adjustments",
                schema: "warehouse",
                columns: table => new
                {
                    Id = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    WarehouseId = table.Column<string>(type: "character varying(50)", nullable: false),
                    Condition = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedById = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedByName = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsNew = table.Column<bool>(type: "boolean", nullable: false),
                    Note = table.Column<string>(type: "text", nullable: false),
                    StocktakeCode = table.Column<string>(type: "text", nullable: false),
                    Type = table.Column<string>(type: "text", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    VoucherCode = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    VoucherDate = table.Column<DateOnly>(type: "date", nullable: false),
                    WarehouseName = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_adjustments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_adjustments_warehouses_WarehouseId",
                        column: x => x.WarehouseId,
                        principalSchema: "warehouse",
                        principalTable: "warehouses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "adjustment_items",
                schema: "warehouse",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    AdjustmentId = table.Column<string>(type: "character varying(50)", nullable: false),
                    MaterialCode = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    AdjustQty = table.Column<decimal>(type: "numeric", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    Reason = table.Column<string>(type: "text", nullable: false),
                    Sku = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_adjustment_items", x => x.Id);
                    table.ForeignKey(
                        name: "FK_adjustment_items_adjustments_AdjustmentId",
                        column: x => x.AdjustmentId,
                        principalSchema: "warehouse",
                        principalTable: "adjustments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_adjustment_items_materials_MaterialCode",
                        column: x => x.MaterialCode,
                        principalSchema: "warehouse",
                        principalTable: "materials",
                        principalColumn: "MaterialCode",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_adjustment_items_AdjustmentId",
                schema: "warehouse",
                table: "adjustment_items",
                column: "AdjustmentId");

            migrationBuilder.CreateIndex(
                name: "IX_adjustment_items_MaterialCode",
                schema: "warehouse",
                table: "adjustment_items",
                column: "MaterialCode");

            migrationBuilder.CreateIndex(
                name: "IX_adjustments_VoucherCode",
                schema: "warehouse",
                table: "adjustments",
                column: "VoucherCode",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_adjustments_WarehouseId",
                schema: "warehouse",
                table: "adjustments",
                column: "WarehouseId");
        }
    }
}
