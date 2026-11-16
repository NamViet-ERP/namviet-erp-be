namespace NamViet.ERP.Api.Application.Dtos.Purchasing;

/// <summary>
/// DTO đại diện cho một dòng dữ liệu import Dịch vụ & Chi phí ngoài.
/// </summary>
public record ServiceExpenseImportRowDto(
    int RowNumber,
    string Code,
    string Name,
    string Type,
    string Unit,
    decimal? ReferencePrice,
    string? Description,
    bool IsValid,
    string? ErrorMessage,
    bool IsExisting);

/// <summary>
/// DTO kết quả đọc và phân tích file Excel import Dịch vụ & Chi phí ngoài.
/// </summary>
public record ServiceExpenseImportParseResultDto(
    int TotalRows,
    int ValidRows,
    int ErrorRows,
    int ExistingRows,
    IReadOnlyList<ServiceExpenseImportRowDto> Rows);

/// <summary>
/// Yêu cầu import hàng loạt Dịch vụ & Chi phí ngoài đã được người dùng xác nhận trên UI.
/// </summary>
public record ServiceExpenseImportBatchRequest(
    IReadOnlyList<ServiceExpenseImportRowDto> Rows,
    bool OverwriteExisting = false)
{
    public ServiceExpenseImportBatchRequest(bool overwriteExisting, IReadOnlyList<ServiceExpenseImportRowDto> rows)
        : this(rows, overwriteExisting) { }

    public ServiceExpenseImportBatchRequest() : this([], false) { }
}

/// <summary>
/// Kết quả sau khi thực hiện import hàng loạt Dịch vụ & Chi phí ngoài vào CSDL.
/// </summary>
public record ServiceExpenseImportResultDto(
    int TotalProcessed,
    int InsertedCount,
    int UpdatedCount,
    int SkippedCount,
    IReadOnlyList<string> Errors);
