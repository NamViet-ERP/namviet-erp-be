namespace NamViet.ERP.Api.Application.Dtos.Purchasing;

public record SupplierPayableAgingBucketDto(string Key, string Label, decimal Amount, int Count);

public record SupplierPayableKpiDto(
    decimal OutstandingTotal,
    decimal OverdueTotal,
    int OverdueSupplierCount,
    int OverLimitSupplierCount);

public record SupplierPayableSupplierRowDto(
    int SupplierId,
    string SupplierCode,
    string SupplierName,
    int PoCount,
    int ProjectCount,
    decimal Requested,
    decimal Paid,
    decimal Outstanding,
    decimal? CreditLimit,
    bool OverLimit,
    decimal OverdueAmount,
    int OverdueCount,
    int UnpaidCount);

public record SupplierPayableLineDto(
    string PaymentCode,
    string PoCode,
    DateOnly? PoDate,
    string? ProjectCode,
    string? ProjectName,
    int? SupplierId,
    string? SupplierCode,
    string? SupplierName,
    int StageNumber,
    decimal Requested,
    decimal Paid,
    decimal Outstanding,
    DateOnly DueDate,
    int DaysOverdue,
    string LineStatus,
    string PaymentStatus,
    string? PaymentTerm,
    DateTime? PaidAt,
    string? PaidByName);

public record SupplierPayableUpcomingDto(
    string PaymentCode,
    string PoCode,
    string? ProjectCode,
    int? SupplierId,
    string? SupplierName,
    DateOnly DueDate,
    decimal Outstanding,
    int DaysUntilDue);

public record SupplierPayableReportDto(
    SupplierPayableKpiDto Kpis,
    IReadOnlyList<SupplierPayableAgingBucketDto> Aging,
    IReadOnlyList<SupplierPayableSupplierRowDto> Suppliers,
    decimal Upcoming7Total,
    decimal Upcoming30Total,
    IReadOnlyList<SupplierPayableUpcomingDto> Upcoming,
    IReadOnlyList<SupplierPayableLineDto> Lines);

public record SupplierPayableSnapshotDto(
    int SupplierId,
    string SupplierCode,
    string SupplierName,
    decimal Outstanding,
    decimal OverdueAmount,
    int UnpaidCount,
    int OverdueCount,
    decimal? CreditLimit,
    bool OverLimit,
    int MaxDebtDays);
