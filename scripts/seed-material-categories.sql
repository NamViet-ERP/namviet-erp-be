-- Material category tree (master data) — idempotent
-- Regenerate: node scripts/generate-material-categories-sql.mjs
BEGIN;

INSERT INTO warehouse.material_categories ("Code", "Name", "Level", "ParentId", "SortOrder", "IsActive", "CreatedAt", "UpdatedAt")
SELECT 'UNCATEGORIZED', 'Chưa phân loại', 1, NULL, 0, TRUE, NOW(), NOW()
WHERE NOT EXISTS (
  SELECT 1 FROM warehouse.material_categories WHERE "Code" = 'UNCATEGORIZED'
);

-- Level 1
INSERT INTO warehouse.material_categories ("Code", "Name", "Level", "ParentId", "SortOrder", "IsActive", "CreatedAt", "UpdatedAt")
SELECT 'CAT-B14887D27EAF', 'Nguyên vật liệu', 1, NULL, 0, TRUE, NOW(), NOW()
WHERE NOT EXISTS (
  SELECT 1 FROM warehouse.material_categories WHERE "Code" = 'CAT-B14887D27EAF'
);

INSERT INTO warehouse.material_categories ("Code", "Name", "Level", "ParentId", "SortOrder", "IsActive", "CreatedAt", "UpdatedAt")
SELECT 'CAT-8881984856EE', 'Phụ kiện', 1, NULL, 31, TRUE, NOW(), NOW()
WHERE NOT EXISTS (
  SELECT 1 FROM warehouse.material_categories WHERE "Code" = 'CAT-8881984856EE'
);

INSERT INTO warehouse.material_categories ("Code", "Name", "Level", "ParentId", "SortOrder", "IsActive", "CreatedAt", "UpdatedAt")
SELECT 'CAT-8088FD32AA09', 'Thiết bị công nghiệp', 1, NULL, 84, TRUE, NOW(), NOW()
WHERE NOT EXISTS (
  SELECT 1 FROM warehouse.material_categories WHERE "Code" = 'CAT-8088FD32AA09'
);

INSERT INTO warehouse.material_categories ("Code", "Name", "Level", "ParentId", "SortOrder", "IsActive", "CreatedAt", "UpdatedAt")
SELECT 'CAT-B2D8ACC7FD99', 'Thiết bị động cơ', 1, NULL, 109, TRUE, NOW(), NOW()
WHERE NOT EXISTS (
  SELECT 1 FROM warehouse.material_categories WHERE "Code" = 'CAT-B2D8ACC7FD99'
);

INSERT INTO warehouse.material_categories ("Code", "Name", "Level", "ParentId", "SortOrder", "IsActive", "CreatedAt", "UpdatedAt")
SELECT 'CAT-3BF4BA205BF9', 'Vật tư bảo ôn', 1, NULL, 126, TRUE, NOW(), NOW()
WHERE NOT EXISTS (
  SELECT 1 FROM warehouse.material_categories WHERE "Code" = 'CAT-3BF4BA205BF9'
);

INSERT INTO warehouse.material_categories ("Code", "Name", "Level", "ParentId", "SortOrder", "IsActive", "CreatedAt", "UpdatedAt")
SELECT 'CAT-C92720EF8FDD', 'Vật tư phụ', 1, NULL, 135, TRUE, NOW(), NOW()
WHERE NOT EXISTS (
  SELECT 1 FROM warehouse.material_categories WHERE "Code" = 'CAT-C92720EF8FDD'
);

-- Level 2
INSERT INTO warehouse.material_categories ("Code", "Name", "Level", "ParentId", "SortOrder", "IsActive", "CreatedAt", "UpdatedAt")
SELECT 'CAT-B07968B76F4F', 'Hệ thép', 2, p."Id", 1, TRUE, NOW(), NOW()
FROM warehouse.material_categories p
WHERE p."Code" = 'CAT-B14887D27EAF'
  AND NOT EXISTS (
    SELECT 1 FROM warehouse.material_categories WHERE "Code" = 'CAT-B07968B76F4F'
  );

INSERT INTO warehouse.material_categories ("Code", "Name", "Level", "ParentId", "SortOrder", "IsActive", "CreatedAt", "UpdatedAt")
SELECT 'CAT-EBAF0680E36C', 'Hệ inox', 2, p."Id", 18, TRUE, NOW(), NOW()
FROM warehouse.material_categories p
WHERE p."Code" = 'CAT-B14887D27EAF'
  AND NOT EXISTS (
    SELECT 1 FROM warehouse.material_categories WHERE "Code" = 'CAT-EBAF0680E36C'
  );

INSERT INTO warehouse.material_categories ("Code", "Name", "Level", "ParentId", "SortOrder", "IsActive", "CreatedAt", "UpdatedAt")
SELECT 'CAT-11C6F80B6F15', 'Phụ kiện lắp đặt', 2, p."Id", 32, TRUE, NOW(), NOW()
FROM warehouse.material_categories p
WHERE p."Code" = 'CAT-8881984856EE'
  AND NOT EXISTS (
    SELECT 1 FROM warehouse.material_categories WHERE "Code" = 'CAT-11C6F80B6F15'
  );

INSERT INTO warehouse.material_categories ("Code", "Name", "Level", "ParentId", "SortOrder", "IsActive", "CreatedAt", "UpdatedAt")
SELECT 'CAT-9DD96328857A', 'Phụ kiện chống sét', 2, p."Id", 64, TRUE, NOW(), NOW()
FROM warehouse.material_categories p
WHERE p."Code" = 'CAT-8881984856EE'
  AND NOT EXISTS (
    SELECT 1 FROM warehouse.material_categories WHERE "Code" = 'CAT-9DD96328857A'
  );

INSERT INTO warehouse.material_categories ("Code", "Name", "Level", "ParentId", "SortOrder", "IsActive", "CreatedAt", "UpdatedAt")
SELECT 'CAT-0925732B4D5E', 'Phụ kiện đo', 2, p."Id", 74, TRUE, NOW(), NOW()
FROM warehouse.material_categories p
WHERE p."Code" = 'CAT-8881984856EE'
  AND NOT EXISTS (
    SELECT 1 FROM warehouse.material_categories WHERE "Code" = 'CAT-0925732B4D5E'
  );

INSERT INTO warehouse.material_categories ("Code", "Name", "Level", "ParentId", "SortOrder", "IsActive", "CreatedAt", "UpdatedAt")
SELECT 'CAT-7FE0F168DC11', 'Phụ kiện cố định', 2, p."Id", 77, TRUE, NOW(), NOW()
FROM warehouse.material_categories p
WHERE p."Code" = 'CAT-8881984856EE'
  AND NOT EXISTS (
    SELECT 1 FROM warehouse.material_categories WHERE "Code" = 'CAT-7FE0F168DC11'
  );

INSERT INTO warehouse.material_categories ("Code", "Name", "Level", "ParentId", "SortOrder", "IsActive", "CreatedAt", "UpdatedAt")
SELECT 'CAT-CB39B0425853', 'Phụ kiện khác', 2, p."Id", 80, TRUE, NOW(), NOW()
FROM warehouse.material_categories p
WHERE p."Code" = 'CAT-8881984856EE'
  AND NOT EXISTS (
    SELECT 1 FROM warehouse.material_categories WHERE "Code" = 'CAT-CB39B0425853'
  );

INSERT INTO warehouse.material_categories ("Code", "Name", "Level", "ParentId", "SortOrder", "IsActive", "CreatedAt", "UpdatedAt")
SELECT 'CAT-CA49A32427D5', 'Van', 2, p."Id", 85, TRUE, NOW(), NOW()
FROM warehouse.material_categories p
WHERE p."Code" = 'CAT-8088FD32AA09'
  AND NOT EXISTS (
    SELECT 1 FROM warehouse.material_categories WHERE "Code" = 'CAT-CA49A32427D5'
  );

INSERT INTO warehouse.material_categories ("Code", "Name", "Level", "ParentId", "SortOrder", "IsActive", "CreatedAt", "UpdatedAt")
SELECT 'CAT-5BC33C4614E9', 'Lọc y', 2, p."Id", 93, TRUE, NOW(), NOW()
FROM warehouse.material_categories p
WHERE p."Code" = 'CAT-8088FD32AA09'
  AND NOT EXISTS (
    SELECT 1 FROM warehouse.material_categories WHERE "Code" = 'CAT-5BC33C4614E9'
  );

INSERT INTO warehouse.material_categories ("Code", "Name", "Level", "ParentId", "SortOrder", "IsActive", "CreatedAt", "UpdatedAt")
SELECT 'CAT-DDDF37D1D5CA', 'Kính thuy sáng', 2, p."Id", 96, TRUE, NOW(), NOW()
FROM warehouse.material_categories p
WHERE p."Code" = 'CAT-8088FD32AA09'
  AND NOT EXISTS (
    SELECT 1 FROM warehouse.material_categories WHERE "Code" = 'CAT-DDDF37D1D5CA'
  );

INSERT INTO warehouse.material_categories ("Code", "Name", "Level", "ParentId", "SortOrder", "IsActive", "CreatedAt", "UpdatedAt")
SELECT 'CAT-6F3924E46A39', 'Bẫy hơi', 2, p."Id", 99, TRUE, NOW(), NOW()
FROM warehouse.material_categories p
WHERE p."Code" = 'CAT-8088FD32AA09'
  AND NOT EXISTS (
    SELECT 1 FROM warehouse.material_categories WHERE "Code" = 'CAT-6F3924E46A39'
  );

INSERT INTO warehouse.material_categories ("Code", "Name", "Level", "ParentId", "SortOrder", "IsActive", "CreatedAt", "UpdatedAt")
SELECT 'CAT-D5CA6B7FEA0D', 'Đồng hồ', 2, p."Id", 102, TRUE, NOW(), NOW()
FROM warehouse.material_categories p
WHERE p."Code" = 'CAT-8088FD32AA09'
  AND NOT EXISTS (
    SELECT 1 FROM warehouse.material_categories WHERE "Code" = 'CAT-D5CA6B7FEA0D'
  );

INSERT INTO warehouse.material_categories ("Code", "Name", "Level", "ParentId", "SortOrder", "IsActive", "CreatedAt", "UpdatedAt")
SELECT 'CAT-45072A34FC9A', 'Đầu đốt', 2, p."Id", 110, TRUE, NOW(), NOW()
FROM warehouse.material_categories p
WHERE p."Code" = 'CAT-B2D8ACC7FD99'
  AND NOT EXISTS (
    SELECT 1 FROM warehouse.material_categories WHERE "Code" = 'CAT-45072A34FC9A'
  );

INSERT INTO warehouse.material_categories ("Code", "Name", "Level", "ParentId", "SortOrder", "IsActive", "CreatedAt", "UpdatedAt")
SELECT 'CAT-B11049FEF033', 'Băng tải', 2, p."Id", 113, TRUE, NOW(), NOW()
FROM warehouse.material_categories p
WHERE p."Code" = 'CAT-B2D8ACC7FD99'
  AND NOT EXISTS (
    SELECT 1 FROM warehouse.material_categories WHERE "Code" = 'CAT-B11049FEF033'
  );

INSERT INTO warehouse.material_categories ("Code", "Name", "Level", "ParentId", "SortOrder", "IsActive", "CreatedAt", "UpdatedAt")
SELECT 'CAT-5FA054420F97', 'Quạt', 2, p."Id", 114, TRUE, NOW(), NOW()
FROM warehouse.material_categories p
WHERE p."Code" = 'CAT-B2D8ACC7FD99'
  AND NOT EXISTS (
    SELECT 1 FROM warehouse.material_categories WHERE "Code" = 'CAT-5FA054420F97'
  );

INSERT INTO warehouse.material_categories ("Code", "Name", "Level", "ParentId", "SortOrder", "IsActive", "CreatedAt", "UpdatedAt")
SELECT 'CAT-89601CAB15C2', 'Động cơ giảm tốc', 2, p."Id", 117, TRUE, NOW(), NOW()
FROM warehouse.material_categories p
WHERE p."Code" = 'CAT-B2D8ACC7FD99'
  AND NOT EXISTS (
    SELECT 1 FROM warehouse.material_categories WHERE "Code" = 'CAT-89601CAB15C2'
  );

INSERT INTO warehouse.material_categories ("Code", "Name", "Level", "ParentId", "SortOrder", "IsActive", "CreatedAt", "UpdatedAt")
SELECT 'CAT-0501FD68D3AD', 'Bơm', 2, p."Id", 120, TRUE, NOW(), NOW()
FROM warehouse.material_categories p
WHERE p."Code" = 'CAT-B2D8ACC7FD99'
  AND NOT EXISTS (
    SELECT 1 FROM warehouse.material_categories WHERE "Code" = 'CAT-0501FD68D3AD'
  );

INSERT INTO warehouse.material_categories ("Code", "Name", "Level", "ParentId", "SortOrder", "IsActive", "CreatedAt", "UpdatedAt")
SELECT 'CAT-76C74A2BED9D', 'Palang', 2, p."Id", 123, TRUE, NOW(), NOW()
FROM warehouse.material_categories p
WHERE p."Code" = 'CAT-B2D8ACC7FD99'
  AND NOT EXISTS (
    SELECT 1 FROM warehouse.material_categories WHERE "Code" = 'CAT-76C74A2BED9D'
  );

INSERT INTO warehouse.material_categories ("Code", "Name", "Level", "ParentId", "SortOrder", "IsActive", "CreatedAt", "UpdatedAt")
SELECT 'CAT-73C602653998', 'Bông ống', 2, p."Id", 127, TRUE, NOW(), NOW()
FROM warehouse.material_categories p
WHERE p."Code" = 'CAT-3BF4BA205BF9'
  AND NOT EXISTS (
    SELECT 1 FROM warehouse.material_categories WHERE "Code" = 'CAT-73C602653998'
  );

INSERT INTO warehouse.material_categories ("Code", "Name", "Level", "ParentId", "SortOrder", "IsActive", "CreatedAt", "UpdatedAt")
SELECT 'CAT-88DBDF7C4CB8', 'Bông cuộn Ceramic', 2, p."Id", 130, TRUE, NOW(), NOW()
FROM warehouse.material_categories p
WHERE p."Code" = 'CAT-3BF4BA205BF9'
  AND NOT EXISTS (
    SELECT 1 FROM warehouse.material_categories WHERE "Code" = 'CAT-88DBDF7C4CB8'
  );

INSERT INTO warehouse.material_categories ("Code", "Name", "Level", "ParentId", "SortOrder", "IsActive", "CreatedAt", "UpdatedAt")
SELECT 'CAT-7043555E486D', 'Bông tấm', 2, p."Id", 131, TRUE, NOW(), NOW()
FROM warehouse.material_categories p
WHERE p."Code" = 'CAT-3BF4BA205BF9'
  AND NOT EXISTS (
    SELECT 1 FROM warehouse.material_categories WHERE "Code" = 'CAT-7043555E486D'
  );

INSERT INTO warehouse.material_categories ("Code", "Name", "Level", "ParentId", "SortOrder", "IsActive", "CreatedAt", "UpdatedAt")
SELECT 'CAT-2A9ACA0EB057', 'Kim loại', 2, p."Id", 132, TRUE, NOW(), NOW()
FROM warehouse.material_categories p
WHERE p."Code" = 'CAT-3BF4BA205BF9'
  AND NOT EXISTS (
    SELECT 1 FROM warehouse.material_categories WHERE "Code" = 'CAT-2A9ACA0EB057'
  );

-- Level 3
INSERT INTO warehouse.material_categories ("Code", "Name", "Level", "ParentId", "SortOrder", "IsActive", "CreatedAt", "UpdatedAt")
SELECT 'CAT-F29FE3853BDE', 'Thép tấm chịu nhiệt', 3, p."Id", 2, TRUE, NOW(), NOW()
FROM warehouse.material_categories p
WHERE p."Code" = 'CAT-B07968B76F4F'
  AND NOT EXISTS (
    SELECT 1 FROM warehouse.material_categories WHERE "Code" = 'CAT-F29FE3853BDE'
  );

INSERT INTO warehouse.material_categories ("Code", "Name", "Level", "ParentId", "SortOrder", "IsActive", "CreatedAt", "UpdatedAt")
SELECT 'CAT-DC3CE73897FC', 'Thép tấm thường', 3, p."Id", 3, TRUE, NOW(), NOW()
FROM warehouse.material_categories p
WHERE p."Code" = 'CAT-B07968B76F4F'
  AND NOT EXISTS (
    SELECT 1 FROM warehouse.material_categories WHERE "Code" = 'CAT-DC3CE73897FC'
  );

INSERT INTO warehouse.material_categories ("Code", "Name", "Level", "ParentId", "SortOrder", "IsActive", "CreatedAt", "UpdatedAt")
SELECT 'CAT-B6FAA7F92218', 'Thép ống đúc', 3, p."Id", 4, TRUE, NOW(), NOW()
FROM warehouse.material_categories p
WHERE p."Code" = 'CAT-B07968B76F4F'
  AND NOT EXISTS (
    SELECT 1 FROM warehouse.material_categories WHERE "Code" = 'CAT-B6FAA7F92218'
  );

INSERT INTO warehouse.material_categories ("Code", "Name", "Level", "ParentId", "SortOrder", "IsActive", "CreatedAt", "UpdatedAt")
SELECT 'CAT-787376048D7E', 'Thép ống hàn', 3, p."Id", 7, TRUE, NOW(), NOW()
FROM warehouse.material_categories p
WHERE p."Code" = 'CAT-B07968B76F4F'
  AND NOT EXISTS (
    SELECT 1 FROM warehouse.material_categories WHERE "Code" = 'CAT-787376048D7E'
  );

INSERT INTO warehouse.material_categories ("Code", "Name", "Level", "ParentId", "SortOrder", "IsActive", "CreatedAt", "UpdatedAt")
SELECT 'CAT-35AECE8B89F3', 'Thép hình', 3, p."Id", 8, TRUE, NOW(), NOW()
FROM warehouse.material_categories p
WHERE p."Code" = 'CAT-B07968B76F4F'
  AND NOT EXISTS (
    SELECT 1 FROM warehouse.material_categories WHERE "Code" = 'CAT-35AECE8B89F3'
  );

INSERT INTO warehouse.material_categories ("Code", "Name", "Level", "ParentId", "SortOrder", "IsActive", "CreatedAt", "UpdatedAt")
SELECT 'CAT-B2B4D3796756', 'Thép hộp', 3, p."Id", 13, TRUE, NOW(), NOW()
FROM warehouse.material_categories p
WHERE p."Code" = 'CAT-B07968B76F4F'
  AND NOT EXISTS (
    SELECT 1 FROM warehouse.material_categories WHERE "Code" = 'CAT-B2B4D3796756'
  );

INSERT INTO warehouse.material_categories ("Code", "Name", "Level", "ParentId", "SortOrder", "IsActive", "CreatedAt", "UpdatedAt")
SELECT 'CAT-9744BE1E141F', 'Thép cuộn', 3, p."Id", 14, TRUE, NOW(), NOW()
FROM warehouse.material_categories p
WHERE p."Code" = 'CAT-B07968B76F4F'
  AND NOT EXISTS (
    SELECT 1 FROM warehouse.material_categories WHERE "Code" = 'CAT-9744BE1E141F'
  );

INSERT INTO warehouse.material_categories ("Code", "Name", "Level", "ParentId", "SortOrder", "IsActive", "CreatedAt", "UpdatedAt")
SELECT 'CAT-EB5A8DFF5455', 'Lưới thép', 3, p."Id", 17, TRUE, NOW(), NOW()
FROM warehouse.material_categories p
WHERE p."Code" = 'CAT-B07968B76F4F'
  AND NOT EXISTS (
    SELECT 1 FROM warehouse.material_categories WHERE "Code" = 'CAT-EB5A8DFF5455'
  );

INSERT INTO warehouse.material_categories ("Code", "Name", "Level", "ParentId", "SortOrder", "IsActive", "CreatedAt", "UpdatedAt")
SELECT 'CAT-DA09EA3E8793', 'Inox tấm', 3, p."Id", 19, TRUE, NOW(), NOW()
FROM warehouse.material_categories p
WHERE p."Code" = 'CAT-EBAF0680E36C'
  AND NOT EXISTS (
    SELECT 1 FROM warehouse.material_categories WHERE "Code" = 'CAT-DA09EA3E8793'
  );

INSERT INTO warehouse.material_categories ("Code", "Name", "Level", "ParentId", "SortOrder", "IsActive", "CreatedAt", "UpdatedAt")
SELECT 'CAT-220326717D1B', 'Inox hình', 3, p."Id", 25, TRUE, NOW(), NOW()
FROM warehouse.material_categories p
WHERE p."Code" = 'CAT-EBAF0680E36C'
  AND NOT EXISTS (
    SELECT 1 FROM warehouse.material_categories WHERE "Code" = 'CAT-220326717D1B'
  );

INSERT INTO warehouse.material_categories ("Code", "Name", "Level", "ParentId", "SortOrder", "IsActive", "CreatedAt", "UpdatedAt")
SELECT 'CAT-1C1D92DE81AC', 'Láp inox', 3, p."Id", 30, TRUE, NOW(), NOW()
FROM warehouse.material_categories p
WHERE p."Code" = 'CAT-EBAF0680E36C'
  AND NOT EXISTS (
    SELECT 1 FROM warehouse.material_categories WHERE "Code" = 'CAT-1C1D92DE81AC'
  );

INSERT INTO warehouse.material_categories ("Code", "Name", "Level", "ParentId", "SortOrder", "IsActive", "CreatedAt", "UpdatedAt")
SELECT 'CAT-7F565B9CC8B0', 'Co', 3, p."Id", 33, TRUE, NOW(), NOW()
FROM warehouse.material_categories p
WHERE p."Code" = 'CAT-11C6F80B6F15'
  AND NOT EXISTS (
    SELECT 1 FROM warehouse.material_categories WHERE "Code" = 'CAT-7F565B9CC8B0'
  );

INSERT INTO warehouse.material_categories ("Code", "Name", "Level", "ParentId", "SortOrder", "IsActive", "CreatedAt", "UpdatedAt")
SELECT 'CAT-8580CF724AFA', 'Tê', 3, p."Id", 42, TRUE, NOW(), NOW()
FROM warehouse.material_categories p
WHERE p."Code" = 'CAT-11C6F80B6F15'
  AND NOT EXISTS (
    SELECT 1 FROM warehouse.material_categories WHERE "Code" = 'CAT-8580CF724AFA'
  );

INSERT INTO warehouse.material_categories ("Code", "Name", "Level", "ParentId", "SortOrder", "IsActive", "CreatedAt", "UpdatedAt")
SELECT 'CAT-E524EA3120AC', 'Giảm', 3, p."Id", 45, TRUE, NOW(), NOW()
FROM warehouse.material_categories p
WHERE p."Code" = 'CAT-11C6F80B6F15'
  AND NOT EXISTS (
    SELECT 1 FROM warehouse.material_categories WHERE "Code" = 'CAT-E524EA3120AC'
  );

INSERT INTO warehouse.material_categories ("Code", "Name", "Level", "ParentId", "SortOrder", "IsActive", "CreatedAt", "UpdatedAt")
SELECT 'CAT-93924255218C', 'Mặt bích', 3, p."Id", 52, TRUE, NOW(), NOW()
FROM warehouse.material_categories p
WHERE p."Code" = 'CAT-11C6F80B6F15'
  AND NOT EXISTS (
    SELECT 1 FROM warehouse.material_categories WHERE "Code" = 'CAT-93924255218C'
  );

INSERT INTO warehouse.material_categories ("Code", "Name", "Level", "ParentId", "SortOrder", "IsActive", "CreatedAt", "UpdatedAt")
SELECT 'CAT-AF3C8C31CAB0', 'Ống đúc hai đầu ren', 3, p."Id", 55, TRUE, NOW(), NOW()
FROM warehouse.material_categories p
WHERE p."Code" = 'CAT-11C6F80B6F15'
  AND NOT EXISTS (
    SELECT 1 FROM warehouse.material_categories WHERE "Code" = 'CAT-AF3C8C31CAB0'
  );

INSERT INTO warehouse.material_categories ("Code", "Name", "Level", "ParentId", "SortOrder", "IsActive", "CreatedAt", "UpdatedAt")
SELECT 'CAT-A08B57D70D0C', 'Ống măng xông', 3, p."Id", 56, TRUE, NOW(), NOW()
FROM warehouse.material_categories p
WHERE p."Code" = 'CAT-11C6F80B6F15'
  AND NOT EXISTS (
    SELECT 1 FROM warehouse.material_categories WHERE "Code" = 'CAT-A08B57D70D0C'
  );

INSERT INTO warehouse.material_categories ("Code", "Name", "Level", "ParentId", "SortOrder", "IsActive", "CreatedAt", "UpdatedAt")
SELECT 'CAT-55E59728278C', 'Rắc co hơi', 3, p."Id", 59, TRUE, NOW(), NOW()
FROM warehouse.material_categories p
WHERE p."Code" = 'CAT-11C6F80B6F15'
  AND NOT EXISTS (
    SELECT 1 FROM warehouse.material_categories WHERE "Code" = 'CAT-55E59728278C'
  );

INSERT INTO warehouse.material_categories ("Code", "Name", "Level", "ParentId", "SortOrder", "IsActive", "CreatedAt", "UpdatedAt")
SELECT 'CAT-5AC689688C44', 'Van kính thủy bồn nước
ren đồng 
Lắp ống thủy Ø20', 3, p."Id", 60, TRUE, NOW(), NOW()
FROM warehouse.material_categories p
WHERE p."Code" = 'CAT-11C6F80B6F15'
  AND NOT EXISTS (
    SELECT 1 FROM warehouse.material_categories WHERE "Code" = 'CAT-5AC689688C44'
  );

INSERT INTO warehouse.material_categories ("Code", "Name", "Level", "ParentId", "SortOrder", "IsActive", "CreatedAt", "UpdatedAt")
SELECT 'CAT-4EA8472075C7', 'Ống thủy tinh', 3, p."Id", 61, TRUE, NOW(), NOW()
FROM warehouse.material_categories p
WHERE p."Code" = 'CAT-11C6F80B6F15'
  AND NOT EXISTS (
    SELECT 1 FROM warehouse.material_categories WHERE "Code" = 'CAT-4EA8472075C7'
  );

INSERT INTO warehouse.material_categories ("Code", "Name", "Level", "ParentId", "SortOrder", "IsActive", "CreatedAt", "UpdatedAt")
SELECT 'CAT-084825BA6BB4', 'Béc phun nước inox', 3, p."Id", 62, TRUE, NOW(), NOW()
FROM warehouse.material_categories p
WHERE p."Code" = 'CAT-11C6F80B6F15'
  AND NOT EXISTS (
    SELECT 1 FROM warehouse.material_categories WHERE "Code" = 'CAT-084825BA6BB4'
  );

INSERT INTO warehouse.material_categories ("Code", "Name", "Level", "ParentId", "SortOrder", "IsActive", "CreatedAt", "UpdatedAt")
SELECT 'CAT-23567EFF4979', 'Ống xiphong', 3, p."Id", 63, TRUE, NOW(), NOW()
FROM warehouse.material_categories p
WHERE p."Code" = 'CAT-11C6F80B6F15'
  AND NOT EXISTS (
    SELECT 1 FROM warehouse.material_categories WHERE "Code" = 'CAT-23567EFF4979'
  );

INSERT INTO warehouse.material_categories ("Code", "Name", "Level", "ParentId", "SortOrder", "IsActive", "CreatedAt", "UpdatedAt")
SELECT 'CAT-8FE6376C37BB', 'Cọc thu lôi', 3, p."Id", 65, TRUE, NOW(), NOW()
FROM warehouse.material_categories p
WHERE p."Code" = 'CAT-9DD96328857A'
  AND NOT EXISTS (
    SELECT 1 FROM warehouse.material_categories WHERE "Code" = 'CAT-8FE6376C37BB'
  );

INSERT INTO warehouse.material_categories ("Code", "Name", "Level", "ParentId", "SortOrder", "IsActive", "CreatedAt", "UpdatedAt")
SELECT 'CAT-6B77A1A8C861', 'Cọc tiếp địa', 3, p."Id", 66, TRUE, NOW(), NOW()
FROM warehouse.material_categories p
WHERE p."Code" = 'CAT-9DD96328857A'
  AND NOT EXISTS (
    SELECT 1 FROM warehouse.material_categories WHERE "Code" = 'CAT-6B77A1A8C861'
  );

INSERT INTO warehouse.material_categories ("Code", "Name", "Level", "ParentId", "SortOrder", "IsActive", "CreatedAt", "UpdatedAt")
SELECT 'CAT-86D5595C4ACD', 'Dây thu lôi', 3, p."Id", 67, TRUE, NOW(), NOW()
FROM warehouse.material_categories p
WHERE p."Code" = 'CAT-9DD96328857A'
  AND NOT EXISTS (
    SELECT 1 FROM warehouse.material_categories WHERE "Code" = 'CAT-86D5595C4ACD'
  );

INSERT INTO warehouse.material_categories ("Code", "Name", "Level", "ParentId", "SortOrder", "IsActive", "CreatedAt", "UpdatedAt")
SELECT 'CAT-8E05A0C8BAC3', 'Ốc siết cáp dây thu lôi', 3, p."Id", 68, TRUE, NOW(), NOW()
FROM warehouse.material_categories p
WHERE p."Code" = 'CAT-9DD96328857A'
  AND NOT EXISTS (
    SELECT 1 FROM warehouse.material_categories WHERE "Code" = 'CAT-8E05A0C8BAC3'
  );

INSERT INTO warehouse.material_categories ("Code", "Name", "Level", "ParentId", "SortOrder", "IsActive", "CreatedAt", "UpdatedAt")
SELECT 'CAT-44468ED09104', 'Dây cáp thép bọc nhựa', 3, p."Id", 69, TRUE, NOW(), NOW()
FROM warehouse.material_categories p
WHERE p."Code" = 'CAT-9DD96328857A'
  AND NOT EXISTS (
    SELECT 1 FROM warehouse.material_categories WHERE "Code" = 'CAT-44468ED09104'
  );

INSERT INTO warehouse.material_categories ("Code", "Name", "Level", "ParentId", "SortOrder", "IsActive", "CreatedAt", "UpdatedAt")
SELECT 'CAT-0828C5350B93', 'Ốc siết cáp', 3, p."Id", 70, TRUE, NOW(), NOW()
FROM warehouse.material_categories p
WHERE p."Code" = 'CAT-9DD96328857A'
  AND NOT EXISTS (
    SELECT 1 FROM warehouse.material_categories WHERE "Code" = 'CAT-0828C5350B93'
  );

INSERT INTO warehouse.material_categories ("Code", "Name", "Level", "ParentId", "SortOrder", "IsActive", "CreatedAt", "UpdatedAt")
SELECT 'CAT-C224BC892F13', 'Tăng đơ thép', 3, p."Id", 71, TRUE, NOW(), NOW()
FROM warehouse.material_categories p
WHERE p."Code" = 'CAT-9DD96328857A'
  AND NOT EXISTS (
    SELECT 1 FROM warehouse.material_categories WHERE "Code" = 'CAT-C224BC892F13'
  );

INSERT INTO warehouse.material_categories ("Code", "Name", "Level", "ParentId", "SortOrder", "IsActive", "CreatedAt", "UpdatedAt")
SELECT 'CAT-235944ED2D3A', 'Qủa sứ', 3, p."Id", 72, TRUE, NOW(), NOW()
FROM warehouse.material_categories p
WHERE p."Code" = 'CAT-9DD96328857A'
  AND NOT EXISTS (
    SELECT 1 FROM warehouse.material_categories WHERE "Code" = 'CAT-235944ED2D3A'
  );

INSERT INTO warehouse.material_categories ("Code", "Name", "Level", "ParentId", "SortOrder", "IsActive", "CreatedAt", "UpdatedAt")
SELECT 'CAT-D6871AED326B', 'Cùm chữ U', 3, p."Id", 73, TRUE, NOW(), NOW()
FROM warehouse.material_categories p
WHERE p."Code" = 'CAT-9DD96328857A'
  AND NOT EXISTS (
    SELECT 1 FROM warehouse.material_categories WHERE "Code" = 'CAT-D6871AED326B'
  );

INSERT INTO warehouse.material_categories ("Code", "Name", "Level", "ParentId", "SortOrder", "IsActive", "CreatedAt", "UpdatedAt")
SELECT 'CAT-868E0C61B68C', 'Đồng hồ nhiệt độ', 3, p."Id", 75, TRUE, NOW(), NOW()
FROM warehouse.material_categories p
WHERE p."Code" = 'CAT-0925732B4D5E'
  AND NOT EXISTS (
    SELECT 1 FROM warehouse.material_categories WHERE "Code" = 'CAT-868E0C61B68C'
  );

INSERT INTO warehouse.material_categories ("Code", "Name", "Level", "ParentId", "SortOrder", "IsActive", "CreatedAt", "UpdatedAt")
SELECT 'CAT-FB7A82AF7028', 'Đồng hồ áp suất', 3, p."Id", 76, TRUE, NOW(), NOW()
FROM warehouse.material_categories p
WHERE p."Code" = 'CAT-0925732B4D5E'
  AND NOT EXISTS (
    SELECT 1 FROM warehouse.material_categories WHERE "Code" = 'CAT-FB7A82AF7028'
  );

INSERT INTO warehouse.material_categories ("Code", "Name", "Level", "ParentId", "SortOrder", "IsActive", "CreatedAt", "UpdatedAt")
SELECT 'CAT-CFFBDB48F760', 'Bulong thường', 3, p."Id", 78, TRUE, NOW(), NOW()
FROM warehouse.material_categories p
WHERE p."Code" = 'CAT-7FE0F168DC11'
  AND NOT EXISTS (
    SELECT 1 FROM warehouse.material_categories WHERE "Code" = 'CAT-CFFBDB48F760'
  );

INSERT INTO warehouse.material_categories ("Code", "Name", "Level", "ParentId", "SortOrder", "IsActive", "CreatedAt", "UpdatedAt")
SELECT 'CAT-8FA5852AD22B', 'Bulong neo', 3, p."Id", 79, TRUE, NOW(), NOW()
FROM warehouse.material_categories p
WHERE p."Code" = 'CAT-7FE0F168DC11'
  AND NOT EXISTS (
    SELECT 1 FROM warehouse.material_categories WHERE "Code" = 'CAT-8FA5852AD22B'
  );

INSERT INTO warehouse.material_categories ("Code", "Name", "Level", "ParentId", "SortOrder", "IsActive", "CreatedAt", "UpdatedAt")
SELECT 'CAT-0BE6FDCA382C', 'Cửa gang đúc', 3, p."Id", 81, TRUE, NOW(), NOW()
FROM warehouse.material_categories p
WHERE p."Code" = 'CAT-CB39B0425853'
  AND NOT EXISTS (
    SELECT 1 FROM warehouse.material_categories WHERE "Code" = 'CAT-0BE6FDCA382C'
  );

INSERT INTO warehouse.material_categories ("Code", "Name", "Level", "ParentId", "SortOrder", "IsActive", "CreatedAt", "UpdatedAt")
SELECT 'CAT-FB85E321ECAF', 'Cửa người chui', 3, p."Id", 82, TRUE, NOW(), NOW()
FROM warehouse.material_categories p
WHERE p."Code" = 'CAT-CB39B0425853'
  AND NOT EXISTS (
    SELECT 1 FROM warehouse.material_categories WHERE "Code" = 'CAT-FB85E321ECAF'
  );

INSERT INTO warehouse.material_categories ("Code", "Name", "Level", "ParentId", "SortOrder", "IsActive", "CreatedAt", "UpdatedAt")
SELECT 'CAT-8388420B63BF', 'Nấm ghi gang đúc', 3, p."Id", 83, TRUE, NOW(), NOW()
FROM warehouse.material_categories p
WHERE p."Code" = 'CAT-CB39B0425853'
  AND NOT EXISTS (
    SELECT 1 FROM warehouse.material_categories WHERE "Code" = 'CAT-8388420B63BF'
  );

INSERT INTO warehouse.material_categories ("Code", "Name", "Level", "ParentId", "SortOrder", "IsActive", "CreatedAt", "UpdatedAt")
SELECT 'CAT-ED49F8513660', 'Van cầu', 3, p."Id", 86, TRUE, NOW(), NOW()
FROM warehouse.material_categories p
WHERE p."Code" = 'CAT-CA49A32427D5'
  AND NOT EXISTS (
    SELECT 1 FROM warehouse.material_categories WHERE "Code" = 'CAT-ED49F8513660'
  );

INSERT INTO warehouse.material_categories ("Code", "Name", "Level", "ParentId", "SortOrder", "IsActive", "CreatedAt", "UpdatedAt")
SELECT 'CAT-0934B74ACF20', 'Van an toàn', 3, p."Id", 87, TRUE, NOW(), NOW()
FROM warehouse.material_categories p
WHERE p."Code" = 'CAT-CA49A32427D5'
  AND NOT EXISTS (
    SELECT 1 FROM warehouse.material_categories WHERE "Code" = 'CAT-0934B74ACF20'
  );

INSERT INTO warehouse.material_categories ("Code", "Name", "Level", "ParentId", "SortOrder", "IsActive", "CreatedAt", "UpdatedAt")
SELECT 'CAT-424919C1D9C2', 'Van bi tay gạt mặt bích', 3, p."Id", 90, TRUE, NOW(), NOW()
FROM warehouse.material_categories p
WHERE p."Code" = 'CAT-CA49A32427D5'
  AND NOT EXISTS (
    SELECT 1 FROM warehouse.material_categories WHERE "Code" = 'CAT-424919C1D9C2'
  );

INSERT INTO warehouse.material_categories ("Code", "Name", "Level", "ParentId", "SortOrder", "IsActive", "CreatedAt", "UpdatedAt")
SELECT 'CAT-B9143EAE64D5', 'Van 1 chiều đĩa', 3, p."Id", 91, TRUE, NOW(), NOW()
FROM warehouse.material_categories p
WHERE p."Code" = 'CAT-CA49A32427D5'
  AND NOT EXISTS (
    SELECT 1 FROM warehouse.material_categories WHERE "Code" = 'CAT-B9143EAE64D5'
  );

INSERT INTO warehouse.material_categories ("Code", "Name", "Level", "ParentId", "SortOrder", "IsActive", "CreatedAt", "UpdatedAt")
SELECT 'CAT-96F01C55AB46', 'Van cầu ren gang', 3, p."Id", 92, TRUE, NOW(), NOW()
FROM warehouse.material_categories p
WHERE p."Code" = 'CAT-CA49A32427D5'
  AND NOT EXISTS (
    SELECT 1 FROM warehouse.material_categories WHERE "Code" = 'CAT-96F01C55AB46'
  );

INSERT INTO warehouse.material_categories ("Code", "Name", "Level", "ParentId", "SortOrder", "IsActive", "CreatedAt", "UpdatedAt")
SELECT 'CAT-5117DE574163', 'Hơi', 3, p."Id", 94, TRUE, NOW(), NOW()
FROM warehouse.material_categories p
WHERE p."Code" = 'CAT-5BC33C4614E9'
  AND NOT EXISTS (
    SELECT 1 FROM warehouse.material_categories WHERE "Code" = 'CAT-5117DE574163'
  );

INSERT INTO warehouse.material_categories ("Code", "Name", "Level", "ParentId", "SortOrder", "IsActive", "CreatedAt", "UpdatedAt")
SELECT 'CAT-BC907EE95D5C', 'Nước', 3, p."Id", 95, TRUE, NOW(), NOW()
FROM warehouse.material_categories p
WHERE p."Code" = 'CAT-5BC33C4614E9'
  AND NOT EXISTS (
    SELECT 1 FROM warehouse.material_categories WHERE "Code" = 'CAT-BC907EE95D5C'
  );

INSERT INTO warehouse.material_categories ("Code", "Name", "Level", "ParentId", "SortOrder", "IsActive", "CreatedAt", "UpdatedAt")
SELECT 'CAT-72D2101F7512', 'Cơ', 3, p."Id", 97, TRUE, NOW(), NOW()
FROM warehouse.material_categories p
WHERE p."Code" = 'CAT-DDDF37D1D5CA'
  AND NOT EXISTS (
    SELECT 1 FROM warehouse.material_categories WHERE "Code" = 'CAT-72D2101F7512'
  );

INSERT INTO warehouse.material_categories ("Code", "Name", "Level", "ParentId", "SortOrder", "IsActive", "CreatedAt", "UpdatedAt")
SELECT 'CAT-091F41E352BC', 'LED', 3, p."Id", 98, TRUE, NOW(), NOW()
FROM warehouse.material_categories p
WHERE p."Code" = 'CAT-DDDF37D1D5CA'
  AND NOT EXISTS (
    SELECT 1 FROM warehouse.material_categories WHERE "Code" = 'CAT-091F41E352BC'
  );

INSERT INTO warehouse.material_categories ("Code", "Name", "Level", "ParentId", "SortOrder", "IsActive", "CreatedAt", "UpdatedAt")
SELECT 'CAT-F2BFE2C119A4', 'Bẫy nhiệt động', 3, p."Id", 100, TRUE, NOW(), NOW()
FROM warehouse.material_categories p
WHERE p."Code" = 'CAT-6F3924E46A39'
  AND NOT EXISTS (
    SELECT 1 FROM warehouse.material_categories WHERE "Code" = 'CAT-F2BFE2C119A4'
  );

INSERT INTO warehouse.material_categories ("Code", "Name", "Level", "ParentId", "SortOrder", "IsActive", "CreatedAt", "UpdatedAt")
SELECT 'CAT-94425F632171', 'Bẫy đồng tiền', 3, p."Id", 101, TRUE, NOW(), NOW()
FROM warehouse.material_categories p
WHERE p."Code" = 'CAT-6F3924E46A39'
  AND NOT EXISTS (
    SELECT 1 FROM warehouse.material_categories WHERE "Code" = 'CAT-94425F632171'
  );

INSERT INTO warehouse.material_categories ("Code", "Name", "Level", "ParentId", "SortOrder", "IsActive", "CreatedAt", "UpdatedAt")
SELECT 'CAT-8918D83E8817', 'Điện tử', 3, p."Id", 103, TRUE, NOW(), NOW()
FROM warehouse.material_categories p
WHERE p."Code" = 'CAT-D5CA6B7FEA0D'
  AND NOT EXISTS (
    SELECT 1 FROM warehouse.material_categories WHERE "Code" = 'CAT-8918D83E8817'
  );

INSERT INTO warehouse.material_categories ("Code", "Name", "Level", "ParentId", "SortOrder", "IsActive", "CreatedAt", "UpdatedAt")
SELECT 'CAT-0206B3F2C922', 'Cơ', 3, p."Id", 106, TRUE, NOW(), NOW()
FROM warehouse.material_categories p
WHERE p."Code" = 'CAT-D5CA6B7FEA0D'
  AND NOT EXISTS (
    SELECT 1 FROM warehouse.material_categories WHERE "Code" = 'CAT-0206B3F2C922'
  );

INSERT INTO warehouse.material_categories ("Code", "Name", "Level", "ParentId", "SortOrder", "IsActive", "CreatedAt", "UpdatedAt")
SELECT 'CAT-1ABAA7CF99F5', 'Nhiên liệu gas', 3, p."Id", 111, TRUE, NOW(), NOW()
FROM warehouse.material_categories p
WHERE p."Code" = 'CAT-45072A34FC9A'
  AND NOT EXISTS (
    SELECT 1 FROM warehouse.material_categories WHERE "Code" = 'CAT-1ABAA7CF99F5'
  );

INSERT INTO warehouse.material_categories ("Code", "Name", "Level", "ParentId", "SortOrder", "IsActive", "CreatedAt", "UpdatedAt")
SELECT 'CAT-EA833F17EBB0', 'Nhiên liệu dầu', 3, p."Id", 112, TRUE, NOW(), NOW()
FROM warehouse.material_categories p
WHERE p."Code" = 'CAT-45072A34FC9A'
  AND NOT EXISTS (
    SELECT 1 FROM warehouse.material_categories WHERE "Code" = 'CAT-EA833F17EBB0'
  );

INSERT INTO warehouse.material_categories ("Code", "Name", "Level", "ParentId", "SortOrder", "IsActive", "CreatedAt", "UpdatedAt")
SELECT 'CAT-BEC21A3AEEC6', 'Hút', 3, p."Id", 115, TRUE, NOW(), NOW()
FROM warehouse.material_categories p
WHERE p."Code" = 'CAT-5FA054420F97'
  AND NOT EXISTS (
    SELECT 1 FROM warehouse.material_categories WHERE "Code" = 'CAT-BEC21A3AEEC6'
  );

INSERT INTO warehouse.material_categories ("Code", "Name", "Level", "ParentId", "SortOrder", "IsActive", "CreatedAt", "UpdatedAt")
SELECT 'CAT-21CA4A691A9D', 'Thổi', 3, p."Id", 116, TRUE, NOW(), NOW()
FROM warehouse.material_categories p
WHERE p."Code" = 'CAT-5FA054420F97'
  AND NOT EXISTS (
    SELECT 1 FROM warehouse.material_categories WHERE "Code" = 'CAT-21CA4A691A9D'
  );

INSERT INTO warehouse.material_categories ("Code", "Name", "Level", "ParentId", "SortOrder", "IsActive", "CreatedAt", "UpdatedAt")
SELECT 'CAT-98D9FA7A97F2', 'Có thắng', 3, p."Id", 118, TRUE, NOW(), NOW()
FROM warehouse.material_categories p
WHERE p."Code" = 'CAT-89601CAB15C2'
  AND NOT EXISTS (
    SELECT 1 FROM warehouse.material_categories WHERE "Code" = 'CAT-98D9FA7A97F2'
  );

INSERT INTO warehouse.material_categories ("Code", "Name", "Level", "ParentId", "SortOrder", "IsActive", "CreatedAt", "UpdatedAt")
SELECT 'CAT-B6B794A10D6C', 'Không thắng', 3, p."Id", 119, TRUE, NOW(), NOW()
FROM warehouse.material_categories p
WHERE p."Code" = 'CAT-89601CAB15C2'
  AND NOT EXISTS (
    SELECT 1 FROM warehouse.material_categories WHERE "Code" = 'CAT-B6B794A10D6C'
  );

INSERT INTO warehouse.material_categories ("Code", "Name", "Level", "ParentId", "SortOrder", "IsActive", "CreatedAt", "UpdatedAt")
SELECT 'CAT-436E10B686C7', 'Trục đứng', 3, p."Id", 121, TRUE, NOW(), NOW()
FROM warehouse.material_categories p
WHERE p."Code" = 'CAT-0501FD68D3AD'
  AND NOT EXISTS (
    SELECT 1 FROM warehouse.material_categories WHERE "Code" = 'CAT-436E10B686C7'
  );

INSERT INTO warehouse.material_categories ("Code", "Name", "Level", "ParentId", "SortOrder", "IsActive", "CreatedAt", "UpdatedAt")
SELECT 'CAT-D478377DB618', 'Trục ngang', 3, p."Id", 122, TRUE, NOW(), NOW()
FROM warehouse.material_categories p
WHERE p."Code" = 'CAT-0501FD68D3AD'
  AND NOT EXISTS (
    SELECT 1 FROM warehouse.material_categories WHERE "Code" = 'CAT-D478377DB618'
  );

INSERT INTO warehouse.material_categories ("Code", "Name", "Level", "ParentId", "SortOrder", "IsActive", "CreatedAt", "UpdatedAt")
SELECT 'CAT-2913BC91EA96', 'Kéo tay', 3, p."Id", 124, TRUE, NOW(), NOW()
FROM warehouse.material_categories p
WHERE p."Code" = 'CAT-76C74A2BED9D'
  AND NOT EXISTS (
    SELECT 1 FROM warehouse.material_categories WHERE "Code" = 'CAT-2913BC91EA96'
  );

INSERT INTO warehouse.material_categories ("Code", "Name", "Level", "ParentId", "SortOrder", "IsActive", "CreatedAt", "UpdatedAt")
SELECT 'CAT-B5356BE4A3B2', 'Tự động', 3, p."Id", 125, TRUE, NOW(), NOW()
FROM warehouse.material_categories p
WHERE p."Code" = 'CAT-76C74A2BED9D'
  AND NOT EXISTS (
    SELECT 1 FROM warehouse.material_categories WHERE "Code" = 'CAT-B5356BE4A3B2'
  );

INSERT INTO warehouse.material_categories ("Code", "Name", "Level", "ParentId", "SortOrder", "IsActive", "CreatedAt", "UpdatedAt")
SELECT 'CAT-00E684D78F32', 'Có bạc', 3, p."Id", 128, TRUE, NOW(), NOW()
FROM warehouse.material_categories p
WHERE p."Code" = 'CAT-73C602653998'
  AND NOT EXISTS (
    SELECT 1 FROM warehouse.material_categories WHERE "Code" = 'CAT-00E684D78F32'
  );

INSERT INTO warehouse.material_categories ("Code", "Name", "Level", "ParentId", "SortOrder", "IsActive", "CreatedAt", "UpdatedAt")
SELECT 'CAT-1A15463CE517', 'Không bạc', 3, p."Id", 129, TRUE, NOW(), NOW()
FROM warehouse.material_categories p
WHERE p."Code" = 'CAT-73C602653998'
  AND NOT EXISTS (
    SELECT 1 FROM warehouse.material_categories WHERE "Code" = 'CAT-1A15463CE517'
  );

INSERT INTO warehouse.material_categories ("Code", "Name", "Level", "ParentId", "SortOrder", "IsActive", "CreatedAt", "UpdatedAt")
SELECT 'CAT-EA4BD9216EEB', 'Inox', 3, p."Id", 133, TRUE, NOW(), NOW()
FROM warehouse.material_categories p
WHERE p."Code" = 'CAT-2A9ACA0EB057'
  AND NOT EXISTS (
    SELECT 1 FROM warehouse.material_categories WHERE "Code" = 'CAT-EA4BD9216EEB'
  );

INSERT INTO warehouse.material_categories ("Code", "Name", "Level", "ParentId", "SortOrder", "IsActive", "CreatedAt", "UpdatedAt")
SELECT 'CAT-699747300DF5', 'Tôn mạ màu', 3, p."Id", 134, TRUE, NOW(), NOW()
FROM warehouse.material_categories p
WHERE p."Code" = 'CAT-2A9ACA0EB057'
  AND NOT EXISTS (
    SELECT 1 FROM warehouse.material_categories WHERE "Code" = 'CAT-699747300DF5'
  );

-- Level 4
INSERT INTO warehouse.material_categories ("Code", "Name", "Level", "ParentId", "SortOrder", "IsActive", "CreatedAt", "UpdatedAt")
SELECT 'CAT-C90364AA151F', 'Cán nóng', 4, p."Id", 5, TRUE, NOW(), NOW()
FROM warehouse.material_categories p
WHERE p."Code" = 'CAT-B6FAA7F92218'
  AND NOT EXISTS (
    SELECT 1 FROM warehouse.material_categories WHERE "Code" = 'CAT-C90364AA151F'
  );

INSERT INTO warehouse.material_categories ("Code", "Name", "Level", "ParentId", "SortOrder", "IsActive", "CreatedAt", "UpdatedAt")
SELECT 'CAT-FFC736B52368', 'Cán nguội', 4, p."Id", 6, TRUE, NOW(), NOW()
FROM warehouse.material_categories p
WHERE p."Code" = 'CAT-B6FAA7F92218'
  AND NOT EXISTS (
    SELECT 1 FROM warehouse.material_categories WHERE "Code" = 'CAT-FFC736B52368'
  );

INSERT INTO warehouse.material_categories ("Code", "Name", "Level", "ParentId", "SortOrder", "IsActive", "CreatedAt", "UpdatedAt")
SELECT 'CAT-722536774109', 'Hình V', 4, p."Id", 9, TRUE, NOW(), NOW()
FROM warehouse.material_categories p
WHERE p."Code" = 'CAT-35AECE8B89F3'
  AND NOT EXISTS (
    SELECT 1 FROM warehouse.material_categories WHERE "Code" = 'CAT-722536774109'
  );

INSERT INTO warehouse.material_categories ("Code", "Name", "Level", "ParentId", "SortOrder", "IsActive", "CreatedAt", "UpdatedAt")
SELECT 'CAT-ED8A37060671', 'Hình U', 4, p."Id", 10, TRUE, NOW(), NOW()
FROM warehouse.material_categories p
WHERE p."Code" = 'CAT-35AECE8B89F3'
  AND NOT EXISTS (
    SELECT 1 FROM warehouse.material_categories WHERE "Code" = 'CAT-ED8A37060671'
  );

INSERT INTO warehouse.material_categories ("Code", "Name", "Level", "ParentId", "SortOrder", "IsActive", "CreatedAt", "UpdatedAt")
SELECT 'CAT-134D6FFD6176', 'Hình I', 4, p."Id", 11, TRUE, NOW(), NOW()
FROM warehouse.material_categories p
WHERE p."Code" = 'CAT-35AECE8B89F3'
  AND NOT EXISTS (
    SELECT 1 FROM warehouse.material_categories WHERE "Code" = 'CAT-134D6FFD6176'
  );

INSERT INTO warehouse.material_categories ("Code", "Name", "Level", "ParentId", "SortOrder", "IsActive", "CreatedAt", "UpdatedAt")
SELECT 'CAT-1FEBD3C4089B', 'Hình H', 4, p."Id", 12, TRUE, NOW(), NOW()
FROM warehouse.material_categories p
WHERE p."Code" = 'CAT-35AECE8B89F3'
  AND NOT EXISTS (
    SELECT 1 FROM warehouse.material_categories WHERE "Code" = 'CAT-1FEBD3C4089B'
  );

INSERT INTO warehouse.material_categories ("Code", "Name", "Level", "ParentId", "SortOrder", "IsActive", "CreatedAt", "UpdatedAt")
SELECT 'CAT-1867CFA07F08', 'Hộp vuông', 4, p."Id", 15, TRUE, NOW(), NOW()
FROM warehouse.material_categories p
WHERE p."Code" = 'CAT-9744BE1E141F'
  AND NOT EXISTS (
    SELECT 1 FROM warehouse.material_categories WHERE "Code" = 'CAT-1867CFA07F08'
  );

INSERT INTO warehouse.material_categories ("Code", "Name", "Level", "ParentId", "SortOrder", "IsActive", "CreatedAt", "UpdatedAt")
SELECT 'CAT-93AD77A2A26B', 'Hộp chữ nhật', 4, p."Id", 16, TRUE, NOW(), NOW()
FROM warehouse.material_categories p
WHERE p."Code" = 'CAT-9744BE1E141F'
  AND NOT EXISTS (
    SELECT 1 FROM warehouse.material_categories WHERE "Code" = 'CAT-93AD77A2A26B'
  );

INSERT INTO warehouse.material_categories ("Code", "Name", "Level", "ParentId", "SortOrder", "IsActive", "CreatedAt", "UpdatedAt")
SELECT 'CAT-EB1294BE84B6', 'Inox 304', 4, p."Id", 20, TRUE, NOW(), NOW()
FROM warehouse.material_categories p
WHERE p."Code" = 'CAT-DA09EA3E8793'
  AND NOT EXISTS (
    SELECT 1 FROM warehouse.material_categories WHERE "Code" = 'CAT-EB1294BE84B6'
  );

INSERT INTO warehouse.material_categories ("Code", "Name", "Level", "ParentId", "SortOrder", "IsActive", "CreatedAt", "UpdatedAt")
SELECT 'CAT-F48D0A4E5518', 'Inox 201', 4, p."Id", 23, TRUE, NOW(), NOW()
FROM warehouse.material_categories p
WHERE p."Code" = 'CAT-DA09EA3E8793'
  AND NOT EXISTS (
    SELECT 1 FROM warehouse.material_categories WHERE "Code" = 'CAT-F48D0A4E5518'
  );

INSERT INTO warehouse.material_categories ("Code", "Name", "Level", "ParentId", "SortOrder", "IsActive", "CreatedAt", "UpdatedAt")
SELECT 'CAT-4C539EF2CB8D', 'Hình V', 4, p."Id", 26, TRUE, NOW(), NOW()
FROM warehouse.material_categories p
WHERE p."Code" = 'CAT-220326717D1B'
  AND NOT EXISTS (
    SELECT 1 FROM warehouse.material_categories WHERE "Code" = 'CAT-4C539EF2CB8D'
  );

INSERT INTO warehouse.material_categories ("Code", "Name", "Level", "ParentId", "SortOrder", "IsActive", "CreatedAt", "UpdatedAt")
SELECT 'CAT-8E6E882E5D8A', 'Hình U', 4, p."Id", 27, TRUE, NOW(), NOW()
FROM warehouse.material_categories p
WHERE p."Code" = 'CAT-220326717D1B'
  AND NOT EXISTS (
    SELECT 1 FROM warehouse.material_categories WHERE "Code" = 'CAT-8E6E882E5D8A'
  );

INSERT INTO warehouse.material_categories ("Code", "Name", "Level", "ParentId", "SortOrder", "IsActive", "CreatedAt", "UpdatedAt")
SELECT 'CAT-F0A16BCCBE3C', 'Hình I', 4, p."Id", 28, TRUE, NOW(), NOW()
FROM warehouse.material_categories p
WHERE p."Code" = 'CAT-220326717D1B'
  AND NOT EXISTS (
    SELECT 1 FROM warehouse.material_categories WHERE "Code" = 'CAT-F0A16BCCBE3C'
  );

INSERT INTO warehouse.material_categories ("Code", "Name", "Level", "ParentId", "SortOrder", "IsActive", "CreatedAt", "UpdatedAt")
SELECT 'CAT-2916582B2FB6', 'Hình H', 4, p."Id", 29, TRUE, NOW(), NOW()
FROM warehouse.material_categories p
WHERE p."Code" = 'CAT-220326717D1B'
  AND NOT EXISTS (
    SELECT 1 FROM warehouse.material_categories WHERE "Code" = 'CAT-2916582B2FB6'
  );

INSERT INTO warehouse.material_categories ("Code", "Name", "Level", "ParentId", "SortOrder", "IsActive", "CreatedAt", "UpdatedAt")
SELECT 'CAT-95146559EDEA', 'Đúc', 4, p."Id", 34, TRUE, NOW(), NOW()
FROM warehouse.material_categories p
WHERE p."Code" = 'CAT-7F565B9CC8B0'
  AND NOT EXISTS (
    SELECT 1 FROM warehouse.material_categories WHERE "Code" = 'CAT-95146559EDEA'
  );

INSERT INTO warehouse.material_categories ("Code", "Name", "Level", "ParentId", "SortOrder", "IsActive", "CreatedAt", "UpdatedAt")
SELECT 'CAT-B16F21872681', 'Hàn', 4, p."Id", 38, TRUE, NOW(), NOW()
FROM warehouse.material_categories p
WHERE p."Code" = 'CAT-7F565B9CC8B0'
  AND NOT EXISTS (
    SELECT 1 FROM warehouse.material_categories WHERE "Code" = 'CAT-B16F21872681'
  );

INSERT INTO warehouse.material_categories ("Code", "Name", "Level", "ParentId", "SortOrder", "IsActive", "CreatedAt", "UpdatedAt")
SELECT 'CAT-1B25E1367869', 'Đúc', 4, p."Id", 43, TRUE, NOW(), NOW()
FROM warehouse.material_categories p
WHERE p."Code" = 'CAT-8580CF724AFA'
  AND NOT EXISTS (
    SELECT 1 FROM warehouse.material_categories WHERE "Code" = 'CAT-1B25E1367869'
  );

INSERT INTO warehouse.material_categories ("Code", "Name", "Level", "ParentId", "SortOrder", "IsActive", "CreatedAt", "UpdatedAt")
SELECT 'CAT-05D87267C853', 'Hàn', 4, p."Id", 44, TRUE, NOW(), NOW()
FROM warehouse.material_categories p
WHERE p."Code" = 'CAT-8580CF724AFA'
  AND NOT EXISTS (
    SELECT 1 FROM warehouse.material_categories WHERE "Code" = 'CAT-05D87267C853'
  );

INSERT INTO warehouse.material_categories ("Code", "Name", "Level", "ParentId", "SortOrder", "IsActive", "CreatedAt", "UpdatedAt")
SELECT 'CAT-1006ADD4C677', 'Đúc', 4, p."Id", 46, TRUE, NOW(), NOW()
FROM warehouse.material_categories p
WHERE p."Code" = 'CAT-E524EA3120AC'
  AND NOT EXISTS (
    SELECT 1 FROM warehouse.material_categories WHERE "Code" = 'CAT-1006ADD4C677'
  );

INSERT INTO warehouse.material_categories ("Code", "Name", "Level", "ParentId", "SortOrder", "IsActive", "CreatedAt", "UpdatedAt")
SELECT 'CAT-F8136B7B7C45', 'Hàn', 4, p."Id", 49, TRUE, NOW(), NOW()
FROM warehouse.material_categories p
WHERE p."Code" = 'CAT-E524EA3120AC'
  AND NOT EXISTS (
    SELECT 1 FROM warehouse.material_categories WHERE "Code" = 'CAT-F8136B7B7C45'
  );

INSERT INTO warehouse.material_categories ("Code", "Name", "Level", "ParentId", "SortOrder", "IsActive", "CreatedAt", "UpdatedAt")
SELECT 'CAT-254DDC993E3B', 'Bích rỗng', 4, p."Id", 53, TRUE, NOW(), NOW()
FROM warehouse.material_categories p
WHERE p."Code" = 'CAT-93924255218C'
  AND NOT EXISTS (
    SELECT 1 FROM warehouse.material_categories WHERE "Code" = 'CAT-254DDC993E3B'
  );

INSERT INTO warehouse.material_categories ("Code", "Name", "Level", "ParentId", "SortOrder", "IsActive", "CreatedAt", "UpdatedAt")
SELECT 'CAT-37847B1E8349', 'Bích mù', 4, p."Id", 54, TRUE, NOW(), NOW()
FROM warehouse.material_categories p
WHERE p."Code" = 'CAT-93924255218C'
  AND NOT EXISTS (
    SELECT 1 FROM warehouse.material_categories WHERE "Code" = 'CAT-37847B1E8349'
  );

INSERT INTO warehouse.material_categories ("Code", "Name", "Level", "ParentId", "SortOrder", "IsActive", "CreatedAt", "UpdatedAt")
SELECT 'CAT-88306221F885', 'Thép', 4, p."Id", 57, TRUE, NOW(), NOW()
FROM warehouse.material_categories p
WHERE p."Code" = 'CAT-A08B57D70D0C'
  AND NOT EXISTS (
    SELECT 1 FROM warehouse.material_categories WHERE "Code" = 'CAT-88306221F885'
  );

INSERT INTO warehouse.material_categories ("Code", "Name", "Level", "ParentId", "SortOrder", "IsActive", "CreatedAt", "UpdatedAt")
SELECT 'CAT-5F37B8DDEE89', 'Inox', 4, p."Id", 58, TRUE, NOW(), NOW()
FROM warehouse.material_categories p
WHERE p."Code" = 'CAT-A08B57D70D0C'
  AND NOT EXISTS (
    SELECT 1 FROM warehouse.material_categories WHERE "Code" = 'CAT-5F37B8DDEE89'
  );

INSERT INTO warehouse.material_categories ("Code", "Name", "Level", "ParentId", "SortOrder", "IsActive", "CreatedAt", "UpdatedAt")
SELECT 'CAT-AF695E6B0BF9', 'Proportional', 4, p."Id", 88, TRUE, NOW(), NOW()
FROM warehouse.material_categories p
WHERE p."Code" = 'CAT-0934B74ACF20'
  AND NOT EXISTS (
    SELECT 1 FROM warehouse.material_categories WHERE "Code" = 'CAT-AF695E6B0BF9'
  );

INSERT INTO warehouse.material_categories ("Code", "Name", "Level", "ParentId", "SortOrder", "IsActive", "CreatedAt", "UpdatedAt")
SELECT 'CAT-4136A39D3981', 'Full Lift', 4, p."Id", 89, TRUE, NOW(), NOW()
FROM warehouse.material_categories p
WHERE p."Code" = 'CAT-0934B74ACF20'
  AND NOT EXISTS (
    SELECT 1 FROM warehouse.material_categories WHERE "Code" = 'CAT-4136A39D3981'
  );

INSERT INTO warehouse.material_categories ("Code", "Name", "Level", "ParentId", "SortOrder", "IsActive", "CreatedAt", "UpdatedAt")
SELECT 'CAT-934424CFC0EA', 'Đồng hồ lưu lượng hơi', 4, p."Id", 104, TRUE, NOW(), NOW()
FROM warehouse.material_categories p
WHERE p."Code" = 'CAT-8918D83E8817'
  AND NOT EXISTS (
    SELECT 1 FROM warehouse.material_categories WHERE "Code" = 'CAT-934424CFC0EA'
  );

INSERT INTO warehouse.material_categories ("Code", "Name", "Level", "ParentId", "SortOrder", "IsActive", "CreatedAt", "UpdatedAt")
SELECT 'CAT-7BCB1907A5AA', 'Đồng hồ lưu lượng nước', 4, p."Id", 105, TRUE, NOW(), NOW()
FROM warehouse.material_categories p
WHERE p."Code" = 'CAT-8918D83E8817'
  AND NOT EXISTS (
    SELECT 1 FROM warehouse.material_categories WHERE "Code" = 'CAT-7BCB1907A5AA'
  );

INSERT INTO warehouse.material_categories ("Code", "Name", "Level", "ParentId", "SortOrder", "IsActive", "CreatedAt", "UpdatedAt")
SELECT 'CAT-E7F9979EF872', 'Đồng hồ nước nóng', 4, p."Id", 107, TRUE, NOW(), NOW()
FROM warehouse.material_categories p
WHERE p."Code" = 'CAT-0206B3F2C922'
  AND NOT EXISTS (
    SELECT 1 FROM warehouse.material_categories WHERE "Code" = 'CAT-E7F9979EF872'
  );

INSERT INTO warehouse.material_categories ("Code", "Name", "Level", "ParentId", "SortOrder", "IsActive", "CreatedAt", "UpdatedAt")
SELECT 'CAT-13012E84F574', 'Đồng hồ nước lạnh', 4, p."Id", 108, TRUE, NOW(), NOW()
FROM warehouse.material_categories p
WHERE p."Code" = 'CAT-0206B3F2C922'
  AND NOT EXISTS (
    SELECT 1 FROM warehouse.material_categories WHERE "Code" = 'CAT-13012E84F574'
  );

-- Level 5
INSERT INTO warehouse.material_categories ("Code", "Name", "Level", "ParentId", "SortOrder", "IsActive", "CreatedAt", "UpdatedAt")
SELECT 'CAT-6E4454A1B693', '2B', 5, p."Id", 21, TRUE, NOW(), NOW()
FROM warehouse.material_categories p
WHERE p."Code" = 'CAT-EB1294BE84B6'
  AND NOT EXISTS (
    SELECT 1 FROM warehouse.material_categories WHERE "Code" = 'CAT-6E4454A1B693'
  );

INSERT INTO warehouse.material_categories ("Code", "Name", "Level", "ParentId", "SortOrder", "IsActive", "CreatedAt", "UpdatedAt")
SELECT 'CAT-352BDCDC350A', 'No1', 5, p."Id", 22, TRUE, NOW(), NOW()
FROM warehouse.material_categories p
WHERE p."Code" = 'CAT-EB1294BE84B6'
  AND NOT EXISTS (
    SELECT 1 FROM warehouse.material_categories WHERE "Code" = 'CAT-352BDCDC350A'
  );

INSERT INTO warehouse.material_categories ("Code", "Name", "Level", "ParentId", "SortOrder", "IsActive", "CreatedAt", "UpdatedAt")
SELECT 'CAT-A32D71E67059', 'BA', 5, p."Id", 24, TRUE, NOW(), NOW()
FROM warehouse.material_categories p
WHERE p."Code" = 'CAT-F48D0A4E5518'
  AND NOT EXISTS (
    SELECT 1 FROM warehouse.material_categories WHERE "Code" = 'CAT-A32D71E67059'
  );

INSERT INTO warehouse.material_categories ("Code", "Name", "Level", "ParentId", "SortOrder", "IsActive", "CreatedAt", "UpdatedAt")
SELECT 'CAT-C42BF5DA3F66', 'Co 45', 5, p."Id", 35, TRUE, NOW(), NOW()
FROM warehouse.material_categories p
WHERE p."Code" = 'CAT-95146559EDEA'
  AND NOT EXISTS (
    SELECT 1 FROM warehouse.material_categories WHERE "Code" = 'CAT-C42BF5DA3F66'
  );

INSERT INTO warehouse.material_categories ("Code", "Name", "Level", "ParentId", "SortOrder", "IsActive", "CreatedAt", "UpdatedAt")
SELECT 'CAT-EB76839F495E', 'Co 90', 5, p."Id", 36, TRUE, NOW(), NOW()
FROM warehouse.material_categories p
WHERE p."Code" = 'CAT-95146559EDEA'
  AND NOT EXISTS (
    SELECT 1 FROM warehouse.material_categories WHERE "Code" = 'CAT-EB76839F495E'
  );

INSERT INTO warehouse.material_categories ("Code", "Name", "Level", "ParentId", "SortOrder", "IsActive", "CreatedAt", "UpdatedAt")
SELECT 'CAT-6A6A15E92BA9', 'Co 180', 5, p."Id", 37, TRUE, NOW(), NOW()
FROM warehouse.material_categories p
WHERE p."Code" = 'CAT-95146559EDEA'
  AND NOT EXISTS (
    SELECT 1 FROM warehouse.material_categories WHERE "Code" = 'CAT-6A6A15E92BA9'
  );

INSERT INTO warehouse.material_categories ("Code", "Name", "Level", "ParentId", "SortOrder", "IsActive", "CreatedAt", "UpdatedAt")
SELECT 'CAT-EFA45F98552C', 'Co 45', 5, p."Id", 39, TRUE, NOW(), NOW()
FROM warehouse.material_categories p
WHERE p."Code" = 'CAT-B16F21872681'
  AND NOT EXISTS (
    SELECT 1 FROM warehouse.material_categories WHERE "Code" = 'CAT-EFA45F98552C'
  );

INSERT INTO warehouse.material_categories ("Code", "Name", "Level", "ParentId", "SortOrder", "IsActive", "CreatedAt", "UpdatedAt")
SELECT 'CAT-4185FDA33E41', 'Co 90', 5, p."Id", 40, TRUE, NOW(), NOW()
FROM warehouse.material_categories p
WHERE p."Code" = 'CAT-B16F21872681'
  AND NOT EXISTS (
    SELECT 1 FROM warehouse.material_categories WHERE "Code" = 'CAT-4185FDA33E41'
  );

INSERT INTO warehouse.material_categories ("Code", "Name", "Level", "ParentId", "SortOrder", "IsActive", "CreatedAt", "UpdatedAt")
SELECT 'CAT-67B12E22FE79', 'Co 180', 5, p."Id", 41, TRUE, NOW(), NOW()
FROM warehouse.material_categories p
WHERE p."Code" = 'CAT-B16F21872681'
  AND NOT EXISTS (
    SELECT 1 FROM warehouse.material_categories WHERE "Code" = 'CAT-67B12E22FE79'
  );

INSERT INTO warehouse.material_categories ("Code", "Name", "Level", "ParentId", "SortOrder", "IsActive", "CreatedAt", "UpdatedAt")
SELECT 'CAT-4FC38A449FB9', 'Thường', 5, p."Id", 47, TRUE, NOW(), NOW()
FROM warehouse.material_categories p
WHERE p."Code" = 'CAT-1006ADD4C677'
  AND NOT EXISTS (
    SELECT 1 FROM warehouse.material_categories WHERE "Code" = 'CAT-4FC38A449FB9'
  );

INSERT INTO warehouse.material_categories ("Code", "Name", "Level", "ParentId", "SortOrder", "IsActive", "CreatedAt", "UpdatedAt")
SELECT 'CAT-EBFFE1588D56', 'Lệch tâm', 5, p."Id", 48, TRUE, NOW(), NOW()
FROM warehouse.material_categories p
WHERE p."Code" = 'CAT-1006ADD4C677'
  AND NOT EXISTS (
    SELECT 1 FROM warehouse.material_categories WHERE "Code" = 'CAT-EBFFE1588D56'
  );

INSERT INTO warehouse.material_categories ("Code", "Name", "Level", "ParentId", "SortOrder", "IsActive", "CreatedAt", "UpdatedAt")
SELECT 'CAT-E80AC6650F28', 'Thường', 5, p."Id", 50, TRUE, NOW(), NOW()
FROM warehouse.material_categories p
WHERE p."Code" = 'CAT-F8136B7B7C45'
  AND NOT EXISTS (
    SELECT 1 FROM warehouse.material_categories WHERE "Code" = 'CAT-E80AC6650F28'
  );

INSERT INTO warehouse.material_categories ("Code", "Name", "Level", "ParentId", "SortOrder", "IsActive", "CreatedAt", "UpdatedAt")
SELECT 'CAT-8B0E46E62EAB', 'Lệch tâm', 5, p."Id", 51, TRUE, NOW(), NOW()
FROM warehouse.material_categories p
WHERE p."Code" = 'CAT-F8136B7B7C45'
  AND NOT EXISTS (
    SELECT 1 FROM warehouse.material_categories WHERE "Code" = 'CAT-8B0E46E62EAB'
  );

COMMIT;
