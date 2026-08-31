# Scripts — master data & bootstrap (thủ công)

Các script trong thư mục này **không chạy tự động** khi API startup. Ops chạy tay sau khi business / security chốt.

## Phân loại an toàn

| Nhãn | Ý nghĩa |
|------|---------|
| **Production-safe** | Có thể chạy trên prod khi đã có sign-off (idempotent / không ghi đè mật khẩu). |
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

## Smoke API — local / CI

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
