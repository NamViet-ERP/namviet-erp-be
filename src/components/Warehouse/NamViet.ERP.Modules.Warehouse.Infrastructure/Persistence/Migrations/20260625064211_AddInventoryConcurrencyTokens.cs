using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NamViet.ERP.Modules.Warehouse.Infrastructure.Persistence.Migrations;

/// <summary>
/// Maps RowVersion to PostgreSQL xmin (system column). No DDL — xmin already exists on every row.
/// </summary>
public partial class AddInventoryConcurrencyTokens : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
    }
}
