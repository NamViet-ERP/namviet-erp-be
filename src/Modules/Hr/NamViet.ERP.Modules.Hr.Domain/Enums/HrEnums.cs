namespace NamViet.ERP.Modules.Hr.Domain.Enums;

public enum RewardType
{
    KhenThuong,
    KyLuat
}

public enum AdminRequestType
{
    Asset,
    Stationery,
    Uniform,
    Handbook,
    Flight,
    MealCount,
    Brand,
    Announcement,
    Other
}

public static class HrEnumMapper
{
    public static RewardType ParseRewardType(string value) => value.Trim().ToLowerInvariant() switch
    {
        "khen thưởng" or "khenthuong" => RewardType.KhenThuong,
        "kỷ luật" or "ky luat" or "kyluat" => RewardType.KyLuat,
        _ => Enum.TryParse<RewardType>(value, true, out var parsed) ? parsed : RewardType.KhenThuong
    };

    public static string ToDisplayString(RewardType type) => type switch
    {
        RewardType.KyLuat => "Kỷ luật",
        _ => "Khen thưởng"
    };

    public static AdminRequestType ParseAdminRequestType(string value) =>
        Enum.TryParse<AdminRequestType>(value, true, out var parsed) ? parsed : AdminRequestType.Other;

    public static string ToDisplayString(AdminRequestType type) => type.ToString();
}
