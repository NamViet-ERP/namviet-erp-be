-- Repair EF migration history after clean-db truncated __ef_migrations_* tables.
-- Run when API fails with: relation "materials" already exists

INSERT INTO warehouse.__ef_migrations_warehouse ("MigrationId", "ProductVersion") VALUES
  ('20260611040824_InitialCreate', '8.0.28'),
  ('20260613031856_WmsFacadeSchema', '8.0.28'),
  ('20260615020101_SchemaIntegrityFix', '8.0.28'),
  ('20260615030100_WmsVoucherDateCleanup', '8.0.28'),
  ('20260617130000_RenameProjectFeIdToClientProjectId', '8.0.28'),
  ('20260618043624_AddCategoryTreeAndReservations', '8.0.28'),
  ('20260618120000_MaterialLifecycleAndTxSnapshots', '8.0.28'),
  ('20260619120000_AddTransactionItemSupplier', '8.0.28'),
  ('20260620120000_FixWarehouseApprovalStatusLegacy', '8.0.28')
ON CONFLICT DO NOTHING;

INSERT INTO hr.__ef_migrations_hr ("MigrationId", "ProductVersion") VALUES
  ('20260611040831_InitialCreate', '8.0.28'),
  ('20260611070519_AddHrRelationships', '8.0.28'),
  ('20260613031918_HrFeExtensions', '8.0.28'),
  ('20260615020144_HrEnumAndAssigneeFk', '8.0.28'),
  ('20260615041416_HrEmployeePhotoFields', '8.0.28')
ON CONFLICT DO NOTHING;

SELECT 'warehouse_migrations' AS metric, count(*)::text AS value FROM warehouse.__ef_migrations_warehouse
UNION ALL
SELECT 'hr_migrations', count(*)::text FROM hr.__ef_migrations_hr;
