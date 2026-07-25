using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NamViet.ERP.Api.Application.Dtos.Purchasing;
using NamViet.ERP.Api.Application.Services.Wms;
using NamViet.ERP.Api.Infrastructure.Authorization;
using NamViet.ERP.Modules.Warehouse.Infrastructure.Persistence;
using NamViet.ERP.SharedKernel.Interfaces;

namespace NamViet.ERP.Api.Controllers.Purchasing;

/// <summary>
/// Kế hoạch hàng về — dòng KHMH còn chờ nhận (chưa có ActualReceiptDate),
/// theo Ngày DK (ExpectedDeliveryDate), nhóm theo dự án.
/// </summary>
[ApiController]
[Route("api/purchasing/arrival-plans")]
[Authorize]
public class ArrivalPlansController(
    WarehouseDbContext db,
    ICurrentUser currentUser,
    ProjectWarehouseAccessService projectAccess) : ControllerBase
{
    [HttpGet]
    [RequirePermission("purchasing.arrival_plans.view")]
    public async Task<ActionResult<ArrivalPlanListResponse>> List(
        [FromQuery] int daysAhead = 7,
        [FromQuery] string? projectCode = null,
        CancellationToken ct = default)
    {
        daysAhead = Math.Clamp(daysAhead, 0, 90);
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var windowTo = today.AddDays(daysAhead);
        var canViewPrices = currentUser.HasPermission("wms.prices.view")
            || currentUser.IsInRole("Admin");

        var scoped = await projectAccess.GetScopedProjectCodesOrNullAsync(ct);

        var query = db.PurchasePlanLines.AsNoTracking()
            .Include(l => l.Plan).ThenInclude(p => p.Project)
            .Where(l => l.ExpectedDeliveryDate != null)
            .Where(l => l.ActualReceiptDate == null)
            .Where(l => l.ExpectedDeliveryDate!.Value >= today
                        && l.ExpectedDeliveryDate.Value <= windowTo)
            .Where(l => l.Quantity - l.ReceivedQty > 0);

        if (!string.IsNullOrWhiteSpace(projectCode))
            query = query.Where(l => l.Plan.ProjectCode == projectCode);

        if (scoped is not null)
        {
            if (scoped.Count == 0)
            {
                return Ok(new ArrivalPlanListResponse(daysAhead, today, windowTo, []));
            }

            query = query.Where(l => scoped.Contains(l.Plan.ProjectCode));
        }

        var rows = await query
            .OrderBy(l => l.ExpectedDeliveryDate)
            .ThenBy(l => l.Plan.ProjectCode)
            .ThenBy(l => l.LineNo)
            .Select(l => new
            {
                l.Id,
                l.PlanCode,
                ProjectCode = l.Plan.ProjectCode,
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
                var lines = g.Select(r => new ArrivalPlanLineDto(
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
                    canViewPrices ? r.UnitPrice : null)).ToList();

                return new ArrivalPlanProjectDto(
                    g.Key,
                    g.First().ProjectName,
                    HasArrivalInWindow: true,
                    EarliestExpectedDate: lines.Min(l => l.ExpectedDeliveryDate),
                    LineCount: lines.Count,
                    TotalRemainingQty: lines.Sum(l => l.RemainingQty),
                    Lines: lines);
            })
            .OrderBy(p => p.EarliestExpectedDate)
            .ThenBy(p => p.ProjectCode)
            .ToList();

        return Ok(new ArrivalPlanListResponse(daysAhead, today, windowTo, projects));
    }
}
