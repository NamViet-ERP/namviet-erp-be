using NamViet.ERP.SharedKernel.Entities;

namespace NamViet.ERP.Modules.Hr.Domain.Entities;

public enum LeaveStatus { Pending, Approved, Rejected, Cancelled }

/// <summary>Loại nghỉ phép</summary>
public class LeaveType : BaseEntity
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public bool IsPaid { get; set; } = true;
}

/// <summary>Đơn nghỉ phép của nhân viên</summary>
public class LeaveRequest : BaseEntity
{
    public int Id { get; set; }
    public int EmployeeId { get; set; }
    public int LeaveTypeId { get; set; }
    public DateOnly FromDate { get; set; }
    public DateOnly ToDate { get; set; }
    public decimal Days { get; set; }
    public string? Reason { get; set; }
    public LeaveStatus Status { get; set; } = LeaveStatus.Pending;
    public int? ApproverId { get; set; }

    public Employee Employee { get; set; } = null!;
    public LeaveType LeaveType { get; set; } = null!;
}

/// <summary>Theo dõi số ngày phép còn lại theo năm</summary>
public class LeaveBalance : BaseEntity
{
    public int Id { get; set; }
    public int EmployeeId { get; set; }
    public int Year { get; set; }
    public decimal TotalDays { get; set; }
    public decimal UsedDays { get; set; }
    public decimal RemainingDays => TotalDays - UsedDays;

    public Employee Employee { get; set; } = null!;
}

/// <summary>Bản ghi chấm công hàng ngày</summary>
public class Attendance : BaseEntity
{
    public int Id { get; set; }
    public int EmployeeId { get; set; }
    public DateOnly Date { get; set; }
    public TimeOnly? CheckIn { get; set; }
    public TimeOnly? CheckOut { get; set; }
    public decimal WorkHours { get; set; }
    public string Status { get; set; } = "Present";    // Present/Late/Absent/HalfDay
    public string? Source { get; set; }                 // Appsheet/Manual/System

    public Employee Employee { get; set; } = null!;
}

/// <summary>Tổng hợp chấm công theo tháng</summary>
public class AttendanceSummary : BaseEntity
{
    public int Id { get; set; }
    public int EmployeeId { get; set; }
    public int Month { get; set; }
    public int Year { get; set; }
    public decimal WorkDays { get; set; }
    public decimal LeaveDays { get; set; }
    public decimal OtHours { get; set; }

    public Employee Employee { get; set; } = null!;
}
