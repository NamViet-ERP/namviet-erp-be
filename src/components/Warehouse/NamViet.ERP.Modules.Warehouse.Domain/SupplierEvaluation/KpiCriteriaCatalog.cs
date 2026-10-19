namespace NamViet.ERP.Modules.Warehouse.Domain.SupplierEvaluation;

/// <summary>
/// Catalog 5 nhóm / 30 tiêu chí KPI định kỳ (thang 1–5).
/// Keep in sync with FE <c>src/lib/suppliers/kpiCriteriaCatalog.ts</c> and QuanLyCungUng_EPC.html.
/// </summary>
public static class KpiCriteriaCatalog
{
    public const int CatalogVersion = 1;
    public const decimal MinScore = 1m;
    public const decimal MaxScore = 5m;
    public const int ExpectedCriterionCount = 30;

    public static IReadOnlyList<KpiCriteriaGroup> Groups { get; } =
    [
        new("flex", "1. Khả năng đáp ứng & linh hoạt",
        [
            "Khả năng đáp ứng đơn hàng gấp",
            "Khả năng tăng sản lượng khi nhu cầu tăng",
            "Khả năng thay đổi lịch giao hàng",
            "Khả năng chia nhỏ đơn hàng",
            "Khả năng đáp ứng nhiều địa điểm giao hàng",
            "Khả năng cung cấp sản phẩm/vật tư thay thế",
        ]),
        new("coord", "2. Khả năng phối hợp & xử lý vấn đề",
        [
            "Tốc độ phản hồi khi có vấn đề",
            "Tốc độ xác nhận đơn hàng",
            "Mức độ chủ động cập nhật tình trạng đơn hàng",
            "Khả năng phối hợp khi giao hàng trễ",
            "Khả năng phối hợp khi hàng bị lỗi",
            "Khả năng đưa ra phương án xử lý",
        ]),
        new("supply", "3. Năng lực cung ứng & độ tin cậy",
        [
            "Năng lực cung ứng thực tế",
            "Độ ổn định của nguồn cung",
            "Mức độ phụ thuộc vào nguồn cung khác",
            "Khả năng duy trì nguồn cung khi thị trường biến động",
        ]),
        new("commercial", "4. Năng lực thương mại & hỗ trợ cung ứng",
        [
            "Tốc độ cung cấp báo giá",
            "Mức độ rõ ràng của báo giá",
            "Khả năng giữ giá trong thời gian cam kết",
            "Khả năng đưa ra phương án thương mại phù hợp",
            "Mức độ linh hoạt về điều khoản thanh toán",
            "Khả năng cung cấp thông tin thị trường",
            "Khả năng cung cấp thông tin về biến động giá và nguồn hàng",
        ]),
        new("tech", "5. Năng lực kỹ thuật & giải pháp",
        [
            "Mức độ hiểu rõ sản phẩm",
            "Khả năng tư vấn kỹ thuật",
            "Khả năng đề xuất sản phẩm/vật tư thay thế",
            "Khả năng hỗ trợ xử lý vấn đề kỹ thuật",
            "Khả năng xác định nguyên nhân khi vật tư không phù hợp",
            "Khả năng hỗ trợ hướng dẫn sử dụng/lắp đặt",
            "Khả năng cung cấp tài liệu kỹ thuật và hồ sơ liên quan",
        ]),
    ];

    public static IReadOnlyList<string> AllKeys { get; } = Groups
        .SelectMany(g => g.Criteria.Select((_, i) => CriterionKey(g.Key, i)))
        .ToArray();

    public static string CriterionKey(string groupKey, int index) => $"{groupKey}_{index}";
}

public sealed record KpiCriteriaGroup(
    string Key,
    string Name,
    IReadOnlyList<string> Criteria);

/// <summary>Kết quả tính điểm KPI định kỳ.</summary>
public sealed record KpiEvaluationResult(
    decimal OverallScore,
    IReadOnlyDictionary<string, decimal> GroupAverages);

public static class KpiEvaluationScoring
{
    /// <summary>
    /// Validate đủ 30 key và mỗi điểm 1–5, rồi tính trung bình từng nhóm rồi overall.
    /// </summary>
    public static KpiEvaluationResult Compute(IReadOnlyDictionary<string, int> scores)
    {
        if (scores is null)
            throw new ArgumentNullException(nameof(scores));

        var missing = KpiCriteriaCatalog.AllKeys.Where(k => !scores.ContainsKey(k)).ToList();
        if (missing.Count > 0)
        {
            throw new ArgumentException(
                $"Thiếu điểm tiêu chí KPI: {string.Join(", ", missing.Take(5))}"
                + (missing.Count > 5 ? $" … (+{missing.Count - 5})" : "."));
        }

        var extras = scores.Keys.Except(KpiCriteriaCatalog.AllKeys).ToList();
        if (extras.Count > 0)
        {
            throw new ArgumentException(
                $"Key tiêu chí KPI không hợp lệ: {string.Join(", ", extras.Take(5))}.");
        }

        foreach (var key in KpiCriteriaCatalog.AllKeys)
        {
            var v = scores[key];
            if (v < (int)KpiCriteriaCatalog.MinScore || v > (int)KpiCriteriaCatalog.MaxScore)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(scores),
                    $"Tiêu chí '{key}' phải từ {KpiCriteriaCatalog.MinScore} đến {KpiCriteriaCatalog.MaxScore}.");
            }
        }

        var groupAvgs = new Dictionary<string, decimal>(StringComparer.Ordinal);
        foreach (var group in KpiCriteriaCatalog.Groups)
        {
            decimal sum = 0;
            for (var i = 0; i < group.Criteria.Count; i++)
                sum += scores[KpiCriteriaCatalog.CriterionKey(group.Key, i)];
            groupAvgs[group.Key] = Math.Round(
                sum / group.Criteria.Count, 2, MidpointRounding.AwayFromZero);
        }

        var overall = Math.Round(
            groupAvgs.Values.Average(), 2, MidpointRounding.AwayFromZero);

        return new KpiEvaluationResult(overall, groupAvgs);
    }
}
