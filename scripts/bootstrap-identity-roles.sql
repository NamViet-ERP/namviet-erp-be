-- Standard Identity roles (one-time, idempotent).
-- Schema: identity.* — see IdentityDbContext.
-- Run BEFORE bootstrap-first-admin tool if roles table is empty.

INSERT INTO identity.roles ("Id", "Name", "NormalizedName", "ConcurrencyStamp", "Description", "Module", "IsActive")
VALUES
  ('11111111-1111-1111-1111-111111111101', 'Admin',             'ADMIN',             gen_random_uuid()::text, 'Quản trị viên',              'General', true),
  ('11111111-1111-1111-1111-111111111102', 'HR',                'HR',                gen_random_uuid()::text, 'Nhân sự (HR)',               'General', true),
  ('11111111-1111-1111-1111-111111111103', 'PM',                'PM',                gen_random_uuid()::text, 'Quản lý dự án (PM)',         'General', true),
  ('11111111-1111-1111-1111-111111111104', 'Warehouse_Keeper',  'WAREHOUSE_KEEPER',  gen_random_uuid()::text, 'Thủ kho (Storekeeper)',      'General', true),
  ('11111111-1111-1111-1111-111111111105', 'Accountant',        'ACCOUNTANT',        gen_random_uuid()::text, 'Kế toán trưởng',             'General', true),
  ('11111111-1111-1111-1111-111111111106', 'Procurement',       'PROCUREMENT',       gen_random_uuid()::text, 'Phòng mua hàng',             'General', true),
  ('11111111-1111-1111-1111-111111111107', 'Employee',          'EMPLOYEE',          gen_random_uuid()::text, 'Nhân viên',                  'General', true)
ON CONFLICT ("Id") DO NOTHING;
