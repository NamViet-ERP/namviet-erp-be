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

## Liên quan repo

- Deploy checklist: `docs/DEPLOY.md`  
- Endpoint public: `docs/SECURITY_PUBLIC_ENDPOINTS.md`
