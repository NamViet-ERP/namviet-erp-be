namespace NamViet.ERP.Api.Application.Dtos.Purchasing;

public record PurchaseRfqQuoteDto(
    Guid Id,
    string MaterialCode,
    string MaterialName,
    int SupplierId,
    string SupplierName,
    string? ProjectCode,
    DateOnly QuoteDate,
    decimal UnitPrice,
    DateOnly? ValidUntil,
    int? LeadTimeDays,
    string? PaymentTerm,
    string Status,
    string? PoRef,
    string? Note);

public record PurchaseRfqQuotePagedResponse(
    IReadOnlyList<PurchaseRfqQuoteDto> Items,
    int TotalCount,
    int Page,
    int PageSize,
    int TotalPages);

public record UpsertPurchaseRfqQuoteRequest(
    string MaterialCode,
    int SupplierId,
    string? ProjectCode,
    DateOnly QuoteDate,
    decimal UnitPrice,
    DateOnly ValidUntil,
    int? LeadTimeDays,
    string? PaymentTerm,
    string? Status,
    string? Note);

public record PurchaseRfqAverageDto(
    string Period,
    int Year,
    int? Quarter,
    decimal AveragePrice,
    int QuoteCount);

public record PurchaseRfqWinRateDto(
    int SupplierId,
    string SupplierName,
    int Won,
    int Total,
    int Rate);
