namespace NamViet.ERP.SharedKernel.Time;

/// <summary>Ngày/giờ nghiệp vụ theo múi giờ Việt Nam (không dùng UTC date cho lịch làm việc).</summary>
public static class VietnamClock
{
    private static readonly TimeZoneInfo Zone = Resolve();

    public static TimeZoneInfo TimeZone => Zone;

    private static TimeZoneInfo Resolve()
    {
        if (OperatingSystem.IsWindows())
        {
            try
            {
                return TimeZoneInfo.FindSystemTimeZoneById("SE Asia Standard Time");
            }
            catch (TimeZoneNotFoundException)
            {
            }
            catch (InvalidTimeZoneException)
            {
            }

            return TimeZoneInfo.FindSystemTimeZoneById("Asia/Ho_Chi_Minh");
        }

        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById("Asia/Ho_Chi_Minh");
        }
        catch (TimeZoneNotFoundException)
        {
        }
        catch (InvalidTimeZoneException)
        {
        }

        return TimeZoneInfo.FindSystemTimeZoneById("SE Asia Standard Time");
    }

    public static DateTime Now => TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, Zone);

    public static DateOnly Today => DateOnly.FromDateTime(Now);

    /// <summary>Đổi ngày lịch VN + giờ địa phương thành UTC (Kind=Utc).</summary>
    public static DateTime ToUtc(DateOnly date, TimeOnly time)
    {
        var local = DateTime.SpecifyKind(date.ToDateTime(time), DateTimeKind.Unspecified);
        return TimeZoneInfo.ConvertTimeToUtc(local, Zone);
    }
}
