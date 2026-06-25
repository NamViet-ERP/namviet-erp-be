# BE Audit Hardening — Task Tracker

> **Source:** `code_audit_report.md` (2026-06-25)  
> **Scope:** `WareHouse_Management_BE`

---

## Phase A — Secrets & JWT (CRITICAL quick wins)

| ID | Task | Status |
|----|------|--------|
| A1 | Xóa credentials khỏi `appsettings.Development.json` | [x] |
| A2 | Docker Compose: bỏ password hardcoded PgAdmin | [x] |
| A3 | Startup validate `Jwt:SigningKey` (≥32 chars, không placeholder) | [x] |
| A4 | Startup validate `ConnectionStrings:Default` không rỗng | [x] |
| A5 | Cập nhật `.env.example` + hướng dẫn user-secrets | [x] |
| A6 | `dotnet build` verify | [x] |

## Phase B — Concurrency control (CRITICAL)

| ID | Task | Status |
|----|------|--------|
| B1 | `RowVersion` trên `ProjectInventory` | [x] |
| B2 | `RowVersion` trên `WarehouseTransaction` | [x] |
| B3 | EF migration + handle `DbUpdateConcurrencyException` | [x] |

## Phase C — HIGH (BE)

| ID | Task | Status |
|----|------|--------|
| C1 | Tách `AdminUsersController` → `AdminUserQueryService` | [x] |
| C2 | DRY Imports/Exports/Transfers → `WmsVoucherControllerResponses` | [x] |
| C3 | `MaterialsController` — category lookup theo ancestor, không load full tree mọi request | [x] |
| C4 | Login — thống nhất message chống user enumeration | [x] |

## FE HIGH (tham chiếu)

| ID | Task | Status |
|----|------|--------|
| H-1 | Mock tree-shake (`__USE_MOCK__`) | [x] (mock-consolidation) |
| H-2 | Dashboard tách hook/component | [x] (data mock giữ đến khi có API) |
| H-5 | `getSessionUser` không fallback mock khi `USE_MOCK=false` | [x] |

---

## Dev setup sau Phase A

```powershell
cd src/ApiHost/NamViet.ERP.Api
dotnet user-secrets set "ConnectionStrings:Default" "Host=localhost;Port=5434;Database=namviet_erp;Username=erp_admin;Password=YOUR_STRONG_PASSWORD"
dotnet user-secrets set "Jwt:SigningKey" "YOUR_RANDOM_SECRET_AT_LEAST_32_CHARACTERS_LONG"
```

Hoặc copy `.env.example` → `.env` (docker + local).
