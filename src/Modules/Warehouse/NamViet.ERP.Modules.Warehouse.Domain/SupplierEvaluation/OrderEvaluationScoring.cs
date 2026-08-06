namespace NamViet.ERP.Modules.Warehouse.Domain.SupplierEvaluation;

/// <summary>
/// Trọng số đánh giá NCC theo đơn hàng (thang 1–10).
/// Keep in sync with FE <c>src/lib/suppliers/orderEvalScoring.ts</c> and QuanLyCungUng_EPC.html.
/// </summary>
public static class OrderEvaluationWeights
{
    public const decimal Quality = 0.30m;
    public const decimal Delivery = 0.25m;
    public const decimal Price = 0.20m;
    public const decimal Service = 0.15m;
    public const decimal Compliance = 0.10m;

    public const decimal MinCriterion = 1m;
    public const decimal MaxCriterion = 10m;
}

/// <summary>Năm tiêu chí đánh giá theo lần nhận hàng / PO.</summary>
public readonly record struct OrderEvaluationCriteria(
    decimal Quality,
    decimal Delivery,
    decimal Price,
    decimal Service,
    decimal Compliance);

/// <summary>Tính điểm tổng có trọng số — pure domain, không phụ thuộc EF/API.</summary>
public static class OrderEvaluationScoring
{
    public static decimal Compute(OrderEvaluationCriteria criteria)
    {
        ValidateCriterion(criteria.Quality, "Chất lượng hàng hoá");
        ValidateCriterion(criteria.Delivery, "Tiến độ giao hàng");
        ValidateCriterion(criteria.Price, "Giá cả cạnh tranh");
        ValidateCriterion(criteria.Service, "Dịch vụ & hỗ trợ");
        ValidateCriterion(criteria.Compliance, "Tuân thủ hợp đồng/hồ sơ");

        var score =
            criteria.Quality * OrderEvaluationWeights.Quality
            + criteria.Delivery * OrderEvaluationWeights.Delivery
            + criteria.Price * OrderEvaluationWeights.Price
            + criteria.Service * OrderEvaluationWeights.Service
            + criteria.Compliance * OrderEvaluationWeights.Compliance;

        return Math.Round(score, 2, MidpointRounding.AwayFromZero);
    }

    public static void ValidateCriterion(decimal value, string fieldName)
    {
        if (value < OrderEvaluationWeights.MinCriterion
            || value > OrderEvaluationWeights.MaxCriterion)
        {
            throw new ArgumentOutOfRangeException(
                nameof(value),
                $"{fieldName} phải từ {OrderEvaluationWeights.MinCriterion} đến {OrderEvaluationWeights.MaxCriterion}.");
        }
    }
}
