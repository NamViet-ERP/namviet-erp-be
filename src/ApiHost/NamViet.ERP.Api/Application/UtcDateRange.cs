using NamViet.ERP.SharedKernel.Time;

namespace NamViet.ERP.Api.Application;

/// <summary>
/// Quy đổi DateOnly lịch Việt Nam (UTC+7) sang khoảng timestamp UTC để lọc CreatedAt.
/// </summary>
public static class UtcDateRange
{
    public static DateTime? Start(DateOnly? date) =>
        date.HasValue ? VietnamClock.ToUtc(date.Value, TimeOnly.MinValue) : null;

    public static DateTime? ExclusiveEnd(DateOnly? date) =>
        date.HasValue ? VietnamClock.ToUtc(date.Value.AddDays(1), TimeOnly.MinValue) : null;
}
