namespace NamViet.ERP.Api.Application.Dtos.Wms;

public record StockMatrixProjectColumnDto(
    string ProjectCode,
    string ProjectId,
    string ProjectName,
    decimal ActualQty,
    decimal PlannedQty,
    decimal ReservedQty);

public record StockMatrixRowDto(
    string MaterialCode,
    string MaterialName,
    string Unit,
    bool IsActive,
    decimal GeneralActualQty,
    decimal GeneralReservedQty,
    decimal GeneralAvailableQty,
    IReadOnlyList<StockMatrixProjectColumnDto> Projects);

public record MaterialCategoryApiDto(
    int Id, string Code, string Name, int Level, int? ParentId, int SortOrder, bool IsActive);

public record WarehouseProjectApiDto(
    string ProjectCode,
    string ProjectName,
    string ProjectType,
    string Status,
    DateOnly? EndDate);

public record WarehouseProjectInventoryApiDto(
    string ProjectCode,
    string MaterialCode,
    string MaterialName,
    string Unit,
    decimal PlannedImportQty,
    decimal PlannedExportQty,
    decimal ActualImportQty,
    decimal ActualExportQty,
    decimal ActualInventoryQty,
    decimal PlannedInventoryQty,
    decimal ReservedQty);

public record MaterialApiDto(
    string MaterialCode,
    string MaterialName,
    int CategoryId,
    string? CategoryName,
    string Unit,
    string? TechnicalSpec,
    string? QualityStandard,
    bool IsConsumable);
