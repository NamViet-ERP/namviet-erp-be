using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using NamViet.ERP.Modules.Warehouse.Infrastructure.Persistence;

#nullable disable

namespace NamViet.ERP.Modules.Warehouse.Infrastructure.Persistence.Migrations;

[DbContext(typeof(WarehouseDbContext))]
[Migration("20260618120000_MaterialLifecycleAndTxSnapshots")]
public class MaterialLifecycleAndTxSnapshots : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<bool>(
            name: "IsActive",
            schema: "warehouse",
            table: "materials",
            type: "boolean",
            nullable: false,
            defaultValue: true);

        migrationBuilder.AddColumn<string>(
            name: "MaterialNameSnapshot",
            schema: "warehouse",
            table: "warehouse_transaction_items",
            type: "character varying(255)",
            maxLength: 255,
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "UnitSnapshot",
            schema: "warehouse",
            table: "warehouse_transaction_items",
            type: "character varying(50)",
            maxLength: 50,
            nullable: true);

        migrationBuilder.Sql("""
            UPDATE warehouse.warehouse_transaction_items ti
            SET "MaterialNameSnapshot" = m."MaterialName",
                "UnitSnapshot" = m."Unit"
            FROM warehouse.materials m
            WHERE m."MaterialCode" = ti."MaterialCode"
              AND (ti."MaterialNameSnapshot" IS NULL OR ti."UnitSnapshot" IS NULL);
            """);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "IsActive",
            schema: "warehouse",
            table: "materials");

        migrationBuilder.DropColumn(
            name: "MaterialNameSnapshot",
            schema: "warehouse",
            table: "warehouse_transaction_items");

        migrationBuilder.DropColumn(
            name: "UnitSnapshot",
            schema: "warehouse",
            table: "warehouse_transaction_items");
    }
}
