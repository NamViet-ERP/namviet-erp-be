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
    bool IsPrimary);

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
    bool IsPrimary);

public record SupplierEvaluationDto(
    int Id,
    int SupplierId,
    string SupplierName,
    string Period,
    decimal Score,
    string Grade,
    DateOnly EvaluatedAt,
    decimal OnTimeDeliveryRate,
    decimal DefectRate,
    decimal WarrantyCompletionRate,
    string? Note,
    string? EvaluatedByName);

public record SupplierEvaluationPagedResponse(
    IReadOnlyList<SupplierEvaluationDto> Items,
    int TotalCount,
    int Page,
    int PageSize,
    int TotalPages);

/// <summary>Xếp hạng không nhận từ client — luôn suy ra từ Score và ngưỡng trong thiết lập.</summary>
public record UpsertSupplierEvaluationRequest(
    int SupplierId,
    string Period,
    decimal Score,
    decimal OnTimeDeliveryRate,
    decimal DefectRate,
    decimal WarrantyCompletionRate,
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
