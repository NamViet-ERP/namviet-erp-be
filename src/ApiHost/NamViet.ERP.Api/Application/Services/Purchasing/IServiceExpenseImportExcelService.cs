using NamViet.ERP.Api.Application.Dtos.Purchasing;

namespace NamViet.ERP.Api.Application.Services.Purchasing;

public interface IServiceExpenseImportExcelService
{
    /// <summary>
    /// Sinh file Excel (.xlsx) mẫu danh mục Dịch vụ & Chi phí ngoài kèm 2 dòng dữ liệu chuẩn nghiệp vụ.
    /// </summary>
    Task<byte[]> GenerateTemplateAsync(CancellationToken ct = default);

    /// <summary>
    /// Đọc và phân tích cú pháp dữ liệu file Excel/CSV, thực hiện validation chi tiết từng dòng.
    /// </summary>
    Task<ServiceExpenseImportParseResultDto> ParseImportFileAsync(Stream stream, string fileName, CancellationToken ct = default);

    /// <summary>
    /// Nhập hàng loạt các dòng hợp lệ vào cơ sở dữ liệu (hỗ trợ ghi đè / cập nhật nếu được chọn).
    /// </summary>
    Task<ServiceExpenseImportResultDto> ImportBatchAsync(ServiceExpenseImportBatchRequest request, CancellationToken ct = default);
}
