using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NamViet.ERP.Api.Application.Dtos.Purchasing;
using NamViet.ERP.Api.Application.Services.Wms;
using NamViet.ERP.Api.Infrastructure.Authorization;
using NamViet.ERP.Modules.Warehouse.Domain.Entities;
using NamViet.ERP.Modules.Warehouse.Infrastructure.Persistence;
using NamViet.ERP.SharedKernel.Interfaces;

namespace NamViet.ERP.Api.Controllers.Purchasing;

/// <summary>
/// Kế hoạch hàng về — dòng KHMH còn chờ nhận,
/// theo Ngày DK, gồm sắp về / đến hạn / quá hạn trong cửa sổ ngày.
/// </summary>
[ApiController]
[Route("api/purchasing/arrival-plans")]
[Authorize]
public class ArrivalPlansController(
    WarehouseDbContext db,
    ICurrentUser currentUser,
    ProjectWarehouseAccessService projectAccess) : ControllerBase
{
    /// <summary>Số ngày quá khứ tối đa vẫn hiện dòng quá hạn (cùng cửa sổ daysAhead về phía trước).</summary>
    private const int DefaultOverdueLookbackDays = 30;

    [HttpGet]
    [RequirePermission("purchasing.arrival_plans.view")]
    public async Task<ActionResult<ArrivalPlanListResponse>> List(
        [FromQuery] int daysAhead = 7,
        [FromQuery] int overdueLookbackDays = DefaultOverdueLookbackDays,
        [FromQuery] string? projectCode = null,
        [FromQuery] string? lineStatus = null,
        CancellationToken ct = default)
    {
        daysAhead = Math.Clamp(daysAhead, 0, 90);
        overdueLookbackDays = Math.Clamp(overdueLookbackDays, 0, 180);
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var windowFrom = today.AddDays(-overdueLookbackDays);
        var windowTo = today.AddDays(daysAhead);
        var canViewPrices = currentUser.HasPermission("wms.prices.view")
            || currentUser.IsInRole("Admin");

        var statusFilter = string.IsNullOrWhiteSpace(lineStatus) ? null : lineStatus.Trim().ToLowerInvariant();
        if (statusFilter is not null && !ArrivalPlanLineStatuses.All.Contains(statusFilter))
            return BadRequest("lineStatus phải là upcoming, due_today hoặc overdue.");

        var scoped = await projectAccess.GetScopedProjectCodesOrNullAsync(ct);

        var query = db.PurchasePlanLines.AsNoTracking()
            .Include(l => l.Plan).ThenInclude(p => p!.Project)
            .Where(l => l.ExpectedDeliveryDate != null)
            .Where(l => l.ActualReceiptDate == null)
            .Where(l => l.ExpectedDeliveryDate!.Value >= windowFrom
                        && l.ExpectedDeliveryDate.Value <= windowTo)
            .Where(l => l.Quantity - l.ReceivedQty > 0);

        if (!string.IsNullOrWhiteSpace(projectCode))
            query = query.Where(l => l.Plan!.ProjectCode == projectCode);

        if (scoped is not null)
        {
            if (scoped.Count == 0)
            {
                return Ok(new ArrivalPlanListResponse(daysAhead, overdueLookbackDays, windowFrom, windowTo, []));
            }

            query = query.Where(l => scoped.Contains(l.Plan!.ProjectCode));
        }

        var rows = await query
            .OrderBy(l => l.ExpectedDeliveryDate)
            .ThenBy(l => l.Plan!.ProjectCode)
            .ThenBy(l => l.LineNo)
            .Select(l => new
            {
                l.Id,
                l.PlanCode,
                ProjectCode = l.Plan!.ProjectCode,
                ProjectName = l.Plan.Project != null ? l.Plan.Project.ProjectName : l.Plan.ProjectCode,
                l.MaterialCode,
                l.ProductName,
                l.Unit,
                l.Quantity,
                l.OrderedQty,
                l.ReceivedQty,
                ExpectedDeliveryDate = l.ExpectedDeliveryDate!.Value,
                l.PurchaseSupplierName,
                UnitPrice = l.PurchaseUnitPrice ?? l.SelectedUnitPrice,
            })
            .ToListAsync(ct);

        var projects = rows
            .GroupBy(r => r.ProjectCode, StringComparer.OrdinalIgnoreCase)
            .Select(g =>
            {
                var lines = g.Select(r =>
                {
                    var status = ArrivalPlanLineStatuses.FromExpectedDate(r.ExpectedDeliveryDate, today);
                    return new ArrivalPlanLineDto(
                        r.Id,
                        r.PlanCode,
                        r.MaterialCode,
                        r.ProductName,
                        r.Unit,
                        r.Quantity,
                        r.OrderedQty,
                        r.ReceivedQty,
                        Math.Max(0m, r.Quantity - r.ReceivedQty),
                        r.ExpectedDeliveryDate,
                        r.PurchaseSupplierName,
                        canViewPrices ? r.UnitPrice : null,
                        status);
                })
                .Where(l => statusFilter is null || l.LineStatus == statusFilter)
                .ToList();

                if (lines.Count == 0)
                    return null;

                return new ArrivalPlanProjectDto(
                    g.Key,
                    g.First().ProjectName,
                    HasArrivalInWindow: true,
                    EarliestExpectedDate: lines.Min(l => l.ExpectedDeliveryDate),
                    LineCount: lines.Count,
                    TotalRemainingQty: lines.Sum(l => l.RemainingQty),
                    UpcomingCount: lines.Count(l => l.LineStatus == ArrivalPlanLineStatuses.Upcoming),
                    DueTodayCount: lines.Count(l => l.LineStatus == ArrivalPlanLineStatuses.DueToday),
                    OverdueCount: lines.Count(l => l.LineStatus == ArrivalPlanLineStatuses.Overdue),
                    Lines: lines);
            })
            .Where(p => p is not null)
            .Select(p => p!)
            .OrderBy(p => p.EarliestExpectedDate)
            .ThenBy(p => p.ProjectCode)
            .ToList();

        return Ok(new ArrivalPlanListResponse(daysAhead, overdueLookbackDays, windowFrom, windowTo, projects));
    }
}
