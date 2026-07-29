using NamViet.ERP.SharedKernel.Entities;

namespace NamViet.ERP.Modules.Warehouse.Domain.Entities;

/// <summary>Đánh giá nhà cung cấp theo kỳ. Xếp hạng suy ra từ điểm + ngưỡng trong thiết lập.</summary>
public class SupplierEvaluation : BaseEntity
{
    public int Id { get; set; }
    public int SupplierId { get; set; }
    public string? SupplierNameSnapshot { get; set; }

    /// <summary>Kỳ đánh giá, ví dụ "Q2/2026" hoặc "2026".</summary>
    public string Period { get; set; } = string.Empty;

    public decimal Score { get; set; }
    public string Grade { get; set; } = SupplierGrades.C;
    public DateOnly EvaluatedAt { get; set; }
    public decimal OnTimeDeliveryRate { get; set; }
    public decimal DefectRate { get; set; }
    public decimal WarrantyCompletionRate { get; set; }
    public string? Note { get; set; }

    public Guid? EvaluatedById { get; set; }
    public string? EvaluatedByName { get; set; }

    public Supplier? Supplier { get; set; }
}

public static class SupplierGrades
{
    public const string A = "A";
    public const string B = "B";
    public const string C = "C";
    public const string D = "D";
    public const string E = "E";
    public const string F = "F";
    public const string G = "G";
    public const string H = "H";

    public static readonly string[] All = [A, B, C, D, E, F, G, H];

    /// <summary>
    /// Điểm càng cao hạng càng tốt. So khớp ngưỡng theo thứ tự A→H;
    /// dưới ngưỡng H vẫn xếp hạng H (hạng thấp nhất).
    /// </summary>
    public static string FromScore(
        decimal score,
        decimal gradeAMinScore,
        decimal gradeBMinScore,
        decimal gradeCMinScore,
        decimal gradeDMinScore,
        decimal gradeEMinScore,
        decimal gradeFMinScore,
        decimal gradeGMinScore,
        decimal gradeHMinScore)
    {
        if (score >= gradeAMinScore) return A;
        if (score >= gradeBMinScore) return B;
        if (score >= gradeCMinScore) return C;
        if (score >= gradeDMinScore) return D;
        if (score >= gradeEMinScore) return E;
        if (score >= gradeFMinScore) return F;
        if (score >= gradeGMinScore) return G;
        if (score >= gradeHMinScore) return H;
        return H;
    }
}

public static class SupplierEvaluationCycles
{
    public const string Monthly = "monthly";
    public const string Quarterly = "quarterly";
    public const string Yearly = "yearly";

    public static readonly HashSet<string> All = new(StringComparer.OrdinalIgnoreCase)
    {
        Monthly, Quarterly, Yearly,
    };
}
