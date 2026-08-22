namespace NamViet.ERP.Api.Application.Dtos.Purchasing;

public record PoDefectEligiblePurchaseOrderDto(
    string PoCode,
    string PlanCode,
    string ProjectCode,
    string SupplierName,
    decimal AvailableQty);

public record PoDefectLinePreviewDto(
    int PoItemId,
    string Name,
    string? MaterialCode,
    string Unit,
    decimal Ordered,
    decimal Received,
    decimal Defected,
    decimal ReservedPendingNk,
    decimal ReservedPendingDefect,
    decimal Available,
    decimal UnitPrice);

public record PoDefectPreviewDto(
    string PoCode,
    string PlanCode,
    string ProjectCode,
    string SupplierName,
    IReadOnlyList<PoDefectLinePreviewDto> Lines);

public record CreateDefectFromPoLineRequest(int PoItemId, decimal Qty, string? LineReason = null);

public record CreateDefectFromPoRequest(
    IReadOnlyList<CreateDefectFromPoLineRequest> Items,
    string? Reason = null,
    string? Note = null);

public record StockDefectLinePreviewDto(
    string MaterialCode,
    string Name,
    string Unit,
    decimal OnHand,
    decimal ReservedPendingDefect,
    decimal Available);

public record StockDefectPreviewDto(
    string ProjectCode,
    IReadOnlyList<StockDefectLinePreviewDto> Lines);

public record CreateDefectFromStockLineRequest(string MaterialCode, decimal Qty, string? LineReason = null);

public record CreateDefectFromStockRequest(
    string ProjectCode,
    IReadOnlyList<CreateDefectFromStockLineRequest> Items,
    string? Reason = null,
    string? Note = null);

public record PurchaseDefectItemDto(
    int Id,
    int PoItemId,
    string? MaterialCode,
    string Name,
    string Unit,
    decimal Quantity,
    string? LineReason);

public record PurchaseDefectAttachmentDto(
    Guid Id,
    string Kind,
    string FileName,
    string StorageKey,
    DateTime UploadedAt);

public record PurchaseDefectDto(
    string Id,
    string TicketCode,
    string SourceType,
    string? PoCode,
    string PlanCode,
    string ProjectCode,
    string? SupplierName,
    string Status,
    /// <summary>approved | processing | processed when Status=approved; else mirrors Status.</summary>
    string ProcessingPhase,
    string? Reason,
    string? Note,
    string? CauseSource,
    string? SupplierFeedback,
    string? HandlingMethod,
    string CreatedByName,
    DateTime CreatedAt,
    string? ApprovedByName,
    DateTime? ApprovedAt,
    string? ProcessedByName,
    DateTime? ProcessedAt,
    IReadOnlyList<PurchaseDefectItemDto> Items,
    IReadOnlyList<PurchaseDefectAttachmentDto> Attachments);

public record UpdateDefectReasonRequest(string? Reason);

public record UpdateDefectProcessingRequest(
    string? CauseSource,
    string? SupplierFeedback,
    string? HandlingMethod);

public record UpdateDefectItemRequest(int Id, decimal Quantity, string? LineReason = null);

public record UpdateDefectItemsRequest(IReadOnlyList<UpdateDefectItemRequest> Items);
