using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using NamViet.ERP.Modules.Warehouse.Infrastructure.Persistence;

#nullable disable

namespace NamViet.ERP.Modules.Warehouse.Infrastructure.Persistence.Migrations;

[DbContext(typeof(WarehouseDbContext))]
[Migration("20260805080000_PurchasePlanApprovalAndKind")]
public class PurchasePlanApprovalAndKind : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "Kind",
            schema: "warehouse",
            table: "purchase_plans",
            type: "character varying(20)",
            maxLength: 20,
            nullable: false,
            defaultValue: "plan");

        migrationBuilder.AddColumn<DateTime>(
            name: "SubmittedAt",
            schema: "warehouse",
            table: "purchase_plans",
            type: "timestamp with time zone",
            nullable: true);

        migrationBuilder.AddColumn<Guid>(
            name: "L1ApprovedById",
            schema: "warehouse",
            table: "purchase_plans",
            type: "uuid",
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "L1ApprovedByName",
            schema: "warehouse",
            table: "purchase_plans",
            type: "character varying(200)",
            maxLength: 200,
            nullable: true);

        migrationBuilder.AddColumn<DateTime>(
            name: "L1ApprovedAt",
            schema: "warehouse",
            table: "purchase_plans",
            type: "timestamp with time zone",
            nullable: true);

        migrationBuilder.AddColumn<Guid>(
            name: "L2ApprovedById",
            schema: "warehouse",
            table: "purchase_plans",
            type: "uuid",
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "L2ApprovedByName",
            schema: "warehouse",
            table: "purchase_plans",
            type: "character varying(200)",
            maxLength: 200,
            nullable: true);

        migrationBuilder.AddColumn<DateTime>(
            name: "L2ApprovedAt",
            schema: "warehouse",
            table: "purchase_plans",
            type: "timestamp with time zone",
            nullable: true);

        migrationBuilder.AddColumn<Guid>(
            name: "RejectedById",
            schema: "warehouse",
            table: "purchase_plans",
            type: "uuid",
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "RejectedByName",
            schema: "warehouse",
            table: "purchase_plans",
            type: "character varying(200)",
            maxLength: 200,
            nullable: true);

        migrationBuilder.AddColumn<DateTime>(
            name: "RejectedAt",
            schema: "warehouse",
            table: "purchase_plans",
            type: "timestamp with time zone",
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "RejectReason",
            schema: "warehouse",
            table: "purchase_plans",
            type: "character varying(2000)",
            maxLength: 2000,
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "RemadeFromCode",
            schema: "warehouse",
            table: "purchase_plans",
            type: "character varying(50)",
            maxLength: 50,
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "RemadeToCode",
            schema: "warehouse",
            table: "purchase_plans",
            type: "character varying(50)",
            maxLength: 50,
            nullable: true);

        migrationBuilder.CreateIndex(
            name: "IX_purchase_plans_Kind",
            schema: "warehouse",
            table: "purchase_plans",
            column: "Kind");

        migrationBuilder.Sql("""
            UPDATE warehouse.purchase_plans
            SET "Status" = 'approved'
            WHERE lower("Status") = 'active';
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            UPDATE warehouse.purchase_plans
            SET "Status" = 'active'
            WHERE lower("Status") = 'approved';
            """);

        migrationBuilder.DropIndex(
            name: "IX_purchase_plans_Kind",
            schema: "warehouse",
            table: "purchase_plans");

        migrationBuilder.DropColumn(name: "RemadeToCode", schema: "warehouse", table: "purchase_plans");
        migrationBuilder.DropColumn(name: "RemadeFromCode", schema: "warehouse", table: "purchase_plans");
        migrationBuilder.DropColumn(name: "RejectReason", schema: "warehouse", table: "purchase_plans");
        migrationBuilder.DropColumn(name: "RejectedAt", schema: "warehouse", table: "purchase_plans");
        migrationBuilder.DropColumn(name: "RejectedByName", schema: "warehouse", table: "purchase_plans");
        migrationBuilder.DropColumn(name: "RejectedById", schema: "warehouse", table: "purchase_plans");
        migrationBuilder.DropColumn(name: "L2ApprovedAt", schema: "warehouse", table: "purchase_plans");
        migrationBuilder.DropColumn(name: "L2ApprovedByName", schema: "warehouse", table: "purchase_plans");
        migrationBuilder.DropColumn(name: "L2ApprovedById", schema: "warehouse", table: "purchase_plans");
        migrationBuilder.DropColumn(name: "L1ApprovedAt", schema: "warehouse", table: "purchase_plans");
        migrationBuilder.DropColumn(name: "L1ApprovedByName", schema: "warehouse", table: "purchase_plans");
        migrationBuilder.DropColumn(name: "L1ApprovedById", schema: "warehouse", table: "purchase_plans");
        migrationBuilder.DropColumn(name: "SubmittedAt", schema: "warehouse", table: "purchase_plans");
        migrationBuilder.DropColumn(name: "Kind", schema: "warehouse", table: "purchase_plans");
    }
}
