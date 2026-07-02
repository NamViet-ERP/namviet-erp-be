# Audit — Endpoint `[AllowAnonymous]` & rate limiting

Tài liệu review bảo mật định kỳ (Phase 4). Cập nhật khi thêm route public mới.

Cấu hình rate limit: `src/ApiHost/NamViet.ERP.Api/Program.cs`  
Hằng số policy: `Infrastructure/RateLimiting/RateLimitPolicies.cs`

---

## Tóm tắt policy

| Policy | Permit / window | Mục đích |
|--------|-----------------|----------|
| `auth` | 10 / phút / IP | Chống brute-force login & refresh |
| `public-write` | 5 / phút / IP | Form ghi công khai (onboard, ứng tuyển) |
| `public-upload` | 10 / giờ / IP | Upload file ẩn danh (CV, ảnh HR) |
| `public-read` | 60 / phút / IP | Đọc công khai (danh sách tuyển dụng) |

Từ chối: HTTP **429** + JSON problem (`title`: "Quá nhiều yêu cầu").

---

## Bảng endpoint anonymous

| Method | Route | Controller | Rate limit | Rủi ro | Ghi chú |
|--------|-------|------------|------------|--------|---------|
| POST | `/api/auth/login` | `AuthController` | `auth` | Brute force | Username = email hoặc userName |
| POST | `/api/auth/refresh` | `AuthController` | `auth` | Token replay | Validator: refresh token không rỗng |
| POST | `/api/employees/onboard` | `EmployeesController` | `public-write` | Spam hồ sơ | FluentValidation onboard DTO |
| POST | `/api/candidates` | `CandidatesController` | `public-write` | Spam ứng viên | Validator create candidate |
| POST | `/api/hr/files/upload` | `HrFilesController` | `public-upload` | Abuse storage | Max 5MB; whitelist MIME |
| GET | `/api/recruitment/positions` | `RecruitmentPositionsController` | `public-read` | Enumeration / scrape | Chỉ đọc danh sách vị trí active |
| GET | `/api/recruitment/hr-staff` | `RecruitmentHrStaffController` | `public-read` | Lộ tên HR active | Public cho form ứng tuyển |

**Không anonymous:** `POST /api/auth/register` — yêu cầu role Admin.

---

## Khuyến nghị harden (cân nhắc khi scale)

### Ưu tiên cao

1. **WAF / reverse proxy**  
   - Giới hạn body size, geo block nếu chỉ phục vụ VN, bot protection trên form public.

### Ưu tiên trung bình

2. **Captcha** trên `POST /api/candidates` và `POST /api/employees/onboard` nếu bị spam.  
3. **Honeypot field** trên form FE (không thay thế rate limit).  
4. **Audit log** ghi IP + user-agent cho public write/upload.

### Upload (`HrFilesController`)

- Giới hạn: 5MB, category bắt buộc (`cv`, `employee-image`, …).  
- File lưu `wwwroot/uploads/hr` — đảm bảo disk quota và backup policy.  
- Không serve executable; kiểm tra extension + content-type.

---

## Endpoint auth & admin (không anonymous — tham chiếu smoke test)

| Nhóm | Route mẫu | Role |
|------|-----------|------|
| Auth | `GET /api/auth/me` | Authenticated |
| Admin users | `/api/admin/users/*` | Admin |
| Admin roles | `/api/admin/roles/*` | Admin |
| WMS vouchers | `/api/warehouse/imports|exports|transfers/*` | Theo policy module WMS |

Smoke test sau deploy: xem `docs/OPS_FIRST_ADMIN.md` §3.3 và `docs/DEPLOY.md` §9.

---

## Quy trình review định kỳ

1. `rg AllowAnonymous src/ApiHost` — so khớp bảng trên.  
2. Kiểm tra mỗi route có rate limit phù hợp.  
3. Xem log 429 — IP lặp lại bất thường.  
4. Cập nhật tài liệu này khi merge PR thêm endpoint public.

---

## Tham chiếu code

```24:54:src/ApiHost/NamViet.ERP.Api/Controllers/AuthController.cs
    [HttpPost("login")]
    [AllowAnonymous]
    [EnableRateLimiting(RateLimitPolicies.Auth)]
    // ...
    [HttpPost("refresh")]
    [AllowAnonymous]
    [EnableRateLimiting(RateLimitPolicies.Auth)]
```

```130:132:src/ApiHost/NamViet.ERP.Api/Controllers/Hr/EmployeesController.cs
    [HttpPost("onboard")]
    [AllowAnonymous]
    [EnableRateLimiting(RateLimitPolicies.PublicWrite)]
```
