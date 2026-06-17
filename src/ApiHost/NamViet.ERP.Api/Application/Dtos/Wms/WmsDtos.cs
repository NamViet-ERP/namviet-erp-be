namespace NamViet.ERP.Api.Application.Dtos.Wms;

public record ProductDto(string Id, string Sku, string Name, string Category, string Unit, decimal Price);
public record CreateProductDto(string Sku, string Name, string Category, string Unit, decimal Price);
public record UpdateProductDto(string? Sku, string? Name, string? Category, string? Unit, decimal? Price);

public record WarehouseDto(
    string Id, string Code, string Name, string Group, string Address,
    string Branch, string Manager, int TotalStock, decimal TotalValue, string Status);
public record CreateWarehouseDto(
    string Code, string Name, string Group, string Address,
    string Branch, string Manager, string Status);
public record UpdateWarehouseDto(
    string? Code, string? Name, string? Group, string? Address,
    string? Branch, string? Manager, string? Status);

public record ImportItemDto(string ProductId, string Sku, string Name, decimal Qty, string Unit, decimal UnitPrice);
public record ImportDto(
    string Id, string VoucherCode, string Date, string Supplier, string ReferenceCode,
    decimal TotalValue, string Status, string WarehouseApproval, string ApprovedBy,
    string WarehouseKeeper, List<ImportItemDto> Items, string CreatedBy, string Note,
    string WarehouseId, bool? IsDirectProcurement, string? Project);
public record CreateImportDto(
    string VoucherCode, string Date, string Supplier, string ReferenceCode,
    decimal TotalValue, string Status, string WarehouseApproval, string ApprovedBy,
    string WarehouseKeeper, List<ImportItemDto> Items, string CreatedBy, string Note,
    string WarehouseId, bool? IsDirectProcurement, string? Project);

public record ExportItemDto(string ProductId, string Sku, string Name, decimal Qty, string Unit);
public record ExportDto(
    string Id, string VoucherCode, string Date, string SoCode, string Customer,
    string WarehouseId, string WarehouseName, List<ExportItemDto> Items,
    string Status, string CreatedBy, string Note);

public record TransferItemDto(string ProductId, string Sku, string Name, decimal Qty, string Unit);
public record TransferDto(
    string Id, string VoucherCode, string Date, string FromWarehouseId, string FromWarehouseName,
    string ToWarehouseId, string ToWarehouseName, List<TransferItemDto> Items,
    string Status, string CreatedBy, string Note, string? ApprovedBy);

public record StocktakeItemDto(string ProductId, string Sku, string Name, decimal SystemQty, decimal ActualQty, decimal Diff);
public record StocktakeDto(
    string Id, string VoucherCode, string Date, string CreatedBy, string Status,
    string WarehouseId, string WarehouseName, string Condition, bool IsFrozen,
    string Note, List<StocktakeItemDto> Items);

public record AdjustmentItemDto(string ProductId, string Sku, string Name, decimal AdjustQty, string Reason);
public record AdjustmentDto(
    string Id, string VoucherCode, string Date, string StocktakeCode, string Type,
    string CreatedBy, string Condition, string WarehouseId, string WarehouseName,
    string Note, bool IsNew, List<AdjustmentItemDto> Items);

public record MemberDto(
    string Id, string Code, string Name, string Role, string RoleLabel,
    string Email, string Phone, string Status, string JoinedDate);
public record CreateMemberDto(
    string Code, string Name, string Role, string RoleLabel,
    string Email, string Phone, string Status, string JoinedDate, string Password);
public record UpdateMemberDto(
    string? Code, string? Name, string? Role, string? RoleLabel,
    string? Email, string? Phone, string? Status, string? JoinedDate, string? Password);

public record ProjectDto(string Id, string Name, string Stage, string Pm);
public record ConsumableQuotaDto(string ProjectId, string ProductId, decimal QuotaQty, decimal IssuedQty);
public record ProjectStockCellDto(
    string ProductId, string ProjectId, decimal OpeningQty,
    decimal ImportQty, decimal ExportQty, decimal ClosingQty);
public record LendingLogDto(
    string Id, string VoucherCode, string Date, string BorrowingProjectId,
    string LendingProjectId, string ProductId, decimal Qty, string Status,
    bool BorrowPmSigned, bool LendPmSigned, string Note, string? CreatedBy, string? CreatedTime);

public record ConversionItemDto(string ProductId, string Sku, string Name, decimal Qty, string Unit, string Reason);
public record ConversionDto(
    string Id, string VoucherCode, string Date, string FromWarehouseId, string FromWarehouseName,
    string FromType, string ToWarehouseId, string ToWarehouseName, string ToType,
    List<ConversionItemDto> Items, string Status, string CreatedBy, string? ApprovedBy, string Note);

public record SupplierSummaryDto(string Name, decimal Price, int Credit, int LeadTime, string Rating, string Badge);
public record PriceHistoryEntryDto(string Month, decimal Steel, decimal Cement, decimal Cable);
public record ExpectedImportDto(string Id, string Project, string Sku, string Name, decimal Qty, decimal Surplus, string Unit, string Date);
public record PoRecordDto(string Code, string Project, string Supplier, string Date, List<ReconciliationItemDto> Items);
public record ReconciliationItemDto(string Sku, string Name, decimal EstPrice, decimal PoPrice, decimal Ordered, decimal Received, string Unit);

public record DashboardKpiDto(int TotalStock, decimal TotalValue, int ActiveWarehouses, int TodayImports, int TodayExports, int PendingApprovals);
public record DashboardTrendDto(string Month, decimal ImportValue, decimal ExportValue);
public record DashboardCategoryDto(string Category, decimal Value);
public record DashboardActivityDto(string Type, string Code, string Description, string Time);
public record BirthdayDto(string Name, string Department, int Day);
public record TopProductDto(string Sku, string Name, decimal Qty, decimal Value);
