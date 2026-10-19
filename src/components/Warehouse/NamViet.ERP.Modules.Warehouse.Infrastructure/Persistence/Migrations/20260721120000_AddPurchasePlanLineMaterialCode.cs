using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using NamViet.ERP.Modules.Warehouse.Infrastructure.Persistence;

#nullable disable

namespace NamViet.ERP.Modules.Warehouse.Infrastructure.Persistence.Migrations;

[DbContext(typeof(WarehouseDbContext))]
[Migration("20260721120000_AddPurchasePlanLineMaterialCode")]
public class AddPurchasePlanLineMaterialCode : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "MaterialCode",
            schema: "warehouse",
            table: "purchase_plan_lines",
            type: "character varying(100)",
            maxLength: 100,
            nullable: true);

        migrationBuilder.CreateIndex(
            name: "IX_purchase_plan_lines_MaterialCode",
            schema: "warehouse",
            table: "purchase_plan_lines",
            column: "MaterialCode");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "IX_purchase_plan_lines_MaterialCode",
            schema: "warehouse",
            table: "purchase_plan_lines");

        migrationBuilder.DropColumn(
            name: "MaterialCode",
            schema: "warehouse",
            table: "purchase_plan_lines");
    }
}
