namespace NamViet.ERP.Api.Application.Dtos.Wms;

public record WmsMovementSummaryDto(
    int ImportCount,
    decimal ImportValue,
    int ExportCount,
    decimal ExportValue,
    int TransferCount);

public record WmsStockReportRowDto(
    string ProjectCode,
    string ProjectName,
    string MaterialCode,
    string MaterialName,
    string Unit,
    decimal Qty);

public record WmsPendingVoucherRowDto(
    string Kind,
    string VoucherCode,
    string Date,
    string CreatedBy,
    string? Project);

public record WmsPendingReportDto(
    int ImportCount,
    int ExportCount,
    int TransferCount,
    int StocktakeCount,
    IReadOnlyList<WmsPendingVoucherRowDto> Items);
