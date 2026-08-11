namespace NamViet.ERP.Api.Application.Dtos.Purchasing;

public record PurchaseContractDto(
    Guid Id,
    string Code,
    string ContractNo,
    string Title,
    string? PoCode,
    string? ProjectCode,
    int? SupplierId,
    string? SupplierName,
    string Status,
    DateOnly? SignedDate,
    DateOnly? EffectiveFrom,
    DateOnly? EffectiveTo,
    decimal? Amount,
    string? Note,
    string? FileName,
    bool HasFile,
    DateTime? UploadedAt,
    string? UploadedByName,
    DateTime CreatedAt);

public record PurchaseContractPagedResponse(
    IReadOnlyList<PurchaseContractDto> Items,
    int TotalCount,
    int Page,
    int PageSize,
    int TotalPages);

public record CreatePurchaseContractRequest(
    string ContractNo,
    string Title,
    string? PoCode,
    string? ProjectCode,
    int? SupplierId,
    string? Status,
    DateOnly? SignedDate,
    DateOnly? EffectiveFrom,
    DateOnly? EffectiveTo,
    decimal? Amount,
    string? Note);

public record UpdatePurchaseContractRequest(
    string? ContractNo,
    string? Title,
    string? PoCode,
    string? ProjectCode,
    int? SupplierId,
    string? Status,
    DateOnly? SignedDate,
    DateOnly? EffectiveFrom,
    DateOnly? EffectiveTo,
    decimal? Amount,
    string? Note);

public record ImportPurchaseContractsFromPoResult(int Imported, int Skipped);
