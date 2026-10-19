using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using NamViet.ERP.Modules.Warehouse.Infrastructure.Persistence;

#nullable disable

namespace NamViet.ERP.Modules.Warehouse.Infrastructure.Persistence.Migrations;

[DbContext(typeof(WarehouseDbContext))]
[Migration("20260701100500_DropExpectedImports")]
public class DropExpectedImports : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("DROP TABLE IF EXISTS warehouse.expected_imports;");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "expected_imports",
            schema: "warehouse",
            columns: table => new
            {
                Id = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                ProjectCode = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                MaterialCode = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                Name = table.Column<string>(type: "text", nullable: false),
                Qty = table.Column<decimal>(type: "numeric", nullable: false),
                Surplus = table.Column<decimal>(type: "numeric", nullable: false),
                Unit = table.Column<string>(type: "text", nullable: false),
                ExpectedDate = table.Column<DateOnly>(type: "date", nullable: false),
                CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_expected_imports", x => x.Id);
                table.ForeignKey(
                    name: "FK_expected_imports_materials_MaterialCode",
                    column: x => x.MaterialCode,
                    principalSchema: "warehouse",
                    principalTable: "materials",
                    principalColumn: "MaterialCode",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_expected_imports_projects_ProjectCode",
                    column: x => x.ProjectCode,
                    principalSchema: "warehouse",
                    principalTable: "projects",
                    principalColumn: "ProjectCode",
                    onDelete: ReferentialAction.Restrict);
            });

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
    }
}
