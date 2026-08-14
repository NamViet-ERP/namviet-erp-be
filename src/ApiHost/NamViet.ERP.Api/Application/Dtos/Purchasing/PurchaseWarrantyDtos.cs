namespace NamViet.ERP.Api.Application.Dtos.Purchasing;

public record PurchaseWarrantyDto(
    Guid Id,
    string Code,
    string PoCode,
    int PoItemId,
    string? ProjectCode,
    string? PlanCode,
    int? SupplierId,
    string? SupplierName,
    string? MaterialCode,
    string MaterialName,
    string Unit,
    decimal Quantity,
    int WarrantyMonths,
    string AnchorType,
    DateOnly? DeliveryDate,
    DateOnly? AcceptanceDate,
    DateOnly? WarrantyEndDate,
    string Status,
    string? ImportTicketCode,
    int ClaimCount,
    DateTime CreatedAt);

public record PurchaseWarrantyDetailDto(
    Guid Id,
    string Code,
    string PoCode,
    int PoItemId,
    string? ProjectCode,
    string? PlanCode,
    int? SupplierId,
    string? SupplierName,
    string? MaterialCode,
    string MaterialName,
    string Unit,
    decimal Quantity,
    int WarrantyMonths,
    string AnchorType,
    DateOnly? DeliveryDate,
    DateOnly? AcceptanceDate,
    DateOnly? WarrantyEndDate,
    string Status,
    string? ImportTicketCode,
    DateTime CreatedAt,
    IReadOnlyList<PurchaseWarrantyClaimDto> Claims);

public record PurchaseWarrantyClaimDto(
    Guid Id,
    string TicketCode,
    Guid WarrantyId,
    string Status,
    string Reason,
    string? Note,
    Guid CreatedById,
    string CreatedByName,
    DateTime? SubmittedAt,
    DateTime? ClosedAt,
    DateTime CreatedAt,
    int? ServiceRating = null,
    string? ServiceNote = null,
    string Kind = "warranty",
    string? DefectType = null,
    string? Severity = null);

public record PurchaseWarrantyClaimListDto(
    Guid Id,
    string TicketCode,
    Guid WarrantyId,
    string Kind,
    string Status,
    string Reason,
    string? Note,
    string? DefectType,
    string? Severity,
    Guid CreatedById,
    string CreatedByName,
    DateTime? SubmittedAt,
    DateTime? ClosedAt,
    DateTime CreatedAt,
    int? ServiceRating,
    string? ServiceNote,
    string WarrantyCode,
    string PoCode,
    string? ProjectCode,
    string? SupplierName,
    string? MaterialCode,
    string MaterialName,
    DateOnly? WarrantyEndDate,
    string WarrantyStatus);

public record UpdatePurchaseWarrantyRequest(
    int? WarrantyMonths,
    string? AnchorType,
    DateOnly? AcceptanceDate,
    DateOnly? DeliveryDate);

public record CreatePurchaseWarrantyClaimRequest(
    string Reason,
    string? Note,
    bool Submit = true,
    string Kind = "warranty",
    string? DefectType = null,
    string? Severity = null);

public record ClosePurchaseWarrantyClaimRequest(
    int ServiceRating,
    string? ServiceNote = null);

public record SyncPurchaseWarrantiesResult(int Created, int Updated);
