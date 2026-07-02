# BE-only API modules (chưa có FE)

Các endpoint dưới đây tồn tại trên API host nhưng **chưa được** repo `namviet-erp-fe` gọi (rà soát Phase 3).  
Giữ lại cho roadmap / tích hợp sau — **không** xóa trừ khi business xác nhận bỏ nghiệp vụ.

| Controller | Route prefix | Ghi chú |
|------------|--------------|---------|
| `AttendanceController` | `/api/attendance` | Chấm công |
| `InsuranceController` | `/api/insurance` | Bảo hiểm |
| `KpiController` | `/api/kpi` | KPI HR |
| `LeaveController` | `/api/leave` | Nghỉ phép |
| `LoansController` | `/api/loans` | Tạm ứng / vay |
| `PriceBooksController` | `/api/price-books` | Bảng giá |
| `TransactionsController` | `/api/transactions` | Giao dịch tổng hợp (legacy?) |
| `AdminRequestsController` | `/api/admin-requests` | Yêu cầu admin |
| `DepartmentsController` | `/api/departments` | Phòng ban (HR có thể dùng gián tiếp) |
| `Wms/ConversionsController` | `/api/conversions` | Chuyển đổi kho |
| `Wms/LendingLogsController` | `/api/lending-logs` | Mượn vật tư giữa dự án |

## Projects — một contract duy nhất

| Dùng | Không dùng (đã gỡ Phase 3) |
|------|------------------------------|
| `GET/POST /api/warehouse/projects` (`ProjectsController`) | ~~`/api/projects`~~ legacy facade |

FE chỉ gọi `/api/warehouse/projects`.

## Dashboard WMS

FE `DashboardWarehousePage` gọi:

- `GET /api/dashboard/kpi`
- `GET /api/dashboard/trend`
- `GET /api/dashboard/stock-by-category`
- `GET /api/dashboard/activity-feed`

Portal dashboard: `top-products`, `top-materials`, sinh nhật: `GET /api/hr/birthdays`.
