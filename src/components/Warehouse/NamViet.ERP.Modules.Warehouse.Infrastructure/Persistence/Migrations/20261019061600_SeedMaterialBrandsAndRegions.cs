using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NamViet.ERP.Modules.Warehouse.Infrastructure.Persistence.Migrations
{
    [DbContext(typeof(WarehouseDbContext))]
    [Migration("20261019061600_SeedMaterialBrandsAndRegions")]
    public partial class SeedMaterialBrandsAndRegions : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // No-op: business master-data seeding was removed so fresh databases start purely operational.
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Do not delete master data on rollback
        }
    }
}
