namespace NamViet.ERP.Api.Application;

public static class UtcDateRange
{
    public static DateTime? Start(DateOnly? date) =>
        date.HasValue
            ? DateTime.SpecifyKind(date.Value.ToDateTime(TimeOnly.MinValue), DateTimeKind.Utc)
            : null;

    public static DateTime? ExclusiveEnd(DateOnly? date) =>
        date.HasValue
            ? DateTime.SpecifyKind(date.Value.AddDays(1).ToDateTime(TimeOnly.MinValue), DateTimeKind.Utc)
            : null;
}
