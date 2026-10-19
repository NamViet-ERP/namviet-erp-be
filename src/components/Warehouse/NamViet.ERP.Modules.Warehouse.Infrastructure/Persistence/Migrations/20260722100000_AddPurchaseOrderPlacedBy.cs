using System;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using NamViet.ERP.Modules.Warehouse.Infrastructure.Persistence;

#nullable disable

namespace NamViet.ERP.Modules.Warehouse.Infrastructure.Persistence.Migrations;

[DbContext(typeof(WarehouseDbContext))]
[Migration("20260722100000_AddPurchaseOrderPlacedBy")]
public class AddPurchaseOrderPlacedBy : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<Guid>(
            name: "PlacedById",
            schema: "warehouse",
            table: "purchase_orders",
            type: "uuid",
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "PlacedByName",
            schema: "warehouse",
            table: "purchase_orders",
            type: "character varying(200)",
            maxLength: 200,
            nullable: true);

        migrationBuilder.AddColumn<DateTime>(
            name: "PlacedAt",
            schema: "warehouse",
            table: "purchase_orders",
            type: "timestamp with time zone",
            nullable: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "PlacedById",
            schema: "warehouse",
            table: "purchase_orders");

        migrationBuilder.DropColumn(
            name: "PlacedByName",
            schema: "warehouse",
            table: "purchase_orders");

        migrationBuilder.DropColumn(
            name: "PlacedAt",
            schema: "warehouse",
            table: "purchase_orders");
    }
}
