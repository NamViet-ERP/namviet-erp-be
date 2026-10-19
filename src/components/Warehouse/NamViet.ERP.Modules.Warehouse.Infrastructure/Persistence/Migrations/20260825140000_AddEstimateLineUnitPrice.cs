using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using NamViet.ERP.Modules.Warehouse.Infrastructure.Persistence;

#nullable disable

namespace NamViet.ERP.Modules.Warehouse.Infrastructure.Persistence.Migrations;

[DbContext(typeof(WarehouseDbContext))]
[Migration("20260825140000_AddEstimateLineUnitPrice")]
public class AddEstimateLineUnitPrice : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            ALTER TABLE warehouse.purchase_estimate_lines
            ADD COLUMN IF NOT EXISTS "EstimateUnitPrice" numeric(18,2) NOT NULL DEFAULT 0;
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            ALTER TABLE warehouse.purchase_estimate_lines
            DROP COLUMN IF EXISTS "EstimateUnitPrice";
            """);
    }
}
