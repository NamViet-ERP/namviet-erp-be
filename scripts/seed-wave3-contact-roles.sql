-- Apply Role column (same as migration 20260812180000)
ALTER TABLE warehouse.supplier_contacts
    ADD COLUMN IF NOT EXISTS "Role" character varying(20) NOT NULL DEFAULT 'secondary';

UPDATE warehouse.supplier_contacts
SET "Role" = CASE WHEN "IsPrimary" THEN 'primary' ELSE 'secondary' END;

UPDATE warehouse.supplier_contacts
SET "IsPrimary" = ("Role" = 'primary');

CREATE INDEX IF NOT EXISTS "IX_supplier_contacts_SupplierId_Role"
    ON warehouse.supplier_contacts ("SupplierId", "Role")
    WHERE "DeletedAt" IS NULL;

-- Seed Phụ / Backup for key NCC (idempotent by name+supplier)
INSERT INTO warehouse.supplier_contacts (
  "SupplierId", "FullName", "Position", "Phone", "Email", "Note",
  "IsPrimary", "Role", "CreatedAt", "UpdatedAt"
)
SELECT s."Id", v.full_name, v.position, v.phone, v.email, v.note,
       FALSE, v.role, NOW(), NOW()
FROM (VALUES
  ('NCC-CADIVI', 'Trần Thị Lan', 'NV Kinh doanh', '0903123456', 'lan.tran@cadivi.vn', 'Liên hệ phụ báo giá', 'secondary'),
  ('NCC-CADIVI', 'Lê Quốc Huy', 'Kho vận', '0903987654', 'huy.le@cadivi.vn', 'Backup giao nhận', 'backup'),
  ('NCC-HOAPHAT', 'Phạm Văn Đức', 'Sale nội địa', '0912222333', 'duc.pham@hoaphat.vn', NULL, 'secondary'),
  ('NCC-HOAPHAT', 'Ngô Minh Tuấn', 'CSKH', '0912444555', 'tuan.ngo@hoaphat.vn', 'Backup', 'backup'),
  ('NCC-HATIEN', 'Đỗ Thị Hương', 'Kinh doanh miền Nam', '0933111222', 'huong.do@hatien.vn', NULL, 'secondary'),
  ('NCC-BINHMINH', 'Võ Anh Khoa', 'Kỹ thuật ống', '0977555666', 'khoa.vo@binhminhplastic.vn', 'Backup kỹ thuật', 'backup')
) AS v(scode, full_name, position, phone, email, note, role)
JOIN warehouse.suppliers s ON s."SupplierCode" = v.scode AND s."DeletedAt" IS NULL
WHERE NOT EXISTS (
  SELECT 1 FROM warehouse.supplier_contacts c
  WHERE c."SupplierId" = s."Id"
    AND c."FullName" = v.full_name
    AND c."DeletedAt" IS NULL
);

-- Ensure existing primary contacts have Role=primary
UPDATE warehouse.supplier_contacts
SET "Role" = 'primary', "UpdatedAt" = NOW()
WHERE "IsPrimary" = TRUE AND "DeletedAt" IS NULL AND "Role" <> 'primary';
