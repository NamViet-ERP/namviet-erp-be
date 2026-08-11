namespace NamViet.ERP.Api.Application.Dtos.Purchasing;

public record PurchaseDocumentDto(
    Guid Id,
    string PoCode,
    string? ProjectCode,
    int? SupplierId,
    string? SupplierName,
    string DocType,
    string FileName,
    DateTime UploadedAt,
    string? UploadedByName);

public record PurchaseDocumentPagedResponse(
    IReadOnlyList<PurchaseDocumentDto> Items,
    int TotalCount,
    int Page,
    int PageSize,
    int TotalPages);
