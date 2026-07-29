using NamViet.ERP.Modules.Warehouse.Domain.Enums;

namespace NamViet.ERP.Modules.Warehouse.Application.DTOs;

// ── Materials ─────────────────────────────────────────────────────────────
public record MaterialDto(
    string MaterialCode, string MaterialName, int CategoryId, string? CategoryName,
    MaterialCategoryPathDto CategoryPath,
    string Unit, string? TechnicalSpec, string? QualityStandard, bool IsConsumable,
    bool IsActive);

public record UpsertMaterialRequest(
    string MaterialCode, string MaterialName, int CategoryId,
    string Unit, string? TechnicalSpec, string? QualityStandard, bool IsConsumable = false);

// ── Suppliers ─────────────────────────────────────────────────────────────
public record SupplierDto(
    int Id, string SupplierCode, string SupplierName,
    string? ContactPhone,
    int MaxDebtDays,
    string? PaymentTerms,
    string Rating,
    decimal PurchasedRevenue,
    IReadOnlyList<string> IndustryGroups,
    SupplierLatestEvaluationDto? LatestEvaluation);

public record SupplierLatestEvaluationDto(
    string Period,
    decimal Score,
    string Grade,
    DateOnly EvaluatedAt,
    decimal OnTimeDeliveryRate,
    decimal DefectRate,
    decimal WarrantyCompletionRate);

public record UpsertSupplierRequest(
    string SupplierCode, string SupplierName,
    string? ContactPhone, int MaxDebtDays = 30, string? PaymentTerms = null, string Rating = "A");

// ── Projects ──────────────────────────────────────────────────────────────
public record ProjectWarehouseKeeperDto(Guid UserId, string DisplayName);

public record ProjectDto(
    string ProjectCode, string ProjectName, string ProjectType,
    Guid PmId, string PmName, DateOnly StartDate, DateOnly? EndDate,
    decimal MaterialBudget, string Status,
    decimal StockActualQty = 0,
    decimal StockPlannedQty = 0,
    decimal StockReservedQty = 0,
    int StockMaterialCount = 0,
    decimal ApprovedImportValue = 0,
    decimal MaterialBudgetRemaining = 0,
    IReadOnlyList<ProjectWarehouseKeeperDto>? WarehouseKeepers = null);

public record CreateProjectRequest(
    string ProjectCode, string ProjectName, ProjectType ProjectType,
    Guid PmId, string PmName, DateOnly StartDate, DateOnly? EndDate,
    decimal MaterialBudget = 0m,
    IReadOnlyList<ProjectWarehouseKeeperDto>? WarehouseKeepers = null);

public record UpdateProjectRequest(
    string ProjectName, Guid PmId, string PmName,
    DateOnly? EndDate, decimal MaterialBudget, ProjectStatus Status,
    IReadOnlyList<ProjectWarehouseKeeperDto>? WarehouseKeepers = null);

// ── Project Inventory ─────────────────────────────────────────────────────
public record ProjectInventoryDto(
    string ProjectCode, string MaterialCode, string MaterialName, string Unit,
    decimal PlannedImportQty, decimal PlannedExportQty,
    decimal ActualImportQty, decimal ActualExportQty,
    decimal ActualInventoryQty, decimal PlannedInventoryQty,
    decimal ReservedQty, decimal MinStockQty);

public record UpdateProjectInventoryMinStockRequest(decimal MinStockQty);

public record ProjectLedgerEntryDto(
    string Id,
    string Kind,
    string VoucherCode,
    string Date,
    string Status,
    decimal ItemQty,
    decimal? TotalValue);

public record MaterialCategoryDto(
    int Id, string Code, string Name, int Level, int? ParentId, int SortOrder, bool IsActive);

public record UpsertMaterialCategoryRequest(
    string Code, string Name, int Level, int? ParentId, int SortOrder = 0, bool IsActive = true);

public record MaterialUnitDto(
    int Id, string Name, int SortOrder, bool IsActive);

public record UpsertMaterialUnitRequest(
    string Name, int SortOrder = 0, bool IsActive = true);

// ── Transactions ──────────────────────────────────────────────────────────
public record TransactionItemRequest(
    string MaterialCode, decimal Quantity, decimal UnitPrice,
    string? ConstructionPhase, string? Notes);

public record CreateTransactionRequest(
    TransactionType TransactionType,
    string? ProjectCode,
    int? SupplierId,
    string? InvoiceNumber,
    string? DnttNumber,
    string? Notes,
    List<TransactionItemRequest> Items);

public record TransactionDto(
    string TicketCode, string TransactionType, string? ProjectCode,
    int? SupplierId, string? SupplierName,
    string? InvoiceNumber, string? DnttNumber,
    string CreatedByName, string ApprovalStatus,
    string? ApprovedByName, DateTime? ApprovedAt,
    string? Notes, DateTime CreatedAt,
    List<TransactionItemDto> Items);

public record TransactionItemDto(
    int Id, string MaterialCode, string MaterialName,
    decimal Quantity, string Unit, decimal UnitPrice, decimal TotalAmount,
    string? ConstructionPhase, string? Notes);

public record ApproveTransactionRequest(bool Approve, string? Reason = null);

// ── Loans ─────────────────────────────────────────────────────────────────
public record LoanDto(
    string LoanCode, string BorrowingProjectCode, string LendingProjectCode,
    string MaterialCode, string MaterialName, decimal LoanQty,
    DateTime LoanDate, DateOnly? DueDate, string Status,
    string? LoanReason, string? ReturnTicketLink);

public record CreateLoanRequest(
    string BorrowingProjectCode, string LendingProjectCode,
    string MaterialCode, decimal LoanQty,
    DateOnly? DueDate, string? LoanReason);
