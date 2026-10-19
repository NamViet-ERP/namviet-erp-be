using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using NamViet.ERP.Modules.Warehouse.Infrastructure.Persistence;

#nullable disable

namespace NamViet.ERP.Modules.Warehouse.Infrastructure.Persistence.Migrations;

[DbContext(typeof(WarehouseDbContext))]
[Migration("20260811130000_AddEstimateShareExpiryAndCeilingBackfill")]
public class AddEstimateShareExpiryAndCeilingBackfill : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            ALTER TABLE warehouse.purchase_estimates
                ADD COLUMN IF NOT EXISTS "ShareTokenExpiresAt" timestamp with time zone;

            UPDATE warehouse.purchase_estimates
            SET "ShareTokenExpiresAt" = NOW() + INTERVAL '7 days'
            WHERE "ShareToken" IS NOT NULL
              AND "ShareTokenExpiresAt" IS NULL
              AND "DeletedAt" IS NULL;

            UPDATE warehouse.materials AS m
            SET
                "CeilingPrice" = src.price,
                "UpdatedAt" = NOW()
            FROM (
                SELECT "MaterialCode", MAX(price) AS price
                FROM (
                    SELECT l."MaterialCode", l."BudgetUnitPrice" AS price
                    FROM warehouse.purchase_plan_lines l
                    JOIN warehouse.purchase_plans p ON p."Code" = l."PlanCode"
                    WHERE l."MaterialCode" IS NOT NULL
                      AND l."BudgetUnitPrice" > 0
                      AND p."DeletedAt" IS NULL
                      AND p."Status" IN ('approved', 'closed', 'active')

                    UNION ALL
                    SELECT l."MaterialCode", l."PurchaseUnitPrice"
                    FROM warehouse.purchase_plan_lines l
                    JOIN warehouse.purchase_plans p ON p."Code" = l."PlanCode"
                    WHERE l."MaterialCode" IS NOT NULL
                      AND l."PurchaseUnitPrice" > 0
                      AND p."DeletedAt" IS NULL
                      AND p."Status" IN ('approved', 'closed', 'active')

                    UNION ALL
                    SELECT i."MaterialCode", i."PoPrice"
                    FROM warehouse.purchase_order_items i
                    JOIN warehouse.purchase_orders o ON o."Code" = i."PoCode"
                    WHERE i."MaterialCode" IS NOT NULL
                      AND i."PoPrice" > 0
                      AND o."DeletedAt" IS NULL
                      AND o."ApprovalStatus" = 'approved'

                    UNION ALL
                    SELECT ti."MaterialCode", ti."UnitPrice"
                    FROM warehouse.warehouse_transaction_items ti
                    JOIN warehouse.warehouse_transactions tx ON tx."TicketCode" = ti."TicketCode"
                    WHERE ti."MaterialCode" IS NOT NULL
                      AND ti."UnitPrice" > 0
                      AND tx."DeletedAt" IS NULL
                      AND tx."TransactionType" = 'Import'
                      AND tx."ApprovalStatus" = 'Approved'
                ) prices
                GROUP BY "MaterialCode"
            ) src
            WHERE m."MaterialCode" = src."MaterialCode"
              AND (m."CeilingPrice" IS NULL OR m."CeilingPrice" <= 0)
              AND m."DeletedAt" IS NULL;
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            ALTER TABLE warehouse.purchase_estimates
                DROP COLUMN IF EXISTS "ShareTokenExpiresAt";
            """);
    }
}
