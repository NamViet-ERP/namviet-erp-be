namespace NamViet.ERP.Api.Application.Dtos.Purchasing;

public record PurchasePaymentDto(
    string Code,
    string PoCode,
    int? SupplierId,
    string? SupplierName,
    string? InvoiceNo,
    decimal Amount,
    DateOnly DueDate,
    string Status,
    string? Note,
    DateTime CreatedAt,
    string? CreatedByName);

public record PurchasePaymentPagedResponse(
    IReadOnlyList<PurchasePaymentDto> Items,
    int TotalCount,
    int Page,
    int PageSize,
    int TotalPages);

public record CreatePurchasePaymentRequest(
    string PoCode,
    string? InvoiceNo,
    decimal? Amount,
    DateOnly DueDate,
    string? Note);
