using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NamViet.ERP.Api.Application.Dtos.Wms;
using NamViet.ERP.Api.Application.Services.Wms;
using NamViet.ERP.Api.Infrastructure.Authorization;

namespace NamViet.ERP.Api.Controllers.Wms;

[ApiController]
[Route("api/warehouse/reports")]
[Authorize]
public class WmsReportsController(IWmsReportService reports) : ControllerBase
{
    [HttpGet("movement-summary")]
    [RequirePermission("wms.reports.view:all")]
    public async Task<ActionResult<WmsMovementSummaryDto>> MovementSummary(
        [FromQuery] DateTime? dateFrom, [FromQuery] DateTime? dateTo, CancellationToken ct)
        => Ok(await reports.GetMovementSummaryAsync(dateFrom, dateTo, ct));

    [HttpGet("stock")]
    [RequirePermission("wms.reports.view:all")]
    public async Task<ActionResult<IReadOnlyList<WmsStockReportRowDto>>> Stock(
        [FromQuery] string? projectCode, CancellationToken ct)
        => Ok(await reports.GetStockReportAsync(projectCode, ct));

    [HttpGet("pending")]
    [RequirePermission("wms.reports.view:all")]
    public async Task<ActionResult<WmsPendingReportDto>> Pending(CancellationToken ct)
        => Ok(await reports.GetPendingReportAsync(ct));

    [HttpGet("export/movement-summary")]
    [RequirePermission("wms.reports.export")]
    public async Task<IActionResult> ExportMovementSummary(
        [FromQuery] DateTime? dateFrom, [FromQuery] DateTime? dateTo, CancellationToken ct)
    {
        var summary = await reports.GetMovementSummaryAsync(dateFrom, dateTo, ct);
        var bytes = reports.BuildMovementSummaryWorkbook(summary);
        return File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            "bao-cao-nhap-xuat.xlsx");
    }

    [HttpGet("export/stock")]
    [RequirePermission("wms.reports.export")]
    public async Task<IActionResult> ExportStock([FromQuery] string? projectCode, CancellationToken ct)
    {
        var rows = await reports.GetStockReportAsync(projectCode, ct);
        var bytes = reports.BuildStockReportWorkbook(rows);
        return File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            "bao-cao-ton-kho.xlsx");
    }

    [HttpGet("export/pending")]
    [RequirePermission("wms.reports.export")]
    public async Task<IActionResult> ExportPending(CancellationToken ct)
    {
        var report = await reports.GetPendingReportAsync(ct);
        var bytes = reports.BuildPendingReportWorkbook(report);
        return File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            "bao-cao-cho-duyet.xlsx");
    }
}
