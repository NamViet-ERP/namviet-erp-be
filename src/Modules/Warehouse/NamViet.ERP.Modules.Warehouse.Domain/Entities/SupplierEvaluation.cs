using NamViet.ERP.Modules.Warehouse.Domain.SupplierEvaluation;
using NamViet.ERP.SharedKernel.Entities;

namespace NamViet.ERP.Modules.Warehouse.Domain.Entities;

/// <summary>
/// Đánh giá nhà cung cấp định kỳ (KPI). Điểm/hạng suy ra từ 30 tiêu chí (thang 1–5).
/// </summary>
public class SupplierEvaluation : BaseEntity
{
    public int Id { get; set; }
    public int SupplierId { get; set; }
    public string? SupplierNameSnapshot { get; set; }

    /// <summary>Kỳ đánh giá, ví dụ "Q2/2026" hoặc "2026".</summary>
    public string Period { get; set; } = string.Empty;

    /// <summary>Điểm tổng (trung bình 5 nhóm), thang 1–5 — BE tính, không nhận từ client.</summary>
    public decimal Score { get; set; }

    /// <summary>Hạng KPI A–D — BE tính.</summary>
    public string Grade { get; set; } = SupplierKpiGrades.C;

    public DateOnly EvaluatedAt { get; set; }

    /// <summary>
    /// Điểm từng tiêu chí: key <c>flex_0</c>… theo <see cref="KpiCriteriaCatalog"/>.
    /// </summary>
    public Dictionary<string, int> CriteriaScores { get; set; } = new(StringComparer.Ordinal);

    public string? Note { get; set; }

    public Guid? EvaluatedById { get; set; }
    public string? EvaluatedByName { get; set; }

    public Supplier? Supplier { get; set; }
}

/// <summary>Hạng hồ sơ NCC / đánh giá theo đơn hàng (A–H), map từ điểm trung bình thang 0–10.</summary>
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

    /// <summary>Chưa có đánh giá đơn hàng hoàn thành.</summary>
    public const string Unrated = "—";

    public static readonly string[] All = [A, B, C, D, E, F, G, H];

    public static string FromScore(
        decimal score,
        decimal gradeAMinScore,
        decimal gradeBMinScore,
        decimal gradeCMinScore,
        decimal gradeDMinScore,
        decimal gradeEMinScore,
        decimal gradeFMinScore,
        decimal gradeGMinScore,
        decimal gradeHMinScore) =>
        SupplierGradeMapper.FromOrderScore(
            score,
            gradeAMinScore,
            gradeBMinScore,
            gradeCMinScore,
            gradeDMinScore,
            gradeEMinScore,
            gradeFMinScore,
            gradeGMinScore,
            gradeHMinScore);
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
