namespace NamViet.ERP.Modules.Warehouse.Domain.SupplierEvaluation;

/// <summary>Trần số tiền / đơn giá mua hàng — chặn giá trị vô lý trên wire.</summary>
public static class PurchasingMoneyLimits
{
    /// <summary>Đơn giá dự toán / thành tiền dòng tối đa (VNĐ).</summary>
    public const decimal MaxUnitPrice = 1_000_000_000_000m; // 1 nghìn tỷ

    /// <summary>Tồn tối thiểu tối đa (đơn vị kho).</summary>
    public const decimal MaxMinStockQty = 1_000_000_000m;
}

/// <summary>Chuẩn hoá / validate SĐT Việt Nam (optional field).</summary>
public static class VietnamPhoneRules
{
    public const int MaxLength = 50;
    public const string InvalidMessage =
        "SĐT không hợp lệ. Chỉ nhập 10 số, bắt đầu bằng 0 (vd. 0912345678).";

    /// <summary>
    /// Trả về số đã chuẩn hoá (10 chữ số, +84→0) hoặc null nếu empty.
    /// Không bỏ qua chữ cái — có ký tự lạ thì reject.
    /// </summary>
    public static string? NormalizeOptional(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return null;

        var trimmed = raw.Trim();
        if (trimmed.Length > MaxLength)
            throw new ArgumentException($"Số điện thoại tối đa {MaxLength} ký tự.");

        if (trimmed.Any(ch => !IsAllowedPhoneChar(ch)))
            throw new ArgumentException(InvalidMessage);

        var digits = new string(trimmed.Where(char.IsDigit).ToArray());
        if (digits.StartsWith("84", StringComparison.Ordinal) && digits.Length is 11 or 12)
            digits = "0" + digits[2..];

        if (digits.Length == 10 && digits[0] == '0')
            return digits;

        throw new ArgumentException(InvalidMessage);
    }

    private static bool IsAllowedPhoneChar(char ch) =>
        char.IsDigit(ch) || ch is ' ' or '-' or '.' or '(' or ')' or '+';
}
