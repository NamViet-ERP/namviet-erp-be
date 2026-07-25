namespace NamViet.ERP.Api.Application.Dtos.Purchasing;

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
    string PoCode,
    string PlanCode,
    string ProjectCode,
    string? SupplierName,
    string Status,
    string? Reason,
    string? Note,
    string CreatedByName,
    DateTime CreatedAt,
    string? ApprovedByName,
    DateTime? ApprovedAt,
    IReadOnlyList<PurchaseDefectItemDto> Items,
    IReadOnlyList<PurchaseDefectAttachmentDto> Attachments);

public record UpdateDefectReasonRequest(string? Reason);
