using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NamViet.ERP.Api.Application.Services.Purchasing;
using NamViet.ERP.Api.Infrastructure.Authorization;
using NamViet.ERP.Modules.Warehouse.Application.DTOs;
using NamViet.ERP.Modules.Warehouse.Domain.Entities;
using NamViet.ERP.Modules.Warehouse.Domain.Enums;
using NamViet.ERP.Modules.Warehouse.Infrastructure.Persistence;

namespace NamViet.ERP.Api.Controllers;

[ApiController]
[Route("api/warehouse/projects")]
[Authorize]
public class ProjectPurchasingProgressController(WarehouseDbContext db) : ControllerBase
{
    [HttpGet("purchasing-progress")]
    [RequirePermission("wms.projects.view:all")]
    public async Task<ActionResult<List<ProjectPurchasingProgressDto>>> List(
        [FromQuery] string? projectCode,
        CancellationToken ct)
    {
        var today = TodayVn();
        var codeFilter = string.IsNullOrWhiteSpace(projectCode) ? null : projectCode.Trim();

        var orders = await db.PurchaseOrders.AsNoTracking()
            .Include(o => o.Items)
            .Where(o => o.ProjectCode != null && o.ProjectCode != "")
            .Where(o => codeFilter == null || o.ProjectCode == codeFilter)
            .OrderBy(o => o.ProjectCode)
            .ThenBy(o => o.OrderDate)
            .ThenBy(o => o.Code)
            .ToListAsync(ct);

        var plans = await db.PurchasePlans.AsNoTracking()
            .Include(p => p.Lines)
            .Where(p => p.ProjectCode != "")
            .Where(p => codeFilter == null || p.ProjectCode == codeFilter)
            .Where(p => p.Status != PurchasePlanStatuses.Rejected)
            .OrderBy(p => p.ProjectCode)
            .ThenBy(p => p.Kind)
            .ThenBy(p => p.CreatedAt)
            .ThenBy(p => p.Code)
            .ToListAsync(ct);

        var projectCodes = orders.Select(o => o.ProjectCode!)
            .Concat(plans.Select(p => p.ProjectCode))
            .Distinct()
            .ToList();

        if (codeFilter is not null && !projectCodes.Contains(codeFilter))
        {
            var lone = await db.Projects.AsNoTracking()
                .FirstOrDefaultAsync(p => p.ProjectCode == codeFilter, ct);
            if (lone is null) return NotFound();
            return Ok(new List<ProjectPurchasingProgressDto>
            {
                new(lone.ProjectCode, lone.ProjectName, null, null, null, 0, "planned", [], []),
            });
        }

        var projects = await db.Projects.AsNoTracking()
            .Where(p => projectCodes.Contains(p.ProjectCode))
            .ToDictionaryAsync(p => p.ProjectCode, ct);

        var poCodes = orders.Select(o => o.Code).ToList();
        var importsByPo = new Dictionary<string, List<WarehouseTransaction>>(StringComparer.OrdinalIgnoreCase);
        if (poCodes.Count > 0)
        {
            var imports = await db.WarehouseTransactions.AsNoTracking()
                .Include(t => t.Items)
                .Where(t => t.TransactionType == TransactionType.Import
                    && t.ReferenceCode != null
                    && poCodes.Contains(t.ReferenceCode))
                .ToListAsync(ct);

            foreach (var g in imports.GroupBy(t => t.ReferenceCode!, StringComparer.OrdinalIgnoreCase))
                importsByPo[g.Key] = g.ToList();
        }

        var orderDtoByCode = orders.ToDictionary(
            o => o.Code,
            o => ToOrderDto(o, importsByPo.GetValueOrDefault(o.Code) ?? [], today),
            StringComparer.OrdinalIgnoreCase);

        var result = projectCodes
            .Select(code =>
            {
                var project = projects.GetValueOrDefault(code);
                var projectOrders = orders
                    .Where(o => string.Equals(o.ProjectCode, code, StringComparison.OrdinalIgnoreCase))
                    .Select(o => orderDtoByCode[o.Code])
                    .ToList();
                var projectPlans = plans.Where(p => p.ProjectCode == code).ToList();
                return ToProjectDto(
                    code,
                    project?.ProjectName ?? code,
                    projectPlans,
                    projectOrders,
                    today);
            })
            .OrderBy(p => p.ProjectCode)
            .ToList();

        return Ok(result);
    }

    private static ProjectPurchasingProgressOrderDto ToOrderDto(
        PurchaseOrder order,
        IReadOnlyList<WarehouseTransaction> imports,
        DateOnly today)
    {
        var start = order.OrderDate;
        var lineEnds = order.Items
            .Select(i => i.ExpectedDeliveryDate ?? start)
            .DefaultIfEmpty(start);
        var end = lineEnds.Max();

        var approvedImports = imports
            .Where(PurchaseOrderReceiptService.IsActualReceipt)
            .ToList();
        var receivedByItem = SumApprovedQtyByPoItem(order, approvedImports);
        DateOnly? receiptDate = approvedImports.Count == 0
            ? null
            : ToVnDate(approvedImports.Max(t => t.ApprovedAt ?? t.CreatedAt));

        var lines = order.Items
            .OrderBy(i => i.Id)
            .Select(i =>
            {
                var lineEnd = i.ExpectedDeliveryDate ?? start;
                var lineReceived = Math.Max(i.Received, receivedByItem.GetValueOrDefault(i.Id));
                var linePct = Percent(lineReceived, i.Ordered);
                var lineDone = i.Ordered > 0 && lineReceived >= i.Ordered;
                return new ProjectPurchasingProgressLineDto(
                    i.Id,
                    string.IsNullOrWhiteSpace(i.Name) ? i.Sku : i.Name,
                    i.Sku,
                    start,
                    lineEnd,
                    lineDone || lineReceived > 0 ? receiptDate : null,
                    i.Ordered,
                    lineReceived,
                    linePct,
                    Status(start, lineEnd, linePct, lineDone, today));
            })
            .ToList();

        var receipts = imports
            .Where(t => t.ApprovalStatus != ApprovalStatus.Rejected)
            .OrderBy(t => t.CreatedAt)
            .ThenBy(t => t.TicketCode)
            .Select(t => new ProjectPurchasingProgressReceiptDto(
                t.TicketCode,
                PurchaseOrderReceiptService.IsActualReceipt(t)
                    ? "approved"
                    : t.ApprovalStatus == ApprovalStatus.Draft ? "draft" : "pending",
                ToVnDate(
                    PurchaseOrderReceiptService.IsActualReceipt(t)
                        ? t.ApprovedAt ?? t.CreatedAt
                        : t.CreatedAt),
                t.Items.Sum(i => i.Quantity),
                t.Items
                    .OrderBy(i => i.Id)
                    .Select(i => new ProjectPurchasingProgressReceiptLineDto(
                        string.IsNullOrWhiteSpace(i.MaterialNameSnapshot) ? i.MaterialCode : i.MaterialNameSnapshot,
                        string.IsNullOrWhiteSpace(i.MaterialCode) ? "" : i.MaterialCode,
                        i.Quantity))
                    .ToList()))
            .ToList();

        var ordered = lines.Sum(l => l.Ordered);
        var received = lines.Sum(l => l.Received);
        var pct = Percent(received, ordered);
        var complete = string.Equals(order.DeliveryStatus, PurchaseOrderDeliveryStatuses.Complete, StringComparison.OrdinalIgnoreCase)
            || (ordered > 0 && received >= ordered);
        DateOnly? actual = received > 0 ? receiptDate : null;

        return new ProjectPurchasingProgressOrderDto(
            order.Code,
            string.IsNullOrWhiteSpace(order.SupplierNameSnapshot) ? null : order.SupplierNameSnapshot,
            order.PlanCode,
            start,
            end,
            actual,
            ordered,
            received,
            pct,
            order.DeliveryStatus,
            Status(start, end, pct, complete, today),
            lines,
            receipts);
    }

    private static Dictionary<int, decimal> SumApprovedQtyByPoItem(
        PurchaseOrder order,
        IReadOnlyList<WarehouseTransaction> approvedImports)
    {
        var map = new Dictionary<int, decimal>();
        foreach (var import in approvedImports)
        {
            foreach (var item in import.Items)
            {
                var poItem = PurchaseOrderReceiptService.MatchPoItem(order.Items, item);
                if (poItem is null)
                    continue;
                map[poItem.Id] = map.GetValueOrDefault(poItem.Id) + item.Quantity;
            }
        }

        return map;
    }

    private static ProjectPurchasingProgressDto ToProjectDto(
        string projectCode,
        string projectName,
        IReadOnlyList<PurchasePlan> plans,
        IReadOnlyList<ProjectPurchasingProgressOrderDto> orders,
        DateOnly today)
    {
        var ordersByPlan = orders
            .GroupBy(o => o.PlanCode ?? "", StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.ToList(), StringComparer.OrdinalIgnoreCase);

        var planDtos = plans
            .OrderBy(p => string.Equals(p.Kind, PurchasePlanKinds.Adhoc, StringComparison.OrdinalIgnoreCase) ? 1 : 0)
            .ThenBy(p => p.CreatedAt)
            .ThenBy(p => p.Code)
            .Select(p => ToPlanDto(
                p,
                ordersByPlan.GetValueOrDefault(p.Code) ?? [],
                today))
            .ToList();

        if (planDtos.Count == 0 && orders.Count == 0)
            return new(projectCode, projectName, null, null, null, 0, "planned", planDtos, orders);

        var startCandidates = planDtos.Select(p => p.PlannedStart).Concat(orders.Select(o => o.PlannedStart));
        var endCandidates = planDtos.Select(p => p.PlannedEnd).Concat(orders.Select(o => o.PlannedEnd));
        var start = startCandidates.DefaultIfEmpty(today).Min();
        var end = endCandidates.DefaultIfEmpty(start).Max();
        var ordered = orders.Sum(o => o.Ordered);
        var received = orders.Sum(o => o.Received);
        var pct = Percent(received, ordered);
        var complete = orders.Count > 0 && (
            orders.All(o => o.Status == "completed")
            || (ordered > 0 && received >= ordered));
        var actual = complete
            ? orders.Select(o => o.ActualEnd).Where(d => d is not null).Max()
            : null;

        return new(
            projectCode,
            projectName,
            start,
            end,
            actual,
            pct,
            Status(start, end, pct, complete, today),
            planDtos,
            orders);
    }

    private static ProjectPurchasingProgressPlanDto ToPlanDto(
        PurchasePlan plan,
        IReadOnlyList<ProjectPurchasingProgressOrderDto> orders,
        DateOnly today)
    {
        var start = ToVnDate(plan.CreatedAt);
        var lineDates = plan.Lines
            .Select(l => l.ExpectedDeliveryDate ?? l.RequiredDate)
            .Where(d => d is not null)
            .Select(d => d!.Value);
        var hasDeliveryDate = lineDates.Any() || orders.Count > 0;
        var end = lineDates
            .Concat(orders.Select(o => o.PlannedEnd))
            .DefaultIfEmpty(start)
            .Max();
        if (end < start)
            end = start;

        var ordered = orders.Sum(o => o.Ordered);
        var received = orders.Sum(o => o.Received);
        var pct = Percent(received, ordered);
        var complete = orders.Count > 0 && orders.All(o => o.Status == "completed");
        DateOnly? actual = complete
            ? orders.Select(o => o.ActualEnd).Where(d => d is not null).Max()
            : null;

        var status = hasDeliveryDate
            ? Status(start, end, pct, complete, today)
            : complete
                ? "completed"
                : PurchasePlanStatuses.CanCreatePurchaseOrder(plan.Status)
                    ? "in_progress"
                    : "planned";

        return new(
            plan.Code,
            string.IsNullOrWhiteSpace(plan.Kind) ? PurchasePlanKinds.Plan : plan.Kind,
            plan.Status,
            start,
            end,
            actual,
            ordered,
            received,
            pct,
            status,
            orders);
    }

    private static int Percent(decimal received, decimal ordered)
    {
        if (ordered <= 0) return received > 0 ? 100 : 0;
        return (int)Math.Clamp(Math.Round(received / ordered * 100m), 0, 100);
    }

    private static string Status(DateOnly start, DateOnly end, int percent, bool complete, DateOnly today)
    {
        if (complete || percent >= 100) return "completed";
        if (today > end) return "late";
        if (today >= start || percent > 0) return "in_progress";
        return "planned";
    }

    private static DateOnly TodayVn()
    {
        var tz = TimeZoneInfo.FindSystemTimeZoneById(
            OperatingSystem.IsWindows() ? "SE Asia Standard Time" : "Asia/Ho_Chi_Minh");
        return DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, tz));
    }

    private static DateOnly ToVnDate(DateTime stamp)
    {
        var utc = stamp.Kind == DateTimeKind.Unspecified
            ? DateTime.SpecifyKind(stamp, DateTimeKind.Utc)
            : stamp.ToUniversalTime();
        var tz = TimeZoneInfo.FindSystemTimeZoneById(
            OperatingSystem.IsWindows() ? "SE Asia Standard Time" : "Asia/Ho_Chi_Minh");
        return DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(utc, tz));
    }
}
