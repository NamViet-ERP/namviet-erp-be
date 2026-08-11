namespace NamViet.ERP.Api.Application.Dtos.Purchasing;

public record PurchaseEstimateListItemDto(
    Guid Id,
    string Code,
    string ProjectCode,
    string Title,
    string Status,
    int CurrentVersion,
    int FilledLineCount,
    int LineCount,
    string? PreparerName,
    DateTime? PreparerDate,
    DateTime CreatedAt,
    string CreatedByName,
    DateTime UpdatedAt,
    string UpdatedByName);

public record PurchaseEstimatePagedResponse(
    IReadOnlyList<PurchaseEstimateListItemDto> Items,
    int TotalCount,
    int Page,
    int PageSize,
    int TotalPages);

public record PurchaseEstimateVersionSummaryDto(
    Guid Id,
    int VersionNumber,
    DateTime CreatedAt,
    string UpdatedByName);

public record PurchaseEstimateLineDto(
    Guid Id,
    string MaterialCode,
    string MaterialName,
    string? CategoryName,
    string Unit,
    decimal OldQty,
    decimal NewQty);

public record PurchaseEstimateProposalDto(
    Guid Id,
    string Name,
    string? Spec,
    string Unit,
    decimal Qty,
    string? Note);

public record PurchaseEstimateApprovalDto(
    Guid Id,
    int Level,
    string Name,
    string Status,
    string? ActionByName,
    DateTime? ActionAt,
    string? Note);

public record PurchaseEstimateDetailDto(
    Guid Id,
    string Code,
    string ProjectCode,
    string Title,
    string Status,
    int CurrentVersion,
    int ViewedVersion,
    Guid? ShareToken,
    string? PreparerName,
    DateTime? PreparerDate,
    string? ReviewedByName,
    DateTime? ReviewedAt,
    string? ConvertedPlanCode,
    DateTime CreatedAt,
    string CreatedByName,
    DateTime UpdatedAt,
    string UpdatedByName,
    IReadOnlyList<PurchaseEstimateVersionSummaryDto> Versions,
    IReadOnlyList<PurchaseEstimateLineDto> Lines,
    IReadOnlyList<PurchaseEstimateProposalDto> Proposals,
    IReadOnlyList<PurchaseEstimateApprovalDto> Approvals);

public record PurchaseEstimateLineInput(string MaterialCode, decimal NewQty);

public record PurchaseEstimateProposalInput(
    string Name,
    string? Spec,
    string Unit,
    decimal Qty,
    string? Note);

public record CreatePurchaseEstimateRequest(
    string ProjectCode,
    string Title,
    IReadOnlyList<PurchaseEstimateLineInput>? Lines = null);

public record UpdatePurchaseEstimateRequest(
    string? Title,
    IReadOnlyList<PurchaseEstimateLineInput> Lines);

public record PurchaseEstimateShareLinkDto(string Token, string Path);

public record DecidePurchaseEstimateApprovalRequest(int Level, string Decision, string? Note);

public record ConvertPurchaseEstimateResult(string PlanCode);

public record PublicPurchaseEstimateLineDto(
    string MaterialCode,
    string MaterialName,
    string? CategoryName,
    string Unit,
    decimal Qty);

public record PublicPurchaseEstimatePreparerDto(
    Guid Id,
    string Name,
    string Role,
    string RoleLabel);

public record PublicPurchaseEstimateDto(
    string Code,
    string Title,
    string ProjectCode,
    string? ProjectName,
    string Status,
    bool CanFill,
    string? PreparerName,
    DateTime? PreparerDate,
    IReadOnlyList<PublicPurchaseEstimateLineDto> Lines,
    IReadOnlyList<PurchaseEstimateProposalDto> Proposals,
    IReadOnlyList<PublicPurchaseEstimatePreparerDto> Preparers);

public record SubmitPublicPurchaseEstimateRequest(
    string PreparerName,
    IReadOnlyList<PurchaseEstimateLineInput> Lines,
    IReadOnlyList<PurchaseEstimateProposalInput>? Proposals = null);
