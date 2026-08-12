-- Wave D: Soft-delete DEMO/SEED/orphan gây lỗi UI (giữ chuỗi NV-* Wave B/C).
-- Idempotent. Apply: docker cp + psql -f (UTF-8).

BEGIN;

-- ---------------------------------------------------------------------------
-- 1) DNTT gắn PO demo/seed
-- ---------------------------------------------------------------------------
UPDATE warehouse.purchase_payments
SET "DeletedAt" = NOW(), "UpdatedAt" = NOW()
WHERE "DeletedAt" IS NULL
  AND (
    "PoCode" ILIKE 'SEED-PO-%'
    OR "PoCode" ILIKE 'PO-DEMO-%'
    OR "PoCode" IN ('PO-0001')
    OR "Code" ILIKE 'SEED-DNTT-%'
    OR "Code" ILIKE 'DNTT-DEMO-%'
  );

-- ---------------------------------------------------------------------------
-- 2) Bảo hành / RFQ demo
-- ---------------------------------------------------------------------------
UPDATE warehouse.purchase_warranties
SET "DeletedAt" = NOW(), "UpdatedAt" = NOW()
WHERE "DeletedAt" IS NULL
  AND (
    "PoCode" ILIKE 'SEED-PO-%'
    OR "PoCode" ILIKE 'PO-DEMO-%'
    OR "PoCode" = 'PO-0001'
    OR "ProjectCode" ILIKE '%DEMO%'
    OR "Code" ILIKE 'BH-DEMO-%'
    OR "Code" ILIKE 'SEED-BH-%'
  );

UPDATE warehouse.purchase_rfq_quotes
SET "DeletedAt" = NOW(), "UpdatedAt" = NOW()
WHERE "DeletedAt" IS NULL
  AND (
    "MaterialCode" ILIKE 'VT-DEMO%'
    OR "ProjectCode" ILIKE '%DEMO%'
    OR "SupplierName" ILIKE '%DEMO%'
    OR "PoRef" ILIKE 'SEED-PO-%'
    OR "PoRef" ILIKE 'PO-DEMO-%'
  );

-- ---------------------------------------------------------------------------
-- 3) PO demo/seed + orphan (NCC đã soft-delete)
-- ---------------------------------------------------------------------------
UPDATE warehouse.purchase_orders o
SET "DeletedAt" = NOW(), "UpdatedAt" = NOW()
WHERE o."DeletedAt" IS NULL
  AND (
    o."Code" ILIKE 'SEED-PO-%'
    OR o."Code" ILIKE 'PO-DEMO-%'
    OR o."Code" = 'PO-0001'
    OR o."ProjectCode" ILIKE '%DEMO%'
    OR o."PlanCode" ILIKE '%DEMO%'
    OR o."PlanCode" ILIKE 'SEED-KHMH-%'
    OR EXISTS (
      SELECT 1 FROM warehouse.suppliers s
      WHERE s."Id" = o."SupplierId" AND s."DeletedAt" IS NOT NULL
    )
  );

-- ---------------------------------------------------------------------------
-- 4) KHMH demo/seed / gắn dự án demo
-- ---------------------------------------------------------------------------
UPDATE warehouse.purchase_plans
SET "DeletedAt" = NOW(), "UpdatedAt" = NOW()
WHERE "DeletedAt" IS NULL
  AND (
    "Code" ILIKE 'SEED-KHMH-%'
    OR "Code" ILIKE 'KHMH-DEMO-%'
    OR "ProjectCode" ILIKE '%DEMO%'
  );

-- ---------------------------------------------------------------------------
-- 5) Dự án demo
-- ---------------------------------------------------------------------------
UPDATE warehouse.projects
SET "DeletedAt" = NOW(), "UpdatedAt" = NOW()
WHERE "DeletedAt" IS NULL
  AND (
    "ProjectCode" ILIKE '%DEMO%'
    OR "ProjectName" ILIKE '%demo%'
  );

-- ---------------------------------------------------------------------------
-- 6) NCC rác / placeholder còn sống
-- ---------------------------------------------------------------------------
UPDATE warehouse.suppliers
SET "DeletedAt" = NOW(), "UpdatedAt" = NOW()
WHERE "DeletedAt" IS NULL
  AND (
    "SupplierCode" ILIKE 'NCC-DEMO%'
    OR "SupplierCode" IN ('NCC-0001', 'NCC-0002')
    OR "SupplierName" ILIKE 'FULL LOCAL%'
    OR "SupplierName" ILIKE 'PHONE %'
    OR lower("SupplierName") LIKE 'test%'
  );

-- ---------------------------------------------------------------------------
-- 7) Vật tư VT-DEMO-* (không còn PO live tham chiếu sau bước 3)
-- ---------------------------------------------------------------------------
UPDATE warehouse.materials m
SET "DeletedAt" = NOW(), "UpdatedAt" = NOW()
WHERE m."DeletedAt" IS NULL
  AND m."MaterialCode" ILIKE 'VT-DEMO%'
  AND NOT EXISTS (
    SELECT 1
    FROM warehouse.purchase_order_items i
    JOIN warehouse.purchase_orders o ON o."Code" = i."PoCode"
    WHERE i."MaterialCode" = m."MaterialCode"
      AND o."DeletedAt" IS NULL
  );

COMMIT;

INSERT INTO warehouse.wms_settings ("Key", "Value", "CreatedAt", "UpdatedAt")
SELECT 'demo_orphan_cleanup_v1', 'wave-d-2026-08', NOW(), NOW()
WHERE NOT EXISTS (SELECT 1 FROM warehouse.wms_settings WHERE "Key" = 'demo_orphan_cleanup_v1');

UPDATE warehouse.wms_settings
SET "Value" = 'wave-d-2026-08', "UpdatedAt" = NOW()
WHERE "Key" = 'demo_orphan_cleanup_v1';
