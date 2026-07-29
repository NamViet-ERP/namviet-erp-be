namespace NamViet.ERP.Api.Application.Dtos.Purchasing;

public record PurchaseOrderDto(
    string Code,
    string PlanCode,
    string? ProjectCode,
    string? ProjectName,
    int? SupplierId,
    string? SupplierName,
    DateOnly OrderDate,
    DateTime CreatedAt,
    decimal TotalAmount,
    string ApprovalStatus,
    string OrderPlacementStatus,
    string DeliveryStatus,
    string PaymentStatus,
    bool PaymentWarning,
    string? Note,
    int SupplierPaymentTermsDays,
    DateOnly? PaymentDueDate,
    string? ApprovedByName,
    DateTime? ApprovedAt,
    string? PlacedByName,
    DateTime? PlacedAt,
    string? CreatedByName);

public record PurchaseOrderDetailDto(
    string Code,
    string PlanCode,
    string? ProjectCode,
    string? ProjectName,
    int? SupplierId,
    string? SupplierName,
    DateOnly OrderDate,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    decimal TotalAmount,
    string ApprovalStatus,
    string OrderPlacementStatus,
    string DeliveryStatus,
    string PaymentStatus,
    bool PaymentWarning,
    string? Note,
    int SupplierPaymentTermsDays,
    DateOnly? PaymentDueDate,
    string? ApprovedByName,
    DateTime? ApprovedAt,
    string? PlacedByName,
    DateTime? PlacedAt,
    string? CreatedByName,
    List<PurchaseOrderLineDto> Lines);

public record PurchaseOrderLineDto(
    int Id,
    int? PlanLineId,
    string? MaterialCode,
    string Sku,
    string Name,
    string? Spec,
    string Unit,
    decimal Ordered,
    decimal Received,
    decimal PoPrice,
    decimal EstPrice,
    decimal LineAmount,
    DateOnly? ExpectedDeliveryDate,
    string? Note);

public record PurchaseOrderPagedResponse(
    IReadOnlyList<PurchaseOrderDto> Items,
    int TotalCount,
    int Page,
    int PageSize,
    int TotalPages);

public record CreatePurchaseOrderRequest(
    string PlanCode,
    int SupplierId,
    DateOnly? OrderDate,
    string? Note,
    List<CreatePurchaseOrderLineRequest> Lines);

public record CreatePurchaseOrderLineRequest(
    int PlanLineId,
    decimal Qty,
    decimal UnitPrice,
    DateOnly? ExpectedDeliveryDate,
    string? Note);

public record UpdatePurchaseOrderRequest(
    string? Note,
    DateOnly? OrderDate,
    List<CreatePurchaseOrderLineRequest> Lines);

public record PurchaseOrderAttachmentDto(
    Guid Id,
    string PoCode,
    string DocType,
    string FileName,
    string StorageKey,
    DateTime UploadedAt,
    string? UploadedByName);
