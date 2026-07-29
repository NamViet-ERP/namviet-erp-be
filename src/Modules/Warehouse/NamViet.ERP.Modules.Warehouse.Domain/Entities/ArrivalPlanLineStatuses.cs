namespace NamViet.ERP.Modules.Warehouse.Domain.Entities;

/// <summary>Trạng thái dòng kế hoạch hàng về so với ngày hiện tại (UTC date).</summary>
public static class ArrivalPlanLineStatuses
{
    /// <summary>Ngày DK còn ở tương lai — sắp về.</summary>
    public const string Upcoming = "upcoming";

    /// <summary>Ngày DK đúng hôm nay — đến hạn.</summary>
    public const string DueToday = "due_today";

    /// <summary>Ngày DK đã qua — quá hạn.</summary>
    public const string Overdue = "overdue";

    public static readonly HashSet<string> All = new(StringComparer.OrdinalIgnoreCase)
    {
        Upcoming, DueToday, Overdue,
    };

    public static string FromExpectedDate(DateOnly expected, DateOnly today)
    {
        if (expected < today) return Overdue;
        if (expected == today) return DueToday;
        return Upcoming;
    }
}
