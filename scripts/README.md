# Scripts — master data & bootstrap (thủ công)

Các script trong thư mục này **không chạy tự động** khi API startup. Ops chạy tay sau khi business / security chốt.

---

## Bootstrap admin đầu tiên

| File | Mục đích |
|------|----------|
| `bootstrap-identity-roles.sql` | Insert role chuẩn (idempotent) nếu DB mới |
| `BootstrapFirstAdmin/` | CLI tạo user Admin (idempotent theo email) |

Chi tiết: `docs/OPS_FIRST_ADMIN.md`

```powershell
# 1. Roles (psql)
psql ... -f scripts/bootstrap-identity-roles.sql

# 2. Admin user
cd scripts/BootstrapFirstAdmin
$env:ConnectionStrings__Default = "Host=...;..."
dotnet run -- --email admin@namviet.vn --name "Quản trị viên" --password "YourStrong1Pass"
```

---

## Cây vật tư (138 node)

| File | Mục đích |
|------|----------|
| `seed-material-category-tree.sql` | INSERT cây category 5 cấp vào PostgreSQL |
| `generate-material-category-tree-sql.mjs` | Tạo lại SQL từ JSON nguồn |

**Chỉ chạy khi business đã chốt taxonomy.** Migration EF `SeedMaterialCategoryTree` là no-op.

```bash
psql -h <host> -U <user> -d <database> -f scripts/seed-material-category-tree.sql
```

Regenerate SQL:

```bash
node scripts/generate-material-category-tree-sql.mjs
```

### Trước khi chạy seed vật tư

- [ ] Backup database  
- [ ] Xác nhận bảng `warehouse.material_categories` trống hoặc chấp nhận merge theo logic script  
- [ ] Không chạy trên staging/production mà chưa có sign-off

### Sau khi chạy

- [ ] Kiểm tra số node (~138) trong Admin / WMS category UI  
- [ ] Ghi nhận ngày chạy vào sổ vận hành

---

## Dữ liệu demo WMS (kho, vật tư, phiếu mẫu)

| File | Mục đích |
|------|----------|
| `SeedWmsDemoData/` | CLI seed idempotent: kho tổng, 2 dự án demo, 5 vật tư, NCC, phiếu NK/XK để dashboard có số liệu |

**Chỉ chạy khi đã có DB + migration.** Không chạy tự động lúc API startup.

```powershell
cd scripts/SeedWmsDemoData
# Dùng connection string giống API (user-secrets hoặc env)
$env:ConnectionStrings__Default = "Host=localhost;Port=5432;Database=namviet_erp;Username=...;Password=..."
dotnet run
```

Lần chạy sau sẽ bỏ qua theo marker (`demo_data_seed_v1`, `demo_data_seed_v2_bulk10`). Dùng `--force` để bỏ qua marker (vẫn skip theo mã đã tồn tại).

**Batch v3** thêm 3 nháp + 3 chờ duyệt cho NK/XK/KC (`NK-STATUS-*`, `XK-STATUS-*`, `KC-STATUS-*`).

**Batch v4** thêm 3 từ chối mỗi loại (`*-STATUS-REJ-01`…`03`) — đã hủy dự kiến, có ghi chú lý do.

**Batch v5** gán cây danh mục 5 cấp + quy cách/tiêu chuẩn cho `VT-DEMO-*` (cột Nhóm/Loại/Phân loại/Kiểu không còn trống).

**Batch v6** thêm 3 phiếu kiểm kê (`KK-DEMO-PENDING-01`, `KK-DEMO-PENDING-02`, `KK-DEMO-APPROVED-01`) + bản ghi `warehouses` cho kho dự án demo (FK kiểm kê).

**Batch v7** bổ sung phiếu KK để test UI: khôi phục `KK-DEMO-PENDING-01` nếu đã xóa mềm, thêm `KK-DEMO-PENDING-03`, `KK-DEMO-PENDING-04`, `KK-DEMO-CANCELLED-01`.

---

## Liên quan repo

- Deploy checklist: `docs/DEPLOY.md`  
- Endpoint public: `docs/SECURITY_PUBLIC_ENDPOINTS.md`
