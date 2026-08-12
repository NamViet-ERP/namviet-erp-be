-- Migration: MakeEmployeeDepartmentOptional
-- Cho phép nhân viên chưa phân phòng (onboard công khai).

ALTER TABLE hr.employees DROP CONSTRAINT IF EXISTS "FK_employees_departments_DepartmentId";
ALTER TABLE hr.employees ALTER COLUMN "DepartmentId" DROP NOT NULL;
ALTER TABLE hr.employees
  ADD CONSTRAINT "FK_employees_departments_DepartmentId"
  FOREIGN KEY ("DepartmentId") REFERENCES hr.departments("Id") ON DELETE SET NULL;

-- EF history table may live in public (or be absent on some local DBs). Skip if missing.
DO $$
BEGIN
  IF to_regclass('public."__EFMigrationsHistory"') IS NOT NULL THEN
    INSERT INTO public."__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    SELECT '20260812050000_MakeEmployeeDepartmentOptional', '8.0.29'
    WHERE NOT EXISTS (
      SELECT 1 FROM public."__EFMigrationsHistory"
      WHERE "MigrationId" = '20260812050000_MakeEmployeeDepartmentOptional'
    );
  END IF;
END $$;
