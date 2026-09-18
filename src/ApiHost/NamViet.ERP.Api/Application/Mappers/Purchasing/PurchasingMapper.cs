using NamViet.ERP.Api.Application.Dtos.Purchasing;
using NamViet.ERP.Modules.Warehouse.Domain.Entities;
using System.Text.Json;

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
        plan.Kind,
        plan.Note,
        plan.CreatedByName,
        plan.RejectReason,
        plan.RemadeFromCode,
        plan.RemadeToCode,
        plan.L1ApprovedByName,
        plan.L1ApprovedAt,
        plan.L2ApprovedByName,
        plan.L2ApprovedAt);

    public static PurchasePlanDetailDto ToPlanDetailDto(
        PurchasePlan plan,
        decimal? paidTotal = null,
        IReadOnlyDictionary<int, decimal>? pendingOrderedByLineId = null,
        IReadOnlyDictionary<int, List<PurchasePlanLinePoDto>>? posByLineId = null,
        IReadOnlyList<PurchasePlanRelatedPoDto>? relatedOrders = null) => new(
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
        plan.Kind,
        plan.Note,
        plan.CreatedByName,
        plan.Lines
            .OrderBy(l => l.LineNo)
            .Select(l => ToPlanLineDto(
                l,
                pendingOrderedByLineId != null && pendingOrderedByLineId.TryGetValue(l.Id, out var pending)
                    ? Math.Max(0m, l.Quantity - l.OrderedQty - pending)
                    : l.RemainingOrderableQty,
                posByLineId != null && posByLineId.TryGetValue(l.Id, out var pos) ? pos : null))
            .ToList(),
        plan.SubmittedAt,
        plan.L1ApprovedByName,
        plan.L1ApprovedAt,
        plan.L2ApprovedByName,
        plan.L2ApprovedAt,
        plan.RejectedByName,
        plan.RejectedAt,
        plan.RejectReason,
        plan.RemadeFromCode,
        plan.RemadeToCode,
        relatedOrders?.ToList());

    public static PurchasePlanLineDto ToPlanLineDto(PurchasePlanLine line) =>
        ToPlanLineDto(line, line.RemainingOrderableQty);

    public static PurchasePlanLineDto ToPlanLineDto(
        PurchasePlanLine line,
        decimal remainingOrderableQty,
        IReadOnlyList<PurchasePlanLinePoDto>? pos = null) => new(
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
        remainingOrderableQty,
        line.Quotes
            .OrderBy(q => q.SortOrder)
            .Select(q => new PurchasePlanLineQuoteDto(
                q.Id, q.SortOrder, q.KgPerUnit, q.UnitPrice, q.Amount, q.SupplierName, q.Note, q.Status))
            .ToList(),
        pos?.ToList());

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
        ComputePaymentWarning(order),
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
        ComputePaymentWarning(order),
        order.Note,
        order.SupplierPaymentTermsDays,
        order.PaymentDueDate,
        order.ApprovedByName,
        order.ApprovedAt,
        order.PlacedByName,
        order.PlacedAt,
        order.CreatedByName,
        order.Items.Select(ToOrderLineDto).ToList(),
        order.Installments
            .OrderBy(i => i.StageNumber)
            .Select(ToInstallmentDto)
            .ToList());

    public static PurchaseOrderInstallmentDto ToInstallmentDto(PurchaseOrderInstallment installment) => new(
        installment.Id,
        installment.StageNumber,
        installment.Percent,
        installment.Status,
        DeserializeDocTypes(installment.RequiredDocTypes),
        installment.Documents
            .OrderByDescending(d => d.UploadedAt)
            .Select(d => new PurchaseOrderInstallmentDocumentDto(
                d.Id,
                d.InstallmentId,
                d.DocType,
                d.FileName,
                d.ContentType,
                d.StoragePath,
                d.UploadedAt,
                d.UploadedByName))
            .ToList());

    /// <summary>
    /// In-memory only: unpaid/partial/overdue past due date → warning (does not persist).
    /// </summary>
    public static bool ComputePaymentWarning(PurchaseOrder order)
    {
        if (order.PaymentWarning)
            return true;

        if (string.Equals(order.PaymentStatus, PurchaseOrderPaymentStatuses.Paid, StringComparison.OrdinalIgnoreCase))
            return false;

        if (order.PaymentDueDate is null)
            return false;

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        return order.PaymentDueDate.Value < today;
    }

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
        item.Note,
        item.WarrantyMonths,
        string.IsNullOrWhiteSpace(item.WarrantyAnchorType) ? "delivery" : item.WarrantyAnchorType);

    public static PurchasePaymentDto ToPaymentDto(PurchasePayment payment) => new(
        payment.Code,
        payment.PoCode,
        payment.StageNumber,
        payment.SupplierId,
        payment.SupplierNameSnapshot ?? payment.Supplier?.SupplierName,
        payment.InvoiceNo,
        payment.Amount,
        payment.DueDate,
        payment.Status,
        payment.Note,
        payment.CreatedAt,
        payment.CreatedByName,
        payment.PaidAt,
        payment.PaidByName);

    private static List<string> DeserializeDocTypes(string json)
    {
        try
        {
            return JsonSerializer.Deserialize<List<string>>(json) ?? [];
        }
        catch (JsonException)
        {
            return [];
        }
    }
}
