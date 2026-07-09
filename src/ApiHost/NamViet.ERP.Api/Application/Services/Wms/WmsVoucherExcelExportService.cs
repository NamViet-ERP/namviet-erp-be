using ClosedXML.Excel;
using NamViet.ERP.Api.Application.Dtos.Wms;

namespace NamViet.ERP.Api.Application.Services.Wms;

public interface IWmsVoucherExcelExportService
{
    byte[] BuildImportWorkbook(IReadOnlyList<ImportDto> rows);
    byte[] BuildExportWorkbook(IReadOnlyList<ExportDto> rows);
    byte[] BuildTransferWorkbook(IReadOnlyList<TransferDto> rows);
}

public sealed class WmsVoucherExcelExportService : IWmsVoucherExcelExportService
{
    public byte[] BuildImportWorkbook(IReadOnlyList<ImportDto> rows) =>
        BuildWorkbook(
            "Phieu nhap",
            ["Mã chứng từ", "Ngày", "Kho dự án", "NCC", "Mã tham chiếu", "Tổng GT", "Trạng thái", "Người tạo", "Ghi chú"],
            rows.Select(r => new object?[]
            {
                r.VoucherCode,
                r.Date,
                r.Project ?? "",
                r.Supplier,
                r.ReferenceCode,
                r.TotalValue,
                r.Status,
                r.CreatedBy,
                r.Note,
            }));

    public byte[] BuildExportWorkbook(IReadOnlyList<ExportDto> rows) =>
        BuildWorkbook(
            "Phieu xuat",
            ["Mã chứng từ", "Ngày", "Kho dự án", "Khách hàng", "Mã SO", "Trạng thái", "Người tạo", "Ghi chú"],
            rows.Select(r => new object?[]
            {
                r.VoucherCode,
                r.Date,
                r.Project ?? "",
                r.Customer,
                r.SoCode,
                r.Status,
                r.CreatedBy,
                r.Note,
            }));

    public byte[] BuildTransferWorkbook(IReadOnlyList<TransferDto> rows) =>
        BuildWorkbook(
            "Phieu chuyen",
            ["Mã chứng từ", "Ngày", "Kho nguồn", "Kho đích", "Dự án nguồn", "Dự án đích", "Trạng thái", "Người tạo", "Ghi chú"],
            rows.Select(r => new object?[]
            {
                r.VoucherCode,
                r.Date,
                r.FromWarehouseName,
                r.ToWarehouseName,
                r.Project ?? "",
                r.ToProject ?? "",
                r.Status,
                r.CreatedBy,
                r.Note,
            }));

    private static byte[] BuildWorkbook(string sheetName, string[] headers, IEnumerable<object?[]> dataRows)
    {
        using var workbook = new XLWorkbook();
        var sheet = workbook.Worksheets.Add(sheetName);

        for (var col = 0; col < headers.Length; col++)
        {
            sheet.Cell(1, col + 1).Value = headers[col];
            sheet.Cell(1, col + 1).Style.Font.Bold = true;
        }

        var rowIndex = 2;
        foreach (var row in dataRows)
        {
            for (var col = 0; col < row.Length; col++)
            {
                var value = row[col];
                if (value is decimal or double or float or int or long)
                    sheet.Cell(rowIndex, col + 1).Value = Convert.ToDecimal(value);
                else
                    sheet.Cell(rowIndex, col + 1).Value = value?.ToString() ?? "";
            }
            rowIndex++;
        }

        sheet.Columns().AdjustToContents();
        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }
}
