-- Wave D+ : Fix encoding NCC + dọn rác + chuẩn bị hợp đồng theo workflow PO
-- Apply: docker cp + psql -f (UTF-8)

BEGIN;

-- ---------------------------------------------------------------------------
-- 1) UTF-8: nhóm NCC (update theo Id hiện có)
-- ---------------------------------------------------------------------------
UPDATE warehouse.supplier_groups SET "Name" = 'Thép & kim loại', "Description" = 'Thép hình, thép cuộn, tôn, inox', "UpdatedAt" = NOW() WHERE "Id" = 3;
UPDATE warehouse.supplier_groups SET "Name" = 'Xi măng & vật liệu xây', "Description" = 'Xi măng, cát, đá, gạch', "UpdatedAt" = NOW() WHERE "Id" = 4;
UPDATE warehouse.supplier_groups SET "Name" = 'Điện & cáp', "Description" = 'Dây cáp, thiết bị điện công nghiệp', "UpdatedAt" = NOW() WHERE "Id" = 5;
UPDATE warehouse.supplier_groups SET "Name" = 'Ống nhựa & cấp thoát nước', "Description" = 'PVC, PPR, phụ kiện đường ống', "UpdatedAt" = NOW() WHERE "Id" = 6;
UPDATE warehouse.supplier_groups SET "Name" = 'Sơn & hoàn thiện', "Description" = 'Sơn, keo, vật tư hoàn thiện nội thất', "UpdatedAt" = NOW() WHERE "Id" = 7;
UPDATE warehouse.supplier_groups SET "Name" = 'Thiết bị M&E', "Description" = 'Điện nước, HVAC, thiết bị cơ điện', "UpdatedAt" = NOW() WHERE "Id" = 8;

-- ---------------------------------------------------------------------------
-- 2) UTF-8: tên NCC + payment terms + blacklist reason
-- ---------------------------------------------------------------------------
UPDATE warehouse.suppliers SET "SupplierName" = 'CTCP Dây cáp điện Việt Nam (CADIVI)', "PaymentTerms" = 'Công nợ 45 ngày, chuyển khoản', "UpdatedAt" = NOW() WHERE "SupplierCode" = 'NCC-CADIVI';
UPDATE warehouse.suppliers SET "SupplierName" = 'Tập đoàn Hòa Phát', "PaymentTerms" = 'Thanh toán 30% khi đặt, 70% khi giao', "UpdatedAt" = NOW() WHERE "SupplierCode" = 'NCC-HOAPHAT';
UPDATE warehouse.suppliers SET "SupplierName" = 'CTCP Xi măng Hà Tiên 1', "PaymentTerms" = 'Công nợ 30 ngày', "UpdatedAt" = NOW() WHERE "SupplierCode" = 'NCC-HATIEN';
UPDATE warehouse.suppliers SET "SupplierName" = 'CTCP Nhựa Bình Minh', "PaymentTerms" = 'Công nợ 30 ngày', "UpdatedAt" = NOW() WHERE "SupplierCode" = 'NCC-BINHMINH';
UPDATE warehouse.suppliers SET "SupplierName" = 'Tổng công ty Viglacera', "PaymentTerms" = 'Công nợ 30 ngày theo hợp đồng', "UpdatedAt" = NOW() WHERE "SupplierCode" = 'NCC-VIGLACERA';
UPDATE warehouse.suppliers SET "SupplierName" = 'Jotun Việt Nam', "PaymentTerms" = 'Thanh toán trước khi giao hàng', "UpdatedAt" = NOW() WHERE "SupplierCode" = 'NCC-JOTUN';
UPDATE warehouse.suppliers SET "SupplierName" = 'CTCP Gỗ An Cường', "PaymentTerms" = 'Công nợ 30 ngày', "UpdatedAt" = NOW() WHERE "SupplierCode" = 'NCC-ANCUONG';
UPDATE warehouse.suppliers SET "SupplierName" = 'CTCP Thiết bị điện Lioa', "PaymentTerms" = 'Công nợ 30 ngày', "UpdatedAt" = NOW() WHERE "SupplierCode" = 'NCC-LIOA';
UPDATE warehouse.suppliers SET "SupplierName" = 'CTCP Thép Đông Nam', "PaymentTerms" = 'Công nợ 30 ngày', "UpdatedAt" = NOW() WHERE "SupplierCode" = 'NCC-DONGNAM';
UPDATE warehouse.suppliers SET "SupplierName" = 'CTCP Quốc tế Sơn Hà', "UpdatedAt" = NOW() WHERE "SupplierCode" = 'NCC-SONHA';
UPDATE warehouse.suppliers SET "SupplierName" = 'Công ty TNHH Kim loại Việt Á', "UpdatedAt" = NOW() WHERE "SupplierCode" = 'NCC-KIMLOAI';
UPDATE warehouse.suppliers SET
  "SupplierName" = 'Công ty TNHH Ống nhựa Xuân Nghiêm',
  "BlacklistReason" = 'Giao hàng chậm liên tục 3 lần trong Q2/2026, không phản hồi khi được nhắc nhở, ảnh hưởng tiến độ công trình.',
  "BlacklistedByName" = 'Nguyễn Văn Khoa',
  "UpdatedAt" = NOW()
WHERE "SupplierCode" = 'NCC-ONGNHUA-XN';
UPDATE warehouse.suppliers SET
  "SupplierName" = 'Công ty TNHH Thép Miền Trung',
  "BlacklistReason" = 'Phát hiện lô thép không đạt TCVN khi nghiệm thu; từ chối nhận hàng và yêu cầu đổi nhà cung cấp.',
  "BlacklistedByName" = 'Nguyễn Văn Khoa',
  "UpdatedAt" = NOW()
WHERE "SupplierCode" = 'NCC-THEP-MT';

-- ---------------------------------------------------------------------------
-- 3) UTF-8: liên hệ NCC (cập nhật theo SupplierCode + Phone)
-- ---------------------------------------------------------------------------
UPDATE warehouse.supplier_contacts c
SET "FullName" = v.full_name, "Position" = v.position, "Email" = v.email, "Note" = v.note, "UpdatedAt" = NOW()
FROM (VALUES
  ('NCC-CADIVI', '0908123456', 'Nguyễn Thanh Sơn', 'Trưởng phòng Kinh doanh miền Nam', 'thanhsondkd@cadivi.vn', 'Liên hệ báo giá cáp lực'),
  ('NCC-HOAPHAT', '0912333444', 'Lê Minh Quân', 'Giám đốc bán hàng khu vực', 'minhquan.sales@hoaphat.com.vn', 'Thép xây dựng & tôn'),
  ('NCC-HATIEN', '0933555666', 'Trần Thị Lan', 'Chuyên viên CSKH', 'lan.tt@hatien1.com.vn', NULL),
  ('NCC-BINHMINH', '0977888999', 'Phạm Đức Anh', 'Sale Engineer', 'ducanh@binhminhplastic.com.vn', 'Ống PVC / PPR'),
  ('NCC-VIGLACERA', '0966123789', 'Hoàng Văn Đức', 'Phụ trách dự án', 'duc.hv@viglacera.com.vn', NULL),
  ('NCC-JOTUN', '0988001122', 'Ngô Thị Hạnh', 'Technical Sales', 'hanh.ngo@jotun.com', 'Tư vấn hệ sơn công trình'),
  ('NCC-ANCUONG', '0909333444', 'Đỗ Quang Huy', 'Kinh doanh dự án', 'huy.dq@ancuong.com', NULL),
  ('NCC-LIOA', '0944555666', 'Vũ Thị Nga', 'CSKH doanh nghiệp', 'nga.vu@lioa.com.vn', NULL),
  ('NCC-DONGNAM', '0911002233', 'Bùi Văn Khoa', 'Sale', 'khoa.bv@thepdongnam.vn', 'Chờ duyệt hồ sơ'),
  ('NCC-SONHA', '0922003344', 'Lý Thị Phương', 'Sale Admin', 'phuong.lt@sonha.com.vn', NULL),
  ('NCC-KIMLOAI', '0903111222', 'Mai Quốc Việt', 'Giám đốc', 'viet.mq@kimloaivieta.vn', 'Hồ sơ bị từ chối'),
  ('NCC-ONGNHUA-XN', '0901000002', 'Trịnh Văn Long', 'Sale', 'long.tv@xuannghiem.vn', 'Đã blacklist'),
  ('NCC-THEP-MT', '0901000008', 'Hà Thị Yến', 'Kinh doanh', 'yen.ht@thepmientrung.vn', 'Đã blacklist')
) AS v(scode, phone, full_name, position, email, note)
JOIN warehouse.suppliers s ON s."SupplierCode" = v.scode AND s."DeletedAt" IS NULL
WHERE c."SupplierId" = s."Id" AND c."DeletedAt" IS NULL AND c."Phone" = v.phone;

-- Re-link group members (tên nhóm đã đúng UTF-8)
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
-- 4) Dọn dự án rác (giữ DA-2026-NV-TOWER)
-- ---------------------------------------------------------------------------
UPDATE warehouse.projects
SET "DeletedAt" = NOW(), "UpdatedAt" = NOW()
WHERE "DeletedAt" IS NULL
  AND "ProjectCode" IN ('DA-2026-76', 'DA-2026-496', 'DA-2026-680');

UPDATE warehouse.purchase_plans
SET "DeletedAt" = NOW(), "UpdatedAt" = NOW()
WHERE "DeletedAt" IS NULL
  AND "ProjectCode" IN ('DA-2026-76', 'DA-2026-496', 'DA-2026-680', 'DEMO-PUR-PROJ');

UPDATE warehouse.purchase_orders
SET "DeletedAt" = NOW(), "UpdatedAt" = NOW()
WHERE "DeletedAt" IS NULL
  AND "ProjectCode" IN ('DA-2026-76', 'DA-2026-496', 'DA-2026-680', 'DEMO-PUR-PROJ');

UPDATE warehouse.purchase_estimates
SET "DeletedAt" = NOW(), "UpdatedAt" = NOW()
WHERE "DeletedAt" IS NULL
  AND "ProjectCode" IN ('DA-2026-76', 'DA-2026-496', 'DA-2026-680', 'DEMO-PUR-PROJ');

-- ---------------------------------------------------------------------------
-- 5) Dọn DEMO-SKU + đổi nhãn danh mục "Vật tư demo" (không xóa VT-* đang dùng PO)
-- ---------------------------------------------------------------------------
UPDATE warehouse.materials
SET "DeletedAt" = NOW(), "UpdatedAt" = NOW()
WHERE "DeletedAt" IS NULL
  AND ("MaterialCode" ILIKE 'DEMO-SKU%' OR "MaterialCode" ILIKE 'VT-DEMO%');

UPDATE warehouse.material_categories
SET "Name" = 'Vật tư công trình', "UpdatedAt" = NOW()
WHERE "DeletedAt" IS NULL AND "Code" = 'DEMO-DANH-MUC';

UPDATE warehouse.material_categories
SET "DeletedAt" = NOW(), "UpdatedAt" = NOW()
WHERE "DeletedAt" IS NULL
  AND "Code" LIKE 'DEMO-PATH-%'
  AND NOT EXISTS (
    SELECT 1 FROM warehouse.materials m
    WHERE m."CategoryId" = warehouse.material_categories."Id" AND m."DeletedAt" IS NULL
  );

-- ---------------------------------------------------------------------------
-- 6) Hồ sơ HĐ trên PO đã duyệt (workflow: đính kèm contract → import HĐ)
-- ---------------------------------------------------------------------------
INSERT INTO warehouse.purchase_order_attachments (
  "Id", "PoCode", "DocType", "FileName", "StorageKey", "UploadedAt", "UploadedByName"
)
SELECT gen_random_uuid(), v.po, 'contract', v.fname, v.skey, NOW(), 'Trần Hoàng Nam'
FROM (VALUES
  ('PO-NV-0001', 'HD-CADIVI-PO-NV-0001.pdf', 'purchasing/docs/PO-NV-0001/contract.pdf'),
  ('PO-NV-0003', 'HD-HATIEN-PO-NV-0003.pdf', 'purchasing/docs/PO-NV-0003/contract.pdf')
) AS v(po, fname, skey)
WHERE EXISTS (SELECT 1 FROM warehouse.purchase_orders o WHERE o."Code" = v.po AND o."DeletedAt" IS NULL)
  AND NOT EXISTS (
    SELECT 1 FROM warehouse.purchase_order_attachments a
    WHERE a."PoCode" = v.po AND a."DocType" = 'contract'
  );

COMMIT;

INSERT INTO warehouse.wms_settings ("Key", "Value", "CreatedAt", "UpdatedAt")
SELECT 'data_cleanup_encoding_v2', 'wave-d-plus-2026-08', NOW(), NOW()
WHERE NOT EXISTS (SELECT 1 FROM warehouse.wms_settings WHERE "Key" = 'data_cleanup_encoding_v2');

UPDATE warehouse.wms_settings
SET "Value" = 'wave-d-plus-2026-08', "UpdatedAt" = NOW()
WHERE "Key" = 'data_cleanup_encoding_v2';
