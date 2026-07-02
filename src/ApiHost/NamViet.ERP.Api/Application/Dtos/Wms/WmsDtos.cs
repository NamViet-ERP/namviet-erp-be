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
    string? Supplier = null, int? SupplierId = null);
public record ImportDto(
    string Id, string VoucherCode, string Date, string Supplier, string ReferenceCode,
    decimal TotalValue, string Status, string WarehouseApproval, string ApprovedBy,
    string? ApprovedAt, string WarehouseKeeper, List<ImportItemDto> Items, string CreatedBy, string Note,
    string WarehouseId, bool? IsDirectProcurement, string? Project,
    string CreatedAt);
public record CreateImportDto(
    string VoucherCode, string Date, string Supplier, string ReferenceCode,
    decimal TotalValue, string Status, string WarehouseApproval, string ApprovedBy,
    string WarehouseKeeper, List<ImportItemDto> Items, string CreatedBy, string Note,
    string WarehouseId, bool? IsDirectProcurement, string? Project);

public record ExportItemDto(string ProductId, string Sku, string Name, decimal Qty, string Unit);
public record ExportDto(
    string Id, string VoucherCode, string Date, string SoCode, string Customer,
    string WarehouseId, string WarehouseName, List<ExportItemDto> Items,
    string Status, string CreatedBy, string Note, string? ApprovedBy, string? ApprovedAt,
    string? Project, string? ProjectName,
    string CreatedAt);
public record CreateExportDto(
    string VoucherCode, string Date, string SoCode, string Customer,
    string WarehouseId, List<ExportItemDto> Items,
    string Status, string CreatedBy, string Note, string? Project);

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
    List<TransferItemDto> Items, string Status, string CreatedBy, string Note,
    string? Project, string? ToProject);

public record StocktakeItemDto(string ProductId, string Sku, string Name, decimal SystemQty, decimal ActualQty, decimal Diff);
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
