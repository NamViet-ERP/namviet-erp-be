namespace NamViet.ERP.Api.Application.Dtos.Purchasing;

public record PurchasePlanDto(
    string Code,
    string ProjectCode,
    string? ProjectName,
    DateTime CreatedAt,
    decimal BudgetTotal,
    decimal ProposalTotal,
    decimal PurchaseTotal,
    decimal PaidTotal,
    string Status,
    string Kind,
    string? Note,
    string? CreatedByName,
    string? RejectReason = null,
    string? RemadeFromCode = null,
    string? RemadeToCode = null,
    string? L1ApprovedByName = null,
    DateTime? L1ApprovedAt = null,
    string? L2ApprovedByName = null,
    DateTime? L2ApprovedAt = null);

public record PurchasePlanDetailDto(
    string Code,
    string ProjectCode,
    string? ProjectName,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    decimal BudgetTotal,
    decimal ProposalTotal,
    decimal PurchaseTotal,
    decimal PaidTotal,
    string Status,
    string Kind,
    string? Note,
    string? CreatedByName,
    List<PurchasePlanLineDto> Lines,
    DateTime? SubmittedAt = null,
    string? L1ApprovedByName = null,
    DateTime? L1ApprovedAt = null,
    string? L2ApprovedByName = null,
    DateTime? L2ApprovedAt = null,
    string? RejectedByName = null,
    DateTime? RejectedAt = null,
    string? RejectReason = null,
    string? RemadeFromCode = null,
    string? RemadeToCode = null,
    List<PurchasePlanRelatedPoDto>? RelatedOrders = null);

public record PurchasePlanRelatedPoDto(
    string Code,
    string? SupplierName,
    decimal TotalAmount,
    string ApprovalStatus,
    string OrderPlacementStatus,
    string DeliveryStatus,
    string PaymentStatus,
    int ItemCount,
    DateTime CreatedAt);

public record PurchasePlanLinePoDto(
    string PoCode,
    decimal OrderedQty,
    decimal ReceivedQty);

public record PurchasePlanLineDto(
    int Id,
    int LineNo,
    string? SystemName,
    string? Category,
    string? MaterialCode,
    string ProductName,
    string? Spec,
    string Unit,
    decimal Quantity,
    decimal? KgPerUnit,
    string? LineNote,
    string? PurchaseStatus,
    string? StatusNote,
    decimal? BudgetUnitPrice,
    decimal? BudgetAmount,
    decimal? ProposalKgPerUnit,
    decimal? ProposalUnitPrice,
    decimal? ProposalAmount,
    string? ProposalSupplierName,
    string? ProposalNote,
    string? ProposalStatus,
    DateOnly? RequiredDate,
    DateOnly? OrderedDate,
    DateOnly? ExpectedDeliveryDate,
    DateOnly? ActualReceiptDate,
    string? DeliveryNote,
    decimal? PurchaseKgPerUnit,
    decimal? PurchaseUnitPrice,
    decimal? PurchaseAmount,
    string? PurchaseSupplierName,
    string? PurchaseNote,
    decimal? SelectedUnitPrice,
    decimal? SelectedAmount,
    string? SelectedSupplierName,
    string? SelectionReason,
    decimal OrderedQty,
    decimal ReceivedQty,
    decimal DefectQty,
    decimal RemainingOrderableQty,
    List<PurchasePlanLineQuoteDto> Quotes,
    List<PurchasePlanLinePoDto>? Pos = null);

public record PurchasePlanLineQuoteDto(
    int Id,
    int SortOrder,
    decimal? KgPerUnit,
    decimal? UnitPrice,
    decimal? Amount,
    string? SupplierName,
    string? Note,
    string? Status);

public record PurchasePlanPagedResponse(
    IReadOnlyList<PurchasePlanDto> Items,
    int TotalCount,
    int Page,
    int PageSize,
    int TotalPages);

public record CreatePurchasePlanLineQuoteRequest(
    int SortOrder = 1,
    decimal? KgPerUnit = null,
    decimal? UnitPrice = null,
    decimal? Amount = null,
    string? SupplierName = null,
    string? Note = null,
    string? Status = null);

public record CreatePurchasePlanLineRequest(
    string MaterialCode,
    string ProductName,
    string Unit,
    decimal Quantity,
    string? SystemName = null,
    string? Category = null,
    string? Spec = null,
    string? LineNote = null,
    decimal? KgPerUnit = null,
    string? PurchaseStatus = null,
    string? StatusNote = null,
    decimal? BudgetUnitPrice = null,
    decimal? BudgetAmount = null,
    decimal? ProposalKgPerUnit = null,
    decimal? ProposalUnitPrice = null,
    decimal? ProposalAmount = null,
    string? ProposalSupplierName = null,
    string? ProposalNote = null,
    string? ProposalStatus = null,
    DateOnly? RequiredDate = null,
    DateOnly? OrderedDate = null,
    DateOnly? ExpectedDeliveryDate = null,
    DateOnly? ActualReceiptDate = null,
    string? DeliveryNote = null,
    decimal? PurchaseKgPerUnit = null,
    decimal? PurchaseUnitPrice = null,
    decimal? PurchaseAmount = null,
    string? PurchaseSupplierName = null,
    string? PurchaseNote = null,
    decimal? SelectedUnitPrice = null,
    decimal? SelectedAmount = null,
    string? SelectedSupplierName = null,
    string? SelectionReason = null,
    List<CreatePurchasePlanLineQuoteRequest>? Quotes = null);

public record CreatePurchasePlanRequest(
    string ProjectCode,
    string? Note,
    string Kind = "plan",
    List<CreatePurchasePlanLineRequest>? Lines = null);

public record UpdatePurchasePlanRequest(
    string? Note,
    string? Status,
    List<CreatePurchasePlanLineRequest>? Lines = null);

public record RejectPurchasePlanRequest(string? Reason);

public record PurchasePlanImportPreviewDto(
    string ProjectCode,
    string SheetName,
    int TotalRows,
    int ValidRowCount,
    int ErrorRowCount,
    List<PurchasePlanImportColumnMapDto> MappedColumns,
    List<PurchasePlanImportLinePreviewDto> Lines,
    List<PurchasePlanImportErrorDto> Errors);

public record PurchasePlanImportColumnMapDto(string Field, string Header, int ColumnIndex);

public record PurchasePlanImportLinePreviewDto(
    int RowNumber,
    int LineNo,
    string? SystemName,
    string? Category,
    string MaterialCode,
    string ProductName,
    string? Spec,
    string Unit,
    decimal Quantity,
    decimal? KgPerUnit,
    string? LineNote,
    string? PurchaseStatus,
    string? StatusNote,
    decimal? BudgetUnitPrice,
    decimal? BudgetAmount,
    decimal? ProposalKgPerUnit,
    decimal? ProposalUnitPrice,
    decimal? ProposalAmount,
    string? ProposalSupplierName,
    string? ProposalNote,
    string? ProposalStatus,
    DateOnly? RequiredDate,
    DateOnly? OrderedDate,
    DateOnly? ExpectedDeliveryDate,
    DateOnly? ActualReceiptDate,
    string? DeliveryNote,
    decimal? PurchaseKgPerUnit,
    decimal? PurchaseUnitPrice,
    decimal? PurchaseAmount,
    string? PurchaseSupplierName,
    string? PurchaseNote,
    decimal? SelectedUnitPrice,
    decimal? SelectedAmount,
    string? SelectedSupplierName,
    string? SelectionReason,
    List<PurchasePlanImportQuotePreviewDto> Quotes,
    bool IsValid);

public record PurchasePlanImportQuotePreviewDto(
    int SortOrder,
    decimal? KgPerUnit,
    decimal? UnitPrice,
    decimal? Amount,
    string? SupplierName,
    string? Note,
    string? Status);

public record PurchasePlanImportErrorDto(int RowNumber, string Field, string Message);

public record ConfirmPurchasePlanImportRequest(
    string ProjectCode,
    string? Note,
    List<PurchasePlanImportLinePreviewDto> Lines);

public record PurchasePlanVarianceDto(
    string PlanCode,
    string ProjectCode,
    IReadOnlyList<PurchasePlanVarianceLineDto> Lines);

public record PurchasePlanVarianceLineDto(
    string PlanCode,
    string ProjectCode,
    string? MaterialCode,
    string ProductName,
    decimal PlannedQty,
    decimal OrderedQty,
    decimal ReceivedQty,
    decimal RemainingOrderableQty,
    decimal VarianceOrderedPct,
    decimal VarianceReceivedPct,
    string Status);

public static class PurchasePlanVarianceStatuses
{
    public const string OnTrack = "on_track";
    public const string UnderOrdered = "under_ordered";
    public const string OverReceived = "over_received";
    public const string Delayed = "delayed";
}
