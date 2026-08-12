-- Wave E2: Phiếu kho thật gắn DA-2026-NV-TOWER (NK/XK từ chuỗi PO-NV).
-- Idempotent theo TicketCode NK-NV-* / XK-NV-*.
-- Apply: docker cp + psql -f (UTF-8).

BEGIN;

-- ---------------------------------------------------------------------------
-- 0) Kho dự án Tower
-- ---------------------------------------------------------------------------
INSERT INTO warehouse.warehouses (
  "Id", "Code", "Name", "Group", "Address", "Branch", "Manager", "Status",
  "CreatedAt", "UpdatedAt"
)
SELECT
  'DA-2026-NV-TOWER',
  'DA-2026-NV-TOWER',
  'Kho công trình Nam Việt Tower - Q.1',
  'Kho dự án',
  'Quận 1, TP.HCM',
  'TP.HCM',
  'Phạm Minh Châu',
  'active',
  NOW(), NOW()
WHERE NOT EXISTS (
  SELECT 1 FROM warehouse.warehouses w WHERE w."Id" = 'DA-2026-NV-TOWER'
);

UPDATE warehouse.warehouses
SET "Name" = 'Kho công trình Nam Việt Tower - Q.1',
    "Status" = 'active',
    "DeletedAt" = NULL,
    "UpdatedAt" = NOW()
WHERE "Id" = 'DA-2026-NV-TOWER';

-- ---------------------------------------------------------------------------
-- 1) NK từ PO-NV (đã duyệt)
-- ---------------------------------------------------------------------------
INSERT INTO warehouse.warehouse_transactions (
  "Id", "TicketCode", "TransactionType", "ProjectCode", "SupplierId", "SupplierName",
  "ReferenceCode", "PurchasePlanCode", "CreatedById", "CreatedByName",
  "ApprovalStatus", "ApprovedById", "ApprovedByName", "ApprovedAt",
  "WarehouseId", "WarehouseKeeper", "WarehouseApprovalStatus",
  "IsDirectProcurement", "Notes", "CreatedAt", "UpdatedAt"
)
SELECT
  gen_random_uuid(),
  v.ticket,
  'Import',
  'DA-2026-NV-TOWER',
  s."Id",
  s."SupplierName",
  v.po,
  v.plan,
  'aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee'::uuid,
  'Phạm Minh Châu',
  'Approved',
  'aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee'::uuid,
  'Lê Văn Tám',
  NOW() - INTERVAL '5 days',
  'DA-2026-NV-TOWER',
  'Phạm Minh Châu',
  'Approved',
  false,
  v.note,
  NOW() - INTERVAL '6 days',
  NOW()
FROM (VALUES
  ('NK-NV-0001', 'PO-NV-0001', 'KHMH-NV-2026-01', 'NCC-CADIVI',
   'Nhập dây điện CV 2.5 theo PO-NV-0001'),
  ('NK-NV-0002', 'PO-NV-0002', 'KHMH-NV-2026-01', 'NCC-HOAPHAT',
   'Nhập thép Φ16 theo PO-NV-0002'),
  ('NK-NV-0003', 'PO-NV-0003', 'KHMH-NV-2026-01', 'NCC-HATIEN',
   'Nhập xi măng PCB40 theo PO-NV-0003')
) AS v(ticket, po, plan, supplier_code, note)
JOIN warehouse.suppliers s
  ON s."SupplierCode" = v.supplier_code AND s."DeletedAt" IS NULL
WHERE NOT EXISTS (
  SELECT 1 FROM warehouse.warehouse_transactions t WHERE t."TicketCode" = v.ticket
);

UPDATE warehouse.warehouse_transactions
SET "DeletedAt" = NULL,
    "ProjectCode" = 'DA-2026-NV-TOWER',
    "WarehouseId" = 'DA-2026-NV-TOWER',
    "ApprovalStatus" = 'Approved',
    "UpdatedAt" = NOW()
WHERE "TicketCode" IN ('NK-NV-0001', 'NK-NV-0002', 'NK-NV-0003', 'XK-NV-0001');

-- Items
INSERT INTO warehouse.warehouse_transaction_items (
  "TicketCode", "MaterialCode", "Quantity", "UnitPrice",
  "MaterialNameSnapshot", "UnitSnapshot", "SupplierId", "SupplierName"
)
SELECT v.ticket, v.mat, v.qty, v.price, m."MaterialName", m."Unit", s."Id", s."SupplierName"
FROM (VALUES
  ('NK-NV-0001', 'VT-CAP-CV-2.5', 120::numeric, 300000::numeric, 'NCC-CADIVI'),
  ('NK-NV-0002', 'VT-THEP-PHI-16', 8500::numeric, 18000::numeric, 'NCC-HOAPHAT'),
  ('NK-NV-0003', 'VT-XM-PCB40', 600::numeric, 93000::numeric, 'NCC-HATIEN')
) AS v(ticket, mat, qty, price, supplier_code)
JOIN warehouse.materials m ON m."MaterialCode" = v.mat AND m."DeletedAt" IS NULL
JOIN warehouse.suppliers s ON s."SupplierCode" = v.supplier_code AND s."DeletedAt" IS NULL
WHERE EXISTS (
  SELECT 1 FROM warehouse.warehouse_transactions t
  WHERE t."TicketCode" = v.ticket AND t."DeletedAt" IS NULL
)
AND NOT EXISTS (
  SELECT 1 FROM warehouse.warehouse_transaction_items i
  WHERE i."TicketCode" = v.ticket AND i."MaterialCode" = v.mat
);

-- ---------------------------------------------------------------------------
-- 2) XK nội bộ công trình (đã duyệt)
-- ---------------------------------------------------------------------------
INSERT INTO warehouse.warehouse_transactions (
  "Id", "TicketCode", "TransactionType", "ProjectCode",
  "CreatedById", "CreatedByName",
  "ApprovalStatus", "ApprovedById", "ApprovedByName", "ApprovedAt",
  "WarehouseId", "WarehouseKeeper", "WarehouseApprovalStatus",
  "IsDirectProcurement", "Notes", "CreatedAt", "UpdatedAt"
)
SELECT
  gen_random_uuid(),
  'XK-NV-0001',
  'Export',
  'DA-2026-NV-TOWER',
  'aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee'::uuid,
  'Phạm Minh Châu',
  'Approved',
  'aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee'::uuid,
  'Lê Văn Tám',
  NOW() - INTERVAL '2 days',
  'DA-2026-NV-TOWER',
  'Phạm Minh Châu',
  'Approved',
  false,
  'Xuất thép + dây điện thi công tầng hầm',
  NOW() - INTERVAL '3 days',
  NOW()
WHERE NOT EXISTS (
  SELECT 1 FROM warehouse.warehouse_transactions t WHERE t."TicketCode" = 'XK-NV-0001'
);

INSERT INTO warehouse.warehouse_transaction_items (
  "TicketCode", "MaterialCode", "Quantity", "UnitPrice",
  "MaterialNameSnapshot", "UnitSnapshot"
)
SELECT v.ticket, v.mat, v.qty, v.price, m."MaterialName", m."Unit"
FROM (VALUES
  ('XK-NV-0001', 'VT-CAP-CV-2.5', 40::numeric, 300000::numeric),
  ('XK-NV-0001', 'VT-THEP-PHI-16', 2000::numeric, 18000::numeric)
) AS v(ticket, mat, qty, price)
JOIN warehouse.materials m ON m."MaterialCode" = v.mat AND m."DeletedAt" IS NULL
WHERE EXISTS (
  SELECT 1 FROM warehouse.warehouse_transactions t
  WHERE t."TicketCode" = v.ticket AND t."DeletedAt" IS NULL
)
AND NOT EXISTS (
  SELECT 1 FROM warehouse.warehouse_transaction_items i
  WHERE i."TicketCode" = v.ticket AND i."MaterialCode" = v.mat
);

-- ---------------------------------------------------------------------------
-- 3) Tồn kho dự án Tower (đồng bộ từ phiếu)
-- ---------------------------------------------------------------------------
INSERT INTO warehouse.project_inventory (
  "ProjectCode", "MaterialCode",
  "PlannedImportQty", "PlannedExportQty", "ActualImportQty", "ActualExportQty",
  "ReservedQty", "MinStockQty", "CreatedAt", "UpdatedAt"
)
SELECT
  'DA-2026-NV-TOWER', v.mat,
  v.planned_in, 0, v.actual_in, v.actual_out,
  0, 0, NOW(), NOW()
FROM (VALUES
  ('VT-CAP-CV-2.5', 120::numeric, 120::numeric, 40::numeric),
  ('VT-THEP-PHI-16', 8500::numeric, 8500::numeric, 2000::numeric),
  ('VT-XM-PCB40', 600::numeric, 600::numeric, 0::numeric),
  ('VT-ONG-PVC-D63', 0::numeric, 0::numeric, 0::numeric)
) AS v(mat, planned_in, actual_in, actual_out)
WHERE NOT EXISTS (
  SELECT 1 FROM warehouse.project_inventory pi
  WHERE pi."ProjectCode" = 'DA-2026-NV-TOWER' AND pi."MaterialCode" = v.mat
);

UPDATE warehouse.project_inventory pi
SET "ActualImportQty" = v.actual_in,
    "ActualExportQty" = v.actual_out,
    "PlannedImportQty" = v.planned_in,
    "DeletedAt" = NULL,
    "UpdatedAt" = NOW()
FROM (VALUES
  ('VT-CAP-CV-2.5', 120::numeric, 120::numeric, 40::numeric),
  ('VT-THEP-PHI-16', 8500::numeric, 8500::numeric, 2000::numeric),
  ('VT-XM-PCB40', 600::numeric, 600::numeric, 0::numeric)
) AS v(mat, planned_in, actual_in, actual_out)
WHERE pi."ProjectCode" = 'DA-2026-NV-TOWER'
  AND pi."MaterialCode" = v.mat;

INSERT INTO warehouse.warehouse_inventory (
  "WarehouseId", "MaterialCode", "Qty", "CreatedAt", "UpdatedAt"
)
SELECT 'DA-2026-NV-TOWER', v.mat, v.qty, NOW(), NOW()
FROM (VALUES
  ('VT-CAP-CV-2.5', 80::numeric),
  ('VT-THEP-PHI-16', 6500::numeric),
  ('VT-XM-PCB40', 600::numeric)
) AS v(mat, qty)
WHERE NOT EXISTS (
  SELECT 1 FROM warehouse.warehouse_inventory wi
  WHERE wi."WarehouseId" = 'DA-2026-NV-TOWER' AND wi."MaterialCode" = v.mat
);

UPDATE warehouse.warehouse_inventory wi
SET "Qty" = v.qty, "DeletedAt" = NULL, "UpdatedAt" = NOW()
FROM (VALUES
  ('VT-CAP-CV-2.5', 80::numeric),
  ('VT-THEP-PHI-16', 6500::numeric),
  ('VT-XM-PCB40', 600::numeric)
) AS v(mat, qty)
WHERE wi."WarehouseId" = 'DA-2026-NV-TOWER' AND wi."MaterialCode" = v.mat;

COMMIT;
