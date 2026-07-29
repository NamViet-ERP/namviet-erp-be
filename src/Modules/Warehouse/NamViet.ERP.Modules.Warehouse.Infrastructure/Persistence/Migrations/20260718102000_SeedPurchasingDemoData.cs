using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using NamViet.ERP.Modules.Warehouse.Infrastructure.Persistence;

#nullable disable

namespace NamViet.ERP.Modules.Warehouse.Infrastructure.Persistence.Migrations;

/// <summary>
/// Historical migration id — kept for __EFMigrationsHistory compatibility.
/// Demo purchasing data is no longer injected on migrate.
/// Opt-in only: <c>dotnet run --project scripts/SeedPurchasingDemoData</c>
/// </summary>
[DbContext(typeof(WarehouseDbContext))]
[Migration("20260718102000_SeedPurchasingDemoData")]
public class SeedPurchasingDemoData : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // No-op: business demo DML must not run via EF migrations (prod/staging risk).
        // Use scripts/SeedPurchasingDemoData when a local/demo DB needs sample KHMH/PO rows.
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        // No-op: do not delete rows that may have been created by the CLI seed or by operators.
    }
}
