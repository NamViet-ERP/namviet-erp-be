namespace NamViet.ERP.Modules.Warehouse.Domain.Enums;

public enum ProjectType
{
    Project,            // Dự án thi công đang chạy
    GeneralWarehouse    // Kho tổng / Kho kết chuyển chung
}

public enum ProjectStatus
{
    Active,
    Transferring,
    Transferred,
    Closed
}

public enum LoanStatus
{
    Borrowing,  // Đang mượn
    Returned,   // Đã trả đủ
    Overdue     // Quá hạn trả
}

public enum TransactionType
{
    Import,       // Nhập kho
    Export,       // Xuất kho
    LoanExport,   // Xuất mượn
    LoanImport,   // Nhập trả mượn
    Transfer,     // Kết chuyển
    ExportReturn  // Hoàn trả vật tư theo phiếu xuất
}

public enum ApprovalStatus
{
    Draft,      // Nháp — tạo reservation, chưa gửi duyệt
    Pending,    // Chờ duyệt (Thủ kho)
    Approved,   // Đã duyệt — khóa chỉnh sửa
    Rejected    // Từ chối
}

public enum WarehouseKeeperApprovalStatus
{
    Pending,
    Approved,
    Rejected
}
