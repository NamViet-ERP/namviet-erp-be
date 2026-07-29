# Checklist deploy — NamViet ERP (Backend)

Hướng dẫn deploy **production** cho repo `WareHouse_Management_BE` (.NET 8 API, PostgreSQL).  
Frontend: file `docs/DEPLOY.md` trong repo **`WareHouse_Management`**.

---

## Nguyên tắc vận hành

| Được | Không được |
|------|------------|
| Chạy migrator một lần trước khi rollout API | Cho web replica tự migrate schema khi startup |
| Master data qua script SQL **thủ công** sau khi business chốt | Chạy seed cây vật tư tự động trong migration |
| Secrets qua env / user-secrets / vault | JWT key hoặc password DB placeholder trên production |

Migration `SeedMaterialCategoryTree` là **no-op** — dữ liệu cây vật tư chỉ qua `scripts/seed-material-category-tree.sql` khi được phép.

---

## 1. Yêu cầu hạ tầng

- .NET 8+ runtime (hoặc publish self-contained)
- PostgreSQL 16+ (hoặc Docker `docker compose up -d postgres`)
- Reverse proxy HTTPS (khuyến nghị) — Nginx, IIS, Caddy, …
- Thư mục ghi file upload: `wwwroot/uploads/hr` (tạo tự động lúc startup)

---

## 2. Biến môi trường / cấu hình

Tham chiếu `.env.example` (Docker) và override cho API:

| Key | Bắt buộc | Ghi chú |
|-----|----------|---------|
| `ConnectionStrings__Default` | Có | Host, port, database, user, password mạnh |
| `Jwt__SigningKey` | Có | ≥ 32 ký tự, random, không placeholder |
| `Jwt__Issuer` / `Jwt__Audience` | Có | Khớp cấu hình client |
| `Cors__AllowedOrigins` | Có | URL frontend production, phân tách bằng dấu phẩy |
| `ForwardedHeaders__ClearKnownLists` | Không | Mặc định `true` khi API chỉ nhận traffic từ reverse proxy |
| `ForwardedHeaders__KnownProxies` | Khuyến nghị (prod) | IP proxy tin cậy, phân tách bằng dấu phẩy (vd. `10.0.0.5`). Khi set, chỉ tin các IP này |
| `FileStorage__Root` | Không | Thư mục upload private; mặc định `App_Data/private-uploads` |

`ConfigurationSecurityValidator` **fail fast** khi:

- Thiếu connection string hoặc JWT key  
- JWT key quá ngắn hoặc chứa chuỗi placeholder  
- Production: connection string chứa password yếu đã biết (`change_me_strong`, …)

### Ví dụ set env (Linux)

```bash
export ConnectionStrings__Default="Host=...;Port=5432;Database=namviet_erp;Username=...;Password=..."
export Jwt__SigningKey="your-random-secret-at-least-32-characters-long"
export Cors__AllowedOrigins="https://erp.namviet.vn"
export ASPNETCORE_ENVIRONMENT=Production
```

### Ví dụ user-secrets (dev máy cá nhân)

```bash
cd src/ApiHost/NamViet.ERP.Api
dotnet user-secrets set "ConnectionStrings:Default" "Host=localhost;Port=5434;..."
dotnet user-secrets set "Jwt:SigningKey" "your-random-secret-at-least-32-characters-long"
```

---

## 3. Database

### Docker (dev/staging)

```bash
cp .env.example .env
# Điền POSTGRES_PASSWORD, Jwt, ConnectionStrings
docker compose up -d postgres
```

### Migrate

API không migrate khi khởi động. Chạy migrator riêng trước khi rollout API; migrator lấy duy nhất
`ConnectionStrings:Default`, giữ PostgreSQL advisory lock và chạy lần lượt Identity, Warehouse, HR.

Local Docker (đợi PostgreSQL healthy rồi migrate):

```bash
docker compose up -d postgres
docker compose run --rm migrator
dotnet run --project src/ApiHost/NamViet.ERP.Api
```

PowerShell / CI runner:

```powershell
$env:ConnectionStrings__Default = "<secret connection string>"
./scripts/Invoke-DatabaseMigration.ps1

# Tạo SQL idempotent để review hoặc lưu artifact; lệnh này không kết nối/apply DB.
./scripts/Invoke-DatabaseMigration.ps1 -Command script -OutputPath artifacts/database-migrations.sql
```

---

## 4. Publish & chạy

```bash
dotnet restore
dotnet publish src/ApiHost/NamViet.ERP.Api -c Release -o ./publish
cd publish
dotnet NamViet.ERP.Api.dll
```

Health:

| Endpoint | Mục đích | Kỳ vọng |
|----------|----------|---------|
| `GET /health` | Liveness (process sống) | 200 — **không** phụ thuộc DB |
| `GET /health/ready` | Readiness (DB + file storage ghi được) | 200 khi sẵn sàng nhận traffic; 503 nếu DB/storage lỗi |

Orchestrator / load balancer: probe liveness → `/health`; chỉ route traffic khi `/health/ready` = 200.

**Swagger:** chỉ bật khi `ASPNETCORE_ENVIRONMENT=Development` — production không expose `/swagger` (mặc định trong `Program.cs`).

**Reverse proxy:** bật `X-Forwarded-For` / `X-Forwarded-Proto`. API dùng `UseForwardedHeaders` để rate-limit theo IP client thật (không phải IP proxy).

---

## 5. Checklist trước go-live

- [ ] `ConnectionStrings:Default` — password mạnh, không placeholder  
- [ ] `Jwt:SigningKey` — ≥ 32 ký tự, unique per environment  
- [ ] `Cors:AllowedOrigins` — đúng domain FE production  
- [ ] TLS terminate tại reverse proxy; `X-Forwarded-For` / `X-Forwarded-Proto` tới API  
- [ ] `ForwardedHeaders:KnownProxies` (hoặc mạng chỉ proxy mới vào được API)  
- [ ] PostgreSQL backup policy  
- [ ] `GET /health` = 200 và `GET /health/ready` = 200 sau migrate  
- [ ] Log (Serilog) đi tới file/aggregator — **không** log JWT / password  
- [ ] Rate limit auth/public endpoint — đã bật; IP lấy sau forwarded headers  
- [ ] Bootstrap admin **một lần** (`docs/OPS_FIRST_ADMIN.md`) — **không** chạy demo seed / `BootstrapRbacPilotUsers` trên production  
- [ ] Không có user admin cứng trong migration  

---

## 6. Tạo admin đầu tiên (không seed)

**Runbook chi tiết:** [`docs/OPS_FIRST_ADMIN.md`](OPS_FIRST_ADMIN.md)

Tóm tắt:

1. Chạy API + migrate xong.  
2. Break-glass một lần: `scripts/bootstrap-identity-roles.sql` + `scripts/BootstrapFirstAdmin/` (xem runbook).  
3. Sau đó mọi user tạo qua Admin API / UI — không lặp SQL.  
4. Kiểm tra login FE → menu Admin.

Không có tài khoản mặc định trong repo — đây là chủ đích.

---

## 7. Master data tùy chọn

Xem [`scripts/README.md`](../scripts/README.md).

### Cây vật tư (138 node)

Chỉ sau khi business chốt taxonomy:

```bash
psql -h <host> -U <user> -d <database> -f scripts/seed-material-category-tree.sql
```

Tạo lại SQL từ JSON: `node scripts/generate-material-category-tree-sql.mjs`

---

## 8. GitHub Actions (BE)

Workflow: `.github/workflows/ci.yml` — `dotnet build` + `dotnet test` trên branch **`production`** (push và PR).

Migration production chỉ chạy thủ công qua `.github/workflows/database-migrate.yml` (`workflow_dispatch`).
Tạo protected environment (ví dụ `production`), bật required reviewers và thêm environment secret
`DATABASE_CONNECTION_STRING`. Workflow migration có concurrency lock; PR CI không migrate.

---

## 9. Sau deploy — smoke test API

```bash
curl -s -o /dev/null -w "%{http_code}" https://api.<domain>/health
# Kỳ vọng: 200

curl -s -o /dev/null -w "%{http_code}" https://api.<domain>/health/ready
# Kỳ vọng: 200 (503 nếu DB/storage chưa sẵn)

curl -X POST https://api.<domain>/api/auth/login \
  -H "Content-Type: application/json" \
  -d '{"username":"...","password":"...","rememberMe":false}'
# Kỳ vọng: 200 + token (sau khi có user)
```

Staging (sau login): thử **1 flow Kho** (vd. danh sách NK hoặc tồn) + **1 flow Mua hàng** (vd. KHMH hoặc PO).

---

## 10. Endpoint public (cần biết khi harden)

**Audit đầy đủ:** [`docs/SECURITY_PUBLIC_ENDPOINTS.md`](SECURITY_PUBLIC_ENDPOINTS.md)

Tóm tắt: login/refresh (`auth`), onboard & apply (`public-write`), upload HR (`public-upload`), GET recruitment (`public-read`).

---

## 11. Khắc phục sự cố

| Lỗi startup | Nguyên nhân thường gặp |
|-------------|-------------------------|
| Jwt:SigningKey required | Chưa set env |
| ConnectionStrings:Default required | Chưa set env |
| Lỗi migrator | DB không reachable, thiếu quyền DDL, hoặc thiếu `ConnectionStrings:Default` |
| CORS từ FE | Thiếu origin trong `Cors:AllowedOrigins` |
| `/health/ready` 503 | DB unreachable / password sai, hoặc `FileStorage` không ghi được |

---

## 12. Gate release FE+BE

Checklist tổng hợp (“quy chuẩn hết”): repo FE `docs/RELEASE_GATES.md` + `npm run verify:release-gates`.
