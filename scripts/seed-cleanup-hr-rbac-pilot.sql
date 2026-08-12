-- Wave E3: Xóa PB-RBAC pilot + 80 NV giả; gắn KH tuyển dụng vào phòng thật.
-- Giữ NV-2101..NV-2112 (Wave A). Idempotent.
-- Apply: docker cp + psql -f (UTF-8).

BEGIN;

-- ---------------------------------------------------------------------------
-- 1) Soft-delete 80 NV pilot trong PB-RBAC (không đụng NV-21xx)
-- ---------------------------------------------------------------------------
UPDATE hr.employees e
SET "DeletedAt" = NOW(), "UpdatedAt" = NOW()
WHERE e."DeletedAt" IS NULL
  AND e."DepartmentId" = (
    SELECT d."Id" FROM hr.departments d
    WHERE d."Code" = 'PB-RBAC' AND d."DeletedAt" IS NULL
    LIMIT 1
  )
  AND e."Code" NOT LIKE 'NV-21%';

-- Phòng trống thì soft-delete
UPDATE hr.departments d
SET "DeletedAt" = NOW(), "UpdatedAt" = NOW(), "IsActive" = false
WHERE d."Code" = 'PB-RBAC'
  AND d."DeletedAt" IS NULL
  AND NOT EXISTS (
    SELECT 1 FROM hr.employees e
    WHERE e."DepartmentId" = d."Id" AND e."DeletedAt" IS NULL
  );

-- ---------------------------------------------------------------------------
-- 2) Soft-delete vị trí tuyển dụng E2E rác (giữ vị trí nghiệp vụ)
-- ---------------------------------------------------------------------------
UPDATE hr.recruitment_positions
SET "DeletedAt" = NOW(), "UpdatedAt" = NOW()
WHERE "DeletedAt" IS NULL
  AND ("Name" ILIKE 'E2E%' OR "Name" ILIKE '%Form VT%');

-- ---------------------------------------------------------------------------
-- 3) Cập nhật / thay KH tuyển dụng thật (KT + IT)
-- ---------------------------------------------------------------------------
UPDATE hr.recruitment_plans p
SET "DepartmentId" = (SELECT d."Id" FROM hr.departments d WHERE d."Code" = 'KT' AND d."DeletedAt" IS NULL LIMIT 1),
    "PositionId" = (SELECT x."Id" FROM hr.recruitment_positions x WHERE x."Name" = 'Kế toán trưởng' AND x."DeletedAt" IS NULL LIMIT 1),
    "Headcount" = 1,
    "Reason" = 'Bổ sung Kế toán trưởng cho khối tài chính Tower',
    "Period" = 'Q3-2026',
    "Status" = 'Active',
    "OpenDate" = DATE '2026-07-01',
    "DeadlineDate" = DATE '2026-10-31',
    "UpdatedAt" = NOW(),
    "DeletedAt" = NULL
WHERE p."Id" = 4;

UPDATE hr.recruitment_plans p
SET "DepartmentId" = (SELECT d."Id" FROM hr.departments d WHERE d."Code" = 'IT' AND d."DeletedAt" IS NULL LIMIT 1),
    "PositionId" = (SELECT x."Id" FROM hr.recruitment_positions x WHERE x."Name" = 'Kỹ sư phần mềm' AND x."DeletedAt" IS NULL LIMIT 1),
    "Headcount" = 2,
    "Reason" = 'Tuyển Kỹ sư phần mềm vận hành ERP nội bộ',
    "Period" = 'Q3-2026',
    "Status" = 'Active',
    "OpenDate" = DATE '2026-07-01',
    "DeadlineDate" = DATE '2026-09-30',
    "UpdatedAt" = NOW(),
    "DeletedAt" = NULL
WHERE p."Id" = 3;

-- Soft-delete KH còn gắn PB-RBAC / seed-demo nếu sót
UPDATE hr.recruitment_plans p
SET "DeletedAt" = NOW(), "UpdatedAt" = NOW()
WHERE p."DeletedAt" IS NULL
  AND (
    COALESCE(p."Reason", '') ILIKE '%seed-demo%'
    OR p."DepartmentId" = (
      SELECT d."Id" FROM hr.departments d WHERE d."Code" = 'PB-RBAC' LIMIT 1
    )
  );

COMMIT;
