using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using NamViet.ERP.Modules.Warehouse.Infrastructure.Persistence;
using NamViet.ERP.Modules.Warehouse.Infrastructure.Persistence.Seeding;

#nullable disable

namespace NamViet.ERP.Modules.Warehouse.Infrastructure.Persistence.Migrations;

[DbContext(typeof(WarehouseDbContext))]
[Migration("20260718102000_SeedPurchasingDemoData")]
public class SeedPurchasingDemoData : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder) =>
        migrationBuilder.Sql(PurchasingDemoSeedSql.BuildUpSql());

    protected override void Down(MigrationBuilder migrationBuilder) =>
        migrationBuilder.Sql(PurchasingDemoSeedSql.BuildDownSql());
}
