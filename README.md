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

# 2. Khởi động PostgreSQL bằng Docker
docker compose up -d postgres

# 3. Khôi phục & build
dotnet restore
dotnet build

# 4. Tạo / cập nhật database (chạy migration)
dotnet ef database update -c IdentityDbContext  -p src/Modules/Identity/NamViet.ERP.Modules.Identity.Infrastructure  -s src/ApiHost/NamViet.ERP.Api
dotnet ef database update -c WarehouseDbContext -p src/Modules/Warehouse/NamViet.ERP.Modules.Warehouse.Infrastructure -s src/ApiHost/NamViet.ERP.Api
dotnet ef database update -c HrDbContext        -p src/Modules/Hr/NamViet.ERP.Modules.Hr.Infrastructure                -s src/ApiHost/NamViet.ERP.Api

# 5. Chạy API
dotnet run --project src/ApiHost/NamViet.ERP.Api
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
