using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using NamViet.ERP.Modules.Warehouse.Infrastructure.Persistence;

#nullable disable

namespace NamViet.ERP.Modules.Warehouse.Infrastructure.Persistence.Migrations;

[DbContext(typeof(WarehouseDbContext))]
[Migration("20260918120000_RepairPurchaseOrderPaymentStatuses")]
public class RepairPurchaseOrderPaymentStatuses : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            -- Cập nhật các PO đã trả đủ tiền hoặc đủ các đợt thanh toán thành 'paid'
            UPDATE warehouse.purchase_orders po
            SET "PaymentStatus" = 'paid',
                "UpdatedAt" = NOW() AT TIME ZONE 'UTC'
            WHERE po."PaymentStatus" != 'paid'
              AND (
                -- TH 1: Đủ các đợt theo installments
                (
                  (
                    SELECT COUNT(DISTINCT pp."StageNumber")
                    FROM warehouse.purchase_payments pp
                    WHERE pp."PoCode" = po."Code"
                      AND pp."Status" = 'paid'
                  ) >= (
                    SELECT COUNT(*)
                    FROM warehouse.purchase_order_installments poi
                    WHERE poi."PoCode" = po."Code"
                  )
                  AND (
                    SELECT COUNT(*)
                    FROM warehouse.purchase_order_installments poi
                    WHERE poi."PoCode" = po."Code"
                  ) > 0
                )
                OR
                -- TH 2: Tổng tiền đã thanh toán >= tổng tiền PO
                (
                  (
                    SELECT COALESCE(SUM(pp."Amount"), 0)
                    FROM warehouse.purchase_payments pp
                    WHERE pp."PoCode" = po."Code"
                      AND pp."Status" = 'paid'
                  ) >= (po."TotalAmount" - 0.01)
                  AND po."TotalAmount" > 0
                )
              );

            -- Cập nhật các PO đã trả ít nhất 1 phần nhưng chưa đủ thành 'partial' nếu đang bị kẹt ở 'unpaid'
            UPDATE warehouse.purchase_orders po
            SET "PaymentStatus" = 'partial',
                "UpdatedAt" = NOW() AT TIME ZONE 'UTC'
            WHERE po."PaymentStatus" = 'unpaid'
              AND (
                (
                  SELECT COUNT(DISTINCT pp."StageNumber")
                  FROM warehouse.purchase_payments pp
                  WHERE pp."PoCode" = po."Code"
                    AND pp."Status" = 'paid'
                ) > 0
                OR
                (
                  SELECT COALESCE(SUM(pp."Amount"), 0)
                  FROM warehouse.purchase_payments pp
                  WHERE pp."PoCode" = po."Code"
                    AND pp."Status" = 'paid'
                ) > 0
              );
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        // Data repair — không rollback trạng thái dữ liệu cũ bị sai.
    }
}
