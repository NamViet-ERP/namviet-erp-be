# Scripts — master data & bootstrap (thủ công)

Các script trong thư mục này **không chạy tự động** khi API startup. Ops chạy tay sau khi business / security chốt.

## Phân loại an toàn

| Nhãn | Ý nghĩa |
|------|---------|
| **Production-safe** | Có thể chạy trên prod khi đã có sign-off (idempotent / không ghi đè mật khẩu). |
| **Demo-only** | Chỉ local/demo. Cần `ALLOW_DEMO_BOOTSTRAP=1` hoặc rõ ràng “không dùng production”. |
| **Destructive** | Xóa / ghi đè dữ liệu. Backup trước; không chạy mù trên DB có dữ liệu thật. |

---

## Bootstrap admin đầu tiên — Production-safe

| File | Mục đích |
|------|----------|
| `bootstrap-identity-roles.sql` | Insert role chuẩn (idempotent) nếu DB mới |
| `BootstrapFirstAdmin/` | CLI tạo user Admin (idempotent theo email; **không** ghi đè mật khẩu nếu đã tồn tại) |

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

## Cây vật tư (138 node) — Production-safe (sau sign-off)

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

## Dữ liệu demo WMS — Demo-only

| File | Mục đích |
|------|----------|
| `SeedWmsDemoData/` | CLI seed idempotent: kho tổng, 2 dự án demo, 5 vật tư, NCC, phiếu NK/XK để dashboard có số liệu |

**Chỉ chạy khi đã có DB + migration.** Không chạy tự động lúc API startup. **Không dùng production.**

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

## RBAC pilot users — Demo-only

| File | Mục đích |
|------|----------|
| `BootstrapRbacPilotUsers/` | Tạo user + hồ sơ NV pilot + gán thủ kho vào dự án demo |

**Bắt buộc:**

- `ALLOW_DEMO_BOOTSTRAP=1`
- `--password <Pwd>` (không có mật khẩu mặc định trong code)
- `--force-reset` chỉ khi muốn ghi đè mật khẩu user đã tồn tại

| Email (ví dụ) | Role | Mã NV |
|---------------|------|-------|
| `admin@namviet.vn` | Admin | NV-0001 |
| `hr01@namviet.vn` … | HR | … |
| `keeper01@namviet.vn` … | Warehouse_Keeper | … |
| `proc01@namviet.vn` … | Procurement | … |
| `bod01@namviet.vn` … | BOD | … |

(Xem danh sách đầy đủ trong `Program.cs`.)

Gán thủ kho (nếu dự án đã seed): `keeper01@`… → các mã `DA-2026-DEMO-*` / `DEMO-PUR-PROJ`.

```powershell
# Nên chạy SeedWmsDemoData (+ SeedPurchasingDemoData) trước để có dự án demo
cd scripts/BootstrapRbacPilotUsers
$env:ALLOW_DEMO_BOOTSTRAP = "1"
$env:ConnectionStrings__Default = "Host=localhost;Port=5432;Database=namviet_erp;Username=...;Password=..."
dotnet run -- --password "YourStrong1Pass"
# Reset mật khẩu user đã có:
# dotnet run -- --password "YourStrong1Pass" --force-reset
```

Script idempotent: user mới → tạo với `--password`; user đã có → **không** reset trừ `--force-reset`; sync role / hồ sơ NV; gán keeper bỏ qua nếu đã tồn tại.

---

## Dữ liệu demo mua hàng (KHMH / PO) — Demo-only

| File | Mục đích |
|------|----------|
| `SeedPurchasingDemoData/` | CLI seed dự án `DEMO-PUR-PROJ`, KHMH, PO, NCC demo |

Chỉ chạy khi cần dữ liệu mẫu (local/demo). Migrate warehouse **không** tự seed purchasing.

```powershell
cd scripts/SeedPurchasingDemoData
$env:ConnectionStrings__Default = "Host=localhost;Port=5432;Database=namviet_erp;Username=...;Password=..."
dotnet run
# Idempotent: đã có marker thì bỏ qua. Upsert lại: dotnet run -- --force
```

---

## Smoke API — Demo-only / CI local

| File | Mục đích |
|------|----------|
| `smoke-wms-api.ps1` | Gọi vài endpoint WMS sau login |

**Bắt buộc env** (không hardcode credential):

```powershell
$env:SMOKE_API_USERNAME = "admin@namviet.vn"
$env:SMOKE_API_PASSWORD = "YourStrong1Pass"
.\scripts\smoke-wms-api.ps1
```

---

## Migration có bước Destructive (lưu ý ops)

Một số EF migration **đã apply** chứa DML xóa dữ liệu khi evolve schema. Không đổi file đã apply; chỉ ghi nhận rủi ro khi migrate DB cũ:

| Migration | Hành vi |
|-----------|---------|
| `20260718100000_AddPurchasingPlansAndOrderColumns` | `DELETE` các `purchase_orders` (và items) có `PlanCode` IS NULL trước khi `SET NOT NULL` |

Trên DB đã có PO stub không gắn KHMH: **mất dữ liệu PO đó**. Backup trước khi migrate DB legacy.

---

## Liên quan repo

- Deploy checklist: `docs/DEPLOY.md`  
- Endpoint public: `docs/SECURITY_PUBLIC_ENDPOINTS.md`  
- First admin: `docs/OPS_FIRST_ADMIN.md`
