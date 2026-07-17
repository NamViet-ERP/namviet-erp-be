# NamViet ERP - Backend

Backend cho hệ thống NamViet ERP, xây dựng theo kiến trúc **Modular Monolith + Clean Architecture** trên .NET 8, PostgreSQL, xác thực JWT + ASP.NET Identity.

## Module
- **Identity** - đăng nhập, JWT, phân quyền theo role
- **Warehouse (Kho)** - sản phẩm, kho, nhập/xuất/chuyển/chuyển đổi/kiểm kê/điều chỉnh, tồn kho
- **HR (Nhân sự)** - Tuyển dụng, Nhân viên, C&B (chấm công/nghỉ phép), BHXH, KPI, Công cụ hành chính

## Yêu cầu
- .NET SDK 8 trở lên (đã test với SDK 10)
- PostgreSQL (hoặc Docker Desktop)

## Chạy nhanh (dev)

```bash
# 1. Tạo file môi trường
cp .env.example .env   # Windows: copy .env.example .env

# 2. Khởi động PostgreSQL và đợi healthy
docker compose up -d postgres

# 3. Khôi phục & build
dotnet restore
dotnet build

# 4. Chạy migration một lần (Identity -> Warehouse -> HR)
docker compose run --rm migrator
# Hoặc khi ConnectionStrings__Default / user-secret đã được set:
./scripts/Invoke-DatabaseMigration.ps1

# 5. Chạy API
dotnet run --project src/ApiHost/NamViet.ERP.Api
```

API không tự thay đổi schema khi khởi động. Tạo SQL idempotent để review/deploy:

```powershell
./scripts/Invoke-DatabaseMigration.ps1 -Command script -OutputPath artifacts/database-migrations.sql
```

Swagger: `https://localhost:<port>/swagger`

**Deploy production:** [docs/DEPLOY.md](docs/DEPLOY.md)

## Cấu trúc thư mục

```
src/
  ApiHost/NamViet.ERP.Api/            # Host duy nhất (controllers, DI, middleware)
  BuildingBlocks/
    NamViet.ERP.SharedKernel/
    NamViet.ERP.Application.Abstractions/
  Modules/
    Identity/  | Warehouse/ | Hr/     # mỗi module: Domain | Application | Infrastructure
tests/
```

Mỗi module dùng schema PostgreSQL riêng: `identity`, `warehouse`, `hr`.
