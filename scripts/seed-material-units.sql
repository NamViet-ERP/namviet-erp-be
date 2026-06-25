-- Material units (master data) — idempotent, SortOrder = A→Z (vi)
BEGIN;

INSERT INTO warehouse.material_units ("Name", "SortOrder", "IsActive", "CreatedAt")
SELECT 'Bộ', 1, TRUE, NOW()
WHERE NOT EXISTS (SELECT 1 FROM warehouse.material_units WHERE "Name" = 'Bộ');

INSERT INTO warehouse.material_units ("Name", "SortOrder", "IsActive", "CreatedAt")
SELECT 'Cái', 2, TRUE, NOW()
WHERE NOT EXISTS (SELECT 1 FROM warehouse.material_units WHERE "Name" = 'Cái');

INSERT INTO warehouse.material_units ("Name", "SortOrder", "IsActive", "CreatedAt")
SELECT 'Cuộn', 3, TRUE, NOW()
WHERE NOT EXISTS (SELECT 1 FROM warehouse.material_units WHERE "Name" = 'Cuộn');

INSERT INTO warehouse.material_units ("Name", "SortOrder", "IsActive", "CreatedAt")
SELECT 'Hộp', 4, TRUE, NOW()
WHERE NOT EXISTS (SELECT 1 FROM warehouse.material_units WHERE "Name" = 'Hộp');

INSERT INTO warehouse.material_units ("Name", "SortOrder", "IsActive", "CreatedAt")
SELECT 'Kg', 5, TRUE, NOW()
WHERE NOT EXISTS (SELECT 1 FROM warehouse.material_units WHERE "Name" = 'Kg');

INSERT INTO warehouse.material_units ("Name", "SortOrder", "IsActive", "CreatedAt")
SELECT 'Lít', 6, TRUE, NOW()
WHERE NOT EXISTS (SELECT 1 FROM warehouse.material_units WHERE "Name" = 'Lít');

INSERT INTO warehouse.material_units ("Name", "SortOrder", "IsActive", "CreatedAt")
SELECT 'Mét', 7, TRUE, NOW()
WHERE NOT EXISTS (SELECT 1 FROM warehouse.material_units WHERE "Name" = 'Mét');

INSERT INTO warehouse.material_units ("Name", "SortOrder", "IsActive", "CreatedAt")
SELECT 'Mét khối', 8, TRUE, NOW()
WHERE NOT EXISTS (SELECT 1 FROM warehouse.material_units WHERE "Name" = 'Mét khối');

INSERT INTO warehouse.material_units ("Name", "SortOrder", "IsActive", "CreatedAt")
SELECT 'Tấm', 9, TRUE, NOW()
WHERE NOT EXISTS (SELECT 1 FROM warehouse.material_units WHERE "Name" = 'Tấm');

INSERT INTO warehouse.material_units ("Name", "SortOrder", "IsActive", "CreatedAt")
SELECT 'Tấn', 10, TRUE, NOW()
WHERE NOT EXISTS (SELECT 1 FROM warehouse.material_units WHERE "Name" = 'Tấn');

INSERT INTO warehouse.material_units ("Name", "SortOrder", "IsActive", "CreatedAt")
SELECT 'Thùng', 11, TRUE, NOW()
WHERE NOT EXISTS (SELECT 1 FROM warehouse.material_units WHERE "Name" = 'Thùng');

COMMIT;
