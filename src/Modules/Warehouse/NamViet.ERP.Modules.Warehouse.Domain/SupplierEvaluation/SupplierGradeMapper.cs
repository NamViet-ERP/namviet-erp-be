namespace NamViet.ERP.Modules.Warehouse.Domain.SupplierEvaluation;

/// <summary>
/// Map điểm → hạng. Order/delivery dùng ladder A–H (settings, thang 0–10).
/// KPI định kỳ dùng A–D cố định theo prototype nghiệp vụ.
/// </summary>
public static class SupplierGradeMapper
{
    public static string FromOrderScore(
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
        if (score >= gradeAMinScore) return "A";
        if (score >= gradeBMinScore) return "B";
        if (score >= gradeCMinScore) return "C";
        if (score >= gradeDMinScore) return "D";
        if (score >= gradeEMinScore) return "E";
        if (score >= gradeFMinScore) return "F";
        if (score >= gradeGMinScore) return "G";
        if (score >= gradeHMinScore) return "H";
        return "H";
    }

    public static string OrderGradeLabel(string grade) => grade switch
    {
        "A" => "Hạng A — Ưu tiên hợp tác",
        "B" => "Hạng B — Đạt yêu cầu",
        "C" => "Hạng C — Cần cải thiện",
        "D" => "Hạng D — Hạn chế hợp tác",
        "E" => "Hạng E",
        "F" => "Hạng F",
        "G" => "Hạng G",
        "H" => "Hạng H — Yếu nhất",
        "—" => "Chưa đánh giá",
        _ => string.IsNullOrWhiteSpace(grade) ? "Chưa đánh giá" : $"Hạng {grade}",
    };

    /// <summary>KPI overall 1–5 → A/B/C/D (prototype HTML).</summary>
    public static string FromKpiScore(decimal overallScore)
    {
        if (overallScore >= 4.25m) return SupplierKpiGrades.A;
        if (overallScore >= 3.5m) return SupplierKpiGrades.B;
        if (overallScore >= 2.5m) return SupplierKpiGrades.C;
        return SupplierKpiGrades.D;
    }

    public static string KpiGradeLabel(string grade) => grade switch
    {
        SupplierKpiGrades.A => "Hạng A — Xuất sắc",
        SupplierKpiGrades.B => "Hạng B — Tốt",
        SupplierKpiGrades.C => "Hạng C — Trung bình, cần cải thiện",
        SupplierKpiGrades.D => "Hạng D — Yếu, cần xem xét lại",
        _ => string.IsNullOrWhiteSpace(grade) ? "Chưa đánh giá" : $"Hạng {grade}",
    };
}

/// <summary>Hạng KPI định kỳ (A–D).</summary>
public static class SupplierKpiGrades
{
    public const string A = "A";
    public const string B = "B";
    public const string C = "C";
    public const string D = "D";

    public static readonly string[] All = [A, B, C, D];
}

/// <summary>
/// Ngưỡng mặc định thang 0–10 cho đánh giá theo đơn hàng (khớp prototype: A≥8.5, B≥7, C≥5).
/// </summary>
public static class OrderGradeThresholdDefaults
{
    public const decimal A = 8.5m;
    public const decimal B = 7.0m;
    public const decimal C = 5.0m;
    public const decimal D = 4.0m;
    public const decimal E = 3.0m;
    public const decimal F = 2.0m;
    public const decimal G = 1.0m;
    public const decimal H = 0.0m;
}
