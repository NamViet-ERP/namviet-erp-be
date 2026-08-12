-- Wave B: Nhóm NCC → Nhà cung cấp → Liên hệ (+ blacklist)
-- Dữ liệu thật ngành xây dựng / điện / vật tư VN. Không dùng DEMO-*.
-- Idempotent theo Name (nhóm) / SupplierCode (NCC) / (SupplierId+FullName+Phone) liên hệ.

BEGIN;

-- ---------------------------------------------------------------------------
-- 0) Dọn nhóm rác / placeholder (không ảnh hưởng NCC thật)
-- ---------------------------------------------------------------------------
UPDATE warehouse.supplier_groups
SET "DeletedAt" = NOW(), "UpdatedAt" = NOW()
WHERE "DeletedAt" IS NULL
  AND (
    "Name" IN ('1234567', 'sdfghj', 'qwerty', 'dfghj')
    OR lower("Name") LIKE 'test%'
    OR lower("Name") LIKE 'demo%'
  );

-- Soft-delete NCC demo (giữ lịch sử PO nếu có; Wave D sẽ xử lý sâu hơn)
UPDATE warehouse.suppliers
SET "DeletedAt" = NOW(), "UpdatedAt" = NOW()
WHERE "DeletedAt" IS NULL
  AND (
    "SupplierCode" LIKE 'NCC-DEMO-%'
    OR "SupplierCode" LIKE 'NCC-SUP-%'
    OR "SupplierName" ILIKE '%demo%'
  );

UPDATE warehouse.supplier_contacts c
SET "DeletedAt" = NOW(), "UpdatedAt" = NOW()
FROM warehouse.suppliers s
WHERE c."SupplierId" = s."Id"
  AND c."DeletedAt" IS NULL
  AND s."DeletedAt" IS NOT NULL
  AND (
    s."SupplierCode" LIKE 'NCC-DEMO-%'
    OR s."SupplierCode" LIKE 'NCC-SUP-%'
    OR s."SupplierName" ILIKE '%demo%'
  );

DELETE FROM warehouse.supplier_group_members m
USING warehouse.suppliers s
WHERE m."SupplierId" = s."Id"
  AND (
    s."SupplierCode" LIKE 'NCC-DEMO-%'
    OR s."SupplierCode" LIKE 'NCC-SUP-%'
    OR s."SupplierName" ILIKE '%demo%'
  );

-- ---------------------------------------------------------------------------
-- 1) Nhóm nhà cung cấp
-- ---------------------------------------------------------------------------
INSERT INTO warehouse.supplier_groups ("Name", "Description", "CreatedAt", "UpdatedAt")
SELECT v.name, v.description, NOW(), NOW()
FROM (VALUES
  ('Thép & kim loại', 'Thép hình, thép cuộn, tôn, inox'),
  ('Xi măng & vật liệu xây', 'Xi măng, cát, đá, gạch'),
  ('Điện & cáp', 'Dây cáp, thiết bị điện công nghiệp'),
  ('Ống nhựa & cấp thoát nước', 'PVC, PPR, phụ kiện đường ống'),
  ('Sơn & hoàn thiện', 'Sơn, keo, vật tư hoàn thiện nội thất'),
  ('Thiết bị M&E', 'Điện nước, HVAC, thiết bị cơ điện')
) AS v(name, description)
WHERE NOT EXISTS (
  SELECT 1 FROM warehouse.supplier_groups g
  WHERE g."Name" = v.name AND g."DeletedAt" IS NULL
);

-- ---------------------------------------------------------------------------
-- 2) Nhà cung cấp (đủ trạng thái + hạng)
-- ---------------------------------------------------------------------------
INSERT INTO warehouse.suppliers (
  "SupplierCode", "SupplierName", "ContactPhone", "MaxDebtDays", "PaymentTerms",
  "Rating", "ApprovalStatus",
  "IsBlacklisted", "BlacklistedAt", "BlacklistedByName", "BlacklistReason",
  "CreatedAt", "UpdatedAt"
)
SELECT
  v.code, v.name, v.phone, v.debt_days, v.terms,
  v.rating, v.status,
  v.blacklisted, CASE WHEN v.blacklisted THEN NOW() ELSE NULL END,
  CASE WHEN v.blacklisted THEN 'Nguyễn Văn Khoa' ELSE NULL END,
  v.bl_reason,
  NOW(), NOW()
FROM (VALUES
  -- approved A/B
  ('NCC-CADIVI', 'CTCP Dây cáp điện Việt Nam (CADIVI)', '02838291234', 45, 'Công nợ 45 ngày, chuyển khoản',
   'A', 'approved', false, NULL::text),
  ('NCC-HOAPHAT', 'Tập đoàn Hòa Phát', '02462688668', 30, 'Thanh toán 30% khi đặt, 70% khi giao',
   'A', 'approved', false, NULL::text),
  ('NCC-HATIEN', 'CTCP Xi măng Hà Tiên 1', '02838295151', 30, 'Công nợ 30 ngày',
   'B', 'approved', false, NULL::text),
  ('NCC-BINHMINH', 'CTCP Nhựa Bình Minh', '02838652727', 30, 'Công nợ 30 ngày',
   'B', 'approved', false, NULL::text),
  ('NCC-VIGLACERA', 'Tổng công ty Viglacera', '02438252288', 30, 'Công nợ 30 ngày theo hợp đồng',
   'B', 'approved', false, NULL::text),
  ('NCC-JOTUN', 'Jotun Việt Nam', '02854135555', 21, 'Thanh toán trước khi giao hàng',
   'A', 'approved', false, NULL::text),
  ('NCC-ANCUONG', 'CTCP Gỗ An Cường', '02837560505', 30, 'Công nợ 30 ngày',
   'B', 'approved', false, NULL::text),
  ('NCC-LIOA', 'CTCP Thiết bị điện Lioa', '02438562266', 30, 'Công nợ 30 ngày',
   'C', 'approved', false, NULL::text),
  -- new (chờ duyệt)
  ('NCC-DONGNAM', 'CTCP Thép Đông Nam', '02835121212', 30, 'Công nợ 30 ngày',
   'D', 'new', false, NULL::text),
  ('NCC-SONHA', 'CTCP Quốc tế Sơn Hà', '02437555666', 30, NULL,
   'D', 'new', false, NULL::text),
  -- rejected
  ('NCC-KIMLOAI', 'Công ty TNHH Kim loại Việt Á', '0903111222', 15, NULL,
   'D', 'rejected', false, NULL::text),
  -- approved + blacklist
  ('NCC-ONGNHUA-XN', 'Công ty TNHH Ống nhựa Xuân Nghiêm', '0901000002', 30, 'Công nợ 30 ngày',
   'C', 'approved', true,
   'Giao hàng chậm liên tục 3 lần trong Q2/2026, không phản hồi khi được nhắc nhở, ảnh hưởng tiến độ công trình.'),
  ('NCC-THEP-MT', 'Công ty TNHH Thép Miền Trung', '0901000008', 30, 'Công nợ 30 ngày',
   'D', 'approved', true,
   'Phát hiện lô thép không đạt TCVN khi nghiệm thu; từ chối nhận hàng và yêu cầu đổi nhà cung cấp.')
) AS v(code, name, phone, debt_days, terms, rating, status, blacklisted, bl_reason)
WHERE NOT EXISTS (
  SELECT 1 FROM warehouse.suppliers s
  WHERE s."SupplierCode" = v.code AND s."DeletedAt" IS NULL
);

-- Nếu đã tồn tại (cùng mã) thì cập nhật trạng thái/hạng thật (không tạo trùng)
UPDATE warehouse.suppliers s
SET
  "SupplierName" = v.name,
  "ContactPhone" = v.phone,
  "MaxDebtDays" = v.debt_days,
  "PaymentTerms" = v.terms,
  "Rating" = v.rating,
  "ApprovalStatus" = v.status,
  "IsBlacklisted" = v.blacklisted,
  "BlacklistedAt" = CASE WHEN v.blacklisted THEN COALESCE(s."BlacklistedAt", NOW()) ELSE NULL END,
  "BlacklistedByName" = CASE WHEN v.blacklisted THEN COALESCE(s."BlacklistedByName", 'Nguyễn Văn Khoa') ELSE NULL END,
  "BlacklistReason" = v.bl_reason,
  "DeletedAt" = NULL,
  "UpdatedAt" = NOW()
FROM (VALUES
  ('NCC-CADIVI', 'CTCP Dây cáp điện Việt Nam (CADIVI)', '02838291234', 45, 'Công nợ 45 ngày, chuyển khoản', 'A', 'approved', false, NULL::text),
  ('NCC-HOAPHAT', 'Tập đoàn Hòa Phát', '02462688668', 30, 'Thanh toán 30% khi đặt, 70% khi giao', 'A', 'approved', false, NULL::text),
  ('NCC-HATIEN', 'CTCP Xi măng Hà Tiên 1', '02838295151', 30, 'Công nợ 30 ngày', 'B', 'approved', false, NULL::text),
  ('NCC-BINHMINH', 'CTCP Nhựa Bình Minh', '02838652727', 30, 'Công nợ 30 ngày', 'B', 'approved', false, NULL::text),
  ('NCC-VIGLACERA', 'Tổng công ty Viglacera', '02438252288', 30, 'Công nợ 30 ngày theo hợp đồng', 'B', 'approved', false, NULL::text),
  ('NCC-JOTUN', 'Jotun Việt Nam', '02854135555', 21, 'Thanh toán trước khi giao hàng', 'A', 'approved', false, NULL::text),
  ('NCC-ANCUONG', 'CTCP Gỗ An Cường', '02837560505', 30, 'Công nợ 30 ngày', 'B', 'approved', false, NULL::text),
  ('NCC-LIOA', 'CTCP Thiết bị điện Lioa', '02438562266', 30, 'Công nợ 30 ngày', 'C', 'approved', false, NULL::text),
  ('NCC-DONGNAM', 'CTCP Thép Đông Nam', '02835121212', 30, 'Công nợ 30 ngày', 'D', 'new', false, NULL::text),
  ('NCC-SONHA', 'CTCP Quốc tế Sơn Hà', '02437555666', 30, NULL, 'D', 'new', false, NULL::text),
  ('NCC-KIMLOAI', 'Công ty TNHH Kim loại Việt Á', '0903111222', 15, NULL, 'D', 'rejected', false, NULL::text),
  ('NCC-ONGNHUA-XN', 'Công ty TNHH Ống nhựa Xuân Nghiêm', '0901000002', 30, 'Công nợ 30 ngày', 'C', 'approved', true,
   'Giao hàng chậm liên tục 3 lần trong Q2/2026, không phản hồi khi được nhắc nhở, ảnh hưởng tiến độ công trình.'),
  ('NCC-THEP-MT', 'Công ty TNHH Thép Miền Trung', '0901000008', 30, 'Công nợ 30 ngày', 'D', 'approved', true,
   'Phát hiện lô thép không đạt TCVN khi nghiệm thu; từ chối nhận hàng và yêu cầu đổi nhà cung cấp.')
) AS v(code, name, phone, debt_days, terms, rating, status, blacklisted, bl_reason)
WHERE s."SupplierCode" = v.code;

-- ---------------------------------------------------------------------------
-- 3) Gán nhóm
-- ---------------------------------------------------------------------------
INSERT INTO warehouse.supplier_group_members ("SupplierGroupId", "SupplierId", "CreatedAt")
SELECT g."Id", s."Id", NOW()
FROM (VALUES
  ('Điện & cáp', 'NCC-CADIVI'),
  ('Điện & cáp', 'NCC-LIOA'),
  ('Thép & kim loại', 'NCC-HOAPHAT'),
  ('Thép & kim loại', 'NCC-DONGNAM'),
  ('Thép & kim loại', 'NCC-THEP-MT'),
  ('Thép & kim loại', 'NCC-KIMLOAI'),
  ('Xi măng & vật liệu xây', 'NCC-HATIEN'),
  ('Xi măng & vật liệu xây', 'NCC-VIGLACERA'),
  ('Ống nhựa & cấp thoát nước', 'NCC-BINHMINH'),
  ('Ống nhựa & cấp thoát nước', 'NCC-ONGNHUA-XN'),
  ('Ống nhựa & cấp thoát nước', 'NCC-SONHA'),
  ('Sơn & hoàn thiện', 'NCC-JOTUN'),
  ('Sơn & hoàn thiện', 'NCC-ANCUONG'),
  ('Thiết bị M&E', 'NCC-LIOA'),
  ('Thiết bị M&E', 'NCC-CADIVI')
) AS map(group_name, supplier_code)
JOIN warehouse.supplier_groups g ON g."Name" = map.group_name AND g."DeletedAt" IS NULL
JOIN warehouse.suppliers s ON s."SupplierCode" = map.supplier_code AND s."DeletedAt" IS NULL
WHERE NOT EXISTS (
  SELECT 1 FROM warehouse.supplier_group_members m
  WHERE m."SupplierGroupId" = g."Id" AND m."SupplierId" = s."Id"
);

-- ---------------------------------------------------------------------------
-- 4) Liên hệ NCC (mỗi NCC 1 liên hệ chính)
-- ---------------------------------------------------------------------------
INSERT INTO warehouse.supplier_contacts (
  "SupplierId", "FullName", "Position", "Phone", "Email", "Note", "IsPrimary", "Role",
  "CreatedAt", "UpdatedAt"
)
SELECT
  s."Id", c.full_name, c.position, c.phone, c.email, c.note, true, 'primary',
  NOW(), NOW()
FROM (VALUES
  ('NCC-CADIVI', 'Nguyễn Thanh Sơn', 'Trưởng phòng Kinh doanh miền Nam', '0908123456', 'thanhsondkd@cadivi.vn', 'Liên hệ báo giá cáp lực'),
  ('NCC-HOAPHAT', 'Lê Minh Quân', 'Giám đốc bán hàng khu vực', '0912333444', 'minhquan.sales@hoaphat.com.vn', 'Thép xây dựng & tôn'),
  ('NCC-HATIEN', 'Trần Thị Lan', 'Chuyên viên CSKH', '0933555666', 'lan.tt@hatien1.com.vn', NULL),
  ('NCC-BINHMINH', 'Phạm Đức Anh', 'Sale Engineer', '0977888999', 'ducanh@binhminhplastic.com.vn', 'Ống PVC / PPR'),
  ('NCC-VIGLACERA', 'Hoàng Văn Đức', 'Phụ trách dự án', '0966123789', 'duc.hv@viglacera.com.vn', NULL),
  ('NCC-JOTUN', 'Ngô Thị Hạnh', 'Technical Sales', '0988001122', 'hanh.ngo@jotun.com', 'Tư vấn hệ sơn công trình'),
  ('NCC-ANCUONG', 'Đỗ Quang Huy', 'Kinh doanh dự án', '0909333444', 'huy.dq@ancuong.com', NULL),
  ('NCC-LIOA', 'Vũ Thị Nga', 'CSKH doanh nghiệp', '0944555666', 'nga.vu@lioa.com.vn', NULL),
  ('NCC-DONGNAM', 'Bùi Văn Khoa', 'Sale', '0911002233', 'khoa.bv@thepdongnam.vn', 'Chờ duyệt hồ sơ'),
  ('NCC-SONHA', 'Lý Thị Phương', 'Sale Admin', '0922003344', 'phuong.lt@sonha.com.vn', NULL),
  ('NCC-KIMLOAI', 'Mai Quốc Việt', 'Giám đốc', '0903111222', 'viet.mq@kimloaivieta.vn', 'Hồ sơ bị từ chối'),
  ('NCC-ONGNHUA-XN', 'Trịnh Văn Long', 'Sale', '0901000002', 'long.tv@xuannghiem.vn', 'Đã blacklist'),
  ('NCC-THEP-MT', 'Hà Thị Yến', 'Kinh doanh', '0901000008', 'yen.ht@thepmientrung.vn', 'Đã blacklist')
) AS c(supplier_code, full_name, position, phone, email, note)
JOIN warehouse.suppliers s ON s."SupplierCode" = c.supplier_code AND s."DeletedAt" IS NULL
WHERE NOT EXISTS (
  SELECT 1 FROM warehouse.supplier_contacts x
  WHERE x."SupplierId" = s."Id"
    AND x."DeletedAt" IS NULL
    AND x."FullName" = c.full_name
);

COMMIT;

-- Marker (tuỳ chọn, để biết đã seed Wave B)
INSERT INTO warehouse.wms_settings ("Key", "Value", "CreatedAt", "UpdatedAt")
SELECT 'supplier_master_seed_v1', 'wave-b-real-2026-08', NOW(), NOW()
WHERE NOT EXISTS (
  SELECT 1 FROM warehouse.wms_settings WHERE "Key" = 'supplier_master_seed_v1'
);

UPDATE warehouse.wms_settings
SET "Value" = 'wave-b-real-2026-08', "UpdatedAt" = NOW()
WHERE "Key" = 'supplier_master_seed_v1';
