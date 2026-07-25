using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using NamViet.ERP.Modules.Warehouse.Infrastructure.Persistence;

#nullable disable

namespace NamViet.ERP.Modules.Warehouse.Infrastructure.Persistence.Migrations;

[DbContext(typeof(WarehouseDbContext))]
[Migration("20260720130000_DropPriceBooks")]
public class DropPriceBooks : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "price_book",
            schema: "warehouse");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "price_book",
            schema: "warehouse",
            columns: table => new
            {
                Id = table.Column<int>(type: "integer", nullable: false)
                    .Annotation("Npgsql:ValueGenerationStrategy", Npgsql.EntityFrameworkCore.PostgreSQL.Metadata.NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                MaterialCode = table.Column<string>(type: "character varying(100)", nullable: false),
                SupplierId = table.Column<int>(type: "integer", nullable: false),
                CatalogUrl = table.Column<string>(type: "text", nullable: true),
                CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                PriceDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                PurchaseUrl = table.Column<string>(type: "text", nullable: true),
                UnitPrice = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_price_book", x => x.Id);
                table.ForeignKey(
                    name: "FK_price_book_materials_MaterialCode",
                    column: x => x.MaterialCode,
                    principalSchema: "warehouse",
                    principalTable: "materials",
                    principalColumn: "MaterialCode",
                    onDelete: ReferentialAction.Cascade);
                table.ForeignKey(
                    name: "FK_price_book_suppliers_SupplierId",
                    column: x => x.SupplierId,
                    principalSchema: "warehouse",
                    principalTable: "suppliers",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "IX_price_book_MaterialCode",
            schema: "warehouse",
            table: "price_book",
            column: "MaterialCode");

        migrationBuilder.CreateIndex(
            name: "IX_price_book_SupplierId",
            schema: "warehouse",
            table: "price_book",
            column: "SupplierId");
    }
}
