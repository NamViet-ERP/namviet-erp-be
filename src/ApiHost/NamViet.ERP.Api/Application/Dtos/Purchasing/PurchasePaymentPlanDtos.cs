namespace NamViet.ERP.Api.Application.Dtos.Purchasing;

/// <summary>Trạng thái đợt trên kế hoạch: unpaid (chưa gửi DNTT, kể cả nháp) | in_progress (đã gửi) | paid.</summary>
public record PurchasePaymentPlanStageDto(
    int StageNumber,
    decimal Percent,
    decimal Amount,
    string PaymentStatus,
    string? PaymentCode);

public record PurchasePaymentPlanRowDto(
    string PoCode,
    string? ProjectCode,
    string? ProjectName,
    decimal PoTotalAmount,
    string PoApprovalStatus,
    string PoPaymentStatus,
    int CompletedStages,
    int TotalStages,
    IReadOnlyList<PurchasePaymentPlanStageDto> Stages);

public record PurchasePaymentPlanPagedResponse(
    IReadOnlyList<PurchasePaymentPlanRowDto> Items,
    int TotalCount,
    int Page,
    int PageSize,
    int TotalPages);

/// <summary>Đợt tiếp theo cần tạo DNTT cho PO (tuần tự).</summary>
public record PurchasePaymentNextStageDto(
    string PoCode,
    int StageNumber,
    int TotalStages,
    decimal Percent,
    decimal Amount,
    decimal PoTotalAmount,
    IReadOnlyList<string> MissingDocTypes);
