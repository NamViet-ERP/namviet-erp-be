namespace NamViet.ERP.Modules.Warehouse.Infrastructure.Persistence.Seeding;

/// <summary>
/// Idempotent demo seed for purchase plans (KHMH), lines, quotes, and POs.
/// Safe to re-run: upserts by stable demo codes (KHMH-DEMO-*, PO-DEMO-*).
/// </summary>
internal static class PurchasingDemoSeedSql
{
    private const string SeedMarkerKey = "purchasing_demo_seed_v1";
    private const string ProjectCode = "DEMO-PUR-PROJ";
    private const string SupplierCodeA = "NCC-DEMO-A";
    private const string SupplierCodeB = "NCC-DEMO-B";
    private const string DemoActorId = "aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee";

    private const string PlanDraft = "KHMH-DEMO-001";
    private const string PlanActive = "KHMH-DEMO-002";
    private const string PlanClosed = "KHMH-DEMO-003";

    public static string BuildUpSql() => $$"""
        CREATE OR REPLACE FUNCTION warehouse.purchasing_demo_seed_upsert_po(
            p_code text,
            p_plan_code text,
            p_line_no integer,
            p_supplier_id integer,
            p_approval text,
            p_placement text,
            p_delivery text,
            p_payment text,
            p_payment_warning boolean,
            p_order_date date,
            p_payment_due date,
            p_received numeric,
            p_approved boolean,
            p_now timestamptz
        ) RETURNS void
        LANGUAGE plpgsql
        AS $fn$
        DECLARE
            v_line_id integer;
            v_project_code text;
            v_product text;
            v_spec text;
            v_unit text;
            v_qty numeric;
            v_price numeric;
            v_total numeric;
            v_supplier_name text;
            v_terms integer;
            v_expected date;
        BEGIN
            SELECT l."Id", pl."ProjectCode", l."ProductName", l."Spec", l."Unit", l."Quantity",
                   COALESCE(l."PurchaseUnitPrice", l."SelectedUnitPrice", l."BudgetUnitPrice", 0),
                   s."SupplierName", s."MaxDebtDays", l."ExpectedDeliveryDate"
            INTO v_line_id, v_project_code, v_product, v_spec, v_unit, v_qty, v_price,
                 v_supplier_name, v_terms, v_expected
            FROM warehouse.purchase_plan_lines l
            JOIN warehouse.purchase_plans pl ON pl."Code" = l."PlanCode"
            JOIN warehouse.suppliers s ON s."Id" = p_supplier_id
            WHERE l."PlanCode" = p_plan_code AND l."LineNo" = p_line_no;

            IF v_line_id IS NULL THEN
                RAISE EXCEPTION 'Missing plan line % line %', p_plan_code, p_line_no;
            END IF;

            v_total := ROUND(v_qty * v_price, 2);
            v_expected := COALESCE(v_expected, p_order_date + 14);

            INSERT INTO warehouse.purchase_orders (
                "Code", "ProjectCode", "PlanCode", "SupplierId", "OrderDate",
                "ApprovalStatus", "OrderPlacementStatus", "DeliveryStatus", "PaymentStatus",
                "PaymentWarning", "Note", "TotalAmount",
                "SupplierPaymentTermsDays", "SupplierNameSnapshot", "PaymentDueDate",
                "ApprovedById", "ApprovedByName", "ApprovedAt",
                "CreatedById", "CreatedByName", "CreatedAt", "UpdatedAt", "DeletedAt"
            )
            VALUES (
                p_code, v_project_code, p_plan_code, p_supplier_id, p_order_date,
                p_approval, p_placement, p_delivery, p_payment,
                p_payment_warning,
                format('Seed PO — %s/%s/%s/%s', p_approval, p_placement, p_delivery, p_payment),
                v_total, v_terms, v_supplier_name, p_payment_due,
                CASE WHEN p_approved THEN '{{DemoActorId}}'::uuid ELSE NULL END,
                CASE WHEN p_approved THEN 'Seed Approver' ELSE NULL END,
                CASE WHEN p_approved THEN p_now - interval '1 day' ELSE NULL END,
                '{{DemoActorId}}'::uuid, 'Seed Purchasing Demo', p_now, p_now, NULL
            )
            ON CONFLICT ("Code") DO UPDATE SET
                "ProjectCode" = EXCLUDED."ProjectCode",
                "PlanCode" = EXCLUDED."PlanCode",
                "SupplierId" = EXCLUDED."SupplierId",
                "OrderDate" = EXCLUDED."OrderDate",
                "ApprovalStatus" = EXCLUDED."ApprovalStatus",
                "OrderPlacementStatus" = EXCLUDED."OrderPlacementStatus",
                "DeliveryStatus" = EXCLUDED."DeliveryStatus",
                "PaymentStatus" = EXCLUDED."PaymentStatus",
                "PaymentWarning" = EXCLUDED."PaymentWarning",
                "Note" = EXCLUDED."Note",
                "TotalAmount" = EXCLUDED."TotalAmount",
                "SupplierPaymentTermsDays" = EXCLUDED."SupplierPaymentTermsDays",
                "SupplierNameSnapshot" = EXCLUDED."SupplierNameSnapshot",
                "PaymentDueDate" = EXCLUDED."PaymentDueDate",
                "ApprovedById" = EXCLUDED."ApprovedById",
                "ApprovedByName" = EXCLUDED."ApprovedByName",
                "ApprovedAt" = EXCLUDED."ApprovedAt",
                "DeletedAt" = NULL,
                "UpdatedAt" = p_now;

            IF EXISTS (
                SELECT 1 FROM warehouse.purchase_order_items
                WHERE "PoCode" = p_code
            ) THEN
                UPDATE warehouse.purchase_order_items
                SET
                    "PlanLineId" = v_line_id,
                    "Sku" = format('DEMO-SKU-%s-%s', p_plan_code, p_line_no),
                    "Name" = v_product,
                    "Spec" = v_spec,
                    "EstPrice" = v_price,
                    "PoPrice" = v_price,
                    "Ordered" = v_qty,
                    "Received" = p_received,
                    "Unit" = v_unit,
                    "Note" = 'Seed item',
                    "ExpectedDeliveryDate" = v_expected
                WHERE "PoCode" = p_code;
            ELSE
                INSERT INTO warehouse.purchase_order_items (
                    "PoCode", "PlanLineId", "MaterialCode", "Sku", "Name", "Spec",
                    "EstPrice", "PoPrice", "Ordered", "Received", "Unit", "Note", "ExpectedDeliveryDate"
                )
                VALUES (
                    p_code, v_line_id, NULL,
                    format('DEMO-SKU-%s-%s', p_plan_code, p_line_no),
                    v_product, v_spec, v_price, v_price, v_qty, p_received, v_unit,
                    'Seed item', v_expected
                );
            END IF;
        END
        $fn$;

        DO $purchasing_demo_seed$
        DECLARE
            v_supplier_a_id integer;
            v_supplier_b_id integer;
            v_line_id integer;
            v_now timestamptz := NOW();
            v_today date := CURRENT_DATE;
        BEGIN
            INSERT INTO warehouse.projects (
                "ProjectCode", "ProjectName", "ProjectType", "PmId", "PmName",
                "StartDate", "MaterialBudget", "Status", "CreatedAt", "UpdatedAt", "DeletedAt"
            )
            SELECT
                '{{ProjectCode}}', 'Dự án demo Purchasing', 'Project',
                '00000000-0000-0000-0000-000000000000'::uuid, 'PM Demo Purchasing',
                v_today - 90, 2000000000, 'Active', v_now, v_now, NULL
            WHERE NOT EXISTS (
                SELECT 1 FROM warehouse.projects WHERE "ProjectCode" = '{{ProjectCode}}'
            );

            UPDATE warehouse.projects
            SET "DeletedAt" = NULL, "Status" = 'Active', "UpdatedAt" = v_now
            WHERE "ProjectCode" = '{{ProjectCode}}';

            INSERT INTO warehouse.suppliers (
                "SupplierCode", "SupplierName", "ContactPhone", "MaxDebtDays", "Rating",
                "CreatedAt", "UpdatedAt", "DeletedAt"
            )
            SELECT '{{SupplierCodeA}}', 'NCC Demo Purchasing A', '0900000000', 30, 'A', v_now, v_now, NULL
            WHERE NOT EXISTS (
                SELECT 1 FROM warehouse.suppliers WHERE "SupplierCode" = '{{SupplierCodeA}}'
            );

            INSERT INTO warehouse.suppliers (
                "SupplierCode", "SupplierName", "ContactPhone", "MaxDebtDays", "Rating",
                "CreatedAt", "UpdatedAt", "DeletedAt"
            )
            SELECT '{{SupplierCodeB}}', 'NCC Demo Purchasing B', '0900000001', 45, 'A', v_now, v_now, NULL
            WHERE NOT EXISTS (
                SELECT 1 FROM warehouse.suppliers WHERE "SupplierCode" = '{{SupplierCodeB}}'
            );

            UPDATE warehouse.suppliers
            SET "DeletedAt" = NULL, "UpdatedAt" = v_now
            WHERE "SupplierCode" IN ('{{SupplierCodeA}}', '{{SupplierCodeB}}');

            SELECT "Id" INTO v_supplier_a_id FROM warehouse.suppliers WHERE "SupplierCode" = '{{SupplierCodeA}}';
            SELECT "Id" INTO v_supplier_b_id FROM warehouse.suppliers WHERE "SupplierCode" = '{{SupplierCodeB}}';

            INSERT INTO warehouse.purchase_plans (
                "Code", "ProjectCode", "Status", "Note", "CreatedById", "CreatedByName",
                "BudgetTotal", "ProposalTotal", "PurchaseTotal", "PaidTotal",
                "CreatedAt", "UpdatedAt", "DeletedAt"
            )
            VALUES
                ('{{PlanDraft}}', '{{ProjectCode}}', 'draft', 'Seed demo — kế hoạch nháp',
                 '{{DemoActorId}}'::uuid, 'Seed Purchasing Demo', 0, 0, 0, 0, v_now, v_now, NULL),
                ('{{PlanActive}}', '{{ProjectCode}}', 'active', 'Seed demo — kế hoạch đang mở (đủ trạng thái)',
                 '{{DemoActorId}}'::uuid, 'Seed Purchasing Demo', 0, 0, 0, 0, v_now, v_now, NULL),
                ('{{PlanClosed}}', '{{ProjectCode}}', 'closed', 'Seed demo — kế hoạch đã đóng',
                 '{{DemoActorId}}'::uuid, 'Seed Purchasing Demo', 0, 0, 0, 0, v_now, v_now, NULL)
            ON CONFLICT ("Code") DO UPDATE SET
                "ProjectCode" = EXCLUDED."ProjectCode",
                "Status" = EXCLUDED."Status",
                "Note" = EXCLUDED."Note",
                "DeletedAt" = NULL,
                "UpdatedAt" = v_now;

            INSERT INTO warehouse.purchase_plan_lines (
                "PlanCode", "LineNo", "SystemName", "Category", "ProductName", "Spec", "Unit",
                "Quantity", "PurchaseStatus", "BudgetUnitPrice", "BudgetAmount",
                "OrderedQty", "ReceivedQty", "DefectQty"
            )
            VALUES
                ('{{PlanDraft}}', 1, 'SEED', 'Seed', 'Thép phi 16', 'Demo seed line', 'Kg',
                 200, 'chưa mua', 18500, 3700000, 0, 0, 0),
                ('{{PlanDraft}}', 2, 'SEED', 'Seed', 'Xi măng PCB40', 'Demo seed line', 'Bao',
                 80, 'chưa mua', 95000, 7600000, 0, 0, 0)
            ON CONFLICT ("PlanCode", "LineNo") DO UPDATE SET
                "ProductName" = EXCLUDED."ProductName",
                "Unit" = EXCLUDED."Unit",
                "Quantity" = EXCLUDED."Quantity",
                "PurchaseStatus" = EXCLUDED."PurchaseStatus",
                "BudgetUnitPrice" = EXCLUDED."BudgetUnitPrice",
                "BudgetAmount" = EXCLUDED."BudgetAmount",
                "OrderedQty" = EXCLUDED."OrderedQty",
                "ReceivedQty" = EXCLUDED."ReceivedQty",
                "DefectQty" = EXCLUDED."DefectQty";

            INSERT INTO warehouse.purchase_plan_lines (
                "PlanCode", "LineNo", "SystemName", "Category", "ProductName", "Spec", "Unit",
                "Quantity", "PurchaseStatus", "BudgetUnitPrice", "BudgetAmount",
                "ProposalUnitPrice", "ProposalAmount", "ProposalSupplierName", "ProposalStatus",
                "PurchaseUnitPrice", "PurchaseAmount", "PurchaseSupplierName",
                "SelectedUnitPrice", "SelectedAmount", "SelectedSupplierName",
                "OrderedQty", "ReceivedQty", "DefectQty",
                "OrderedDate", "ExpectedDeliveryDate", "ActualReceiptDate"
            )
            VALUES
                ('{{PlanActive}}', 1, 'SEED', 'Seed', 'Ống PVC D63', 'Demo seed line', 'M',
                 100, 'chưa mua', 42000, 4200000, NULL, NULL, NULL, NULL,
                 NULL, NULL, NULL, 42000, 4200000, NULL, 0, 0, 0, NULL, NULL, NULL),
                ('{{PlanActive}}', 2, 'SEED', 'Seed', 'Cát vàng', 'Demo seed line', 'Khối',
                 50, 'chưa mua', 280000, 14000000, 275000, 13750000, 'NCC Demo Purchasing A', 'đề xuất',
                 NULL, NULL, NULL, 275000, 13750000, 'NCC Demo Purchasing A', 0, 0, 0, NULL, NULL, NULL),
                ('{{PlanActive}}', 3, 'SEED', 'Seed', 'Sơn nước nội thất', 'Demo seed line', 'Thùng',
                 40, 'đã đặt', 650000, 26000000, NULL, NULL, NULL, NULL,
                 640000, 25600000, 'NCC Demo Purchasing A', 640000, 25600000, 'NCC Demo Purchasing A',
                 40, 0, 0, v_today - 10, v_today + 4, NULL),
                ('{{PlanActive}}', 4, 'SEED', 'Seed', 'Gạch ốp lát 60x60', 'Demo seed line', 'Thùng',
                 120, 'giao 1 phần', 185000, 22200000, NULL, NULL, NULL, NULL,
                 180000, 21600000, 'NCC Demo Purchasing B', 180000, 21600000, 'NCC Demo Purchasing B',
                 120, 50, 2, v_today - 20, v_today - 5, v_today - 3),
                ('{{PlanActive}}', 5, 'SEED', 'Seed', 'Thép hình V50', 'Demo seed line', 'Kg',
                 300, 'đã giao', 22000, 6600000, NULL, NULL, NULL, NULL,
                 21500, 6450000, 'NCC Demo Purchasing A', 21500, 6450000, 'NCC Demo Purchasing A',
                 300, 300, 0, v_today - 40, v_today - 25, v_today - 24),
                ('{{PlanActive}}', 6, 'SEED', 'Seed', 'Dây điện CV 2.5', 'Demo seed line', 'Cuộn',
                 60, 'giao 1 phần', 320000, 19200000, NULL, NULL, NULL, NULL,
                 310000, 18600000, 'NCC Demo Purchasing B', 310000, 18600000, 'NCC Demo Purchasing B',
                 60, 25, 0, v_today - 45, v_today - 30, v_today - 28)
            ON CONFLICT ("PlanCode", "LineNo") DO UPDATE SET
                "ProductName" = EXCLUDED."ProductName",
                "PurchaseStatus" = EXCLUDED."PurchaseStatus",
                "BudgetUnitPrice" = EXCLUDED."BudgetUnitPrice",
                "BudgetAmount" = EXCLUDED."BudgetAmount",
                "ProposalUnitPrice" = EXCLUDED."ProposalUnitPrice",
                "ProposalAmount" = EXCLUDED."ProposalAmount",
                "ProposalSupplierName" = EXCLUDED."ProposalSupplierName",
                "ProposalStatus" = EXCLUDED."ProposalStatus",
                "PurchaseUnitPrice" = EXCLUDED."PurchaseUnitPrice",
                "PurchaseAmount" = EXCLUDED."PurchaseAmount",
                "PurchaseSupplierName" = EXCLUDED."PurchaseSupplierName",
                "SelectedUnitPrice" = EXCLUDED."SelectedUnitPrice",
                "SelectedAmount" = EXCLUDED."SelectedAmount",
                "SelectedSupplierName" = EXCLUDED."SelectedSupplierName",
                "OrderedQty" = EXCLUDED."OrderedQty",
                "ReceivedQty" = EXCLUDED."ReceivedQty",
                "DefectQty" = EXCLUDED."DefectQty",
                "OrderedDate" = EXCLUDED."OrderedDate",
                "ExpectedDeliveryDate" = EXCLUDED."ExpectedDeliveryDate",
                "ActualReceiptDate" = EXCLUDED."ActualReceiptDate";

            INSERT INTO warehouse.purchase_plan_lines (
                "PlanCode", "LineNo", "SystemName", "Category", "ProductName", "Spec", "Unit",
                "Quantity", "PurchaseStatus", "BudgetUnitPrice", "BudgetAmount",
                "PurchaseUnitPrice", "PurchaseAmount", "PurchaseSupplierName",
                "SelectedUnitPrice", "SelectedAmount", "SelectedSupplierName",
                "OrderedQty", "ReceivedQty", "DefectQty",
                "OrderedDate", "ExpectedDeliveryDate", "ActualReceiptDate"
            )
            VALUES
                ('{{PlanClosed}}', 1, 'SEED', 'Seed', 'Bulong M12', 'Demo seed line', 'Hộp',
                 30, 'đã giao', 85000, 2550000, 82000, 2460000, 'NCC Demo Purchasing A',
                 82000, 2460000, 'NCC Demo Purchasing A', 30, 30, 0,
                 v_today - 60, v_today - 50, v_today - 48),
                ('{{PlanClosed}}', 2, 'SEED', 'Seed', 'Keo silicone', 'Demo seed line', 'Tuýp',
                 100, 'đã giao', 25000, 2500000, 24000, 2400000, 'NCC Demo Purchasing B',
                 24000, 2400000, 'NCC Demo Purchasing B', 100, 100, 0,
                 v_today - 55, v_today - 45, v_today - 44)
            ON CONFLICT ("PlanCode", "LineNo") DO UPDATE SET
                "ProductName" = EXCLUDED."ProductName",
                "PurchaseStatus" = EXCLUDED."PurchaseStatus",
                "BudgetUnitPrice" = EXCLUDED."BudgetUnitPrice",
                "BudgetAmount" = EXCLUDED."BudgetAmount",
                "PurchaseUnitPrice" = EXCLUDED."PurchaseUnitPrice",
                "PurchaseAmount" = EXCLUDED."PurchaseAmount",
                "PurchaseSupplierName" = EXCLUDED."PurchaseSupplierName",
                "SelectedUnitPrice" = EXCLUDED."SelectedUnitPrice",
                "SelectedAmount" = EXCLUDED."SelectedAmount",
                "SelectedSupplierName" = EXCLUDED."SelectedSupplierName",
                "OrderedQty" = EXCLUDED."OrderedQty",
                "ReceivedQty" = EXCLUDED."ReceivedQty",
                "OrderedDate" = EXCLUDED."OrderedDate",
                "ExpectedDeliveryDate" = EXCLUDED."ExpectedDeliveryDate",
                "ActualReceiptDate" = EXCLUDED."ActualReceiptDate";

            SELECT "Id" INTO v_line_id
            FROM warehouse.purchase_plan_lines
            WHERE "PlanCode" = '{{PlanActive}}' AND "LineNo" = 2;

            IF v_line_id IS NOT NULL THEN
                INSERT INTO warehouse.purchase_plan_line_quotes (
                    "PlanLineId", "SortOrder", "UnitPrice", "Amount", "SupplierName", "Note", "Status"
                )
                SELECT v_line_id, 1, 278000, 13900000, 'NCC Demo Purchasing B', 'Báo giá thay thế', 'chào giá'
                WHERE NOT EXISTS (
                    SELECT 1 FROM warehouse.purchase_plan_line_quotes
                    WHERE "PlanLineId" = v_line_id AND "SortOrder" = 1
                );

                INSERT INTO warehouse.purchase_plan_line_quotes (
                    "PlanLineId", "SortOrder", "UnitPrice", "Amount", "SupplierName", "Note", "Status"
                )
                SELECT v_line_id, 2, 275000, 13750000, 'NCC Demo Purchasing A', 'Báo giá được chọn', 'được chọn'
                WHERE NOT EXISTS (
                    SELECT 1 FROM warehouse.purchase_plan_line_quotes
                    WHERE "PlanLineId" = v_line_id AND "SortOrder" = 2
                );
            END IF;

            PERFORM warehouse.purchasing_demo_seed_upsert_po(
                'PO-DEMO-001', '{{PlanActive}}', 1, v_supplier_a_id,
                'draft', 'not_placed', 'none', 'unpaid', FALSE,
                v_today - 2, NULL, 0, FALSE, v_now);

            PERFORM warehouse.purchasing_demo_seed_upsert_po(
                'PO-DEMO-002', '{{PlanActive}}', 2, v_supplier_a_id,
                'approved', 'not_placed', 'none', 'unpaid', FALSE,
                v_today - 5, v_today + 25, 0, TRUE, v_now);

            PERFORM warehouse.purchasing_demo_seed_upsert_po(
                'PO-DEMO-003', '{{PlanActive}}', 3, v_supplier_a_id,
                'approved', 'placed', 'none', 'unpaid', FALSE,
                v_today - 10, v_today + 20, 0, TRUE, v_now);

            PERFORM warehouse.purchasing_demo_seed_upsert_po(
                'PO-DEMO-004', '{{PlanActive}}', 4, v_supplier_b_id,
                'approved', 'placed', 'partial', 'partial', FALSE,
                v_today - 20, v_today + 10, 50, TRUE, v_now);

            PERFORM warehouse.purchasing_demo_seed_upsert_po(
                'PO-DEMO-005', '{{PlanActive}}', 5, v_supplier_a_id,
                'approved', 'placed', 'complete', 'paid', FALSE,
                v_today - 40, v_today - 10, 300, TRUE, v_now);

            PERFORM warehouse.purchasing_demo_seed_upsert_po(
                'PO-DEMO-006', '{{PlanActive}}', 6, v_supplier_b_id,
                'approved', 'placed', 'partial', 'overdue', TRUE,
                v_today - 45, v_today - 15, 25, TRUE, v_now);

            UPDATE warehouse.purchase_plans p
            SET
                "BudgetTotal" = COALESCE((
                    SELECT ROUND(SUM(l."BudgetAmount"), 2)
                    FROM warehouse.purchase_plan_lines l
                    WHERE l."PlanCode" = p."Code"
                ), 0),
                "ProposalTotal" = COALESCE((
                    SELECT ROUND(SUM(l."ProposalAmount"), 2)
                    FROM warehouse.purchase_plan_lines l
                    WHERE l."PlanCode" = p."Code"
                ), 0),
                "PurchaseTotal" = COALESCE((
                    SELECT ROUND(SUM(COALESCE(l."SelectedAmount", l."PurchaseAmount", 0)), 2)
                    FROM warehouse.purchase_plan_lines l
                    WHERE l."PlanCode" = p."Code"
                ), 0),
                "PaidTotal" = COALESCE((
                    SELECT ROUND(SUM(
                        CASE o."PaymentStatus"
                            WHEN 'paid' THEN o."TotalAmount"
                            WHEN 'partial' THEN ROUND(o."TotalAmount" / 2, 2)
                            ELSE 0
                        END
                    ), 2)
                    FROM warehouse.purchase_orders o
                    WHERE o."PlanCode" = p."Code" AND o."DeletedAt" IS NULL
                ), 0),
                "UpdatedAt" = v_now
            WHERE p."Code" IN ('{{PlanDraft}}', '{{PlanActive}}', '{{PlanClosed}}');

            INSERT INTO warehouse.wms_settings ("Key", "Value", "CreatedAt", "UpdatedAt")
            SELECT '{{SeedMarkerKey}}', v_now::text, v_now, v_now
            WHERE NOT EXISTS (
                SELECT 1 FROM warehouse.wms_settings WHERE "Key" = '{{SeedMarkerKey}}'
            );

            UPDATE warehouse.wms_settings
            SET "Value" = v_now::text, "UpdatedAt" = v_now
            WHERE "Key" = '{{SeedMarkerKey}}';
        END
        $purchasing_demo_seed$;
        """;

    public static string BuildDownSql() => """
        DELETE FROM warehouse.purchase_order_items
        WHERE "PoCode" LIKE 'PO-DEMO-%';

        DELETE FROM warehouse.purchase_orders
        WHERE "Code" LIKE 'PO-DEMO-%';

        DELETE FROM warehouse.purchase_plan_line_quotes q
        USING warehouse.purchase_plan_lines l
        WHERE q."PlanLineId" = l."Id"
          AND l."PlanCode" LIKE 'KHMH-DEMO-%';

        DELETE FROM warehouse.purchase_plan_lines
        WHERE "PlanCode" LIKE 'KHMH-DEMO-%';

        DELETE FROM warehouse.purchase_plans
        WHERE "Code" LIKE 'KHMH-DEMO-%';

        DELETE FROM warehouse.wms_settings
        WHERE "Key" = 'purchasing_demo_seed_v1';

        DROP FUNCTION IF EXISTS warehouse.purchasing_demo_seed_upsert_po(
            text, text, integer, integer, text, text, text, text, boolean, date, date, numeric, boolean, timestamptz
        );
        """;
}
