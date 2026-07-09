using NamViet.ERP.Api.Application.Dtos.Wms;
using NamViet.ERP.Api.Application.Services.Wms;
using Xunit;

namespace NamViet.ERP.Tests.Wms;

public sealed class WmsVoucherExcelExportServiceTests
{
    private readonly WmsVoucherExcelExportService _service = new();

    [Fact]
    public void BuildImportWorkbook_ReturnsNonEmptyBytes()
    {
        var rows = new List<ImportDto>
        {
            new(
                "id-1", "NK-0001", "01/07/2026", "NCC A", "PO-1", 1_500_000m,
                "approved", "approved", "Admin", null, "Keeper", [],
                "Admin", "Ghi chú", "WH-1", false, "DA-001", "2026-07-01T00:00:00Z"),
        };

        var bytes = _service.BuildImportWorkbook(rows);

        Assert.NotEmpty(bytes);
        Assert.Equal(0x50, bytes[0]);
        Assert.Equal(0x4B, bytes[1]);
    }
}
