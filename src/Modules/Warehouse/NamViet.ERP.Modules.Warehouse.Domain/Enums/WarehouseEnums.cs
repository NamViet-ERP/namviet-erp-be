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
    Transfer      // Kết chuyển
}

public enum ApprovalStatus
{
    Pending,    // Chờ duyệt
    Approved,   // Đã duyệt
    Rejected    // Từ chối
}
