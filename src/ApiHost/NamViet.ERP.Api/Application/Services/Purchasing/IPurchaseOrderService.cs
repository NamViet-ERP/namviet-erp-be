using NamViet.ERP.Api.Application.Dtos.Purchasing;

namespace NamViet.ERP.Api.Application.Services.Purchasing;

public interface IPurchaseOrderService
{
    Task<PurchaseOrderPagedResponse> ListAsync(
        int page,
        int pageSize,
        int? supplierId,
        string? planCode,
        string? projectCode,
        string? orderPlacementStatus,
        string? deliveryStatus,
        string? paymentStatus,
        bool? paymentWarning,
        string? search,
        CancellationToken ct = default);

    Task<PurchaseOrderDetailDto?> GetByCodeAsync(string code, CancellationToken ct = default);
    Task<PurchaseOrderDetailDto> CreateAsync(CreatePurchaseOrderRequest request, CancellationToken ct = default);
    Task<PurchaseOrderDetailDto?> UpdateAsync(string code, UpdatePurchaseOrderRequest request, CancellationToken ct = default);
    Task<PurchaseOrderDetailDto?> ApproveAsync(string code, CancellationToken ct = default);
    Task<PurchaseOrderDetailDto?> UnapproveAsync(string code, CancellationToken ct = default);
    Task<PurchaseOrderDetailDto?> PlaceAsync(string code, CancellationToken ct = default);

    Task<IReadOnlyList<PurchaseOrderAttachmentDto>?> ListAttachmentsAsync(string code, CancellationToken ct = default);
    Task<PurchaseOrderAttachmentDto?> UploadAttachmentAsync(
        string code,
        string docType,
        string fileName,
        string contentType,
        Stream content,
        CancellationToken ct = default);
    Task<PurchaseOrderAttachmentDto?> GetAttachmentAsync(string code, Guid attachmentId, CancellationToken ct = default);
    Task<bool> DeleteAttachmentAsync(string code, Guid attachmentId, CancellationToken ct = default);

    Task<IReadOnlyList<PurchaseOrderCommentDto>?> ListCommentsAsync(string code, CancellationToken ct = default);
    Task<PurchaseOrderCommentDto?> CreateCommentAsync(string code, CreatePurchaseOrderCommentRequest request, CancellationToken ct = default);
    Task<PurchaseOrderCommentDto?> UpdateCommentAsync(string code, Guid commentId, UpdatePurchaseOrderCommentRequest request, CancellationToken ct = default);
    Task<bool> DeleteCommentAsync(string code, Guid commentId, CancellationToken ct = default);

    Task<IReadOnlyList<PurchaseOrderInstallmentDocumentDto>?> ListInstallmentDocumentsAsync(
        string code, int stageNumber, CancellationToken ct = default);
    Task<PurchaseOrderInstallmentDocumentDto?> UploadInstallmentDocumentAsync(
        string code, int stageNumber, string docType, string fileName, string contentType,
        Stream content, CancellationToken ct = default);
    Task<PurchaseOrderInstallmentDocumentDto?> GetInstallmentDocumentAsync(
        string code, int stageNumber, int documentId, CancellationToken ct = default);
    Task<bool> DeleteInstallmentDocumentAsync(
        string code, int stageNumber, int documentId, CancellationToken ct = default);
    Task<PurchaseOrderInstallmentDto?> CompleteInstallmentAsync(
        string code, int stageNumber, CancellationToken ct = default);
}

