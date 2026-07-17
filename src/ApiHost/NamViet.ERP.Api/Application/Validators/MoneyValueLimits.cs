namespace NamViet.ERP.Api.Application.Validators;

public static class MoneyValueLimits
{
    public const decimal Numeric18Scale2Max = 9_999_999_999_999_999.99m;

    public static bool IsValidNumeric18Scale2(decimal value) =>
        value >= 0 && value <= Numeric18Scale2Max;
}
