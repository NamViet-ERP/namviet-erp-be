# ============================================================
# Script tạo EF migrations lần đầu cho NamViet ERP Backend
# Chạy từ thư mục gốc solution: D:\WorkspaceRoot\NamViet\WareHouse_Management_BE
# ============================================================

$ApiProject = "src\ApiHost\NamViet.ERP.Api"

Write-Host "== Restore packages ==" -ForegroundColor Cyan
dotnet restore

Write-Host "`n== Tạo migration Identity ==" -ForegroundColor Green
dotnet ef migrations add InitialCreate `
    --context IdentityDbContext `
    --project "src\Modules\Identity\NamViet.ERP.Modules.Identity.Infrastructure" `
    --startup-project $ApiProject `
    --output-dir "Persistence\Migrations"

Write-Host "`n== Tạo migration Warehouse ==" -ForegroundColor Green
dotnet ef migrations add InitialCreate `
    --context WarehouseDbContext `
    --project "src\Modules\Warehouse\NamViet.ERP.Modules.Warehouse.Infrastructure" `
    --startup-project $ApiProject `
    --output-dir "Persistence\Migrations"

Write-Host "`n== Tạo migration HR ==" -ForegroundColor Green
dotnet ef migrations add InitialCreate `
    --context HrDbContext `
    --project "src\Modules\Hr\NamViet.ERP.Modules.Hr.Infrastructure" `
    --startup-project $ApiProject `
    --output-dir "Persistence\Migrations"

Write-Host "`n== Done! Chạy API để apply migration tự động ==" -ForegroundColor Cyan
Write-Host "dotnet run --project $ApiProject" -ForegroundColor Yellow
