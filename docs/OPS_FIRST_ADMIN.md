# SOP — Tạo admin đầu tiên (không seed trong code)

Runbook một lần sau khi deploy BE production lần đầu. **Không** có tài khoản mặc định trong repo.

Tham chiếu: `docs/DEPLOY.md` (checklist deploy), `docs/SECURITY_PUBLIC_ENDPOINTS.md` (endpoint public).

---

## 1. Điều kiện tiên quyết

- [ ] API chạy được, `GET /health` → 200 và `GET /health/ready` → 200  
- [ ] EF migrate xong (Identity + Warehouse + HR)  
- [ ] `Jwt:SigningKey` và `ConnectionStrings:Default` đã set đúng  
- [ ] Biết email và mật khẩu mạnh cho admin đầu tiên (≥ 8 ký tự, có hoa + số — giống rule `RegisterCommandValidator`)

---

## 2. Vấn đề “gà – trứng”

| Endpoint | Yêu cầu auth |
|----------|----------------|
| `POST /api/auth/register` | Role **Admin** |
| `POST /api/admin/users/system` | Role **Admin** |
| `POST /api/admin/users/provision` | Role **Admin** |

→ Admin đầu tiên **không** tạo được qua API khi chưa có bất kỳ user Admin nào. Cần **break-glass** một lần (script thủ công bên dưới).

Sau khi có admin đầu tiên, mọi tài khoản tiếp theo tạo qua Admin UI hoặc API admin — **không** lặp lại SQL.

---

## 3. Break-glass — script thủ công (khuyến nghị)

### Bước 3.1 — Tạo vai trò chuẩn (nếu bảng `identity.roles` trống)

```bash
psql -h <host> -U <user> -d <database> -f scripts/bootstrap-identity-roles.sql
```

Script chỉ `INSERT` role khi chưa tồn tại (`ON CONFLICT DO NOTHING`). Không tạo user.

### Bước 3.2 — Hash mật khẩu + tạo user Admin

```powershell
cd scripts/BootstrapFirstAdmin
$env:ConnectionStrings__Default = "Host=...;Port=5432;Database=...;Username=...;Password=..."
dotnet run -- `
  --email admin@namviet.vn `
  --name "Quản trị viên" `
  --password "YourStrong1Pass" `
  --employee-code "SYS-ADMIN"
```

Tool idempotent: nếu email đã tồn tại → thoát 0, không ghi đè.

### Bước 3.3 — Xác minh login

```bash
curl -s -X POST https://api.<domain>/api/auth/login \
  -H "Content-Type: application/json" \
  -d '{"username":"admin@namviet.vn","password":"YourStrong1Pass","rememberMe":false}'
```

Kỳ vọng: HTTP 200, body có `accessToken`, `user.role` = `"admin"`.

### Bước 3.4 — Smoke trên FE

1. Mở URL production FE  
2. Đăng nhập tài khoản vừa tạo  
3. Vào **Admin → Người dùng** (`/admin/users`)  
4. Tạo thêm user qua **Cấp tài khoản** / **Tài khoản hệ thống** — không dùng SQL nữa

---

## 4. Luồng vận hành thường ngày (sau bootstrap)

### 4.1 Cấp tài khoản cho nhân viên HR

`POST /api/admin/users/provision` (Admin token):

```json
{
  "employeeId": "123",
  "role": "employee",
  "password": "TempStrong1Pass"
}
```

Điều kiện: nhân viên đã có email trong HR, chưa có `UserId`.

### 4.2 Tài khoản hệ thống (không gắn HR)

`POST /api/admin/users/system`:

```json
{
  "name": "Kế toán trưởng",
  "email": "accountant@namviet.vn",
  "phone": null,
  "employeeCode": null,
  "role": "accountant",
  "password": "TempStrong1Pass",
  "department": null,
  "position": null
}
```

### 4.3 Reset mật khẩu

`PUT /api/admin/users/{accountUserId}/password`:

```json
{ "password": "NewStrong1Pass" }
```

### 4.4 Refresh token

`POST /api/auth/refresh` (public, rate limit `auth`):

```json
{ "refreshToken": "<refresh_token_from_login>" }
```

---

## 5. Checklist sau bootstrap

- [ ] Đổi mật khẩu admin break-glass nếu dùng mật khẩu tạm  
- [ ] Ghi nhận email admin vào sổ vận hành (không commit vào git)  
- [ ] Tắt / không commit connection string trên máy ops  
- [ ] Xác nhận rate limit auth hoạt động (thử >10 login/phút → 429)  
- [ ] Không chạy lại `BootstrapFirstAdmin` trên production trừ khi mất toàn bộ admin

---

## 6. Khắc phục sự cố

| Triệu chứng | Hướng xử lý |
|-------------|-------------|
| Login 401 | Kiểm tra `IsActive`, email đúng, hash password |
| Login 200 nhưng không vào Admin | Kiểm tra `identity.user_roles` có role `Admin` |
| Provision 409 | Nhân viên đã có tài khoản |
| Refresh 401 | Token hết hạn / đã revoke — login lại |

---

## 7. Liên quan CI / test

- Unit test validator: `tests/NamViet.ERP.Tests/Identity/`  
- Workflow phiếu kho: `tests/NamViet.ERP.Tests/Wms/VoucherWorkflowTests.cs`  
- CI chạy `dotnet test` — không cần PostgreSQL cho các test hiện tại
