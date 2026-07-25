namespace NamViet.ERP.Api.Application.Dtos.Purchasing;

public record ArrivalPlanLineDto(
    int LineId,
    string PlanCode,
    string? MaterialCode,
    string ProductName,
    string Unit,
    decimal Quantity,
    decimal OrderedQty,
    decimal ReceivedQty,
    decimal RemainingQty,
    DateOnly ExpectedDeliveryDate,
    string? PurchaseSupplierName,
    decimal? UnitPrice);

public record ArrivalPlanProjectDto(
    string ProjectCode,
    string ProjectName,
    bool HasArrivalInWindow,
    DateOnly? EarliestExpectedDate,
    int LineCount,
    decimal TotalRemainingQty,
    IReadOnlyList<ArrivalPlanLineDto> Lines);

public record ArrivalPlanListResponse(
    int DaysAhead,
    DateOnly WindowFrom,
    DateOnly WindowTo,
    IReadOnlyList<ArrivalPlanProjectDto> Projects);
