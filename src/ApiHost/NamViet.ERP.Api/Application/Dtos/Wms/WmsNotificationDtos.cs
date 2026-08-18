namespace NamViet.ERP.Api.Application.Dtos.Wms;

public record WmsNotificationItemDto(
    string Id,
    string Kind,
    string VoucherCode,
    string Title,
    string Message,
    string Route,
    string TimeLabel,
    string? ContextLabel,
    string? CreatedByName);

public record WmsPendingNotificationsDto(
    int TotalCount,
    int ImportCount,
    int ExportCount,
    int TransferCount,
    int StocktakeCount,
    IReadOnlyList<WmsNotificationItemDto> Items);
