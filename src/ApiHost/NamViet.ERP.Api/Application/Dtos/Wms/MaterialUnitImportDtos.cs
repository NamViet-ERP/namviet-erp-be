namespace NamViet.ERP.Api.Application.Dtos.Wms;

/// <summary>
/// DTO đại diện cho một dòng dữ liệu import Đơn vị tính.
/// </summary>
public record MaterialUnitImportRowDto(
    int RowNumber,
    string Name,
    int SortOrder,
    bool IsActive,
    string? Note,
    bool IsValid,
    string? ErrorMessage,
    bool IsExisting);

/// <summary>
/// DTO lỗi chi tiết theo dòng cho file import Đơn vị tính.
/// </summary>
public record MaterialUnitImportErrorDto(int RowNumber, string Field, string Message);

/// <summary>
/// DTO kết quả đọc và phân tích cú pháp file Excel import Đơn vị tính.
/// </summary>
public record MaterialUnitImportParseResultDto(
    int TotalRows,
    int ValidRowCount,
    int ExistingRowCount,
    int ErrorRowCount,
    IReadOnlyList<MaterialUnitImportRowDto> Rows,
    IReadOnlyList<MaterialUnitImportErrorDto> Errors);

/// <summary>
/// Yêu cầu import hàng loạt Đơn vị tính đã được xác nhận.
/// </summary>
public record MaterialUnitImportBatchRequest(
    IReadOnlyList<MaterialUnitImportRowDto> Rows,
    bool OverwriteExisting = false)
{
    public MaterialUnitImportBatchRequest(bool overwriteExisting, IReadOnlyList<MaterialUnitImportRowDto> rows)
        : this(rows, overwriteExisting) { }

    public MaterialUnitImportBatchRequest() : this([], false) { }
}

/// <summary>
/// Kết quả sau khi thực hiện import hàng loạt Đơn vị tính vào CSDL.
/// </summary>
public record MaterialUnitImportResultDto(
    int CreatedCount,
    int UpdatedCount,
    int SkippedCount,
    int TotalProcessed,
    IReadOnlyList<string> ImportedNames,
    IReadOnlyList<string> Errors);
