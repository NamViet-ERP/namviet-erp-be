-- Wave C: Chuỗi mua hàng thật (Dự án → KHMH → PO → đợt TT → DNTT → RFQ → BH)
-- Liên kết NCC Wave B. Idempotent theo mã NV-*.
-- Apply: docker cp + psql -f (giữ UTF-8).

BEGIN;

-- ---------------------------------------------------------------------------
-- 0) Sửa tên NCC bị lỗi encoding (Wave B)
-- ---------------------------------------------------------------------------
UPDATE warehouse.suppliers SET "SupplierName" = 'CTCP Dây cáp điện Việt Nam (CADIVI)', "UpdatedAt" = NOW()
WHERE "SupplierCode" = 'NCC-CADIVI';
UPDATE warehouse.suppliers SET "SupplierName" = 'Tập đoàn Hòa Phát', "UpdatedAt" = NOW()
WHERE "SupplierCode" = 'NCC-HOAPHAT';
UPDATE warehouse.suppliers SET "SupplierName" = 'CTCP Xi măng Hà Tiên 1', "UpdatedAt" = NOW()
WHERE "SupplierCode" = 'NCC-HATIEN';
UPDATE warehouse.suppliers SET "SupplierName" = 'CTCP Nhựa Bình Minh', "UpdatedAt" = NOW()
WHERE "SupplierCode" = 'NCC-BINHMINH';
UPDATE warehouse.suppliers SET "SupplierName" = 'Tổng công ty Viglacera', "UpdatedAt" = NOW()
WHERE "SupplierCode" = 'NCC-VIGLACERA';
UPDATE warehouse.suppliers SET "SupplierName" = 'Jotun Việt Nam', "UpdatedAt" = NOW()
WHERE "SupplierCode" = 'NCC-JOTUN';
UPDATE warehouse.suppliers SET "SupplierName" = 'CTCP Gỗ An Cường', "UpdatedAt" = NOW()
WHERE "SupplierCode" = 'NCC-ANCUONG';
UPDATE warehouse.suppliers SET "SupplierName" = 'CTCP Thiết bị điện Lioa', "UpdatedAt" = NOW()
WHERE "SupplierCode" = 'NCC-LIOA';
UPDATE warehouse.suppliers SET "SupplierName" = 'CTCP Thép Đông Nam', "UpdatedAt" = NOW()
WHERE "SupplierCode" = 'NCC-DONGNAM';
UPDATE warehouse.suppliers SET "SupplierName" = 'CTCP Quốc tế Sơn Hà', "UpdatedAt" = NOW()
WHERE "SupplierCode" = 'NCC-SONHA';
UPDATE warehouse.suppliers SET "SupplierName" = 'Công ty TNHH Kim loại Việt Á', "UpdatedAt" = NOW()
WHERE "SupplierCode" = 'NCC-KIMLOAI';
UPDATE warehouse.suppliers SET "SupplierName" = 'Công ty TNHH Ống nhựa Xuân Nghiêm', "UpdatedAt" = NOW()
WHERE "SupplierCode" = 'NCC-ONGNHUA-XN';
UPDATE warehouse.suppliers SET "SupplierName" = 'Công ty TNHH Thép Miền Trung', "UpdatedAt" = NOW()
WHERE "SupplierCode" = 'NCC-THEP-MT';

-- Soft-delete purchasing demo SEED-* (không xóa cứng vì có thể còn FK lịch sử)
UPDATE warehouse.purchase_payments
SET "DeletedAt" = NOW(), "UpdatedAt" = NOW()
WHERE "DeletedAt" IS NULL AND ("PoCode" LIKE 'SEED-PO-%' OR "Code" LIKE 'SEED-DNTT-%');

UPDATE warehouse.purchase_orders
SET "DeletedAt" = NOW(), "UpdatedAt" = NOW()
WHERE "DeletedAt" IS NULL AND "Code" LIKE 'SEED-PO-%';

UPDATE warehouse.purchase_plans
SET "DeletedAt" = NOW(), "UpdatedAt" = NOW()
WHERE "DeletedAt" IS NULL AND "Code" LIKE 'SEED-KHMH-%'
  AND NOT EXISTS (
    SELECT 1 FROM warehouse.purchase_orders o
    WHERE o."PlanCode" = warehouse.purchase_plans."Code"
      AND o."DeletedAt" IS NULL
  );

-- ---------------------------------------------------------------------------
-- 1) Dự án thật
-- ---------------------------------------------------------------------------
INSERT INTO warehouse.projects (
  "ProjectCode", "ProjectName", "ProjectType", "PmId", "PmName",
  "StartDate", "EndDate", "MaterialBudget", "Status", "ClientName",
  "CreatedAt", "UpdatedAt"
)
SELECT
  'DA-2026-NV-TOWER',
  'Cao ốc văn phòng Nam Việt Tower - Q.1',
  'Project',
  '00000000-0000-0000-0000-000000000000'::uuid,
  'Trần Hoàng Nam',
  DATE '2026-01-15',
  DATE '2027-06-30',
  8500000000,
  'Active',
  'Công ty CP Đầu tư Nam Việt',
  NOW(), NOW()
WHERE NOT EXISTS (
  SELECT 1 FROM warehouse.projects p WHERE p."ProjectCode" = 'DA-2026-NV-TOWER'
);

UPDATE warehouse.projects
SET "ProjectName" = 'Cao ốc văn phòng Nam Việt Tower - Q.1',
    "Status" = 'Active',
    "DeletedAt" = NULL,
    "UpdatedAt" = NOW()
WHERE "ProjectCode" = 'DA-2026-NV-TOWER';

-- ---------------------------------------------------------------------------
-- 2) KHMH (nháp + đã duyệt)
-- ---------------------------------------------------------------------------
INSERT INTO warehouse.purchase_plans (
  "Code", "ProjectCode", "Kind", "Status", "Note",
  "CreatedByName", "SubmittedAt",
  "L1ApprovedByName", "L1ApprovedAt", "L2ApprovedByName", "L2ApprovedAt",
  "BudgetTotal", "ProposalTotal", "PurchaseTotal", "PaidTotal",
  "CreatedAt", "UpdatedAt"
)
SELECT * FROM (VALUES
  (
    'KHMH-NV-2026-01', 'DA-2026-NV-TOWER', 'plan', 'approved',
    'Kế hoạch vật tư kết cấu + điện + ống cho tầng hầm và khối đế',
    'Trần Hoàng Nam', NOW() - INTERVAL '20 days',
    'Nguyễn Thị Định', NOW() - INTERVAL '18 days',
    'Lê Văn Tám', NOW() - INTERVAL '17 days',
    520000000::numeric, 510000000::numeric, 495000000::numeric, 0::numeric,
    NOW(), NOW()
  ),
  (
    'KHMH-NV-2026-02', 'DA-2026-NV-TOWER', 'plan', 'draft',
    'Kế hoạch nháp vật tư hoàn thiện nội thất (chưa gửi duyệt)',
    'Đặng Thị Hồng', NULL::timestamptz,
    NULL::text, NULL::timestamptz, NULL::text, NULL::timestamptz,
    180000000::numeric, 0::numeric, 0::numeric, 0::numeric,
    NOW(), NOW()
  ),
  (
    'KHMH-NV-2026-03', 'DA-2026-NV-TOWER', 'adhoc', 'pending_l1',
    'Mua phát sinh thép V50 bổ sung theo biên bản hiện trường',
    'Bùi Anh Tuấn', NOW() - INTERVAL '2 days',
    NULL::text, NULL::timestamptz, NULL::text, NULL::timestamptz,
    45000000::numeric, 44000000::numeric, 0::numeric, 0::numeric,
    NOW(), NOW()
  )
) AS v(
  code, project_code, kind, status, note,
  created_by, submitted_at,
  l1_name, l1_at, l2_name, l2_at,
  budget_total, proposal_total, purchase_total, paid_total,
  created_at, updated_at
)
WHERE NOT EXISTS (
  SELECT 1 FROM warehouse.purchase_plans p WHERE p."Code" = v.code AND p."DeletedAt" IS NULL
);

-- Dòng KHMH đã duyệt (4 NCC)
INSERT INTO warehouse.purchase_plan_lines (
  "PlanCode", "LineNo", "MaterialCode", "ProductName", "Unit", "Quantity",
  "PurchaseStatus", "BudgetUnitPrice", "BudgetAmount",
  "ProposalUnitPrice", "ProposalAmount", "ProposalSupplierName",
  "PurchaseUnitPrice", "PurchaseAmount", "PurchaseSupplierName",
  "SelectedUnitPrice", "SelectedAmount", "SelectedSupplierName",
  "OrderedQty", "ReceivedQty", "RequiredDate"
)
SELECT
  'KHMH-NV-2026-01', v.line_no, v.mat, v.pname, v.unit, v.qty,
  v.pstatus, v.budget_up, v.budget_up * v.qty,
  v.prop_up, v.prop_up * v.qty, s."SupplierName",
  v.buy_up, v.buy_up * v.qty, s."SupplierName",
  v.buy_up, v.buy_up * v.qty, s."SupplierName",
  v.ordered_qty, v.received_qty, CURRENT_DATE + (v.req_offset || ' days')::interval
FROM (VALUES
  (1, 'VT-CAP-CV-2.5', 'Dây điện CV 2.5mm²', 'Cuộn', 120::numeric, 'đã giao',
   310000::numeric, 305000::numeric, 300000::numeric, 120::numeric, 120::numeric, 7, 'NCC-CADIVI'),
  (2, 'VT-THEP-PHI-16', 'Thép xây dựng Φ16', 'Kg', 8500::numeric, 'đã đặt',
   18500::numeric, 18200::numeric, 18000::numeric, 8500::numeric, 0::numeric, 14, 'NCC-HOAPHAT'),
  (3, 'VT-XM-PCB40', 'Xi măng PCB40', 'Bao', 600::numeric, 'giao 1 phần',
   95000::numeric, 94000::numeric, 93000::numeric, 600::numeric, 250::numeric, 10, 'NCC-HATIEN'),
  (4, 'VT-ONG-PVC-D63', 'Ống PVC D63', 'Ống', 400::numeric, 'chưa mua',
   42000::numeric, 41500::numeric, 41000::numeric, 0::numeric, 0::numeric, 21, 'NCC-BINHMINH')
) AS v(line_no, mat, pname, unit, qty, pstatus, budget_up, prop_up, buy_up, ordered_qty, received_qty, req_offset, scode)
JOIN warehouse.suppliers s ON s."SupplierCode" = v.scode AND s."DeletedAt" IS NULL
WHERE NOT EXISTS (
  SELECT 1 FROM warehouse.purchase_plan_lines l
  WHERE l."PlanCode" = 'KHMH-NV-2026-01' AND l."LineNo" = v.line_no
);

-- Dòng KHMH nháp
INSERT INTO warehouse.purchase_plan_lines (
  "PlanCode", "LineNo", "MaterialCode", "ProductName", "Unit", "Quantity",
  "PurchaseStatus", "BudgetUnitPrice", "BudgetAmount", "OrderedQty", "ReceivedQty"
)
SELECT 'KHMH-NV-2026-02', 1, 'VT-THEP-V50', 'Thép hình V50', 'Kg', 500,
  'chưa mua', 22000, 11000000, 0, 0
WHERE NOT EXISTS (
  SELECT 1 FROM warehouse.purchase_plan_lines WHERE "PlanCode" = 'KHMH-NV-2026-02' AND "LineNo" = 1
);

INSERT INTO warehouse.purchase_plan_lines (
  "PlanCode", "LineNo", "MaterialCode", "ProductName", "Unit", "Quantity",
  "PurchaseStatus", "BudgetUnitPrice", "BudgetAmount",
  "ProposalUnitPrice", "ProposalAmount", "ProposalSupplierName",
  "OrderedQty", "ReceivedQty"
)
SELECT 'KHMH-NV-2026-03', 1, 'VT-THEP-V50', 'Thép hình V50', 'Kg', 2000,
  'chưa mua', 22000, 44000000, 21500, 43000000, s."SupplierName", 0, 0
FROM warehouse.suppliers s
WHERE s."SupplierCode" = 'NCC-HOAPHAT' AND s."DeletedAt" IS NULL
  AND NOT EXISTS (
    SELECT 1 FROM warehouse.purchase_plan_lines WHERE "PlanCode" = 'KHMH-NV-2026-03' AND "LineNo" = 1
  );

-- ---------------------------------------------------------------------------
-- 3) PO
-- ---------------------------------------------------------------------------
INSERT INTO warehouse.purchase_orders (
  "Code", "ProjectCode", "PlanCode", "SupplierId", "OrderDate",
  "ApprovalStatus", "OrderPlacementStatus", "DeliveryStatus", "PaymentStatus",
  "Note", "TotalAmount", "SupplierPaymentTermsDays", "SupplierNameSnapshot",
  "PaymentDueDate", "ApprovedByName", "ApprovedAt", "PlacedByName", "PlacedAt",
  "CreatedByName", "CreatedAt", "UpdatedAt"
)
SELECT
  v.code, 'DA-2026-NV-TOWER', 'KHMH-NV-2026-01', s."Id", v.odate::date,
  v.approval, v.placement, v.delivery, v.payment,
  v.note, v.total, s."MaxDebtDays", s."SupplierName",
  v.due::date, v.appr_by, v.appr_at, v.placed_by, v.placed_at,
  'Trần Hoàng Nam', NOW(), NOW()
FROM (VALUES
  ('PO-NV-0001', 'NCC-CADIVI', '2026-07-05', 'approved', 'placed', 'complete', 'partial',
   'PO dây điện CV 2.5 - giao đủ', 36000000::numeric, '2026-08-19',
   'Nguyễn Thị Định', '2026-07-06'::timestamptz, 'Trần Hoàng Nam', '2026-07-07'::timestamptz),
  ('PO-NV-0002', 'NCC-HOAPHAT', '2026-07-10', 'approved', 'placed', 'none', 'unpaid',
   'PO thép Φ16 - đã đặt NCC', 153000000::numeric, '2026-08-09',
   'Nguyễn Thị Định', '2026-07-11'::timestamptz, 'Trần Hoàng Nam', '2026-07-12'::timestamptz),
  ('PO-NV-0003', 'NCC-HATIEN', '2026-07-15', 'approved', 'placed', 'partial', 'unpaid',
   'PO xi măng - giao 1 phần', 55800000::numeric, '2026-08-14',
   'Lê Văn Tám', '2026-07-16'::timestamptz, 'Trần Hoàng Nam', '2026-07-17'::timestamptz),
  ('PO-NV-0004', 'NCC-BINHMINH', '2026-08-01', 'draft', 'not_placed', 'none', 'unpaid',
   'PO ống PVC - nháp chờ duyệt nội bộ', 16400000::numeric, NULL::date,
   NULL::text, NULL::timestamptz, NULL::text, NULL::timestamptz)
) AS v(code, scode, odate, approval, placement, delivery, payment, note, total, due, appr_by, appr_at, placed_by, placed_at)
JOIN warehouse.suppliers s ON s."SupplierCode" = v.scode AND s."DeletedAt" IS NULL
WHERE NOT EXISTS (
  SELECT 1 FROM warehouse.purchase_orders o WHERE o."Code" = v.code AND o."DeletedAt" IS NULL
);

-- PO items
INSERT INTO warehouse.purchase_order_items (
  "PoCode", "PlanLineId", "MaterialCode", "Sku", "Name", "Unit",
  "EstPrice", "PoPrice", "Ordered", "Received", "Defected", "WarrantyMonths", "WarrantyAnchorType"
)
SELECT
  v.po, l."Id", v.mat, v.mat, v.pname, v.unit,
  v.est, v.price, v.ordered, v.received, 0, v.warranty, 'delivery'
FROM (VALUES
  ('PO-NV-0001', 1, 'VT-CAP-CV-2.5', 'Dây điện CV 2.5mm²', 'Cuộn', 310000::numeric, 300000::numeric, 120::numeric, 120::numeric, 12),
  ('PO-NV-0002', 2, 'VT-THEP-PHI-16', 'Thép xây dựng Φ16', 'Kg', 18500::numeric, 18000::numeric, 8500::numeric, 0::numeric, 0),
  ('PO-NV-0003', 3, 'VT-XM-PCB40', 'Xi măng PCB40', 'Bao', 95000::numeric, 93000::numeric, 600::numeric, 250::numeric, 0),
  ('PO-NV-0004', 4, 'VT-ONG-PVC-D63', 'Ống PVC D63', 'Ống', 42000::numeric, 41000::numeric, 400::numeric, 0::numeric, 6)
) AS v(po, line_no, mat, pname, unit, est, price, ordered, received, warranty)
JOIN warehouse.purchase_plan_lines l
  ON l."PlanCode" = 'KHMH-NV-2026-01' AND l."LineNo" = v.line_no
WHERE NOT EXISTS (
  SELECT 1 FROM warehouse.purchase_order_items i WHERE i."PoCode" = v.po AND i."MaterialCode" = v.mat
);

-- ---------------------------------------------------------------------------
-- 4) Đợt thanh toán + chứng từ tối thiểu (để tạo DNTT)
-- ---------------------------------------------------------------------------
INSERT INTO warehouse.purchase_order_installments ("PoCode", "StageNumber", "Percent", "Status", "RequiredDocTypes")
SELECT v.po, v.stage, v.pct, v.status, v.docs::jsonb
FROM (VALUES
  ('PO-NV-0001', 1, 50::numeric, 'completed', '["invoice"]'),
  ('PO-NV-0001', 2, 50::numeric, 'open', '["invoice"]'),
  ('PO-NV-0002', 1, 30::numeric, 'open', '["invoice"]'),
  ('PO-NV-0002', 2, 70::numeric, 'locked', '["invoice","bbgh"]'),
  ('PO-NV-0003', 1, 100::numeric, 'open', '["invoice"]')
) AS v(po, stage, pct, status, docs)
WHERE EXISTS (SELECT 1 FROM warehouse.purchase_orders o WHERE o."Code" = v.po AND o."DeletedAt" IS NULL)
  AND NOT EXISTS (
    SELECT 1 FROM warehouse.purchase_order_installments i
    WHERE i."PoCode" = v.po AND i."StageNumber" = v.stage
  );

INSERT INTO warehouse.purchase_order_installment_documents (
  "InstallmentId", "DocType", "FileName", "ContentType", "StoragePath", "UploadedAt", "UploadedByName"
)
SELECT i."Id", 'invoice',
  'HD-' || i."PoCode" || '-D' || i."StageNumber" || '.pdf',
  'application/pdf',
  '/uploads/purchasing/seed/' || i."PoCode" || '-d' || i."StageNumber" || '-invoice.pdf',
  NOW(), 'Nguyễn Thị Định'
FROM warehouse.purchase_order_installments i
WHERE i."PoCode" IN ('PO-NV-0001', 'PO-NV-0002', 'PO-NV-0003')
  AND i."StageNumber" = 1
  AND NOT EXISTS (
    SELECT 1 FROM warehouse.purchase_order_installment_documents d WHERE d."InstallmentId" = i."Id"
  );

-- Hồ sơ mua hàng (PO attachments C/O, C/Q)
INSERT INTO warehouse.purchase_order_attachments (
  "Id", "PoCode", "DocType", "FileName", "StorageKey", "UploadedAt", "UploadedByName"
)
SELECT gen_random_uuid(), v.po, v.dtype, v.fname, v.skey, NOW(), 'System Admin'
FROM (VALUES
  ('PO-NV-0001', 'co', 'CO-CADIVI-PO-NV-0001.pdf', 'purchasing/docs/PO-NV-0001/co.pdf'),
  ('PO-NV-0001', 'cq', 'CQ-CADIVI-PO-NV-0001.pdf', 'purchasing/docs/PO-NV-0001/cq.pdf'),
  ('PO-NV-0002', 'contract', 'HD-HOAPHAT-PO-NV-0002.pdf', 'purchasing/docs/PO-NV-0002/contract.pdf'),
  ('PO-NV-0003', 'bbgh', 'BBGH-HATIEN-PO-NV-0003.pdf', 'purchasing/docs/PO-NV-0003/bbgh.pdf')
) AS v(po, dtype, fname, skey)
WHERE EXISTS (SELECT 1 FROM warehouse.purchase_orders o WHERE o."Code" = v.po AND o."DeletedAt" IS NULL)
  AND NOT EXISTS (
    SELECT 1 FROM warehouse.purchase_order_attachments a
    WHERE a."PoCode" = v.po AND a."DocType" = v.dtype
  );

-- ---------------------------------------------------------------------------
-- 5) DNTT (đủ trạng thái)
-- ---------------------------------------------------------------------------
INSERT INTO warehouse.purchase_payments (
  "Code", "PoCode", "StageNumber", "SupplierId", "SupplierNameSnapshot",
  "InvoiceNo", "Amount", "DueDate", "Status", "Note",
  "CreatedByName", "CreatedAt", "UpdatedAt"
)
SELECT
  v.code, v.po, v.stage, s."Id", s."SupplierName",
  v.invoice, v.amount, v.due::date, v.status, v.note,
  'Nguyễn Thị Định', NOW(), NOW()
FROM (VALUES
  ('DNTT-NV-0001', 'PO-NV-0001', 1, 'NCC-CADIVI', 'HD-2607-881', 18000000::numeric, '2026-08-19', 'paid',
   'Thanh toán đợt 1 PO dây điện'),
  ('DNTT-NV-0002', 'PO-NV-0001', 2, 'NCC-CADIVI', NULL, 18000000::numeric, '2026-09-19', 'submitted',
   'Đề nghị đợt 2 - đã gửi kế toán'),
  ('DNTT-NV-0003', 'PO-NV-0002', 1, 'NCC-HOAPHAT', NULL, 45900000::numeric, '2026-08-09', 'draft',
   'Nháp DNTT đợt 1 thép Hòa Phát'),
  ('DNTT-NV-0004', 'PO-NV-0003', 1, 'NCC-HATIEN', 'HD-HATIEN-992', 55800000::numeric, '2026-08-14', 'cancelled',
   'Hủy vì sai số tiền trên hóa đơn, lập lại sau')
) AS v(code, po, stage, scode, invoice, amount, due, status, note)
JOIN warehouse.suppliers s ON s."SupplierCode" = v.scode AND s."DeletedAt" IS NULL
WHERE NOT EXISTS (
  SELECT 1 FROM warehouse.purchase_payments p WHERE p."Code" = v.code AND p."DeletedAt" IS NULL
);

-- ---------------------------------------------------------------------------
-- 6) RFQ / sổ giá chào thầu (đủ status)
-- ---------------------------------------------------------------------------
INSERT INTO warehouse.purchase_rfq_quotes (
  "Id", "MaterialCode", "MaterialName", "SupplierId", "SupplierName",
  "ProjectCode", "QuoteDate", "UnitPrice", "ValidUntil", "LeadTimeDays",
  "PaymentTerm", "Status", "PoRef", "Note", "CreatedAt", "UpdatedAt"
)
SELECT
  gen_random_uuid(), v.mat, m."MaterialName", s."Id", s."SupplierName",
  'DA-2026-NV-TOWER', v.qdate::date, v.price, v.valid::date, v.lead,
  v.term, v.status, v.po_ref, v.note, NOW(), NOW()
FROM (VALUES
  ('VT-CAP-CV-2.5', 'NCC-CADIVI', '2026-06-20', 300000::numeric, '2026-12-31', 7, 'Công nợ 45 ngày', 'won', 'PO-NV-0001', 'Trúng thầu PO dây điện'),
  ('VT-CAP-CV-2.5', 'NCC-LIOA', '2026-06-18', 312000::numeric, '2026-09-30', 10, 'Công nợ 30 ngày', 'lost', NULL, 'Giá cao hơn CADIVI'),
  ('VT-THEP-PHI-16', 'NCC-HOAPHAT', '2026-06-25', 18000::numeric, '2026-10-31', 14, '30/70', 'won', 'PO-NV-0002', NULL),
  ('VT-THEP-PHI-16', 'NCC-DONGNAM', '2026-06-24', 17950::numeric, '2026-08-15', 21, 'Công nợ 30 ngày', 'expired', NULL, 'Hết hạn giữ giá'),
  ('VT-ONG-PVC-D63', 'NCC-BINHMINH', '2026-07-28', 41000::numeric, '2026-11-30', 5, 'Công nợ 30 ngày', 'active', NULL, 'Còn hiệu lực'),
  ('VT-XM-PCB40', 'NCC-HATIEN', '2026-07-01', 93000::numeric, '2026-12-31', 3, 'Công nợ 30 ngày', 'active', 'PO-NV-0003', NULL),
  ('VT-THEP-V50', 'NCC-HOAPHAT', '2026-08-01', 21500::numeric, '2026-10-01', 10, 'Công nợ 30 ngày', 'active', NULL, 'Chờ KHMH phát sinh'),
  ('VT-CAP-CV-4', 'NCC-CADIVI', '2026-08-05', 420000::numeric, '2026-11-05', 7, 'Công nợ 45 ngày', 'active', NULL, NULL),
  ('VT-ONG-PVC-D90', 'NCC-BINHMINH', '2026-05-01', 58000::numeric, '2026-06-30', 5, 'Công nợ 30 ngày', 'expired', NULL, 'Hết hạn'),
  ('VT-THEP-PHI-12', 'NCC-HOAPHAT', '2026-07-20', 17500::numeric, '2026-12-20', 14, '30/70', 'active', NULL, NULL)
) AS v(mat, scode, qdate, price, valid, lead, term, status, po_ref, note)
JOIN warehouse.suppliers s ON s."SupplierCode" = v.scode AND s."DeletedAt" IS NULL
JOIN warehouse.materials m ON m."MaterialCode" = v.mat AND m."DeletedAt" IS NULL
WHERE NOT EXISTS (
  SELECT 1 FROM warehouse.purchase_rfq_quotes q
  WHERE q."DeletedAt" IS NULL
    AND q."MaterialCode" = v.mat
    AND q."SupplierId" = s."Id"
    AND q."QuoteDate" = v.qdate::date
);

-- ---------------------------------------------------------------------------
-- 7) Bảo hành (từ PO đã nhận hàng)
-- ---------------------------------------------------------------------------
INSERT INTO warehouse.purchase_warranties (
  "Id", "Code", "PoCode", "PoItemId", "ProjectCode", "PlanCode",
  "SupplierId", "SupplierName", "MaterialCode", "MaterialName", "Unit", "Quantity",
  "WarrantyMonths", "AnchorType", "DeliveryDate", "WarrantyEndDate", "Status",
  "CreatedAt", "UpdatedAt"
)
SELECT
  gen_random_uuid(),
  'BH-NV-0001',
  i."PoCode",
  i."Id",
  'DA-2026-NV-TOWER',
  'KHMH-NV-2026-01',
  o."SupplierId",
  o."SupplierNameSnapshot",
  i."MaterialCode",
  i."Name",
  i."Unit",
  i."Received",
  i."WarrantyMonths",
  'delivery',
  DATE '2026-07-20',
  DATE '2027-07-19',
  'active',
  NOW(), NOW()
FROM warehouse.purchase_order_items i
JOIN warehouse.purchase_orders o ON o."Code" = i."PoCode" AND o."DeletedAt" IS NULL
WHERE i."PoCode" = 'PO-NV-0001'
  AND i."WarrantyMonths" > 0
  AND NOT EXISTS (
    SELECT 1 FROM warehouse.purchase_warranties w
    WHERE w."PoItemId" = i."Id" AND w."DeletedAt" IS NULL
  );

INSERT INTO warehouse.purchase_warranties (
  "Id", "Code", "PoCode", "PoItemId", "ProjectCode", "PlanCode",
  "SupplierId", "SupplierName", "MaterialCode", "MaterialName", "Unit", "Quantity",
  "WarrantyMonths", "AnchorType", "DeliveryDate", "WarrantyEndDate", "Status",
  "CreatedAt", "UpdatedAt"
)
SELECT
  gen_random_uuid(),
  'BH-NV-0002',
  i."PoCode",
  i."Id",
  'DA-2026-NV-TOWER',
  'KHMH-NV-2026-01',
  o."SupplierId",
  o."SupplierNameSnapshot",
  i."MaterialCode",
  i."Name",
  i."Unit",
  GREATEST(i."Received", 1),
  6,
  'delivery',
  NULL,
  NULL,
  'pending_anchor',
  NOW(), NOW()
FROM warehouse.purchase_order_items i
JOIN warehouse.purchase_orders o ON o."Code" = i."PoCode" AND o."DeletedAt" IS NULL
WHERE i."PoCode" = 'PO-NV-0004'
  AND NOT EXISTS (
    SELECT 1 FROM warehouse.purchase_warranties w
    WHERE w."Code" = 'BH-NV-0002' AND w."DeletedAt" IS NULL
  );

COMMIT;

INSERT INTO warehouse.wms_settings ("Key", "Value", "CreatedAt", "UpdatedAt")
SELECT 'purchasing_chain_seed_v1', 'wave-c-real-2026-08', NOW(), NOW()
WHERE NOT EXISTS (SELECT 1 FROM warehouse.wms_settings WHERE "Key" = 'purchasing_chain_seed_v1');

UPDATE warehouse.wms_settings
SET "Value" = 'wave-c-real-2026-08', "UpdatedAt" = NOW()
WHERE "Key" = 'purchasing_chain_seed_v1';
