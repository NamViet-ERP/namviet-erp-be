using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using NamViet.ERP.Modules.Warehouse.Infrastructure.Persistence;

#nullable disable

namespace NamViet.ERP.Modules.Warehouse.Infrastructure.Persistence.Migrations;

/// <summary>Legacy demo rows stored Export status in WarehouseApprovalStatus.</summary>
[DbContext(typeof(WarehouseDbContext))]
[Migration("20260620120000_FixWarehouseApprovalStatusLegacy")]
public class FixWarehouseApprovalStatusLegacy : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            UPDATE warehouse.warehouse_transactions
            SET "WarehouseApprovalStatus" = 'Approved'
            WHERE "WarehouseApprovalStatus" IS NULL
               OR "WarehouseApprovalStatus" NOT IN ('Pending', 'Approved', 'Rejected');

            UPDATE warehouse.warehouse_transactions
            SET "ApprovalStatus" = 'Approved'
            WHERE "ApprovalStatus" = 'Exported';
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        // Legacy values cannot be restored safely.
    }
}
