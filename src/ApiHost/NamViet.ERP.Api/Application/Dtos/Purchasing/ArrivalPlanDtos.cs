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
    decimal? UnitPrice,
    /// <summary>upcoming | due_today | overdue</summary>
    string LineStatus);

public record ArrivalPlanProjectDto(
    string ProjectCode,
    string ProjectName,
    bool HasArrivalInWindow,
    DateOnly? EarliestExpectedDate,
    int LineCount,
    decimal TotalRemainingQty,
    int UpcomingCount,
    int DueTodayCount,
    int OverdueCount,
    IReadOnlyList<ArrivalPlanLineDto> Lines);

public record ArrivalPlanListResponse(
    int DaysAhead,
    int OverdueLookbackDays,
    /// <summary>Ngày «hôm nay» theo giờ Việt Nam — mốc realtime cho filter.</summary>
    DateOnly AsOfDate,
    DateOnly WindowFrom,
    DateOnly WindowTo,
    IReadOnlyList<ArrivalPlanProjectDto> Projects);
