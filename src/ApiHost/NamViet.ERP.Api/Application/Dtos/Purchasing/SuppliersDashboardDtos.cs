namespace NamViet.ERP.Api.Application.Dtos.Purchasing;

/// <summary>Công nợ một NCC: Debt = tổng DNTT (không hủy), Paid = DNTT đã trả, Remaining = còn phải trả.</summary>
public record SupplierDebtRowDto(
    int SupplierId,
    string SupplierName,
    decimal Debt,
    decimal Paid,
    decimal Remaining);

public record SupplierTrendPointDto(string Month, decimal Debt, decimal Payment);

public record SuppliersDashboardDto(
    int SupplierCount,
    int ContactCount,
    int OrderCount,
    decimal DebtTotal,
    decimal PaymentTotal,
    decimal Remaining,
    IReadOnlyList<SupplierDebtRowDto> DebtBySupplier,
    IReadOnlyList<SupplierTrendPointDto> Trend,
    DateTime From,
    DateTime To,
    string Period,
    DateTime GeneratedAt);
