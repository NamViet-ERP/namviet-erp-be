using NamViet.ERP.Api.Application.Dtos.Purchasing;
using NamViet.ERP.Modules.Warehouse.Domain.Entities;

namespace NamViet.ERP.Api.Application.Mappers.Purchasing;

public static class PurchasingMapper
{
    public static PurchasePlanDto ToPlanDto(PurchasePlan plan, decimal? paidTotal = null) => new(
        plan.Code,
        plan.ProjectCode,
        plan.Project?.ProjectName,
        plan.CreatedAt,
        plan.BudgetTotal,
        plan.ProposalTotal,
        plan.PurchaseTotal,
        paidTotal ?? plan.PaidTotal,
        plan.Status,
        plan.Note,
        plan.CreatedByName);

    public static PurchasePlanDetailDto ToPlanDetailDto(PurchasePlan plan, decimal? paidTotal = null) => new(
        plan.Code,
        plan.ProjectCode,
        plan.Project?.ProjectName,
        plan.CreatedAt,
        plan.UpdatedAt,
        plan.BudgetTotal,
        plan.ProposalTotal,
        plan.PurchaseTotal,
        paidTotal ?? plan.PaidTotal,
        plan.Status,
        plan.Note,
        plan.CreatedByName,
        plan.Lines
            .OrderBy(l => l.LineNo)
            .Select(ToPlanLineDto)
            .ToList());

    public static PurchasePlanLineDto ToPlanLineDto(PurchasePlanLine line) => new(
        line.Id,
        line.LineNo,
        line.SystemName,
        line.Category,
        line.MaterialCode,
        line.ProductName,
        line.Spec,
        line.Unit,
        line.Quantity,
        line.KgPerUnit,
        line.LineNote,
        line.PurchaseStatus,
        line.StatusNote,
        line.BudgetUnitPrice,
        line.BudgetAmount,
        line.ProposalKgPerUnit,
        line.ProposalUnitPrice,
        line.ProposalAmount,
        line.ProposalSupplierName,
        line.ProposalNote,
        line.ProposalStatus,
        line.RequiredDate,
        line.OrderedDate,
        line.ExpectedDeliveryDate,
        line.ActualReceiptDate,
        line.DeliveryNote,
        line.PurchaseKgPerUnit,
        line.PurchaseUnitPrice,
        line.PurchaseAmount,
        line.PurchaseSupplierName,
        line.PurchaseNote,
        line.SelectedUnitPrice,
        line.SelectedAmount,
        line.SelectedSupplierName,
        line.SelectionReason,
        line.OrderedQty,
        line.ReceivedQty,
        line.DefectQty,
        line.RemainingOrderableQty,
        line.Quotes
            .OrderBy(q => q.SortOrder)
            .Select(q => new PurchasePlanLineQuoteDto(
                q.Id, q.SortOrder, q.KgPerUnit, q.UnitPrice, q.Amount, q.SupplierName, q.Note, q.Status))
            .ToList());

    public static PurchaseOrderDto ToOrderDto(PurchaseOrder order) => new(
        order.Code,
        order.PlanCode,
        order.ProjectCode,
        order.Project?.ProjectName,
        order.SupplierId,
        order.SupplierNameSnapshot ?? order.Supplier?.SupplierName,
        order.OrderDate,
        order.CreatedAt,
        order.TotalAmount,
        order.ApprovalStatus,
        order.OrderPlacementStatus,
        order.DeliveryStatus,
        order.PaymentStatus,
        order.PaymentWarning,
        order.Note,
        order.SupplierPaymentTermsDays,
        order.PaymentDueDate,
        order.ApprovedByName,
        order.ApprovedAt,
        order.PlacedByName,
        order.PlacedAt,
        order.CreatedByName);

    public static PurchaseOrderDetailDto ToOrderDetailDto(PurchaseOrder order) => new(
        order.Code,
        order.PlanCode,
        order.ProjectCode,
        order.Project?.ProjectName,
        order.SupplierId,
        order.SupplierNameSnapshot ?? order.Supplier?.SupplierName,
        order.OrderDate,
        order.CreatedAt,
        order.UpdatedAt,
        order.TotalAmount,
        order.ApprovalStatus,
        order.OrderPlacementStatus,
        order.DeliveryStatus,
        order.PaymentStatus,
        order.PaymentWarning,
        order.Note,
        order.SupplierPaymentTermsDays,
        order.PaymentDueDate,
        order.ApprovedByName,
        order.ApprovedAt,
        order.PlacedByName,
        order.PlacedAt,
        order.CreatedByName,
        order.Items.Select(ToOrderLineDto).ToList());

    public static PurchaseOrderLineDto ToOrderLineDto(PurchaseOrderItem item) => new(
        item.Id,
        item.PlanLineId,
        item.MaterialCode,
        item.Sku,
        item.Name,
        item.Spec,
        item.Unit,
        item.Ordered,
        item.Received,
        item.PoPrice,
        item.EstPrice,
        Math.Round(item.Ordered * item.PoPrice, 2),
        item.ExpectedDeliveryDate,
        item.Note);

    public static PurchasePaymentDto ToPaymentDto(PurchasePayment payment) => new(
        payment.Code,
        payment.PoCode,
        payment.SupplierId,
        payment.SupplierNameSnapshot ?? payment.Supplier?.SupplierName,
        payment.InvoiceNo,
        payment.Amount,
        payment.DueDate,
        payment.Status,
        payment.Note,
        payment.CreatedAt,
        payment.CreatedByName);
}
