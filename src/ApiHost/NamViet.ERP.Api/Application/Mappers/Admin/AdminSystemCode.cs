using System.Text.RegularExpressions;

namespace NamViet.ERP.Api.Application.Mappers.Admin;

/// <summary>Mã tài khoản hệ thống: SYS-0001, không dùng timestamp.</summary>
public static class AdminSystemCode
{
    public const string Prefix = "SYS-";
    public const int Digits = 4;
    public const int MaxLength = 50;

    static readonly Regex Sequential = new(
        @"^SYS-(\d{1,4})$",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public static bool IsHrEmployeeStyle(string code) =>
        code.StartsWith("NV-", StringComparison.OrdinalIgnoreCase);

    public static int? ParseSequentialNumber(string? code)
    {
        if (string.IsNullOrWhiteSpace(code)) return null;
        var match = Sequential.Match(code.Trim());
        if (!match.Success || !int.TryParse(match.Groups[1].Value, out var n)) return null;
        return n;
    }

    public static string Format(int num) =>
        $"{Prefix}{num.ToString().PadLeft(Digits, '0')}";

    public static string SuggestNext(IEnumerable<string?> codes)
    {
        var max = 0;
        foreach (var code in codes)
        {
            var n = ParseSequentialNumber(code);
            if (n is int value && value > max) max = value;
        }
        return Format(max + 1);
    }

    public static string Resolve(string? requested, IEnumerable<string?> existing)
    {
        var code = requested?.Trim();
        return string.IsNullOrEmpty(code) ? SuggestNext(existing) : code;
    }
}
