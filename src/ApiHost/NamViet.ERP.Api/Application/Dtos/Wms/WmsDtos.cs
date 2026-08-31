namespace NamViet.ERP.Api.Application.Dtos.Wms;

public record WarehouseDto(
    string Id, string Code, string Name, string Group, string Address,
    string Branch, string Manager, int TotalStock, decimal TotalValue, string Status);
public record CreateWarehouseDto(
    string Code, string Name, string Group, string Address,
    string Branch, string Manager, string Status);
public record UpdateWarehouseDto(
    string? Code, string? Name, string? Group, string? Address,
    string? Branch, string? Manager, string? Status);

public record ImportItemDto(
    string ProductId, string Sku, string Name, decimal Qty, string Unit, decimal UnitPrice,
    string? Supplier = null, int? SupplierId = null, string? Notes = null,
    int? LineId = null,
    decimal? PlannedQty = null,
    decimal? AcceptedQty = null,
    decimal? DefectQty = null,
    decimal? ShortageQty = null,
    string? DefectLineReason = null,
    string? ShortageLineReason = null);
public record ImportAttachmentDto(
    Guid Id,
    string Kind,
    string FileName,
    string StorageKey,
    string UploadedAt);
public record CreateImportDto(
    string VoucherCode, string Date, string Supplier, string ReferenceCode,
    decimal TotalValue, string Status, string WarehouseApproval, string ApprovedBy,
    string WarehouseKeeper, List<ImportItemDto> Items, string? CreatedBy, string Note,
    string WarehouseId, bool? IsDirectProcurement, string? Project,
    string? PlanCode = null,
    bool IssueFromGeneralWarehouse = false);
public record ImportDto(
    string Id, string VoucherCode, string Date, string Supplier, string ReferenceCode,
    decimal TotalValue, string Status, string WarehouseApproval, string ApprovedBy,
    string? ApprovedAt, string WarehouseKeeper, List<ImportItemDto> Items, string CreatedBy, string Note,
    string WarehouseId, bool? IsDirectProcurement, string? Project,
    string CreatedAt,
    string? PlanCode = null,
    string? ReceiptEvaluation = null,
    List<ImportAttachmentDto>? Attachments = null,
    string? ShortageReport = null,
    string? LinkedDefectTicketCode = null,
    string? LinkedTicketCode = null,
    bool IssueFromGeneralWarehouse = false);

public record ApproveImportLineSettlementRequest(
    int LineId,
    decimal AcceptedQty,
    decimal DefectQty,
    decimal ShortageQty,
    string? DefectLineReason = null,
    string? ShortageLineReason = null);

public record ApproveImportRequest(
    bool ConfirmOverBudget = false,
    IReadOnlyList<ApproveImportLineSettlementRequest>? Lines = null,
    string? ShortageReport = null,
    string? DefectReason = null);
public record UpdateImportReceiptEvaluationRequest(string? Evaluation);

public record ImportBudgetExceededDto(
    string Code,
    string Message,
    string ProjectCode,
    decimal MaterialBudget,
    decimal ApprovedImportValue,
    decimal ImportValue,
    decimal OverflowAmount);

public record ImportParsedLineDto(
    int RowNumber,
    string MaterialCode,
    string MaterialName,
    string Unit,
    decimal Qty,
    decimal UnitPrice,
    bool IsValid);

public record ImportParseErrorDto(int RowNumber, string Field, string Message);

public record ImportParseResultDto(
    int TotalRows,
    int ValidRowCount,
    int ErrorRowCount,
    List<ImportParsedLineDto> Lines,
    List<ImportParseErrorDto> Errors);

public record ExportItemDto(string ProductId, string Sku, string Name, decimal Qty, string Unit);
public record ExportDto(
    string Id, string VoucherCode, string Date, string SoCode, string Customer,
    string WarehouseId, string WarehouseName, List<ExportItemDto> Items,
    string Status, string CreatedBy, string Note, string? ApprovedBy, string? ApprovedAt,
    string? Project, string? ProjectName,
    string CreatedAt,
    bool IsDirectProcurement = false,
    string? LinkedTicketCode = null);
public record CreateExportDto(
    string VoucherCode, string Date, string SoCode, string Customer,
    string WarehouseId, List<ExportItemDto> Items,
    string Status, string? CreatedBy, string Note, string? Project);

public record TransferItemDto(
    string ProductId, string Sku, string Name, decimal Qty, string Unit, string? ConstructionPhase = null);
public record TransferDto(
    string Id, string VoucherCode, string Date, string FromWarehouseId, string FromWarehouseName,
    string ToWarehouseId, string ToWarehouseName, List<TransferItemDto> Items,
    string Status, string CreatedBy, string Note, string? ApprovedBy, string? ApprovedAt,
    string? Project, string? ProjectName, string? ToProject, string? ToProjectName,
    string CreatedAt);
public record CreateTransferDto(
    string VoucherCode, string Date, string FromWarehouseId, string ToWarehouseId,
    List<TransferItemDto> Items, string Status, string? CreatedBy, string Note,
    string? Project, string? ToProject);

public record ExportReturnItemDto(
    string ProductId, string Sku, string Name, decimal Qty, string Unit);
public record ExportReturnableLineDto(
    string ProductId, string Sku, string Name, string Unit,
    decimal ExportedQty, decimal ReturnedQty, decimal RemainingQty);
public record ExportReturnableExportDto(
    string Id, string VoucherCode, string Date, string? SoCode, string? Customer,
    string Status, string? ApprovedAt, int ReturnableLineCount, decimal TotalRemainingQty);
public record ExportReturnDto(
    string Id, string VoucherCode, string Date, string SourceExportId, string SourceExportCode,
    string? Project, string? ProjectName, List<ExportReturnItemDto> Items,
    string Status, string CreatedBy, string Note, string? ApprovedBy, string? ApprovedAt,
    string CreatedAt,
    List<ImportAttachmentDto>? Attachments = null);
public record CreateExportReturnDto(
    string VoucherCode, string Date, string SourceExportId,
    List<ExportReturnItemDto> Items, string Status, string? CreatedBy, string Note, string? Project);

public record StocktakeItemDto(string ProductId, string Sku, string Name, decimal SystemQty, decimal ActualQty, decimal Diff);
public record CreateStocktakeDto(
    string? VoucherCode, string Date, string? CreatedBy, string Status,
    string WarehouseId, string WarehouseName, string Condition, bool IsFrozen,
    string? Note, List<StocktakeItemDto> Items);
public record StocktakeDto(
    string Id, string VoucherCode, string Date, string CreatedBy, string Status,
    string WarehouseId, string WarehouseName, string Condition, bool IsFrozen,
    string Note, List<StocktakeItemDto> Items);

public record LendingLogDto(
    string Id, string VoucherCode, string Date, string BorrowingProjectId,
    string LendingProjectId, string ProductId, decimal Qty, string Status,
    bool BorrowPmSigned, bool LendPmSigned, string Note, string? CreatedBy, string? CreatedTime);

public record ConversionItemDto(string ProductId, string Sku, string Name, decimal Qty, string Unit, string Reason);
public record ConversionDto(
    string Id, string VoucherCode, string Date, string FromWarehouseId, string FromWarehouseName,
    string FromType, string ToWarehouseId, string ToWarehouseName, string ToType,
    List<ConversionItemDto> Items, string Status, string CreatedBy, string? ApprovedBy, string Note);

public record DashboardKpiDto(int TotalStock, decimal TotalValue, int ActiveWarehouses, int TodayImports, int TodayExports, int PendingApprovals);
public record DashboardTrendDto(string Month, decimal ImportValue, decimal ExportValue);
public record DashboardCategoryDto(string Category, decimal Value);
public record DashboardActivityDto(string Type, string Code, string Description, string Time);
public record BirthdayDto(string Name, string Department, int Day);
public record TopProductDto(string Sku, string Name, decimal Qty, decimal Value);
