-- Wave E1: Xóa hẳn junk Kho / Hàng lỗi / Kiểm kê DEMO-E2E.
-- Phiếu kho: HARD DELETE (API trước đây không lọc DeletedAt).
-- Hàng lỗi / Kiểm kê / tồn DEMO: soft-delete (có query filter).
-- Idempotent. Apply: docker cp + psql -f (UTF-8).

BEGIN;

-- ---------------------------------------------------------------------------
-- 1) Hủy reservation gắn phiếu junk
-- ---------------------------------------------------------------------------
DELETE FROM warehouse.inventory_reservations r
WHERE EXISTS (
  SELECT 1
  FROM warehouse.warehouse_transactions t
  WHERE t."TicketCode" = r."SourceTicketCode"
    AND (
      COALESCE(t."ProjectCode", '') ILIKE '%DEMO%'
      OR COALESCE(t."BeneficiaryProjectCode", '') ILIKE '%DEMO%'
      OR t."ProjectCode" IN ('DA-2026-680', 'DA-2026-76', 'DA-2026-496')
      OR t."TicketCode" ILIKE '%DEMO%'
      OR t."TicketCode" ILIKE '%STATUS-%'
      OR t."TicketCode" ILIKE 'E2E%'
      OR COALESCE(t."ReferenceCode", '') ILIKE '%DEMO%'
      OR COALESCE(t."ReferenceCode", '') ILIKE 'SEED-PO-%'
      OR COALESCE(t."PurchasePlanCode", '') ILIKE '%DEMO%'
      OR COALESCE(t."SupplierName", '') ILIKE '%Demo%'
      OR COALESCE(t."SoCode", '') ILIKE '%DEMO%'
      OR t."DeletedAt" IS NOT NULL
    )
)
OR COALESCE(r."ProjectCode", '') ILIKE '%DEMO%'
OR r."ProjectCode" IN ('DA-2026-680', 'DA-2026-76', 'DA-2026-496')
OR COALESCE(r."WarehouseId", '') ILIKE '%DEMO%'
OR COALESCE(r."SourceTicketCode", '') ILIKE '%DEMO%';

-- ---------------------------------------------------------------------------
-- 2) HARD DELETE phiếu kho junk (+ cascade items/attachments)
-- ---------------------------------------------------------------------------
DELETE FROM warehouse.warehouse_transactions t
WHERE
  COALESCE(t."ProjectCode", '') ILIKE '%DEMO%'
  OR COALESCE(t."BeneficiaryProjectCode", '') ILIKE '%DEMO%'
  OR t."ProjectCode" IN ('DA-2026-680', 'DA-2026-76', 'DA-2026-496')
  OR t."TicketCode" ILIKE '%DEMO%'
  OR t."TicketCode" ILIKE '%STATUS-%'
  OR t."TicketCode" ILIKE 'E2E%'
  OR COALESCE(t."ReferenceCode", '') ILIKE '%DEMO%'
  OR COALESCE(t."ReferenceCode", '') ILIKE 'SEED-PO-%'
  OR COALESCE(t."ReferenceCode", '') IN ('PO-0001', '1234567890')
  OR COALESCE(t."PurchasePlanCode", '') ILIKE '%DEMO%'
  OR COALESCE(t."PurchasePlanCode", '') ILIKE 'SEED-%'
  OR COALESCE(t."SupplierName", '') ILIKE '%Demo%'
  OR COALESCE(t."SoCode", '') ILIKE '%DEMO%'
  OR COALESCE(t."WarehouseId", '') ILIKE '%DEMO%'
  OR COALESCE(t."FromWarehouseId", '') ILIKE '%DEMO%'
  OR COALESCE(t."ToWarehouseId", '') ILIKE '%DEMO%'
  OR (
    -- các phiếu junk đã soft-delete ở lần chạy trước (không phải NK-NV/XK-NV)
    t."DeletedAt" IS NOT NULL
    AND t."TicketCode" NOT LIKE 'NK-NV-%'
    AND t."TicketCode" NOT LIKE 'XK-NV-%'
  );

-- ---------------------------------------------------------------------------
-- 3) Soft-delete hàng lỗi DEMO
-- ---------------------------------------------------------------------------
UPDATE warehouse.purchase_defects
SET "DeletedAt" = NOW(), "UpdatedAt" = NOW()
WHERE "DeletedAt" IS NULL
  AND (
    COALESCE("PoCode", '') ILIKE '%DEMO%'
    OR COALESCE("PoCode", '') ILIKE 'SEED-%'
    OR COALESCE("PlanCode", '') ILIKE '%DEMO%'
    OR COALESCE("ProjectCode", '') ILIKE '%DEMO%'
    OR COALESCE("SupplierName", '') ILIKE '%Demo%'
    OR "TicketCode" ILIKE '%DEMO%'
  );

-- ---------------------------------------------------------------------------
-- 4) Soft-delete toàn bộ kiểm kê E2E / DEMO (hiện 100% list là junk)
-- ---------------------------------------------------------------------------
UPDATE warehouse.stocktakes
SET "DeletedAt" = NOW(), "UpdatedAt" = NOW()
WHERE "DeletedAt" IS NULL
  AND (
    COALESCE("Note", '') ILIKE 'E2E%'
    OR COALESCE("Note", '') ILIKE '%DEMO%'
    OR COALESCE("VoucherCode", '') ILIKE '%DEMO%'
    OR COALESCE("WarehouseName", '') ILIKE '%demo%'
    OR COALESCE("WarehouseId", '') ILIKE '%DEMO%'
    OR COALESCE("WarehouseId", '') = 'DEMO-PUR-PROJ'
  );

-- ---------------------------------------------------------------------------
-- 5) Soft-delete tồn kho gắn dự án / vật tư DEMO
-- ---------------------------------------------------------------------------
UPDATE warehouse.project_inventory
SET "DeletedAt" = NOW(), "UpdatedAt" = NOW()
WHERE "DeletedAt" IS NULL
  AND (
    "ProjectCode" ILIKE '%DEMO%'
    OR "ProjectCode" IN ('DA-2026-680', 'DA-2026-76', 'DA-2026-496')
    OR "MaterialCode" ILIKE 'VT-DEMO%'
    OR "MaterialCode" ILIKE 'DEMO-%'
  );

UPDATE warehouse.warehouse_inventory
SET "DeletedAt" = NOW(), "UpdatedAt" = NOW()
WHERE "DeletedAt" IS NULL
  AND (
    COALESCE("WarehouseId", '') ILIKE '%DEMO%'
    OR COALESCE("WarehouseId", '') = 'DEMO-PUR-PROJ'
    OR "MaterialCode" ILIKE 'VT-DEMO%'
    OR "MaterialCode" ILIKE 'DEMO-%'
  );

-- ---------------------------------------------------------------------------
-- 6) Soft-delete kho vật lý DEMO (giữ wh-general)
-- ---------------------------------------------------------------------------
UPDATE warehouse.warehouses
SET "DeletedAt" = NOW(), "UpdatedAt" = NOW()
WHERE "DeletedAt" IS NULL
  AND (
    "Id" ILIKE '%DEMO%'
    OR "Code" ILIKE '%DEMO%'
    OR "Id" = 'DEMO-PUR-PROJ'
    OR "Code" = 'DEMO-PUR-PROJ'
  );

COMMIT;
