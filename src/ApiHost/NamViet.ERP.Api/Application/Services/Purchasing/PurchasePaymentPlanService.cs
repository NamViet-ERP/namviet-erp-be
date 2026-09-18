using Microsoft.EntityFrameworkCore;
using NamViet.ERP.Api.Application.Dtos.Purchasing;
using NamViet.ERP.Modules.Warehouse.Domain.Entities;
using NamViet.ERP.Modules.Warehouse.Infrastructure.Persistence;
using NamViet.ERP.SharedKernel.Common;

namespace NamViet.ERP.Api.Application.Services.Purchasing;

public interface IPurchasePaymentPlanService
{
    Task<PurchasePaymentPlanPagedResponse> ListAsync(
        int page,
        int pageSize,
        string? search,
        string? projectCode,
        string? paymentStatus,
        CancellationToken ct = default);
}

public sealed class PurchasePaymentPlanService(WarehouseDbContext db) : IPurchasePaymentPlanService
{
    public const string StageUnpaid = "unpaid";
    public const string StageInProgress = "in_progress";
    public const string StagePaid = "paid";

    public async Task<PurchasePaymentPlanPagedResponse> ListAsync(
        int page,
        int pageSize,
        string? search,
        string? projectCode,
        string? paymentStatus,
        CancellationToken ct = default)
    {
        (page, pageSize) = Paging.Normalize(page, pageSize);

        // Kế hoạch thanh toán sinh từ PO đã duyệt và có đợt.
        var q = db.PurchaseOrders.AsNoTracking()
            .Include(o => o.Installments)
            .Include(o => o.Project)
            .Where(o => o.ApprovalStatus == PurchaseOrderApprovalStatuses.Approved
                        && o.Installments.Any());

        if (!string.IsNullOrWhiteSpace(projectCode))
        {
            var pc = projectCode.Trim();
            q = q.Where(o => o.ProjectCode == pc);
        }

        if (!string.IsNullOrWhiteSpace(paymentStatus))
        {
            var ps = paymentStatus.Trim().ToLowerInvariant();
            if (ps is "paid" or "completed")
            {
                q = q.Where(o => o.PaymentStatus == PurchaseOrderPaymentStatuses.Paid);
            }
            else if (ps is "uncompleted" or "has_unpaid" or "unpaid")
            {
                q = q.Where(o => o.PaymentStatus != PurchaseOrderPaymentStatuses.Paid);
            }
            else if (ps is "partial")
            {
                q = q.Where(o => o.PaymentStatus == PurchaseOrderPaymentStatuses.Partial);
            }
            else if (ps is "none" or "strictly_unpaid")
            {
                q = q.Where(o => o.PaymentStatus == PurchaseOrderPaymentStatuses.Unpaid);
            }
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.Trim();
            q = q.Where(o =>
                o.Code.Contains(s)
                || (o.ProjectCode != null && o.ProjectCode.Contains(s))
                || (o.Project != null && o.Project.ProjectName.Contains(s))
                || (o.Note != null && o.Note.Contains(s)));
        }

        var total = await q.CountAsync(ct);
        var orders = await q
            .OrderByDescending(o => o.ApprovedAt ?? o.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        var poCodes = orders.Select(o => o.Code).ToList();
        var payments = await db.PurchasePayments.AsNoTracking()
            .Where(p => poCodes.Contains(p.PoCode)
                        && p.Status != PurchasePaymentStatuses.Cancelled)
            .ToListAsync(ct);

        var items = orders.Select(order =>
        {
            var stages = order.Installments.OrderBy(i => i.StageNumber).Select(inst =>
            {
                var stagePayments = payments
                    .Where(p => p.PoCode == order.Code && p.StageNumber == inst.StageNumber)
                    .OrderByDescending(p => p.CreatedAt)
                    .ToList();
                var paid = stagePayments.FirstOrDefault(p =>
                    string.Equals(p.Status, PurchasePaymentStatuses.Paid, StringComparison.OrdinalIgnoreCase));
                var submitted = stagePayments.FirstOrDefault(p =>
                    string.Equals(p.Status, PurchasePaymentStatuses.Submitted, StringComparison.OrdinalIgnoreCase));

                string status;
                string? paymentCode;
                if (paid is not null)
                {
                    status = StagePaid;
                    paymentCode = paid.Code;
                }
                else if (submitted is not null)
                {
                    status = StageInProgress;
                    paymentCode = submitted.Code;
                }
                else
                {
                    status = StageUnpaid;
                    paymentCode = null;
                }

                return new PurchasePaymentPlanStageDto(
                    inst.StageNumber,
                    inst.Percent,
                    Math.Round(order.TotalAmount * inst.Percent / 100m, 2),
                    status,
                    paymentCode);
            }).ToList();

            var completed = stages.Count(s => s.PaymentStatus == StagePaid);
            var computedPaymentStatus = (stages.Count > 0 && completed >= stages.Count)
                ? PurchaseOrderPaymentStatuses.Paid
                : completed > 0
                    ? PurchaseOrderPaymentStatuses.Partial
                    : order.PaymentStatus;

            return new PurchasePaymentPlanRowDto(
                order.Code,
                order.ProjectCode,
                order.Project?.ProjectName,
                order.TotalAmount,
                order.ApprovalStatus,
                computedPaymentStatus,
                completed,
                stages.Count,
                stages);
        }).ToList();

        var totalPages = Math.Max(1, (int)Math.Ceiling(total / (double)pageSize));
        return new PurchasePaymentPlanPagedResponse(items, total, page, pageSize, totalPages);
    }
}
