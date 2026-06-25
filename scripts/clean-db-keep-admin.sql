-- Clean NamViet ERP database: keep only seed admin account + system roles.
-- Default admin email: admin@namviet.vip (override via psql variable if needed)
\set admin_email 'admin@namviet.vip'

BEGIN;

-- ── Warehouse: wipe all business data ───────────────────────────────────────
DO $$
DECLARE
    tbl RECORD;
BEGIN
    FOR tbl IN
        SELECT tablename
        FROM pg_tables
        WHERE schemaname = 'warehouse'
          AND tablename NOT LIKE '__ef_migrations%'
          AND tablename <> '__EFMigrationsHistory'
    LOOP
        EXECUTE format('TRUNCATE TABLE warehouse.%I RESTART IDENTITY CASCADE', tbl.tablename);
    END LOOP;
END $$;

-- ── HR: wipe all business data ──────────────────────────────────────────────
DO $$
DECLARE
    tbl RECORD;
BEGIN
    FOR tbl IN
        SELECT tablename
        FROM pg_tables
        WHERE schemaname = 'hr'
          AND tablename NOT LIKE '__ef_migrations%'
          AND tablename <> '__EFMigrationsHistory'
    LOOP
        EXECUTE format('TRUNCATE TABLE hr.%I RESTART IDENTITY CASCADE', tbl.tablename);
    END LOOP;
END $$;

-- ── Identity: remove non-admin users, keep roles ────────────────────────────
DELETE FROM identity.refresh_tokens rt
USING identity.users u
WHERE rt."UserId" = u."Id"
  AND lower(u."Email") <> lower(:'admin_email');

DELETE FROM identity.user_roles ur
USING identity.users u
WHERE ur."UserId" = u."Id"
  AND lower(u."Email") <> lower(:'admin_email');

DELETE FROM identity.user_claims uc
USING identity.users u
WHERE uc."UserId" = u."Id"
  AND lower(u."Email") <> lower(:'admin_email');

DELETE FROM identity.user_logins ul
USING identity.users u
WHERE ul."UserId" = u."Id"
  AND lower(u."Email") <> lower(:'admin_email');

DELETE FROM identity.user_tokens ut
USING identity.users u
WHERE ut."UserId" = u."Id"
  AND lower(u."Email") <> lower(:'admin_email');

DELETE FROM identity.users
WHERE lower("Email") <> lower(:'admin_email');

COMMIT;

-- Summary
SELECT 'users_remaining' AS metric, count(*)::text AS value FROM identity.users
UNION ALL
SELECT 'warehouse_tables', count(*)::text FROM pg_tables WHERE schemaname = 'warehouse' AND tablename <> '__EFMigrationsHistory'
UNION ALL
SELECT 'hr_employees', count(*)::text FROM hr.employees;
