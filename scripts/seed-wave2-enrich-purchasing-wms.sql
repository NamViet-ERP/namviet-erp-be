-- Wave 2: Làm giàu data kho/mua hàng (tồn thấp, meta VT, dự toán, sổ giá, RFQ, đánh giá).
-- Idempotent theo mã NV-CAT-*, DT-NV-*, Period Q2/2026 + NCC thật.
-- Apply: docker cp + psql -f (UTF-8).

BEGIN;

-- ===========================================================================
-- 1) Tồn thấp Tower: MinStockQty > tồn thực tế
-- ===========================================================================
UPDATE warehouse.project_inventory pi
SET "MinStockQty" = v.min_qty,
    "UpdatedAt" = NOW()
FROM (VALUES
  ('VT-CAP-CV-2.5', 150::numeric),
  ('VT-THEP-PHI-16', 8000::numeric),
  ('VT-XM-PCB40', 800::numeric),
  ('VT-ONG-PVC-D63', 100::numeric)
) AS v(mat, min_qty)
WHERE pi."ProjectCode" = 'DA-2026-NV-TOWER'
  AND pi."MaterialCode" = v.mat
  AND pi."DeletedAt" IS NULL;

-- Thêm vài dòng tồn thấp phụ (nếu chưa có) để cảnh báo đa dạng
INSERT INTO warehouse.project_inventory (
  "ProjectCode", "MaterialCode",
  "PlannedImportQty", "PlannedExportQty", "ActualImportQty", "ActualExportQty",
  "ReservedQty", "MinStockQty", "CreatedAt", "UpdatedAt"
)
SELECT
  'DA-2026-NV-TOWER', v.mat,
  v.imp, 0, v.imp, v.exp,
  0, v.min_qty, NOW(), NOW()
FROM (VALUES
  ('VT-CAP-CV-4', 40::numeric, 10::numeric, 80::numeric),
  ('VT-THEP-PHI-12', 2000::numeric, 500::numeric, 2500::numeric),
  ('VT-ONG-PVC-D90', 50::numeric, 20::numeric, 80::numeric),
  ('VT-BULONG-M12', 30::numeric, 5::numeric, 50::numeric)
) AS v(mat, imp, exp, min_qty)
WHERE EXISTS (
  SELECT 1 FROM warehouse.materials m
  WHERE m."MaterialCode" = v.mat AND m."DeletedAt" IS NULL
)
AND NOT EXISTS (
  SELECT 1 FROM warehouse.project_inventory pi
  WHERE pi."ProjectCode" = 'DA-2026-NV-TOWER'
    AND pi."MaterialCode" = v.mat
    AND pi."DeletedAt" IS NULL
);

UPDATE warehouse.project_inventory pi
SET "MinStockQty" = v.min_qty,
    "ActualImportQty" = GREATEST(pi."ActualImportQty", v.imp),
    "ActualExportQty" = GREATEST(pi."ActualExportQty", v.exp),
    "UpdatedAt" = NOW()
FROM (VALUES
  ('VT-CAP-CV-4', 40::numeric, 10::numeric, 80::numeric),
  ('VT-THEP-PHI-12', 2000::numeric, 500::numeric, 2500::numeric),
  ('VT-ONG-PVC-D90', 50::numeric, 20::numeric, 80::numeric),
  ('VT-BULONG-M12', 30::numeric, 5::numeric, 50::numeric)
) AS v(mat, imp, exp, min_qty)
WHERE pi."ProjectCode" = 'DA-2026-NV-TOWER'
  AND pi."MaterialCode" = v.mat
  AND pi."DeletedAt" IS NULL;

-- ===========================================================================
-- 2) Cây danh mục bổ sung (xây dựng / điện dân dụng) — 5 cấp
-- ===========================================================================
CREATE TEMP TABLE _nv_cat (
  "Code" varchar(50) PRIMARY KEY,
  "Name" varchar(255) NOT NULL,
  "Level" int NOT NULL,
  "ParentCode" varchar(50),
  "SortOrder" int NOT NULL DEFAULT 0
);

INSERT INTO _nv_cat ("Code", "Name", "Level", "ParentCode", "SortOrder") VALUES
  ('NV-CAT-XD', 'Vật tư xây dựng', 1, NULL, 10),
  ('NV-CAT-ME', 'M&E dân dụng', 1, NULL, 11),

  ('NV-CAT-COT', 'Cốt thép xây dựng', 2, 'NV-CAT-XD', 0),
  ('NV-CAT-XM', 'Xi măng & kết dính', 2, 'NV-CAT-XD', 1),
  ('NV-CAT-CL', 'Cốt liệu', 2, 'NV-CAT-XD', 2),
  ('NV-CAT-GACH', 'Gạch & ốp', 2, 'NV-CAT-XD', 3),
  ('NV-CAT-COPHA', 'Cốp pha & giàn giáo', 2, 'NV-CAT-XD', 4),
  ('NV-CAT-SON', 'Sơn & chống thấm', 2, 'NV-CAT-XD', 5),
  ('NV-CAT-KEO', 'Keo & phụ trợ', 2, 'NV-CAT-XD', 6),

  ('NV-CAT-DIEN', 'Điện & chiếu sáng', 2, 'NV-CAT-ME', 0),
  ('NV-CAT-ONGN', 'Ống nước nhựa', 2, 'NV-CAT-ME', 1),
  ('NV-CAT-HAN', 'Hàn & cắt', 2, 'NV-CAT-ME', 2),

  ('NV-CAT-THANH', 'Thép thanh vằn', 3, 'NV-CAT-COT', 0),
  ('NV-CAT-PCB', 'Xi măng PCB', 3, 'NV-CAT-XM', 0),
  ('NV-CAT-CAT', 'Cát xây', 3, 'NV-CAT-CL', 0),
  ('NV-CAT-DA', 'Đá dăm', 3, 'NV-CAT-CL', 1),
  ('NV-CAT-GONG', 'Gạch ống', 3, 'NV-CAT-GACH', 0),
  ('NV-CAT-GTHE', 'Gạch thẻ', 3, 'NV-CAT-GACH', 1),
  ('NV-CAT-CPSAT', 'Cốp pha thép', 3, 'NV-CAT-COPHA', 0),
  ('NV-CAT-GGD48', 'Ống giàn giáo', 3, 'NV-CAT-COPHA', 1),
  ('NV-CAT-VEP', 'Ván ép phủ phim', 3, 'NV-CAT-COPHA', 2),
  ('NV-CAT-SEP', 'Sơn epoxy', 3, 'NV-CAT-SON', 0),
  ('NV-CAT-SLOT', 'Sơn lót', 3, 'NV-CAT-SON', 1),
  ('NV-CAT-SNUOC', 'Sơn nước', 3, 'NV-CAT-SON', 2),
  ('NV-CAT-CTHAM', 'Chống thấm', 3, 'NV-CAT-SON', 3),
  ('NV-CAT-KGACH', 'Keo dán gạch', 3, 'NV-CAT-KEO', 0),
  ('NV-CAT-KSIL', 'Keo silicone', 3, 'NV-CAT-KEO', 1),

  ('NV-CAT-CAP', 'Cáp điện', 3, 'NV-CAT-DIEN', 0),
  ('NV-CAT-APT', 'Aptomat & MCB', 3, 'NV-CAT-DIEN', 1),
  ('NV-CAT-CTAC', 'Công tắc ổ cắm', 3, 'NV-CAT-DIEN', 2),
  ('NV-CAT-ONGSP', 'Ống luồn dây', 3, 'NV-CAT-DIEN', 3),
  ('NV-CAT-PVC', 'Ống PVC', 3, 'NV-CAT-ONGN', 0),
  ('NV-CAT-HDPE', 'Ống HDPE', 3, 'NV-CAT-ONGN', 1),
  ('NV-CAT-MHAN', 'Máy hàn', 3, 'NV-CAT-HAN', 0),
  ('NV-CAT-QHAN', 'Que hàn', 3, 'NV-CAT-HAN', 1),

  ('NV-CAT-PHI12', 'Φ12', 4, 'NV-CAT-THANH', 0),
  ('NV-CAT-PHI16', 'Φ16', 4, 'NV-CAT-THANH', 1),
  ('NV-CAT-PHI20', 'Φ20', 4, 'NV-CAT-THANH', 2),
  ('NV-CAT-PCB40', 'PCB40', 4, 'NV-CAT-PCB', 0),
  ('NV-CAT-CATV', 'Cát vàng xây tô', 4, 'NV-CAT-CAT', 0),
  ('NV-CAT-DA12', 'Đá 1x2', 4, 'NV-CAT-DA', 0),
  ('NV-CAT-G4L', '4 lỗ', 4, 'NV-CAT-GONG', 0),
  ('NV-CAT-GDAC', 'Đặc', 4, 'NV-CAT-GTHE', 0),
  ('NV-CAT-CPTAM', 'Tấm', 4, 'NV-CAT-CPSAT', 0),
  ('NV-CAT-GG48', 'D48', 4, 'NV-CAT-GGD48', 0),
  ('NV-CAT-VEP12', '12mm', 4, 'NV-CAT-VEP', 0),
  ('NV-CAT-SEPA', 'Bộ A+B', 4, 'NV-CAT-SEP', 0),
  ('NV-CAT-SLOT5', '5L', 4, 'NV-CAT-SLOT', 0),
  ('NV-CAT-SN18', '18L nội thất', 4, 'NV-CAT-SNUOC', 0),
  ('NV-CAT-CT11', 'CT11A', 4, 'NV-CAT-CTHAM', 0),
  ('NV-CAT-KG25', '25kg', 4, 'NV-CAT-KGACH', 0),
  ('NV-CAT-KS300', '300ml', 4, 'NV-CAT-KSIL', 0),

  ('NV-CAT-CV25', 'CV 2.5mm²', 4, 'NV-CAT-CAP', 0),
  ('NV-CAT-CV4', 'CV 4mm²', 4, 'NV-CAT-CAP', 1),
  ('NV-CAT-CXV', 'CXV 3x50+25', 4, 'NV-CAT-CAP', 2),
  ('NV-CAT-MCB63', 'MCB 63A 3P', 4, 'NV-CAT-APT', 0),
  ('NV-CAT-CT1C', 'Công tắc 1 chiều', 4, 'NV-CAT-CTAC', 0),
  ('NV-CAT-OC3', 'Ổ cắm 3 chấu', 4, 'NV-CAT-CTAC', 1),
  ('NV-CAT-SP20', 'SP20', 4, 'NV-CAT-ONGSP', 0),
  ('NV-CAT-PV63', 'D63', 4, 'NV-CAT-PVC', 0),
  ('NV-CAT-PV90', 'D90', 4, 'NV-CAT-PVC', 1),
  ('NV-CAT-HD110', 'PE100 D110', 4, 'NV-CAT-HDPE', 0),
  ('NV-CAT-INV200', 'Inverter 200A', 4, 'NV-CAT-MHAN', 0),
  ('NV-CAT-J422', 'J422 Φ3.2', 4, 'NV-CAT-QHAN', 0),

  ('NV-CAT-CB300-12', 'CB300-V', 5, 'NV-CAT-PHI12', 0),
  ('NV-CAT-CB300-16', 'CB300-V', 5, 'NV-CAT-PHI16', 0),
  ('NV-CAT-CB300-20', 'CB300-V', 5, 'NV-CAT-PHI20', 0),
  ('NV-CAT-BAO50', 'Bao 50kg', 5, 'NV-CAT-PCB40', 0),
  ('NV-CAT-KHOI', 'Khối', 5, 'NV-CAT-CATV', 0),
  ('NV-CAT-KHOI2', 'Khối', 5, 'NV-CAT-DA12', 0),
  ('NV-CAT-VIEN', 'Viên', 5, 'NV-CAT-G4L', 0),
  ('NV-CAT-VIEN2', 'Viên', 5, 'NV-CAT-GDAC', 0),
  ('NV-CAT-TAM', 'Tấm', 5, 'NV-CAT-CPTAM', 0),
  ('NV-CAT-ONG', 'Ống', 5, 'NV-CAT-GG48', 0),
  ('NV-CAT-TAM12', 'Tấm', 5, 'NV-CAT-VEP12', 0),
  ('NV-CAT-BO', 'Bộ', 5, 'NV-CAT-SEPA', 0),
  ('NV-CAT-LON', 'Lon', 5, 'NV-CAT-SLOT5', 0),
  ('NV-CAT-THUNG', 'Thùng', 5, 'NV-CAT-SN18', 0),
  ('NV-CAT-BAOCT', 'Bao', 5, 'NV-CAT-CT11', 0),
  ('NV-CAT-BAOK', 'Bao', 5, 'NV-CAT-KG25', 0),
  ('NV-CAT-TUYP', 'Tuýp', 5, 'NV-CAT-KS300', 0),
  ('NV-CAT-CUON', 'Cuộn', 5, 'NV-CAT-CV25', 0),
  ('NV-CAT-CUON4', 'Cuộn', 5, 'NV-CAT-CV4', 0),
  ('NV-CAT-MET', 'Mét', 5, 'NV-CAT-CXV', 0),
  ('NV-CAT-CAI', 'Cái', 5, 'NV-CAT-MCB63', 0),
  ('NV-CAT-BOCT', 'Bộ', 5, 'NV-CAT-CT1C', 0),
  ('NV-CAT-BOOC', 'Bộ', 5, 'NV-CAT-OC3', 0),
  ('NV-CAT-ONGSP5', 'Ống', 5, 'NV-CAT-SP20', 0),
  ('NV-CAT-ONG63', 'Ống', 5, 'NV-CAT-PV63', 0),
  ('NV-CAT-ONG90', 'Ống', 5, 'NV-CAT-PV90', 0),
  ('NV-CAT-MHD', 'Mét', 5, 'NV-CAT-HD110', 0),
  ('NV-CAT-BOMH', 'Bộ', 5, 'NV-CAT-INV200', 0),
  ('NV-CAT-THQH', 'Thùng', 5, 'NV-CAT-J422', 0);

INSERT INTO warehouse.material_categories ("Code", "Name", "Level", "ParentId", "SortOrder", "IsActive", "CreatedAt", "UpdatedAt")
SELECT s."Code", s."Name", s."Level", NULL, s."SortOrder", TRUE, NOW(), NOW()
FROM _nv_cat s
WHERE s."Level" = 1
  AND NOT EXISTS (SELECT 1 FROM warehouse.material_categories c WHERE c."Code" = s."Code");

INSERT INTO warehouse.material_categories ("Code", "Name", "Level", "ParentId", "SortOrder", "IsActive", "CreatedAt", "UpdatedAt")
SELECT s."Code", s."Name", s."Level", p."Id", s."SortOrder", TRUE, NOW(), NOW()
FROM _nv_cat s
JOIN warehouse.material_categories p ON p."Code" = s."ParentCode"
WHERE s."Level" = 2
  AND NOT EXISTS (SELECT 1 FROM warehouse.material_categories c WHERE c."Code" = s."Code");

INSERT INTO warehouse.material_categories ("Code", "Name", "Level", "ParentId", "SortOrder", "IsActive", "CreatedAt", "UpdatedAt")
SELECT s."Code", s."Name", s."Level", p."Id", s."SortOrder", TRUE, NOW(), NOW()
FROM _nv_cat s
JOIN warehouse.material_categories p ON p."Code" = s."ParentCode"
WHERE s."Level" = 3
  AND NOT EXISTS (SELECT 1 FROM warehouse.material_categories c WHERE c."Code" = s."Code");

INSERT INTO warehouse.material_categories ("Code", "Name", "Level", "ParentId", "SortOrder", "IsActive", "CreatedAt", "UpdatedAt")
SELECT s."Code", s."Name", s."Level", p."Id", s."SortOrder", TRUE, NOW(), NOW()
FROM _nv_cat s
JOIN warehouse.material_categories p ON p."Code" = s."ParentCode"
WHERE s."Level" = 4
  AND NOT EXISTS (SELECT 1 FROM warehouse.material_categories c WHERE c."Code" = s."Code");

INSERT INTO warehouse.material_categories ("Code", "Name", "Level", "ParentId", "SortOrder", "IsActive", "CreatedAt", "UpdatedAt")
SELECT s."Code", s."Name", s."Level", p."Id", s."SortOrder", TRUE, NOW(), NOW()
FROM _nv_cat s
JOIN warehouse.material_categories p ON p."Code" = s."ParentCode"
WHERE s."Level" = 5
  AND NOT EXISTS (SELECT 1 FROM warehouse.material_categories c WHERE c."Code" = s."Code");

-- Un-delete if previously soft-deleted
UPDATE warehouse.material_categories c
SET "DeletedAt" = NULL, "IsActive" = TRUE, "UpdatedAt" = NOW()
WHERE c."Code" LIKE 'NV-CAT-%' AND c."DeletedAt" IS NOT NULL;

-- ===========================================================================
-- 3) Gán CategoryId + QualityStandard cho VT-*
-- ===========================================================================
UPDATE warehouse.materials m
SET "CategoryId" = c."Id",
    "QualityStandard" = v.qs,
    "UpdatedAt" = NOW()
FROM (VALUES
  -- Xây dựng (NV leaf L5)
  ('VT-THEP-PHI-12', 'NV-CAT-CB300-12', 'TCVN 1651-1:2018'),
  ('VT-THEP-PHI-16', 'NV-CAT-CB300-16', 'TCVN 1651-1:2018'),
  ('VT-THEP-PHI-20', 'NV-CAT-CB300-20', 'TCVN 1651-1:2018'),
  ('VT-XM-PCB40', 'NV-CAT-BAO50', 'TCVN 6260:2020'),
  ('VT-CAT-VANG', 'NV-CAT-KHOI', 'TCVN 7570:2006'),
  ('VT-DA-1X2', 'NV-CAT-KHOI2', 'TCVN 7570:2006'),
  ('VT-GACH-ONG-4L', 'NV-CAT-VIEN', 'TCVN 1451:1998'),
  ('VT-GACH-THE', 'NV-CAT-VIEN2', 'TCVN 1450:1986'),
  ('VT-COPHA-SAT', 'NV-CAT-TAM', 'Nội bộ NV'),
  ('VT-GIAN-GIAO-D48', 'NV-CAT-ONG', 'TCVN 9381:2012'),
  ('VT-VAN-EP-12', 'NV-CAT-TAM12', 'JIS A 5908'),
  ('VT-SON-EPX-A', 'NV-CAT-BO', 'ASTM D4541'),
  ('VT-SON-LOT-5L', 'NV-CAT-LON', 'TCVN 8652:2012'),
  ('VT-SON-NUOC-18L', 'NV-CAT-THUNG', 'TCVN 8652:2012'),
  ('VT-CTHAM-CT11A', 'NV-CAT-BAOCT', 'ASTM C836'),
  ('VT-KEO-GACH-25', 'NV-CAT-BAOK', 'ISO 13007'),
  ('VT-KEO-SIL-300', 'NV-CAT-TUYP', 'ASTM C920'),
  ('VT-CAP-CV-2.5', 'NV-CAT-CUON', 'TCVN 6610-3:2000'),
  ('VT-CAP-CV-4', 'NV-CAT-CUON4', 'TCVN 6610-3:2000'),
  ('VT-CAP-CXV-3x50', 'NV-CAT-MET', 'TCVN 5935-1:2013'),
  ('VT-MCB-63A', 'NV-CAT-CAI', 'IEC 60898'),
  ('VT-CTAC-1C', 'NV-CAT-BOCT', 'IEC 60669'),
  ('VT-OCAM-3CH', 'NV-CAT-BOOC', 'IEC 60884'),
  ('VT-ONG-SP-20', 'NV-CAT-ONGSP5', 'TCVN 7997:2009'),
  ('VT-ONG-PVC-D63', 'NV-CAT-ONG63', 'TCVN 8491-1:2011'),
  ('VT-ONG-PVC-D90', 'NV-CAT-ONG90', 'TCVN 8491-1:2011'),
  ('VT-ONG-HDPE-110', 'NV-CAT-MHD', 'ISO 4427'),
  ('VT-HAN-INVERTER-200', 'NV-CAT-BOMH', 'IEC 60974'),
  ('VT-QUE-HAN-J422', 'NV-CAT-THQH', 'AWS A5.1 E6013'),

  -- Công nghiệp (cây CAT sẵn)
  ('VT-THEP-A515', 'CAT-82BBBBB8DAF1', 'ASTM A515 Gr.70'),
  ('VT-THEP-CT3-10', 'CAT-F4D003320E70', 'TCVN 6522'),
  ('VT-THEP-CT3-6', 'CAT-F4D003320E70', 'TCVN 6522'),
  ('VT-THEP-V50', 'CAT-30680275A959', 'TCVN 7571-15'),
  ('VT-THEP-U100', 'CAT-3C2B669E2614', 'TCVN 7571-15'),
  ('VT-ONG-A106', 'CAT-A6D26ECA64BD', 'ASTM A106 Gr.B'),
  ('VT-ONG-A106-51', 'CAT-A6D26ECA64BD', 'ASTM A106 Gr.B'),
  ('VT-ONG-A106-60', 'CAT-A6D26ECA64BD', 'ASTM A106 Gr.B'),
  ('VT-ONG-A106-90', 'CAT-A6D26ECA64BD', 'ASTM A106 Gr.B'),
  ('VT-ONG-SCH40-48', 'CAT-8DC1D0C3EDEF', 'ASTM A53 Sch40'),
  ('VT-ONG-SCH40-60', 'CAT-8DC1D0C3EDEF', 'ASTM A53 Sch40'),
  ('VT-BULONG-M12', 'CAT-8FD2502D8181', 'ISO 4014 8.8'),
  ('VT-BULONG-M16', 'CAT-8FD2502D8181', 'ISO 4014 8.8'),
  ('VT-DAIOC-M12', 'CAT-8FD2502D8181', 'ISO 4032'),
  ('VT-VAN-BI-DN50', 'CAT-721910CC11AD', 'API 6D'),
  ('VT-VAN-CONG-DN80', 'CAT-0C6BB07F3E70', 'API 600'),
  ('VT-BOM-CN-5.5', 'CAT-E2EC73AF2162', 'ISO 9906'),
  ('VT-LOC-SS304', 'CAT-83D346E064FC', 'AISI 304'),
  ('VT-LUOI-D4', 'CAT-C17AA7A1F1AE', 'TCVN 9391'),
  ('VT-DEN-LED-36W', 'CAT-5DB5645044D6', 'IEC 60598'),
  ('VT-PHEU-INOX-300', 'CAT-D10346DE455E', 'AISI 304')
) AS v(mat, cat_code, qs)
JOIN warehouse.material_categories c ON c."Code" = v.cat_code AND c."DeletedAt" IS NULL
WHERE m."MaterialCode" = v.mat AND m."DeletedAt" IS NULL;

-- Soft-delete DEMO-DANH-MUC (không còn VT gắn)
UPDATE warehouse.material_categories
SET "DeletedAt" = NOW(), "IsActive" = FALSE, "UpdatedAt" = NOW()
WHERE "Code" = 'DEMO-DANH-MUC'
  AND "DeletedAt" IS NULL
  AND NOT EXISTS (
    SELECT 1 FROM warehouse.materials m
    WHERE m."CategoryId" = warehouse.material_categories."Id"
      AND m."DeletedAt" IS NULL
  );

DROP TABLE IF EXISTS _nv_cat;

-- ===========================================================================
-- 4) Dự toán Tower (approved + draft)
-- ===========================================================================
DO $$
DECLARE
  uid uuid := 'aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee'::uuid;
  est1 uuid;
  est2 uuid;
  ver1 uuid;
  ver2 uuid;
BEGIN
  SELECT "Id" INTO est1 FROM warehouse.purchase_estimates WHERE "Code" = 'DT-NV-2026-01' AND "DeletedAt" IS NULL;
  IF est1 IS NULL THEN
    est1 := gen_random_uuid();
    INSERT INTO warehouse.purchase_estimates (
      "Id", "Code", "ProjectCode", "Title", "CurrentVersion",
      "CreatedById", "CreatedByName", "UpdatedById", "UpdatedByName",
      "CreatedAt", "UpdatedAt", "Status", "PreparerName", "PreparerDate",
      "ReviewedById", "ReviewedByName", "ReviewedAt"
    ) VALUES (
      est1, 'DT-NV-2026-01', 'DA-2026-NV-TOWER',
      'Dự toán vật tư kết cấu & điện - Nam Việt Tower',
      1, uid, 'Phạm Minh Châu', uid, 'Phạm Minh Châu',
      NOW() - INTERVAL '20 days', NOW() - INTERVAL '5 days',
      'approved', 'Nguyễn Văn Bình', NOW() - INTERVAL '18 days',
      uid, 'Lê Văn Tám', NOW() - INTERVAL '6 days'
    );
  ELSE
    UPDATE warehouse.purchase_estimates
    SET "Status" = 'approved', "Title" = 'Dự toán vật tư kết cấu & điện - Nam Việt Tower',
        "UpdatedAt" = NOW(), "DeletedAt" = NULL
    WHERE "Id" = est1;
  END IF;

  SELECT "Id" INTO ver1 FROM warehouse.purchase_estimate_versions
  WHERE "EstimateId" = est1 AND "VersionNumber" = 1 AND "DeletedAt" IS NULL;
  IF ver1 IS NULL THEN
    ver1 := gen_random_uuid();
    INSERT INTO warehouse.purchase_estimate_versions (
      "Id", "EstimateId", "VersionNumber", "UpdatedById", "UpdatedByName", "CreatedAt", "UpdatedAt"
    ) VALUES (ver1, est1, 1, uid, 'Phạm Minh Châu', NOW() - INTERVAL '20 days', NOW() - INTERVAL '5 days');
  END IF;

  INSERT INTO warehouse.purchase_estimate_lines (
    "Id", "VersionId", "MaterialCode", "MaterialName", "Unit",
    "OldQty", "NewQty", "CategoryName", "CreatedAt", "UpdatedAt"
  )
  SELECT gen_random_uuid(), ver1, v.mat, m."MaterialName", m."Unit",
         0, v.qty, COALESCE(c."Name", 'Vật tư'), NOW(), NOW()
  FROM (VALUES
    ('VT-CAP-CV-2.5', 150::numeric),
    ('VT-CAP-CV-4', 80::numeric),
    ('VT-THEP-PHI-16', 12000::numeric),
    ('VT-THEP-PHI-12', 5000::numeric),
    ('VT-XM-PCB40', 1000::numeric),
    ('VT-ONG-PVC-D63', 200::numeric),
    ('VT-THEP-V50', 800::numeric),
    ('VT-BULONG-M12', 60::numeric)
  ) AS v(mat, qty)
  JOIN warehouse.materials m ON m."MaterialCode" = v.mat AND m."DeletedAt" IS NULL
  LEFT JOIN warehouse.material_categories c ON c."Id" = m."CategoryId"
  WHERE NOT EXISTS (
    SELECT 1 FROM warehouse.purchase_estimate_lines l
    WHERE l."VersionId" = ver1 AND l."MaterialCode" = v.mat AND l."DeletedAt" IS NULL
  );

  -- Approvals
  INSERT INTO warehouse.purchase_estimate_approvals (
    "Id", "EstimateId", "Level", "Name", "Status",
    "ActionById", "ActionByName", "ActionAt", "CreatedAt", "UpdatedAt"
  )
  SELECT gen_random_uuid(), est1, v.lvl, v.nm, 'approved', uid, v.by_name, NOW() - INTERVAL '6 days', NOW(), NOW()
  FROM (VALUES
    (1, 'Trưởng phòng SC', 'Nguyễn Văn Bình'),
    (2, 'Giám đốc mua hàng', 'Lê Văn Tám')
  ) AS v(lvl, nm, by_name)
  WHERE NOT EXISTS (
    SELECT 1 FROM warehouse.purchase_estimate_approvals a
    WHERE a."EstimateId" = est1 AND a."Level" = v.lvl AND a."DeletedAt" IS NULL
  );

  -- Draft estimate 2
  SELECT "Id" INTO est2 FROM warehouse.purchase_estimates WHERE "Code" = 'DT-NV-2026-02' AND "DeletedAt" IS NULL;
  IF est2 IS NULL THEN
    est2 := gen_random_uuid();
    INSERT INTO warehouse.purchase_estimates (
      "Id", "Code", "ProjectCode", "Title", "CurrentVersion",
      "CreatedById", "CreatedByName", "UpdatedById", "UpdatedByName",
      "CreatedAt", "UpdatedAt", "Status", "PreparerName", "PreparerDate"
    ) VALUES (
      est2, 'DT-NV-2026-02', 'DA-2026-NV-TOWER',
      'Dự toán hoàn thiện nội thất - Tower (nháp)',
      1, uid, 'Phạm Minh Châu', uid, 'Phạm Minh Châu',
      NOW() - INTERVAL '3 days', NOW() - INTERVAL '1 day',
      'draft', 'Phạm Minh Châu', NOW() - INTERVAL '3 days'
    );
  END IF;

  SELECT "Id" INTO ver2 FROM warehouse.purchase_estimate_versions
  WHERE "EstimateId" = est2 AND "VersionNumber" = 1 AND "DeletedAt" IS NULL;
  IF ver2 IS NULL THEN
    ver2 := gen_random_uuid();
    INSERT INTO warehouse.purchase_estimate_versions (
      "Id", "EstimateId", "VersionNumber", "UpdatedById", "UpdatedByName", "CreatedAt", "UpdatedAt"
    ) VALUES (ver2, est2, 1, uid, 'Phạm Minh Châu', NOW() - INTERVAL '3 days', NOW());
  END IF;

  INSERT INTO warehouse.purchase_estimate_lines (
    "Id", "VersionId", "MaterialCode", "MaterialName", "Unit",
    "OldQty", "NewQty", "CategoryName", "CreatedAt", "UpdatedAt"
  )
  SELECT gen_random_uuid(), ver2, v.mat, m."MaterialName", m."Unit",
         0, v.qty, COALESCE(c."Name", 'Vật tư'), NOW(), NOW()
  FROM (VALUES
    ('VT-SON-NUOC-18L', 40::numeric),
    ('VT-SON-LOT-5L', 25::numeric),
    ('VT-DEN-LED-36W', 120::numeric),
    ('VT-GACH-THE', 8000::numeric)
  ) AS v(mat, qty)
  JOIN warehouse.materials m ON m."MaterialCode" = v.mat AND m."DeletedAt" IS NULL
  LEFT JOIN warehouse.material_categories c ON c."Id" = m."CategoryId"
  WHERE NOT EXISTS (
    SELECT 1 FROM warehouse.purchase_estimate_lines l
    WHERE l."VersionId" = ver2 AND l."MaterialCode" = v.mat AND l."DeletedAt" IS NULL
  );
END $$;

-- ===========================================================================
-- 5) Giá dự toán + giá bán Tower
-- ===========================================================================
INSERT INTO warehouse.purchase_item_estimate_prices (
  "Id", "MaterialCode", "MaterialName", "ProjectCode", "EstimatePrice",
  "EffectiveDate", "CreatedByName", "Note", "CreatedAt", "UpdatedAt"
)
SELECT gen_random_uuid(), v.mat, m."MaterialName", 'DA-2026-NV-TOWER', v.price,
       DATE '2026-06-01', 'Phạm Minh Châu', 'Giá dự toán Wave 2', NOW(), NOW()
FROM (VALUES
  ('VT-CAP-CV-2.5', 295000::numeric),
  ('VT-CAP-CV-4', 410000::numeric),
  ('VT-THEP-PHI-12', 17200::numeric),
  ('VT-THEP-PHI-16', 17800::numeric),
  ('VT-THEP-PHI-20', 17600::numeric),
  ('VT-XM-PCB40', 92000::numeric),
  ('VT-ONG-PVC-D63', 40000::numeric),
  ('VT-ONG-PVC-D90', 56000::numeric),
  ('VT-THEP-V50', 21000::numeric),
  ('VT-BULONG-M12', 82000::numeric),
  ('VT-SON-NUOC-18L', 980000::numeric),
  ('VT-DEN-LED-36W', 280000::numeric)
) AS v(mat, price)
JOIN warehouse.materials m ON m."MaterialCode" = v.mat AND m."DeletedAt" IS NULL
WHERE NOT EXISTS (
  SELECT 1 FROM warehouse.purchase_item_estimate_prices p
  WHERE p."MaterialCode" = v.mat
    AND p."ProjectCode" = 'DA-2026-NV-TOWER'
    AND p."EffectiveDate" = DATE '2026-06-01'
    AND p."DeletedAt" IS NULL
);

INSERT INTO warehouse.purchase_sell_prices (
  "Id", "MaterialCode", "MaterialName", "ProjectCode", "SellPrice",
  "EffectiveDate", "CreatedByName", "Note", "CreatedAt", "UpdatedAt"
)
SELECT gen_random_uuid(), v.mat, m."MaterialName", 'DA-2026-NV-TOWER', v.price,
       DATE '2026-06-15', 'Phạm Minh Châu', 'Giá bán nội bộ Tower', NOW(), NOW()
FROM (VALUES
  ('VT-CAP-CV-2.5', 340000::numeric),
  ('VT-CAP-CV-4', 480000::numeric),
  ('VT-THEP-PHI-12', 19800::numeric),
  ('VT-THEP-PHI-16', 20500::numeric),
  ('VT-THEP-PHI-20', 20200::numeric),
  ('VT-XM-PCB40', 105000::numeric),
  ('VT-ONG-PVC-D63', 48000::numeric),
  ('VT-ONG-PVC-D90', 68000::numeric),
  ('VT-THEP-V50', 24500::numeric),
  ('VT-BULONG-M12', 98000::numeric),
  ('VT-SON-NUOC-18L', 1180000::numeric),
  ('VT-DEN-LED-36W', 335000::numeric)
) AS v(mat, price)
JOIN warehouse.materials m ON m."MaterialCode" = v.mat AND m."DeletedAt" IS NULL
WHERE NOT EXISTS (
  SELECT 1 FROM warehouse.purchase_sell_prices p
  WHERE p."MaterialCode" = v.mat
    AND p."ProjectCode" = 'DA-2026-NV-TOWER'
    AND p."EffectiveDate" = DATE '2026-06-15'
    AND p."DeletedAt" IS NULL
);

-- ===========================================================================
-- 6) Làm giàu RFQ (so sánh giá / matrix)
-- ===========================================================================
INSERT INTO warehouse.purchase_rfq_quotes (
  "Id", "MaterialCode", "MaterialName", "SupplierId", "SupplierName",
  "ProjectCode", "QuoteDate", "UnitPrice", "ValidUntil", "LeadTimeDays",
  "PaymentTerm", "Status", "PoRef", "Note", "CreatedAt", "UpdatedAt"
)
SELECT
  gen_random_uuid(), v.mat, m."MaterialName", s."Id", s."SupplierName",
  'DA-2026-NV-TOWER', v.qdate::date, v.price, v.valid::date, v.lead,
  v.term, v.status, v.po, v.note, NOW(), NOW()
FROM (VALUES
  ('VT-CAP-CV-2.5', 'NCC-BINHMINH', '2026-06-19', 308000::numeric, '2026-09-30', 9, 'Công nợ 30 ngày', 'lost', NULL, 'Giá gần CADIVI'),
  ('VT-THEP-PHI-16', 'NCC-LIOA', '2026-06-22', 18400::numeric, '2026-08-31', 18, 'Công nợ 30 ngày', 'lost', NULL, 'Lead time dài'),
  ('VT-XM-PCB40', 'NCC-VIGLACERA', '2026-07-02', 94500::numeric, '2026-10-31', 5, 'Công nợ 30 ngày', 'lost', NULL, NULL),
  ('VT-ONG-PVC-D63', 'NCC-BINHMINH', '2026-08-01', 39500::numeric, '2026-11-30', 7, 'Công nợ 45 ngày', 'active', NULL, 'Giá cạnh tranh'),
  ('VT-ONG-PVC-D63', 'NCC-CADIVI', '2026-08-02', 42500::numeric, '2026-11-15', 10, 'Công nợ 30 ngày', 'active', NULL, 'Ngoài ngành chính'),
  ('VT-CAP-CV-4', 'NCC-LIOA', '2026-08-04', 435000::numeric, '2026-11-04', 8, 'Công nợ 30 ngày', 'active', NULL, NULL),
  ('VT-THEP-PHI-12', 'NCC-DONGNAM', '2026-07-18', 17350::numeric, '2026-10-18', 16, '30/70', 'active', NULL, NULL),
  ('VT-THEP-V50', 'NCC-DONGNAM', '2026-08-02', 21900::numeric, '2026-10-02', 12, 'Công nợ 30 ngày', 'active', NULL, NULL),
  ('VT-BULONG-M12', 'NCC-HOAPHAT', '2026-07-10', 80000::numeric, '2026-12-31', 5, 'Công nợ 30 ngày', 'active', NULL, NULL),
  ('VT-BULONG-M12', 'NCC-ANCUONG', '2026-07-11', 83500::numeric, '2026-12-31', 7, 'Công nợ 15 ngày', 'active', NULL, NULL),
  ('VT-DEN-LED-36W', 'NCC-LIOA', '2026-08-08', 275000::numeric, '2026-12-08', 4, 'Công nợ 30 ngày', 'active', NULL, NULL),
  ('VT-SON-NUOC-18L', 'NCC-JOTUN', '2026-08-06', 1050000::numeric, '2026-11-06', 3, 'Công nợ 30 ngày', 'active', NULL, NULL)
) AS v(mat, supplier_code, qdate, price, valid, lead, term, status, po, note)
JOIN warehouse.materials m ON m."MaterialCode" = v.mat AND m."DeletedAt" IS NULL
JOIN warehouse.suppliers s ON s."SupplierCode" = v.supplier_code AND s."DeletedAt" IS NULL
WHERE NOT EXISTS (
  SELECT 1 FROM warehouse.purchase_rfq_quotes q
  WHERE q."DeletedAt" IS NULL
    AND q."ProjectCode" = 'DA-2026-NV-TOWER'
    AND q."MaterialCode" = v.mat
    AND q."SupplierId" = s."Id"
    AND q."QuoteDate" = v.qdate::date
);

-- ===========================================================================
-- 7) Đánh giá KPI + giao hàng cho NCC thật
-- ===========================================================================
-- Soft-delete demo evaluations
UPDATE warehouse.supplier_evaluations
SET "DeletedAt" = NOW(), "UpdatedAt" = NOW()
WHERE "DeletedAt" IS NULL
  AND ("SupplierNameSnapshot" ILIKE '%Demo%' OR "SupplierNameSnapshot" ILIKE '%DEMO%');

UPDATE warehouse.supplier_delivery_evaluations
SET "DeletedAt" = NOW(), "UpdatedAt" = NOW()
WHERE "DeletedAt" IS NULL
  AND ("SupplierNameSnapshot" ILIKE '%Demo%' OR "PoCode" ILIKE 'PO-DEMO%');

INSERT INTO warehouse.supplier_evaluations (
  "SupplierId", "SupplierNameSnapshot", "Period", "Score", "Grade",
  "EvaluatedAt", "Note", "EvaluatedById", "EvaluatedByName",
  "CreatedAt", "UpdatedAt", "CriteriaScores"
)
SELECT
  s."Id", s."SupplierName", v.period, v.score, v.grade,
  v.eval_at::date, v.note,
  'aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee'::uuid, 'Lê Văn Tám',
  NOW(), NOW(),
  '{"flex_0":4,"flex_1":5,"flex_2":4,"flex_3":4,"flex_4":5,"flex_5":4,"coord_0":5,"coord_1":4,"coord_2":4,"coord_3":5,"coord_4":4,"coord_5":4,"supply_0":5,"supply_1":4,"supply_2":4,"supply_3":5,"commercial_0":4,"commercial_1":5,"commercial_2":4,"commercial_3":4,"commercial_4":5,"commercial_5":4,"commercial_6":4,"tech_0":4,"tech_1":4,"tech_2":4,"tech_3":5,"tech_4":4,"tech_5":4,"tech_6":4}'::jsonb
FROM (VALUES
  ('NCC-CADIVI', 'Q2/2026', 4.35::numeric, 'A', '2026-06-30', 'Ổn định giao cáp điện'),
  ('NCC-HOAPHAT', 'Q2/2026', 4.55::numeric, 'A', '2026-06-30', 'Nguồn thép ổn định'),
  ('NCC-HATIEN', 'Q2/2026', 4.10::numeric, 'A', '2026-06-30', 'Xi măng đúng hạn'),
  ('NCC-BINHMINH', 'Q2/2026', 3.85::numeric, 'B', '2026-06-30', 'Ống PVC tốt, phản hồi chậm'),
  ('NCC-LIOA', 'Q2/2026', 3.60::numeric, 'B', '2026-06-30', 'Giá cạnh tranh, lead time trung bình'),
  ('NCC-JOTUN', 'Q2/2026', 4.20::numeric, 'A', '2026-06-30', 'Sơn & hỗ trợ kỹ thuật tốt')
) AS v(scode, period, score, grade, eval_at, note)
JOIN warehouse.suppliers s ON s."SupplierCode" = v.scode AND s."DeletedAt" IS NULL
WHERE NOT EXISTS (
  SELECT 1 FROM warehouse.supplier_evaluations e
  WHERE e."SupplierId" = s."Id" AND e."Period" = v.period AND e."DeletedAt" IS NULL
);

INSERT INTO warehouse.supplier_delivery_evaluations (
  "SupplierId", "SupplierNameSnapshot", "PoCode", "ImportTicketCode",
  "ReceivedAt", "DueAt", "Status", "Score", "Grade", "Note",
  "EvaluatedById", "EvaluatedByName", "EvaluatedAt",
  "CreatedAt", "UpdatedAt", "ProjectCode",
  "Quality", "Delivery", "Price", "Service", "Compliance"
)
SELECT
  s."Id", s."SupplierName", v.po, v.ticket,
  NOW() - INTERVAL '5 days', NOW() - INTERVAL '4 days',
  'completed', v.score, v.grade, v.note,
  'aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee'::uuid, 'Phạm Minh Châu',
  (CURRENT_DATE - 4), NOW(), NOW(), 'DA-2026-NV-TOWER',
  v.q, v.d, v.p, v.svc, v.c
FROM (VALUES
  ('NCC-CADIVI', 'PO-NV-0001', 'NK-NV-0001', 8.60::numeric, 'B',
   'Giao đủ, chất lượng ổn', 9::numeric, 8::numeric, 9::numeric, 8::numeric, 9::numeric),
  ('NCC-HOAPHAT', 'PO-NV-0002', 'NK-NV-0002', 8.20::numeric, 'B',
   'Giao thép đúng PO', 8::numeric, 8::numeric, 8::numeric, 8::numeric, 9::numeric),
  ('NCC-HATIEN', 'PO-NV-0003', 'NK-NV-0003', 7.80::numeric, 'C',
   'Giao 1 phần, còn lại đúng kế hoạch', 8::numeric, 7::numeric, 8::numeric, 8::numeric, 8::numeric)
) AS v(scode, po, ticket, score, grade, note, q, d, p, svc, c)
JOIN warehouse.suppliers s ON s."SupplierCode" = v.scode AND s."DeletedAt" IS NULL
WHERE NOT EXISTS (
  SELECT 1 FROM warehouse.supplier_delivery_evaluations e
  WHERE e."PoCode" = v.po AND e."DeletedAt" IS NULL
);

COMMIT;
