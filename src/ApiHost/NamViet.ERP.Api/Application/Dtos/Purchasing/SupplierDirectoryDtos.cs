namespace NamViet.ERP.Api.Application.Dtos.Purchasing;

public record SupplierGroupDto(int Id, string Name, string? Description, int SupplierCount);

public record SupplierGroupPagedResponse(
    IReadOnlyList<SupplierGroupDto> Items,
    int TotalCount,
    int Page,
    int PageSize,
    int TotalPages);

public record UpsertSupplierGroupRequest(string Name, string? Description);

/// <summary>Thành viên của một nhóm NCC.</summary>
public record SupplierGroupMemberDto(int SupplierId, string SupplierCode, string SupplierName);

/// <summary>Thay toàn bộ danh sách NCC của một nhóm.</summary>
public record SetSupplierGroupMembersRequest(IReadOnlyList<int> SupplierIds);

/// <summary>Nhóm mà một NCC đang thuộc về.</summary>
public record SupplierGroupRefDto(int Id, string Name);

/// <summary>Thay toàn bộ danh sách nhóm của một NCC.</summary>
public record SetSupplierGroupsRequest(IReadOnlyList<int> GroupIds);

public record SupplierContactDto(
    int Id,
    int SupplierId,
    string SupplierName,
    string FullName,
    string? Position,
    string? Phone,
    string? Email,
    string? Note,
    /// <summary>primary | secondary | backup</summary>
    string Role,
    string RoleLabel);

public record SupplierContactPagedResponse(
    IReadOnlyList<SupplierContactDto> Items,
    int TotalCount,
    int Page,
    int PageSize,
    int TotalPages);

public record UpsertSupplierContactRequest(
    int SupplierId,
    string FullName,
    string? Position,
    string? Phone,
    string? Email,
    string? Note,
    /// <summary>primary | secondary | backup</summary>
    string Role);

/// <summary>Đánh giá KPI định kỳ — score/grade do BE tính từ criteriaScores.</summary>
public record SupplierEvaluationDto(
    int Id,
    int SupplierId,
    string SupplierName,
    string Period,
    decimal Score,
    string Grade,
    string GradeLabel,
    DateOnly EvaluatedAt,
    IReadOnlyDictionary<string, int> CriteriaScores,
    string? Note,
    string? EvaluatedByName);

public record SupplierEvaluationPagedResponse(
    IReadOnlyList<SupplierEvaluationDto> Items,
    int TotalCount,
    int Page,
    int PageSize,
    int TotalPages);

/// <summary>Client chỉ gửi tiêu chí; Score/Grade không nhận từ client.</summary>
public record UpsertSupplierEvaluationRequest(
    int SupplierId,
    string Period,
    IReadOnlyDictionary<string, int> CriteriaScores,
    DateOnly? EvaluatedAt,
    string? Note);

/// <summary>Đánh giá NCC theo lần nhận hàng (sau duyệt phiếu NK gắn PO).</summary>
public record SupplierDeliveryEvaluationDto(
    int Id,
    int SupplierId,
    string SupplierName,
    string PoCode,
    string ImportTicketCode,
    string? ProjectCode,
    DateTime ReceivedAt,
    DateTime DueAt,
    string Status,
    bool IsOverdue,
    decimal? Quality,
    decimal? Delivery,
    decimal? Price,
    decimal? Service,
    decimal? Compliance,
    decimal? Score,
    string? Grade,
    string? GradeLabel,
    string? Note,
    string? EvaluatedByName,
    DateOnly? EvaluatedAt);

public record SupplierDeliveryEvaluationPagedResponse(
    IReadOnlyList<SupplierDeliveryEvaluationDto> Items,
    int TotalCount,
    int Page,
    int PageSize,
    int TotalPages);

/// <summary>Hoàn thành đánh giá — chỉ 5 tiêu chí 1–10; Score/Grade do BE tính.</summary>
public record CompleteSupplierDeliveryEvaluationRequest(
    decimal Quality,
    decimal Delivery,
    decimal Price,
    decimal Service,
    decimal Compliance,
    DateOnly? EvaluatedAt,
    string? Note);

public record SupplierModuleSettingsDto(
    decimal GradeAMinScore,
    decimal GradeBMinScore,
    decimal GradeCMinScore,
    decimal GradeDMinScore,
    decimal GradeEMinScore,
    decimal GradeFMinScore,
    decimal GradeGMinScore,
    decimal GradeHMinScore,
    string EvaluationCycle,
    bool GroupRequired);

public record UpdateSupplierModuleSettingsRequest(
    decimal GradeAMinScore,
    decimal GradeBMinScore,
    decimal GradeCMinScore,
    decimal GradeDMinScore,
    decimal GradeEMinScore,
    decimal GradeFMinScore,
    decimal GradeGMinScore,
    decimal GradeHMinScore,
    string EvaluationCycle,
    bool GroupRequired);

public record BlacklistSupplierRequest(string Reason);

public record SupplierBlacklistDto(
    int Id,
    string SupplierCode,
    string SupplierName,
    string? ContactPhone,
    string Rating,
    DateTime BlacklistedAt,
    string? BlacklistedByName,
    string? BlacklistReason);
