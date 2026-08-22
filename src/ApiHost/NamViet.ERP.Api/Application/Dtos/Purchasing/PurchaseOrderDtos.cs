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
    List<PurchaseOrderLineDto> Lines,
    List<PurchaseOrderInstallmentDto> Installments);

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
    string? Note,
    int WarrantyMonths = 0,
    string WarrantyAnchorType = "delivery");

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
    List<CreatePurchaseOrderLineRequest> Lines,
    int? InstallmentCount = null,
    List<PurchaseOrderInstallmentRequest>? Installments = null);

public record CreatePurchaseOrderLineRequest(
    int PlanLineId,
    decimal Qty,
    decimal UnitPrice,
    DateOnly? ExpectedDeliveryDate,
    string? Note,
    int WarrantyMonths = 0,
    string? WarrantyAnchorType = null);

public record UpdatePurchaseOrderRequest(
    string? Note,
    DateOnly? OrderDate,
    List<CreatePurchaseOrderLineRequest> Lines,
    int? InstallmentCount = null,
    List<PurchaseOrderInstallmentRequest>? Installments = null);

public record PurchaseOrderInstallmentRequest(
    int StageNumber,
    decimal Percent,
    IReadOnlyList<string> RequiredDocTypes);

public record PurchaseOrderInstallmentDto(
    int Id,
    int StageNumber,
    decimal Percent,
    string Status,
    IReadOnlyList<string> RequiredDocTypes,
    IReadOnlyList<PurchaseOrderInstallmentDocumentDto> Documents);

public record PurchaseOrderInstallmentDocumentDto(
    int Id,
    int InstallmentId,
    string DocType,
    string FileName,
    string ContentType,
    string StoragePath,
    DateTime UploadedAt,
    string? UploadedByName);

public record PurchaseOrderAttachmentDto(
    Guid Id,
    string PoCode,
    string DocType,
    string FileName,
    string StorageKey,
    DateTime UploadedAt,
    string? UploadedByName);

public record PurchaseOrderCommentDto(
    Guid Id,
    string PoCode,
    Guid AuthorUserId,
    string AuthorName,
    string Body,
    DateTime CreatedAt,
    DateTime? UpdatedAt);

public record CreatePurchaseOrderCommentRequest(string Body);

public record UpdatePurchaseOrderCommentRequest(string Body);
