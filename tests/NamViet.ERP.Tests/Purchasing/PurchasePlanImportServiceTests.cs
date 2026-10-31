using ClosedXML.Excel;
using NamViet.ERP.Api.Application.Services.Purchasing;
using NamViet.ERP.Modules.Warehouse.Domain.Entities;

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
        Assert.Equal(PurchasePlanLineTypes.Material, line.LineType);
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
        Assert.Equal(PurchasePlanLineTypes.Material, line.LineType);
        Assert.Contains(preview.MappedColumns, c => c.Field == "BudgetUnitPrice");
        Assert.Contains(preview.MappedColumns, c => c.Field == "ProposalUnitPrice");
        Assert.Contains(preview.MappedColumns, c => c.Field == "PurchaseUnitPrice");
        Assert.Contains(preview.MappedColumns, c => c.Field == "SelectedUnitPrice");
        Assert.DoesNotContain(preview.MappedColumns, c => c.Field.StartsWith("Quote", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("VT-01", "Gia công bồn", null, null, null, PurchasePlanLineTypes.Material)]
    [InlineData(null, "Chi phí tiếp khách dự án", null, null, null, PurchasePlanLineTypes.Expense)]
    [InlineData(null, "Lương nhân công vận hành", null, null, null, PurchasePlanLineTypes.Expense)]
    [InlineData(null, "Công tác phí chỉ huy trưởng", null, null, null, PurchasePlanLineTypes.Expense)]
    [InlineData(null, "Chi phí quản lý dự án", null, null, null, PurchasePlanLineTypes.Expense)]
    [InlineData(null, "Bể dập bụi", null, null, null, PurchasePlanLineTypes.Service)]
    [InlineData(null, "Vận chuyển vật tư", null, null, null, PurchasePlanLineTypes.Service)]
    [InlineData(null, "Kiểm định đo khí thải", null, null, null, PurchasePlanLineTypes.Service)]
    [InlineData(null, "Thuê ngoài thầu phụ", null, null, null, PurchasePlanLineTypes.Service)]
    [InlineData(null, "Hệ thống hút lọc", null, null, "Hệ", PurchasePlanLineTypes.Service)]
    [InlineData(null, "Dịch vụ hỗ trợ", null, null, "Gói", PurchasePlanLineTypes.Service)]
    public void ClassifyLineType_MatchesRulesCorrectly(
        string? materialCode, string? productName, string? category, string? systemName, string? unit, string expectedType)
    {
        var actual = PurchasePlanImportService.ClassifyLineType(
            materialCode, productName, category, systemName, unit, hasPriceOrMoney: true);
        Assert.Equal(expectedType, actual);
    }

    [Fact]
    public void Preview_RecognizesServiceAndExpense_WithoutMaterialCode_ZeroErrors()
    {
        using var ms = BuildMultiTypeSampleWorkbook();
        var service = new PurchasePlanImportService();

        var preview = service.Preview(ms, "DA-CHAIN-YARD");

        Assert.Equal("DA-CHAIN-YARD", preview.ProjectCode);
        Assert.Equal(0, preview.ErrorRowCount);
        Assert.Equal(6, preview.ValidRowCount);
        Assert.Equal(6, preview.Lines.Count);

        // 1. Material
        var matLine = preview.Lines[0];
        Assert.Equal("VT-THEP-01", matLine.MaterialCode);
        Assert.Equal("Thép tấm", matLine.ProductName);
        Assert.Equal(PurchasePlanLineTypes.Material, matLine.LineType);
        Assert.Equal(10m, matLine.Quantity);
        Assert.Equal(100_000m, matLine.BudgetUnitPrice);
        Assert.Equal(1_000_000m, matLine.BudgetAmount);

        // 2. Service: Bể dập bụi
        var dustLine = preview.Lines[1];
        Assert.Null(dustLine.MaterialCode);
        Assert.Equal("Bể dập bụi", dustLine.ProductName);
        Assert.Equal(PurchasePlanLineTypes.Service, dustLine.LineType);
        Assert.Equal(1m, dustLine.Quantity);
        Assert.Equal(380_000_000m, dustLine.BudgetAmount);
        Assert.True(dustLine.IsValid);

        // 3. Service: Vận chuyển
        var transportLine = preview.Lines[2];
        Assert.Null(transportLine.MaterialCode);
        Assert.Equal("Vận chuyển hàng hóa", transportLine.ProductName);
        Assert.Equal(PurchasePlanLineTypes.Service, transportLine.LineType);
        Assert.Equal(2m, transportLine.Quantity);
        Assert.Equal(100_000_000m, transportLine.BudgetUnitPrice);
        Assert.Equal(200_000_000m, transportLine.BudgetAmount);
        Assert.True(transportLine.IsValid);

        // 4. Service: Kiểm định đo khí thải
        var auditLine = preview.Lines[3];
        Assert.Null(auditLine.MaterialCode);
        Assert.Equal("Kiểm định đo khí thải", auditLine.ProductName);
        Assert.Equal(PurchasePlanLineTypes.Service, auditLine.LineType);
        Assert.Equal(1m, auditLine.Quantity);
        Assert.Equal(30_000_000m, auditLine.BudgetAmount);
        Assert.True(auditLine.IsValid);

        // 5. Expense: Nhân công quản lý
        var laborLine = preview.Lines[4];
        Assert.Null(laborLine.MaterialCode);
        Assert.Equal("Nhân công quản lý chỉ huy trưởng", laborLine.ProductName);
        Assert.Equal(PurchasePlanLineTypes.Expense, laborLine.LineType);
        Assert.Equal(1m, laborLine.Quantity);
        Assert.Equal(120_000_000m, laborLine.BudgetAmount);
        Assert.True(laborLine.IsValid);

        // 6. Expense: Tiếp khách + di chuyển
        var expenseLine = preview.Lines[5];
        Assert.Null(expenseLine.MaterialCode);
        Assert.Equal("Tiếp khách + di chuyển phát sinh", expenseLine.ProductName);
        Assert.Equal(PurchasePlanLineTypes.Expense, expenseLine.LineType);
        Assert.Equal(1m, expenseLine.Quantity);
        Assert.Equal(100_000_000m, expenseLine.BudgetAmount);
        Assert.True(expenseLine.IsValid);

        // Total Budget = 1M + 380M + 200M + 30M + 120M + 100M = 831,000,000 đ
        var totalBudget = preview.Lines.Sum(l => l.BudgetAmount ?? 0m);
        Assert.Equal(831_000_000m, totalBudget);
    }

    [Fact]
    public void RecalcTotals_SumsBudgetAcrossMaterialServiceAndExpense()
    {
        var plan = new PurchasePlan
        {
            Code = "KH-TEST-01",
            ProjectCode = "DA-TEST",
            Lines =
            [
                new PurchasePlanLine
                {
                    LineType = PurchasePlanLineTypes.Material,
                    MaterialCode = "VT-01",
                    ProductName = "Thép",
                    Quantity = 10,
                    BudgetAmount = 1_000_000m,
                    ProposalAmount = 950_000m,
                    PurchaseAmount = 900_000m
                },
                new PurchasePlanLine
                {
                    LineType = PurchasePlanLineTypes.Service,
                    MaterialCode = null,
                    ProductName = "Bể dập bụi",
                    Quantity = 1,
                    BudgetAmount = 380_000_000m,
                    ProposalAmount = 370_000_000m,
                    PurchaseAmount = 360_000_000m
                },
                new PurchasePlanLine
                {
                    LineType = PurchasePlanLineTypes.Expense,
                    MaterialCode = null,
                    ProductName = "Tiếp khách và di chuyển",
                    Quantity = 1,
                    BudgetAmount = 100_000_000m,
                    ProposalAmount = 100_000_000m,
                    PurchaseAmount = 100_000_000m
                }
            ]
        };

        // Act
        plan.BudgetTotal = Math.Round(plan.Lines.Sum(l => l.BudgetAmount ?? 0m), 2);
        plan.ProposalTotal = Math.Round(plan.Lines.Sum(l => l.ProposalAmount ?? 0m), 2);
        plan.PurchaseTotal = Math.Round(plan.Lines.Sum(l => l.SelectedAmount ?? l.PurchaseAmount ?? 0m), 2);

        // Assert
        Assert.Equal(481_000_000m, plan.BudgetTotal);
        Assert.Equal(470_950_000m, plan.ProposalTotal);
        Assert.Equal(460_900_000m, plan.PurchaseTotal);
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

    private static MemoryStream BuildMultiTypeSampleWorkbook()
    {
        using var wb = new XLWorkbook();
        var ws = wb.AddWorksheet("KHMH");
        // header row at 1
        ws.Cell(1, 1).Value = "STT";
        ws.Cell(1, 2).Value = "Hệ";
        ws.Cell(1, 3).Value = "Mã vật tư";
        ws.Cell(1, 4).Value = "Tên hàng";
        ws.Cell(1, 5).Value = "ĐVT";
        ws.Cell(1, 6).Value = "Số lượng";
        ws.Cell(1, 7).Value = "Đơn giá dự toán";
        ws.Cell(1, 8).Value = "Thành tiền dự toán";

        // Row 2: Section header (should be skipped)
        ws.Cell(2, 4).Value = "I. PHẦN VẬT TƯ CHÍNH";

        // Row 3: Material with code
        ws.Cell(3, 1).Value = 1;
        ws.Cell(3, 2).Value = "Cơ khí";
        ws.Cell(3, 3).Value = "VT-THEP-01";
        ws.Cell(3, 4).Value = "Thép tấm";
        ws.Cell(3, 5).Value = "Tấm";
        ws.Cell(3, 6).Value = 10;
        ws.Cell(3, 7).Value = 100_000;
        ws.Cell(3, 8).Value = 1_000_000;

        // Row 4: Section header
        ws.Cell(4, 4).Value = "II. PHẦN DỊCH VỤ THẦU PHỤ";

        // Row 5: Service: Bể dập bụi (amount only, qty null/empty)
        ws.Cell(5, 1).Value = 2;
        ws.Cell(5, 2).Value = "Xử lý bụi";
        ws.Cell(5, 3).Value = ""; // No code!
        ws.Cell(5, 4).Value = "Bể dập bụi";
        ws.Cell(5, 5).Value = "Hệ";
        ws.Cell(5, 6).Value = ""; // empty quantity, should fallback to 1
        ws.Cell(5, 7).Value = "";
        ws.Cell(5, 8).Value = 380_000_000;

        // Row 6: Service: Vận chuyển hàng hóa (qty 2, unit price 100M)
        ws.Cell(6, 1).Value = 3;
        ws.Cell(6, 2).Value = "Hậu cần";
        ws.Cell(6, 3).Value = ""; // No code!
        ws.Cell(6, 4).Value = "Vận chuyển hàng hóa";
        ws.Cell(6, 5).Value = "Chuyến";
        ws.Cell(6, 6).Value = 2;
        ws.Cell(6, 7).Value = 100_000_000;
        ws.Cell(6, 8).Value = 200_000_000;

        // Row 7: Service: Kiểm định đo khí thải
        ws.Cell(7, 1).Value = 4;
        ws.Cell(7, 2).Value = "Kiểm định";
        ws.Cell(7, 3).Value = ""; // No code!
        ws.Cell(7, 4).Value = "Kiểm định đo khí thải";
        ws.Cell(7, 5).Value = "Lần";
        ws.Cell(7, 6).Value = 1;
        ws.Cell(7, 7).Value = 30_000_000;
        ws.Cell(7, 8).Value = 30_000_000;

        // Row 8: Section header
        ws.Cell(8, 4).Value = "III. PHẦN CHI PHÍ QUẢN LÝ DỰ ÁN";

        // Row 9: Expense: Nhân công quản lý chỉ huy trưởng
        ws.Cell(9, 1).Value = 5;
        ws.Cell(9, 2).Value = "Quản lý";
        ws.Cell(9, 3).Value = ""; // No code!
        ws.Cell(9, 4).Value = "Nhân công quản lý chỉ huy trưởng";
        ws.Cell(9, 5).Value = "Gói";
        ws.Cell(9, 6).Value = 1;
        ws.Cell(9, 7).Value = 120_000_000;
        ws.Cell(9, 8).Value = 120_000_000;

        // Row 10: Expense: Tiếp khách + di chuyển phát sinh
        ws.Cell(10, 1).Value = 6;
        ws.Cell(10, 2).Value = "Chi phí khác";
        ws.Cell(10, 3).Value = ""; // No code!
        ws.Cell(10, 4).Value = "Tiếp khách + di chuyển phát sinh";
        ws.Cell(10, 5).Value = "Gói";
        ws.Cell(10, 6).Value = 1;
        ws.Cell(10, 7).Value = 100_000_000;
        ws.Cell(10, 8).Value = 100_000_000;

        var ms = new MemoryStream();
        wb.SaveAs(ms);
        ms.Position = 0;
        return ms;
    }

    [Fact]
    public void FindKhmhSheet_AcceptsSingleWorksheetRegardlessOfName()
    {
        using var wb1 = new XLWorkbook();
        wb1.AddWorksheet("Sheet1");
        var sheet1 = PurchasePlanImportService.FindKhmhSheet(wb1);
        Assert.NotNull(sheet1);
        Assert.Equal("Sheet1", sheet1.Name);

        using var wb2 = new XLWorkbook();
        wb2.AddWorksheet("MA VAT TU");
        var sheet2 = PurchasePlanImportService.FindKhmhSheet(wb2);
        Assert.NotNull(sheet2);
        Assert.Equal("MA VAT TU", sheet2.Name);
    }

    [Fact]
    public void FindKhmhSheet_AcceptsKeHoachMuaHangAlias()
    {
        using var wb1 = new XLWorkbook();
        wb1.AddWorksheet("TongHop");
        wb1.AddWorksheet("Kế hoạch mua hàng");
        var sheet1 = PurchasePlanImportService.FindKhmhSheet(wb1);
        Assert.NotNull(sheet1);
        Assert.Equal("Kế hoạch mua hàng", sheet1.Name);

        using var wb2 = new XLWorkbook();
        wb2.AddWorksheet("TongHop");
        wb2.AddWorksheet("Ke hoach mua hang");
        var sheet2 = PurchasePlanImportService.FindKhmhSheet(wb2);
        Assert.NotNull(sheet2);
        Assert.Equal("Ke hoach mua hang", sheet2.Name);
    }

    [Fact]
    public void Preview_ThrowsDetailedErrorListingSheets_WhenNoValidSheet()
    {
        using var wb = new XLWorkbook();
        wb.AddWorksheet("TongQuan");
        wb.AddWorksheet("BaoCao");
        using var ms = new MemoryStream();
        wb.SaveAs(ms);
        ms.Position = 0;

        var service = new PurchasePlanImportService();
        var ex = Assert.Throws<PurchasingConflictException>(() => service.Preview(ms, "DA-TEST"));

        Assert.Contains("Không tìm thấy sheet KHMH trong file Excel", ex.Message);
        Assert.Contains("'TongQuan'", ex.Message);
        Assert.Contains("'BaoCao'", ex.Message);
        Assert.Contains("Tải mẫu", ex.Message);
    }
}
