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

    /// <summary>
    /// Trả về số đã chuẩn hoá (chỉ chữ số, +84→0) hoặc null nếu empty.
    /// Throw nếu có giá trị nhưng không hợp lệ.
    /// </summary>
    public static string? NormalizeOptional(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return null;

        var trimmed = raw.Trim();
        if (trimmed.Length > MaxLength)
            throw new ArgumentException($"Số điện thoại tối đa {MaxLength} ký tự.");

        var digits = new string(trimmed.Where(char.IsDigit).ToArray());
        if (digits.StartsWith("84", StringComparison.Ordinal) && digits.Length == 11)
            digits = "0" + digits[2..];

        // Cho phép số di động VN 10 số bắt đầu 0, hoặc số bàn 10–11 số bắt đầu 0.
        if (digits.Length is >= 10 and <= 11 && digits.StartsWith('0'))
            return digits;

        throw new ArgumentException(
            "Số điện thoại không hợp lệ. Dùng số Việt Nam 10–11 chữ số (bắt đầu bằng 0).");
    }
}
