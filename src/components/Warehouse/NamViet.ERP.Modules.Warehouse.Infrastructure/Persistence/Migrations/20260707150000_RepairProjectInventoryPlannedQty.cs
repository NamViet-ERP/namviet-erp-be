using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using NamViet.ERP.Modules.Warehouse.Infrastructure.Persistence;

#nullable disable

namespace NamViet.ERP.Modules.Warehouse.Infrastructure.Persistence.Migrations;

[DbContext(typeof(WarehouseDbContext))]
[Migration("20260707150000_RepairProjectInventoryPlannedQty")]
public class RepairProjectInventoryPlannedQty : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            UPDATE warehouse.project_inventory
            SET "PlannedImportQty" = 0,
                "PlannedExportQty" = 0,
                "UpdatedAt" = NOW() AT TIME ZONE 'UTC';

            UPDATE warehouse.project_inventory pi
            SET "PlannedImportQty" = src.qty,
                "UpdatedAt" = NOW() AT TIME ZONE 'UTC'
            FROM (
                SELECT t."BeneficiaryProjectCode" AS project_code,
                       i."MaterialCode" AS material_code,
                       SUM(i."Quantity") AS qty
                FROM warehouse.warehouse_transactions t
                INNER JOIN warehouse.warehouse_transaction_items i
                    ON i."TicketCode" = t."TicketCode"
                WHERE t."TransactionType" = 'Import'
                  AND t."ApprovalStatus" IN ('Draft', 'Pending')
                  AND t."BeneficiaryProjectCode" IS NOT NULL
                  AND t."BeneficiaryProjectCode" <> ''
                  AND COALESCE(t."IsDirectProcurement", FALSE) = FALSE
                GROUP BY t."BeneficiaryProjectCode", i."MaterialCode"
            ) src
            WHERE pi."ProjectCode" = src.project_code
              AND pi."MaterialCode" = src.material_code;

            UPDATE warehouse.project_inventory pi
            SET "PlannedExportQty" = src.qty,
                "UpdatedAt" = NOW() AT TIME ZONE 'UTC'
            FROM (
                SELECT COALESCE(t."ProjectCode", t."BeneficiaryProjectCode") AS project_code,
                       i."MaterialCode" AS material_code,
                       SUM(i."Quantity") AS qty
                FROM warehouse.warehouse_transactions t
                INNER JOIN warehouse.warehouse_transaction_items i
                    ON i."TicketCode" = t."TicketCode"
                WHERE t."TransactionType" IN ('Export', 'Transfer')
                  AND t."ApprovalStatus" IN ('Draft', 'Pending')
                  AND COALESCE(t."ProjectCode", t."BeneficiaryProjectCode") IS NOT NULL
                  AND COALESCE(t."ProjectCode", t."BeneficiaryProjectCode") <> ''
                GROUP BY COALESCE(t."ProjectCode", t."BeneficiaryProjectCode"), i."MaterialCode"
            ) src
            WHERE pi."ProjectCode" = src.project_code
              AND pi."MaterialCode" = src.material_code;
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        // Data repair — không khôi phục giá trị âm cũ.
    }
}
