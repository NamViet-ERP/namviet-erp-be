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
    public static AdminRequestType ParseAdminRequestType(string value) =>
        Enum.TryParse<AdminRequestType>(value, true, out var parsed) ? parsed : AdminRequestType.Other;

    public static string ToDisplayString(AdminRequestType type) => type.ToString();
}
