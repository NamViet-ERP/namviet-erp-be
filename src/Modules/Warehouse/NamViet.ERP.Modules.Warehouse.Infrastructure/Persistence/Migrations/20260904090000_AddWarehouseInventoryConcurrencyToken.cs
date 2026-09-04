using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NamViet.ERP.Modules.Warehouse.Infrastructure.Persistence.Migrations;

/// <summary>
/// Maps WarehouseInventory.RowVersion to PostgreSQL xmin (system column). No DDL — xmin already exists.
/// Do not apply until deployment window; EF model must match for concurrency tokens.
/// </summary>
public partial class AddWarehouseInventoryConcurrencyToken : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
    }
}
