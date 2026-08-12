using System.Globalization;
using System.Text;
using NamViet.ERP.SharedKernel.Entities;

namespace NamViet.ERP.Modules.Warehouse.Domain.Entities;

public class Supplier : BaseEntity
{
    public int Id { get; set; }
    public string SupplierCode { get; set; } = string.Empty;
    public string SupplierName { get; set; } = string.Empty;
    public string? ContactPhone { get; set; }
    public int MaxDebtDays { get; set; } = 30;
    public string? PaymentTerms { get; set; }

    /// <summary>
    /// Phân loại hồ sơ (A–H). NCC mới mặc định D; sau đó tăng/giảm theo phiếu đánh giá PO.
    /// </summary>
    public string Rating { get; set; } = SupplierGrades.D;

    /// <summary>new | approved | rejected. Chỉ sếp duyệt mới đặt PO được.</summary>
    public string ApprovalStatus { get; set; } = SupplierApprovalStatuses.New;

    public bool IsBlacklisted { get; set; }
    public DateTime? BlacklistedAt { get; set; }
    public Guid? BlacklistedById { get; set; }
    public string? BlacklistedByName { get; set; }
    public string? BlacklistReason { get; set; }

    public ICollection<WarehouseTransaction> Transactions { get; set; } = [];
    public ICollection<SupplierGroupMember> GroupMemberships { get; set; } = [];
    public ICollection<SupplierContact> Contacts { get; set; } = [];
    public ICollection<SupplierEvaluation> Evaluations { get; set; } = [];
    public ICollection<SupplierDeliveryEvaluation> DeliveryEvaluations { get; set; } = [];

    public static string GenerateCode(int sequence) => $"NCC-{sequence:D4}";

    public static bool TryParseSequence(string? code, out int sequence)
    {
        sequence = 0;
        if (string.IsNullOrWhiteSpace(code)
            || !code.StartsWith("NCC-", StringComparison.OrdinalIgnoreCase))
            return false;
        return int.TryParse(code.AsSpan(4), out sequence) && sequence > 0;
    }

    /// <summary>So khớp tên không phân biệt hoa thường / dấu / khoảng trắng thừa.</summary>
    public static string FoldName(string? name)
    {
        if (string.IsNullOrWhiteSpace(name)) return string.Empty;

        var collapsed = string.Join(' ', name.Split(' ', StringSplitOptions.RemoveEmptyEntries));
        var formD = collapsed.Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder(formD.Length);
        foreach (var ch in formD)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(ch) != UnicodeCategory.NonSpacingMark)
                sb.Append(ch);
        }

        return sb.ToString().Normalize(NormalizationForm.FormC).ToLowerInvariant().Replace('đ', 'd');
    }

    public static bool SameFoldedName(string? a, string? b)
    {
        var key = FoldName(a);
        return key.Length > 0 && key == FoldName(b);
    }
}
