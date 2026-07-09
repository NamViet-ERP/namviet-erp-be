namespace NamViet.ERP.Api.Application.Dtos.Wms;

public record WmsLowStockAlertDto(
    string ProjectCode,
    string ProjectName,
    string MaterialCode,
    string MaterialName,
    string Unit,
    decimal ActualQty,
    decimal MinStockQty,
    decimal ShortageQty);
