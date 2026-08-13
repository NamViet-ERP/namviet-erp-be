# BE-only API modules (chưa có FE)

Các endpoint dưới đây tồn tại trên API host nhưng **chưa được** repo `WareHouse_Management` (FE) gọi.  
Giữ lại cho roadmap / tích hợp sau — **không** xóa trừ khi business xác nhận bỏ nghiệp vụ.

## FE đang gọi (không còn BE-only)

| Controller | Route prefix | FE |
|------------|--------------|----|
| `DepartmentsController` | `/api/hr/departments` | `departmentsService`, `recruitmentPlansService`, `DepartmentsSettingsPanel` |

## Chưa có FE

| Controller | Route prefix | Ghi chú |
|------------|--------------|---------|
| `AttendanceController` | `/api/hr/attendance` | Chấm công |
| `InsuranceController` | `/api/hr/insurance` | Bảo hiểm (C&B) |
| `KpiController` | `/api/hr/kpi` | KPI HR |
| `LeaveController` | `/api/hr/leaves` | Nghỉ phép |
| `LoansController` | `/api/warehouse/loans` | Mượn / tạm ứng vật tư |
| `TransactionsController` | `/api/warehouse/transactions` | Giao dịch kho tổng hợp |
| `AdminRequestsController` | `/api/hr/admin-requests` | Yêu cầu admin HR |
| `PositionsController` | `/api/hr/positions` | Chức danh HR (khác `api/recruitment/positions`) |
| `HrAdminEmployeesController` | `/api/hr/employees` | HR admin: contracts, appointments (FE dùng `api/employees`) |
| `Wms/ConversionsController` | `/api/conversions` | Chuyển đổi đơn vị / quy đổi |
| `Wms/LendingLogsController` | `/api/lending-logs` | Mượn vật tư giữa dự án |
| `Wms/WarehousesController` | `/api/warehouses` | Danh mục kho vật lý |
| `Wms/SettingsController` | `/api/settings` | Cấu hình WMS theo section |

> **Đã gỡ:** `PriceBooksController` / bảng giá — migration `DropPriceBooks` (không còn API).

## Projects — contract FE đang dùng

| Dùng (FE `projectsService`) | Không dùng |
|-----------------------------|------------|
| `GET/POST/PUT /api/warehouse/projects` | ~~`/api/projects`~~ — đã gỡ (Phase 3 P3) |

FE chỉ gọi `/api/warehouse/projects` (+ `inventory`, `ledger`, `close`).

## Route naming (nợ kỹ thuật — chưa chuẩn hóa)

Một số resource dùng prefix `api/warehouse/…`, một số dùng route ngắn (`api/imports`, `api/candidates`).  
**Không đổi hàng loạt** trừ khi có migration plan FE+BE; tài liệu này ghi nhận trạng thái hiện tại.

| Nhóm | Ví dụ route |
|------|-------------|
| Warehouse (prefixed) | `/api/warehouse/materials`, `/api/warehouse/projects`, `/api/warehouse/suppliers` |
| Voucher (short) | `/api/imports`, `/api/exports`, `/api/transfers`, `/api/stocktakes` |
| Catalog (short) | `/api/categories`, `/api/material-units`, `/api/stock/matrix` |
| HR (mixed) | `/api/employees`, `/api/hr/birthdays`, `/api/recruitment/positions` |
| Admin | `/api/admin/users`, `/api/admin/roles` |
| Auth | `/api/auth/login` |

## Dashboard WMS

FE `DashboardWarehousePage` gọi:

- `GET /api/dashboard/kpi`
- `GET /api/dashboard/trend`
- `GET /api/dashboard/stock-by-category`
- `GET /api/dashboard/stock-by-project`
- `GET /api/dashboard/activity-feed`

Portal dashboard: `top-products`, `top-materials`, sinh nhật: `GET /api/hr/birthdays`.
