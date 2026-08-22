namespace NamViet.ERP.Api.Application.Dtos.Purchasing;

public record PurchasingKpiLineDto(string Key, string Label, int Count);

public record PurchasingKpiMetricDto(int Count, IReadOnlyList<PurchasingKpiLineDto> Lines);

public record PurchasingDashboardSummaryDto(
    PurchasingKpiMetricDto Plans,
    PurchasingKpiMetricDto Orders,
    PurchasingKpiMetricDto Imports,
    DateTime From,
    DateTime To,
    string Period,
    DateTime GeneratedAt);

public record PurchasingDashboardChartBarDto(string Key, string Label, int Count, decimal Amount);

public record PurchasingDashboardChartDto(
    IReadOnlyList<PurchasingDashboardChartBarDto> Bars,
    DateTime From,
    DateTime To,
    string Period,
    DateTime GeneratedAt);

public record PurchasingActivityItemDto(
    long Id,
    DateTime OccurredAt,
    Guid? ActorUserId,
    string ActorName,
    string Module,
    string Action,
    string? EntityType,
    string? EntityCode,
    string Message,
    string CategoryLabel);

public record PurchasingActivityPagedResponse(
    IReadOnlyList<PurchasingActivityItemDto> Items,
    long? NextCursor,
    bool HasMore);

public record PurchasingActivityActorDto(Guid UserId, string Name);
