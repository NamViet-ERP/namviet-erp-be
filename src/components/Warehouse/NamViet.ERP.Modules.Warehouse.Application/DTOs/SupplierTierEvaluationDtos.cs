namespace NamViet.ERP.Modules.Warehouse.Application.DTOs;

public record SubmitTierEvaluationRequest(
    decimal PriceScore,
    decimal ProfileScore,
    decimal PaymentScore,
    decimal ServiceScore,
    string? TargetGrade = null,
    string? Reason = null,
    string? DetailsJson = null
);

public record SupplierTierEvaluationDto(
    int Id,
    int SupplierId,
    string? OldGrade,
    string NewGrade,
    decimal TotalScore,
    decimal PriceScore,
    decimal ProfileScore,
    decimal PaymentScore,
    decimal ServiceScore,
    string? EvaluationDetailsJson,
    string? Reason,
    string? EvaluatedById,
    string? EvaluatedByName,
    DateTime CreatedAt
);

public record SourcingKpiSummaryDto(
    int TotalSuppliers,
    int TotalNewThisMonth,
    int TotalPromotedThisMonth,
    int GradeACount,
    int GradeBCount,
    int GradeCCount,
    int GradeDCount,
    int OtherGradeCount,
    decimal PromotionRatePercent
);
