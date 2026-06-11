using NamViet.ERP.SharedKernel.Entities;
using NamViet.ERP.Modules.Warehouse.Domain.Enums;

namespace NamViet.ERP.Modules.Warehouse.Domain.Entities;

/// <summary>Mượn vật tư liên dự án – loan_code: MT-YYYYMMDD-XXXX</summary>
public class ProjectMaterialLoan : BaseEntity
{
    public string LoanCode { get; set; } = string.Empty;           // PK
    public string BorrowingProjectCode { get; set; } = string.Empty;
    public string LendingProjectCode { get; set; } = string.Empty;
    public string MaterialCode { get; set; } = string.Empty;
    public decimal LoanQty { get; set; }
    public DateTime LoanDate { get; set; } = DateTime.UtcNow;
    public DateOnly? DueDate { get; set; }
    public LoanStatus Status { get; set; } = LoanStatus.Borrowing;
    public string? ReturnTicketLink { get; set; }   // Liên kết phiếu trả để đối chiếu
    public string? LoanReason { get; set; }         // Lý do mượn

    public Project BorrowingProject { get; set; } = null!;
    public Project LendingProject { get; set; } = null!;
    public Material Material { get; set; } = null!;
}
