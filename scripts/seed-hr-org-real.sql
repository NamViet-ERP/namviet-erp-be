-- Wave A: Phòng ban + nhân viên thật (Nam Việt)
-- Chạy sau scripts/apply-employee-department-optional.sql
-- Idempotent theo mã phòng ban / mã NV.
-- Status/Gender/Education lưu dạng text (EF string conversion).

BEGIN;

INSERT INTO hr.departments ("Code", "Name", "Block", "IsActive", "CreatedAt", "UpdatedAt")
SELECT v.code, v.name, v.block, true, NOW(), NOW()
FROM (VALUES
  ('HCNS', 'Hành chính nhân sự', 'HCNS'),
  ('KT', 'Kế toán', 'TÀI CHÍNH'),
  ('MH', 'Mua hàng - Cung ứng', 'CUNG ỨNG'),
  ('KHO', 'Kho vận', 'CUNG ỨNG'),
  ('IT', 'Công nghệ thông tin', 'IT'),
  ('KD', 'Kinh doanh - Dự án', 'KINH DOANH'),
  ('KT-XD', 'Kỹ thuật thi công', 'KỸ THUẬT'),
  ('QA', 'Quản lý chất lượng', 'KỸ THUẬT'),
  ('PC', 'Pháp chế - Hợp đồng', 'HCNS'),
  ('CSKH', 'Chăm sóc khách hàng', 'KINH DOANH'),
  ('BHXH', 'Bảo hiểm - C&B', 'HCNS'),
  ('AT', 'An toàn lao động', 'KỸ THUẬT')
) AS v(code, name, block)
WHERE NOT EXISTS (
  SELECT 1 FROM hr.departments d WHERE d."Code" = v.code AND d."DeletedAt" IS NULL
);

WITH dept AS (
  SELECT "Id", "Code" FROM hr.departments WHERE "DeletedAt" IS NULL
)
INSERT INTO hr.employees (
  "Code", "FullName", "Gender", "BirthDate", "Phone", "Email",
  "AddressPermanent", "AddressCurrent", "DepartmentId", "JoinDate",
  "Status", "Education", "CreatedAt", "UpdatedAt"
)
SELECT
  e.code, e.full_name, e.gender, e.birth::date, e.phone, e.email,
  e.addr, e.addr, d."Id", e.join_date::date,
  'Active', 'University', NOW(), NOW()
FROM (VALUES
  ('NV-2101', 'Lê Văn Tám', 'Male', '1988-03-12', '0903124587', 'levan.tam@namviet.vn',
   '125 Nguyễn Văn Cừ, P. Nguyễn Cư Trinh, Q.1, TP.HCM', '2019-04-01', 'HCNS'),
  ('NV-2102', 'Nguyễn Thị Định', 'Female', '1992-07-21', '0918456723', 'nguyenthi.dinh@namviet.vn',
   '48 Cách Mạng Tháng Tám, P. Bến Thành, Q.1, TP.HCM', '2020-02-15', 'KT'),
  ('NV-2103', 'Trần Hoàng Nam', 'Male', '1990-11-05', '0935678912', 'tranhoang.nam@namviet.vn',
   '72 Điện Biên Phủ, P. Đa Kao, Q.1, TP.HCM', '2018-09-10', 'MH'),
  ('NV-2104', 'Phạm Minh Châu', 'Female', '1994-01-18', '0978123456', 'phamminh.chau@namviet.vn',
   '19 Nguyễn Hữu Cảnh, P.22, Q. Bình Thạnh, TP.HCM', '2021-06-01', 'KHO'),
  ('NV-2105', 'Võ Quốc Bảo', 'Male', '1987-09-30', '0909786543', 'voquoc.bao@namviet.vn',
   '356 Lê Văn Sỹ, P.14, Q.3, TP.HCM', '2017-03-20', 'IT'),
  ('NV-2106', 'Đặng Thị Hồng', 'Female', '1991-05-14', '0987654321', 'dangthi.hong@namviet.vn',
   '88 Hoàng Văn Thụ, P.4, Q. Tân Bình, TP.HCM', '2019-11-11', 'KD'),
  ('NV-2107', 'Bùi Anh Tuấn', 'Male', '1989-12-02', '0912345678', 'buianh.tuan@namviet.vn',
   '15 Phan Đăng Lưu, P.3, Q. Phú Nhuận, TP.HCM', '2016-08-08', 'KT-XD'),
  ('NV-2108', 'Hoàng Thị Mai', 'Female', '1993-04-27', '0934567890', 'hoangthi.mai@namviet.vn',
   '210 Nguyễn Thị Minh Khai, P.6, Q.3, TP.HCM', '2022-01-17', 'QA'),
  ('NV-2109', 'Ngô Đức Thịnh', 'Male', '1986-08-09', '0966789012', 'ngoduc.thinh@namviet.vn',
   '9 Nguyễn Đình Chiểu, P. Đa Kao, Q.1, TP.HCM', '2015-05-04', 'PC'),
  ('NV-2110', 'Lý Thị Kim Oanh', 'Female', '1995-02-23', '0945678901', 'lythi.kimoanh@namviet.vn',
   '67 Cộng Hòa, P.4, Q. Tân Bình, TP.HCM', '2023-03-01', 'CSKH'),
  ('NV-2111', 'Phan Văn Hùng', 'Male', '1985-06-16', '0901456789', 'phanvan.hung@namviet.vn',
   '142 Lạc Long Quân, P.5, Q.11, TP.HCM', '2014-10-12', 'BHXH'),
  ('NV-2112', 'Trịnh Thị Thu Hà', 'Female', '1990-10-08', '0977012345', 'trinhthi.thuha@namviet.vn',
   '55 Nguyễn Trãi, P.3, Q.5, TP.HCM', '2018-12-03', 'AT')
) AS e(code, full_name, gender, birth, phone, email, addr, join_date, dept_code)
JOIN dept d ON d."Code" = e.dept_code
WHERE NOT EXISTS (
  SELECT 1 FROM hr.employees x WHERE x."Code" = e.code AND x."DeletedAt" IS NULL
);

COMMIT;
