using NamViet.ERP.Api.Application.Dtos.Wms;

namespace NamViet.ERP.Api.Application.Services.Wms;

public interface IMaterialUnitImportExcelService
{
    /// <summary>
    /// Sinh file Excel (.xlsx) mẫu danh mục Đơn vị tính kèm dữ liệu mẫu chuẩn nghiệp vụ.
    /// </summary>
    Task<byte[]> GenerateTemplateAsync(CancellationToken ct = default);

    /// <summary>
    /// Đọc và phân tích cú pháp dữ liệu file Excel/CSV, kiểm tra hợp lệ và trùng lặp.
    /// </summary>
    Task<MaterialUnitImportParseResultDto> ParseImportFileAsync(Stream stream, string fileName, CancellationToken ct = default);

    /// <summary>
    /// Nhập hàng loạt các đơn vị tính hợp lệ vào cơ sở dữ liệu.
    /// </summary>
    Task<MaterialUnitImportResultDto> ImportBatchAsync(MaterialUnitImportBatchRequest request, CancellationToken ct = default);
}
