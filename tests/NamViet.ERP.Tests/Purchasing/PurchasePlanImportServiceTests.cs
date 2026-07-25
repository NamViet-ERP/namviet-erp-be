using ClosedXML.Excel;
using NamViet.ERP.Api.Application.Services.Purchasing;

namespace NamViet.ERP.Tests.Purchasing;

public class PurchasePlanImportServiceTests
{
    [Fact]
    public void Preview_MapsGroupedColumns_AndQuoteChildren()
    {
        using var ms = BuildSampleWorkbook();
        var service = new PurchasePlanImportService();

        var preview = service.Preview(ms, "DA-TEST");

        Assert.Equal("DA-TEST", preview.ProjectCode);
        Assert.Equal("KHMH", preview.SheetName);
        Assert.True(preview.ValidRowCount >= 1);
        Assert.Contains(preview.MappedColumns, c => c.Field == "MaterialCode");
        Assert.Contains(preview.MappedColumns, c => c.Field == "ProductName");
        Assert.Contains(preview.MappedColumns, c => c.Field == "Quantity");

        var line = Assert.Single(preview.Lines, l => l.IsValid);
        Assert.Equal("VT-ONG", line.MaterialCode);
        Assert.Equal("Ống thép", line.ProductName);
        Assert.Equal(10m, line.Quantity);
        Assert.Equal("Cái", line.Unit);
        Assert.Equal(100_000m, line.BudgetUnitPrice);
        Assert.True(line.Quotes.Count >= 1);
        Assert.Equal("NCC Alpha", line.Quotes[0].SupplierName);
        Assert.Equal(95_000m, line.Quotes[0].UnitPrice);
    }

    [Fact]
    public void Preview_BuildImportTemplate_IsValidRoundTrip()
    {
        var service = new PurchasePlanImportService();
        var bytes = service.BuildImportTemplate();
        using var ms = new MemoryStream(bytes);

        var preview = service.Preview(ms, "DA-DEMO");

        Assert.Equal(0, preview.ErrorRowCount);
        Assert.True(preview.ValidRowCount >= 50);
        Assert.Equal(50, preview.Lines.Count);
        Assert.Contains(preview.MappedColumns, c => c.Field == "MaterialCode");
        Assert.Contains(preview.MappedColumns, c => c.Field == "ProductName");
        Assert.Contains(preview.MappedColumns, c => c.Field == "Quantity");
        Assert.Contains(preview.MappedColumns, c => c.Field == "Unit");

        var line = Assert.Single(preview.Lines, l => l.ProductName.Contains("A515"));
        Assert.True(line.IsValid);
        Assert.Equal("VT-THEP-A515", line.MaterialCode);
        Assert.Equal(5m, line.Quantity);
        Assert.Equal("Tấm", line.Unit);
        Assert.Equal(25_500m, line.BudgetUnitPrice);
        Assert.Equal(2651m, line.KgPerUnit);
        Assert.Equal(25_455m, line.ProposalUnitPrice);
        Assert.Equal("ĐÔNG Á", line.ProposalSupplierName);
        Assert.Contains(preview.MappedColumns, c => c.Field == "BudgetUnitPrice");
        Assert.Contains(preview.MappedColumns, c => c.Field == "ProposalUnitPrice");
        Assert.Contains(preview.MappedColumns, c => c.Field == "PurchaseUnitPrice");
        Assert.Contains(preview.MappedColumns, c => c.Field == "SelectedUnitPrice");
        Assert.DoesNotContain(preview.MappedColumns, c => c.Field.StartsWith("Quote", StringComparison.Ordinal));
    }

    [Fact]
    public void Normalize_StripsDiacritics()
    {
        Assert.Equal("don gia", PurchasePlanImportService.Normalize("Đơn giá"));
        Assert.Equal("so luong", PurchasePlanImportService.Normalize("Số lượng"));
        Assert.Equal("ten san pham", PurchasePlanImportService.Normalize("Tên sản phẩm"));
    }

    [Fact]
    public void FindKhmhSheet_AcceptsNumberedName()
    {
        using var wb = new XLWorkbook();
        wb.AddWorksheet("Other");
        wb.AddWorksheet("KHMH (2)");
        var sheet = PurchasePlanImportService.FindKhmhSheet(wb);
        Assert.Equal("KHMH (2)", sheet!.Name);
    }

    private static MemoryStream BuildSampleWorkbook()
    {
        using var wb = new XLWorkbook();
        var ws = wb.AddWorksheet("KHMH");
        // title rows
        ws.Cell(1, 1).Value = "KẾ HOẠCH MUA HÀNG";
        // header
        ws.Cell(3, 1).Value = "Hệ";
        ws.Cell(3, 2).Value = "Nhóm";
        ws.Cell(3, 3).Value = "Mã vật tư";
        ws.Cell(3, 4).Value = "Tên hàng";
        ws.Cell(3, 5).Value = "Quy cách";
        ws.Cell(3, 6).Value = "ĐVT";
        ws.Cell(3, 7).Value = "Số lượng";
        ws.Cell(3, 8).Value = "Đơn giá dự toán";
        ws.Cell(3, 9).Value = "Thành tiền dự toán";
        ws.Cell(3, 10).Value = ""; // separator
        ws.Cell(3, 11).Value = "Báo giá 1";
        ws.Cell(3, 12).Value = "Đơn giá";
        ws.Cell(3, 13).Value = "NCC";
        ws.Cell(3, 14).Value = "Thành tiền";

        ws.Cell(4, 1).Value = "Hệ lạnh";
        ws.Cell(4, 2).Value = "Ống";
        ws.Cell(4, 3).Value = "VT-ONG";
        ws.Cell(4, 4).Value = "Ống thép";
        ws.Cell(4, 5).Value = "D49";
        ws.Cell(4, 6).Value = "Cái";
        ws.Cell(4, 7).Value = 10;
        ws.Cell(4, 8).Value = 100_000;
        ws.Cell(4, 9).Value = 1_000_000;
        ws.Cell(4, 12).Value = 95_000;
        ws.Cell(4, 13).Value = "NCC Alpha";
        ws.Cell(4, 14).Value = 950_000;

        var ms = new MemoryStream();
        wb.SaveAs(ms);
        ms.Position = 0;
        return ms;
    }
}
