namespace NamViet.ERP.Api.Application.Dtos.Wms;

public record StockMatrixProjectColumnDto(
    string ProjectCode,
    string ProjectId,
    string ProjectName,
    decimal ActualQty,
    decimal PlannedImportQty,
    decimal PlannedExportQty);

public record StockMatrixRowDto(
    string MaterialCode,
    string MaterialName,
    string Unit,
    bool IsActive,
    decimal GeneralActualQty,
    decimal GeneralReservedQty,
    decimal GeneralAvailableQty,
    decimal WeightedAverageUnitCost,
    /// <summary>Tổng SL phiếu HL chưa từ chối (draft/pending/approved) theo mã vật tư.</summary>
    decimal DefectedQty,
    IReadOnlyList<StockMatrixProjectColumnDto> Projects);
