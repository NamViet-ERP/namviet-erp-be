-- Optional post-deploy master data: cây vật tư 5 cấp (138 nodes) từ CÂY VẬT TƯ.xlsx
-- Chỉ chạy khi business đã chốt taxonomy. Không chạy tự động qua EF migration.
--
-- Usage (PostgreSQL):
--   psql -h <host> -U <user> -d <database> -f scripts/seed-material-category-tree.sql
--
-- Regenerate from source JSON:
--   node scripts/generate-material-category-tree-sql.mjs

CREATE TEMP TABLE IF NOT EXISTS _material_category_seed (
  "Code" varchar(50) NOT NULL,
  "Name" varchar(255) NOT NULL,
  "Level" integer NOT NULL,
  "ParentCode" varchar(50),
  "SortOrder" integer NOT NULL
);

TRUNCATE _material_category_seed;

INSERT INTO _material_category_seed ("Code", "Name", "Level", "ParentCode", "SortOrder")
VALUES
  ('CAT-3B7EC6007F1B', 'Nguyên vật liệu', 1, NULL, 0),
  ('CAT-A04DBADDF8F6', 'Phụ kiện', 1, NULL, 1),
  ('CAT-E3E3E49A420E', 'Thiết bị công nghiệp', 1, NULL, 2),
  ('CAT-95FAEB1A5F7C', 'Thiết bị động cơ', 1, NULL, 3),
  ('CAT-732FDE992CE1', 'Vật tư bảo ôn', 1, NULL, 4),
  ('CAT-557C983B15D9', 'Vật tư phụ', 1, NULL, 5),
  ('CAT-47F503E51899', 'Hệ thép', 2, 'CAT-3B7EC6007F1B', 0),
  ('CAT-BA9437914538', 'Phụ kiện lắp đặt', 2, 'CAT-A04DBADDF8F6', 0),
  ('CAT-4B17623168FB', 'Van', 2, 'CAT-E3E3E49A420E', 0),
  ('CAT-2AD3F9FEC31E', 'Đầu đốt', 2, 'CAT-95FAEB1A5F7C', 0),
  ('CAT-E93C1781FB99', 'Bông ống', 2, 'CAT-732FDE992CE1', 0),
  ('CAT-FDEF1DA3A048', 'Hệ inox', 2, 'CAT-3B7EC6007F1B', 1),
  ('CAT-6F3D42B3D536', 'Phụ kiện chống sét', 2, 'CAT-A04DBADDF8F6', 1),
  ('CAT-539B6B0D899B', 'Lọc y', 2, 'CAT-E3E3E49A420E', 1),
  ('CAT-6F354D073F86', 'Băng tải', 2, 'CAT-95FAEB1A5F7C', 1),
  ('CAT-115C034DF42A', 'Bông cuộn Ceramic', 2, 'CAT-732FDE992CE1', 1),
  ('CAT-9169EC0EAEE4', 'Phụ kiện đo', 2, 'CAT-A04DBADDF8F6', 2),
  ('CAT-8CD9B098AE90', 'Kính thuy sáng', 2, 'CAT-E3E3E49A420E', 2),
  ('CAT-F9FFFCD5555C', 'Quạt', 2, 'CAT-95FAEB1A5F7C', 2),
  ('CAT-B70CBB5E2505', 'Bông tấm', 2, 'CAT-732FDE992CE1', 2),
  ('CAT-6EABAD90F10D', 'Phụ kiện cố định', 2, 'CAT-A04DBADDF8F6', 3),
  ('CAT-CBE6365062BB', 'Bẫy hơi', 2, 'CAT-E3E3E49A420E', 3),
  ('CAT-22B483EA904D', 'Động cơ giảm tốc', 2, 'CAT-95FAEB1A5F7C', 3),
  ('CAT-F3C17C0D4F1B', 'Kim loại', 2, 'CAT-732FDE992CE1', 3),
  ('CAT-75A579A83BEA', 'Phụ kiện khác', 2, 'CAT-A04DBADDF8F6', 4),
  ('CAT-05331E86C9B5', 'Đồng hồ', 2, 'CAT-E3E3E49A420E', 4),
  ('CAT-767F01CC09B2', 'Bơm', 2, 'CAT-95FAEB1A5F7C', 4),
  ('CAT-63C0B561DBCC', 'Palang', 2, 'CAT-95FAEB1A5F7C', 5),
  ('CAT-82BBBBB8DAF1', 'Thép tấm chịu nhiệt', 3, 'CAT-47F503E51899', 0),
  ('CAT-7938D5094411', 'Inox tấm', 3, 'CAT-FDEF1DA3A048', 0),
  ('CAT-DBD042164133', 'Co', 3, 'CAT-BA9437914538', 0),
  ('CAT-ABB5131FF3BE', 'Cọc thu lôi', 3, 'CAT-6F3D42B3D536', 0),
  ('CAT-89925C95795E', 'Đồng hồ nhiệt độ', 3, 'CAT-9169EC0EAEE4', 0),
  ('CAT-8FD2502D8181', 'Bulong thường', 3, 'CAT-6EABAD90F10D', 0),
  ('CAT-A7335F9305DC', 'Cửa gang đúc', 3, 'CAT-75A579A83BEA', 0),
  ('CAT-0C6BB07F3E70', 'Van cầu', 3, 'CAT-4B17623168FB', 0),
  ('CAT-5CBCB574A921', 'Hơi', 3, 'CAT-539B6B0D899B', 0),
  ('CAT-5E249F2BB520', 'Cơ', 3, 'CAT-8CD9B098AE90', 0),
  ('CAT-416DDAB14FEA', 'Bẫy nhiệt động', 3, 'CAT-CBE6365062BB', 0),
  ('CAT-B42F6833F022', 'Đồng hồ lưu lượng hơi', 3, 'CAT-05331E86C9B5', 0),
  ('CAT-6111BB311BCC', 'Nhiên liệu gas', 3, 'CAT-2AD3F9FEC31E', 0),
  ('CAT-83B8B62544C0', 'Hút', 3, 'CAT-F9FFFCD5555C', 0),
  ('CAT-5C577B9F07A7', 'Có thắng', 3, 'CAT-22B483EA904D', 0),
  ('CAT-37859A6727E6', 'Trục đứng', 3, 'CAT-767F01CC09B2', 0),
  ('CAT-A0EAB56240A3', 'Kéo tay', 3, 'CAT-63C0B561DBCC', 0),
  ('CAT-85B514E2E7FA', 'Có bạc', 3, 'CAT-E93C1781FB99', 0),
  ('CAT-D10346DE455E', 'Inox', 3, 'CAT-F3C17C0D4F1B', 0),
  ('CAT-F4D003320E70', 'Thép tấm thường', 3, 'CAT-47F503E51899', 1),
  ('CAT-F6701BE14E5D', 'Inox hình', 3, 'CAT-FDEF1DA3A048', 1),
  ('CAT-C0933998F71A', 'Tê', 3, 'CAT-BA9437914538', 1),
  ('CAT-691B35586989', 'Cọc tiếp địa', 3, 'CAT-6F3D42B3D536', 1),
  ('CAT-9541E0E56934', 'Đồng hồ áp suất', 3, 'CAT-9169EC0EAEE4', 1),
  ('CAT-36545BBEFE7C', 'Bulong neo', 3, 'CAT-6EABAD90F10D', 1),
  ('CAT-30C1A3344095', 'Cửa người chui', 3, 'CAT-75A579A83BEA', 1),
  ('CAT-CC12558CBAEF', 'Van an toàn', 3, 'CAT-4B17623168FB', 1),
  ('CAT-668A79ADBDBC', 'Nước', 3, 'CAT-539B6B0D899B', 1),
  ('CAT-5DB5645044D6', 'LED', 3, 'CAT-8CD9B098AE90', 1),
  ('CAT-030B94F78143', 'Bẫy đồng tiền', 3, 'CAT-CBE6365062BB', 1),
  ('CAT-259DBD7E8004', 'Đồng hồ lưu lượng nước', 3, 'CAT-05331E86C9B5', 1),
  ('CAT-506242BFC32E', 'Tôn mạ màu', 3, 'CAT-F3C17C0D4F1B', 1),
  ('CAT-7D91F5CA6F3C', 'Thép ống đúc', 3, 'CAT-47F503E51899', 2),
  ('CAT-4AFC9C40E1D7', 'Láp inox', 3, 'CAT-FDEF1DA3A048', 2),
  ('CAT-D452561626A4', 'Giảm', 3, 'CAT-BA9437914538', 2),
  ('CAT-C1F4D06B0A0F', 'Dây thu lôi', 3, 'CAT-6F3D42B3D536', 2),
  ('CAT-3785F319F43F', 'Nấm ghi gang đúc', 3, 'CAT-75A579A83BEA', 2),
  ('CAT-721910CC11AD', 'Van bi tay gạt mặt bích', 3, 'CAT-4B17623168FB', 2),
  ('CAT-2395C65CE271', 'Đồng hồ nước nóng', 3, 'CAT-05331E86C9B5', 2),
  ('CAT-8DC1D0C3EDEF', 'Thép ống hàn', 3, 'CAT-47F503E51899', 3),
  ('CAT-560F7CF5E62D', 'Mặt bích', 3, 'CAT-BA9437914538', 3),
  ('CAT-72AF7A7EA5BE', 'Ốc siết cáp dây thu lôi', 3, 'CAT-6F3D42B3D536', 3),
  ('CAT-24F16C06EB34', 'Van 1 chiều đĩa', 3, 'CAT-4B17623168FB', 3),
  ('CAT-A34493605619', 'Đồng hồ nước lạnh', 3, 'CAT-05331E86C9B5', 3),
  ('CAT-DC7BC81F018D', 'Thép hình', 3, 'CAT-47F503E51899', 4),
  ('CAT-E65463611C89', 'Ống đúc hai đầu ren', 3, 'CAT-BA9437914538', 4),
  ('CAT-E88FE698175C', 'Dây cáp thép bọc nhựa', 3, 'CAT-6F3D42B3D536', 4),
  ('CAT-25980F75A68A', 'Van cầu ren gang', 3, 'CAT-4B17623168FB', 4),
  ('CAT-B834C487FBB9', 'Thép hộp', 3, 'CAT-47F503E51899', 5),
  ('CAT-661CB255AF0A', 'Ống măng xông', 3, 'CAT-BA9437914538', 5),
  ('CAT-F361BD519DDD', 'Ốc siết cáp', 3, 'CAT-6F3D42B3D536', 5),
  ('CAT-D03076D4464A', 'Thép cuộn', 3, 'CAT-47F503E51899', 6),
  ('CAT-1B6FAF92A955', 'Rắc co hơi', 3, 'CAT-BA9437914538', 6),
  ('CAT-8D9160FEDC81', 'Tăng đơ thép', 3, 'CAT-6F3D42B3D536', 6),
  ('CAT-C17AA7A1F1AE', 'Lưới thép', 3, 'CAT-47F503E51899', 7),
  ('CAT-BBE8CD1D0FED', 'Van kính thủy bồn nước ren đồng Lắp ống thủy Ø20', 3, 'CAT-BA9437914538', 7),
  ('CAT-B5DC04C73755', 'Qủa sứ', 3, 'CAT-6F3D42B3D536', 7),
  ('CAT-670FC7EA2589', 'Ống thủy tinh', 3, 'CAT-BA9437914538', 8),
  ('CAT-92C1D2C81876', 'Cùm chữ U', 3, 'CAT-6F3D42B3D536', 8),
  ('CAT-8B9B72B4DE1E', 'Béc phun nước inox', 3, 'CAT-BA9437914538', 9),
  ('CAT-B1CA68DC3292', 'Ống xiphong', 3, 'CAT-BA9437914538', 10),
  ('CAT-A6D26ECA64BD', 'Cán nóng', 4, 'CAT-7D91F5CA6F3C', 0),
  ('CAT-30680275A959', 'Hình V', 4, 'CAT-DC7BC81F018D', 0),
  ('CAT-F540BCB18866', 'Hộp vuông', 4, 'CAT-D03076D4464A', 0),
  ('CAT-AEEB0956606A', 'Inox 304', 4, 'CAT-7938D5094411', 0),
  ('CAT-D027F6C4486C', 'Hình V', 4, 'CAT-F6701BE14E5D', 0),
  ('CAT-E5B80233A90F', 'Đúc', 4, 'CAT-DBD042164133', 0),
  ('CAT-F575808BD043', 'Đúc', 4, 'CAT-C0933998F71A', 0),
  ('CAT-92A1556E2325', 'Đúc', 4, 'CAT-D452561626A4', 0),
  ('CAT-FDB99C36C4F5', 'Bích rỗng', 4, 'CAT-560F7CF5E62D', 0),
  ('CAT-93ED517A5B0C', 'Thép', 4, 'CAT-661CB255AF0A', 0),
  ('CAT-DCA1EF8B23F6', 'Proportional', 4, 'CAT-CC12558CBAEF', 0),
  ('CAT-F75A14A25F68', 'Cơ', 4, 'CAT-2395C65CE271', 0),
  ('CAT-073CFEE780E4', 'Cơ', 4, 'CAT-A34493605619', 0),
  ('CAT-6A2FD1EAF342', 'Nhiên liệu dầu', 4, 'CAT-6111BB311BCC', 0),
  ('CAT-777573672123', 'Thổi', 4, 'CAT-83B8B62544C0', 0),
  ('CAT-12C26E0C2589', 'Không thắng', 4, 'CAT-5C577B9F07A7', 0),
  ('CAT-E2EC73AF2162', 'Trục ngang', 4, 'CAT-37859A6727E6', 0),
  ('CAT-55133A0F7856', 'Tự động', 4, 'CAT-A0EAB56240A3', 0),
  ('CAT-90982F951EAF', 'Không bạc', 4, 'CAT-85B514E2E7FA', 0),
  ('CAT-8058621EC955', 'Cán nguội', 4, 'CAT-7D91F5CA6F3C', 1),
  ('CAT-3C2B669E2614', 'Hình U', 4, 'CAT-DC7BC81F018D', 1),
  ('CAT-EDE21B2FD738', 'Hộp chữ nhật', 4, 'CAT-D03076D4464A', 1),
  ('CAT-81E33AE89D38', 'Inox 201', 4, 'CAT-7938D5094411', 1),
  ('CAT-78450771BA07', 'Hình U', 4, 'CAT-F6701BE14E5D', 1),
  ('CAT-475F543CD73D', 'Hàn', 4, 'CAT-DBD042164133', 1),
  ('CAT-234DDE4EC8C0', 'Hàn', 4, 'CAT-C0933998F71A', 1),
  ('CAT-36874FA9E5A3', 'Hàn', 4, 'CAT-D452561626A4', 1),
  ('CAT-E16E1C151D0D', 'Bích mù', 4, 'CAT-560F7CF5E62D', 1),
  ('CAT-181B87E66F3F', 'Inox', 4, 'CAT-661CB255AF0A', 1),
  ('CAT-05846F1D1F66', 'Full Lift', 4, 'CAT-CC12558CBAEF', 1),
  ('CAT-6354929296D0', 'Điện tử', 4, 'CAT-2395C65CE271', 1),
  ('CAT-C919E212B3E7', 'Điện tử', 4, 'CAT-A34493605619', 1),
  ('CAT-BE1A4C9361B2', 'Hình I', 4, 'CAT-DC7BC81F018D', 2),
  ('CAT-0AEB904BC594', 'Hình I', 4, 'CAT-F6701BE14E5D', 2),
  ('CAT-5281348F35E2', 'Hình H', 4, 'CAT-DC7BC81F018D', 3),
  ('CAT-BFFD68BB1AE7', 'Hình H', 4, 'CAT-F6701BE14E5D', 3),
  ('CAT-83D346E064FC', '2B', 5, 'CAT-AEEB0956606A', 0),
  ('CAT-897B731F5A98', 'BA', 5, 'CAT-81E33AE89D38', 0),
  ('CAT-00C3A31073AE', 'Co 45', 5, 'CAT-E5B80233A90F', 0),
  ('CAT-0BC961FD47EA', 'Co 45', 5, 'CAT-475F543CD73D', 0),
  ('CAT-F6D3A2D5A4F3', 'Thường', 5, 'CAT-92A1556E2325', 0),
  ('CAT-6606F8285973', 'Thường', 5, 'CAT-36874FA9E5A3', 0),
  ('CAT-58A723124EB5', 'No1', 5, 'CAT-AEEB0956606A', 1),
  ('CAT-76849CC8CCA7', 'Co 90', 5, 'CAT-E5B80233A90F', 1),
  ('CAT-8A45CB399A0C', 'Co 90', 5, 'CAT-475F543CD73D', 1),
  ('CAT-D799BBBAC719', 'Lệch tâm', 5, 'CAT-92A1556E2325', 1),
  ('CAT-05362E16E234', 'Lệch tâm', 5, 'CAT-36874FA9E5A3', 1),
  ('CAT-E0303937308D', 'Co 180', 5, 'CAT-E5B80233A90F', 2),
  ('CAT-2D854BD3FA18', 'Co 180', 5, 'CAT-475F543CD73D', 2);

INSERT INTO warehouse.material_categories ("Code", "Name", "Level", "ParentId", "SortOrder", "IsActive", "CreatedAt", "UpdatedAt")
SELECT s."Code", s."Name", s."Level", NULL, s."SortOrder", TRUE, NOW(), NOW()
FROM _material_category_seed s
WHERE s."Level" = 1
  AND s."Code" <> 'UNCATEGORIZED'
  AND NOT EXISTS (SELECT 1 FROM warehouse.material_categories c WHERE c."Code" = s."Code");

INSERT INTO warehouse.material_categories ("Code", "Name", "Level", "ParentId", "SortOrder", "IsActive", "CreatedAt", "UpdatedAt")
SELECT s."Code", s."Name", s."Level", p."Id", s."SortOrder", TRUE, NOW(), NOW()
FROM _material_category_seed s
JOIN warehouse.material_categories p ON p."Code" = s."ParentCode"
WHERE s."Level" = 2
  AND NOT EXISTS (SELECT 1 FROM warehouse.material_categories c WHERE c."Code" = s."Code");

INSERT INTO warehouse.material_categories ("Code", "Name", "Level", "ParentId", "SortOrder", "IsActive", "CreatedAt", "UpdatedAt")
SELECT s."Code", s."Name", s."Level", p."Id", s."SortOrder", TRUE, NOW(), NOW()
FROM _material_category_seed s
JOIN warehouse.material_categories p ON p."Code" = s."ParentCode"
WHERE s."Level" = 3
  AND NOT EXISTS (SELECT 1 FROM warehouse.material_categories c WHERE c."Code" = s."Code");

INSERT INTO warehouse.material_categories ("Code", "Name", "Level", "ParentId", "SortOrder", "IsActive", "CreatedAt", "UpdatedAt")
SELECT s."Code", s."Name", s."Level", p."Id", s."SortOrder", TRUE, NOW(), NOW()
FROM _material_category_seed s
JOIN warehouse.material_categories p ON p."Code" = s."ParentCode"
WHERE s."Level" = 4
  AND NOT EXISTS (SELECT 1 FROM warehouse.material_categories c WHERE c."Code" = s."Code");

INSERT INTO warehouse.material_categories ("Code", "Name", "Level", "ParentId", "SortOrder", "IsActive", "CreatedAt", "UpdatedAt")
SELECT s."Code", s."Name", s."Level", p."Id", s."SortOrder", TRUE, NOW(), NOW()
FROM _material_category_seed s
JOIN warehouse.material_categories p ON p."Code" = s."ParentCode"
WHERE s."Level" = 5
  AND NOT EXISTS (SELECT 1 FROM warehouse.material_categories c WHERE c."Code" = s."Code");

DROP TABLE IF EXISTS _material_category_seed;
