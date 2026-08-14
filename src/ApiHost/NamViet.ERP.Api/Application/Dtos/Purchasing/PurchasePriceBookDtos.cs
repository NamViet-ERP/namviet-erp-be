namespace NamViet.ERP.Api.Application.Dtos.Purchasing;

public record PriceBookItemDto(
    string MaterialCode,
    string MaterialName,
    int CategoryId,
    string? CategoryName,
    decimal? CeilingPrice,
    string? AbcClass,
    string? TechnicalSpec,
    string? GeneralTechnicalSpec,
    decimal? LatestPrice,
    DateOnly? LatestDate,
    string? LatestSupplierName,
    decimal? MinPrice,
    decimal? MaxPrice,
    decimal? AvgPrice,
    int SupplierCount,
    decimal? TrendPct,
    bool OverCeiling,
    decimal? EstimatePrice,
    decimal? EstimateDiff,
    decimal? EstimateDiffPct,
    int HistoryCount);

public record PriceBookHistoryRowDto(
    DateOnly Date,
    string Kind,
    string? Status,
    int? SupplierId,
    string? SupplierName,
    decimal UnitPrice,
    string? PoRef,
    string? ProjectCode);

public record PriceBookItemDetailDto(
    PriceBookItemDto Item,
    IReadOnlyList<PriceBookHistoryRowDto> History);

public record PriceBookMatrixCellDto(
    int SupplierId,
    string SupplierName,
    decimal? UnitPrice,
    bool IsMin);

public record PriceBookMatrixRowDto(
    string MaterialCode,
    string MaterialName,
    IReadOnlyList<PriceBookMatrixCellDto> Cells);

public record PriceBookMatrixDto(
    int CategoryId,
    string CategoryName,
    IReadOnlyList<PriceBookMatrixRowDto> Rows);

public record PriceBookCategoryRowDto(
    int CategoryId,
    string CategoryCode,
    string CategoryName,
    int ItemCount,
    int SupplierCount,
    int PoCount,
    decimal TotalSpend,
    decimal SharePct);

public record PriceBookMarginRowDto(
    string MaterialCode,
    string MaterialName,
    string? ProjectCode,
    decimal SellPrice,
    DateOnly EffectiveDate,
    decimal? PurchaseAvg,
    decimal? PurchaseLatest,
    decimal? Margin,
    decimal? MarginPct,
    int SellHistoryCount);

public record PurchaseSellPriceDto(
    Guid Id,
    string MaterialCode,
    string MaterialName,
    string? ProjectCode,
    decimal SellPrice,
    DateOnly EffectiveDate,
    string CreatedByName,
    string? Note);

public record UpsertPurchaseSellPriceRequest(
    string MaterialCode,
    string? ProjectCode,
    decimal SellPrice,
    DateOnly EffectiveDate,
    string? Note);

public record PriceBookEstimateRowDto(
    string MaterialCode,
    string MaterialName,
    string? ProjectCode,
    decimal EstimatePrice,
    DateOnly EffectiveDate,
    decimal? PurchaseLatest,
    decimal? Diff,
    decimal? DiffPct,
    decimal? MarginPct,
    int EstimateHistoryCount);

public record PurchaseItemEstimatePriceDto(
    Guid Id,
    string MaterialCode,
    string MaterialName,
    string? ProjectCode,
    decimal EstimatePrice,
    DateOnly EffectiveDate,
    string CreatedByName,
    string? Note);

public record UpsertPurchaseItemEstimatePriceRequest(
    string MaterialCode,
    string? ProjectCode,
    decimal EstimatePrice,
    DateOnly EffectiveDate,
    string? Note);
