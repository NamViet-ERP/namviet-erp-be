using Microsoft.EntityFrameworkCore;
using NamViet.ERP.Api.Application.Dtos.Purchasing;
using NamViet.ERP.Modules.Warehouse.Domain.Entities;
using NamViet.ERP.Modules.Warehouse.Domain.SupplierEvaluation;

namespace NamViet.ERP.Api.Application.Services.Purchasing;

public sealed partial class PurchasePlanService
{
    private static PurchasePlanLine CloneLine(PurchasePlanLine src, string planCode)
    {
        var line = new PurchasePlanLine
        {
            PlanCode = planCode,
            LineNo = src.LineNo,
            LineType = src.LineType ?? PurchasePlanLineTypes.Material,
            SystemName = src.SystemName,
            Category = src.Category,
            MaterialCode = src.MaterialCode,
            ProductName = src.ProductName,
            Spec = src.Spec,
            Description = src.Description,
            Unit = src.Unit,
            Quantity = src.Quantity,
            KgPerUnit = src.KgPerUnit,
            LineNote = src.LineNote,
            PurchaseStatus = src.PurchaseStatus,
            StatusNote = src.StatusNote,
            BudgetUnitPrice = src.BudgetUnitPrice,
            BudgetAmount = src.BudgetAmount,
            ProposalKgPerUnit = src.ProposalKgPerUnit,
            ProposalUnitPrice = src.ProposalUnitPrice,
            ProposalAmount = src.ProposalAmount,
            ProposalSupplierName = src.ProposalSupplierName,
            ProposalNote = src.ProposalNote,
            ProposalStatus = src.ProposalStatus,
            RequiredDate = src.RequiredDate,
            OrderedDate = null,
            ExpectedDeliveryDate = src.ExpectedDeliveryDate,
            ActualReceiptDate = null,
            DeliveryNote = src.DeliveryNote,
            PurchaseKgPerUnit = src.PurchaseKgPerUnit,
            PurchaseUnitPrice = src.PurchaseUnitPrice,
            PurchaseAmount = src.PurchaseAmount,
            PurchaseSupplierName = src.PurchaseSupplierName,
            PurchaseNote = src.PurchaseNote,
            SelectedUnitPrice = src.SelectedUnitPrice,
            SelectedAmount = src.SelectedAmount,
            SelectedSupplierName = src.SelectedSupplierName,
            SelectionReason = src.SelectionReason,
            OrderedQty = 0,
            ReceivedQty = 0,
            DefectQty = 0,
        };
        foreach (var q in src.Quotes.OrderBy(x => x.SortOrder))
        {
            line.Quotes.Add(new PurchasePlanLineQuote
            {
                SortOrder = q.SortOrder,
                KgPerUnit = q.KgPerUnit,
                UnitPrice = q.UnitPrice,
                Amount = q.Amount,
                SupplierName = q.SupplierName,
                Note = q.Note,
                Status = q.Status,
            });
        }
        return line;
    }

    private static PurchasePlanVarianceLineDto ToVarianceLine(
        PurchasePlan plan, PurchasePlanLine line, DateOnly today)
    {
        var planned = line.Quantity;
        var ordered = line.OrderedQty;
        var received = line.ReceivedQty;
        var remaining = line.RemainingOrderableQty;

        var varianceOrderedPct = planned <= 0
            ? 0m
            : Math.Round(ordered / planned * 100m, 2);
        var varianceReceivedPct = planned <= 0
            ? 0m
            : Math.Round(received / planned * 100m, 2);

        var status = ResolveVarianceStatus(line, planned, ordered, received, today);

        return new PurchasePlanVarianceLineDto(
            plan.Code,
            plan.ProjectCode,
            line.MaterialCode,
            line.ProductName,
            planned,
            ordered,
            received,
            remaining,
            varianceOrderedPct,
            varianceReceivedPct,
            status);
    }

    private static string ResolveVarianceStatus(
        PurchasePlanLine line,
        decimal planned,
        decimal ordered,
        decimal received,
        DateOnly today)
    {
        if (received > planned)
            return PurchasePlanVarianceStatuses.OverReceived;

        var remainingToReceive = Math.Max(0m, planned - received);
        if (line.ExpectedDeliveryDate is DateOnly expected
            && expected < today
            && remainingToReceive > 0)
            return PurchasePlanVarianceStatuses.Delayed;

        if (ordered < planned)
            return PurchasePlanVarianceStatuses.UnderOrdered;

        return PurchasePlanVarianceStatuses.OnTrack;
    }

    private async Task<decimal> SumPaidTotalForPlanAsync(string planCode, CancellationToken ct)
    {
        var sum = await db.PurchaseOrders.AsNoTracking()
            .Where(o => o.PlanCode == planCode
                        && o.PaymentStatus == PurchaseOrderPaymentStatuses.Paid)
            .SumAsync(o => (decimal?)o.TotalAmount, ct);
        return Math.Round(sum ?? 0m, 2);
    }

    private async Task<Dictionary<string, decimal>> LoadPaidTotalsByPlanCodesAsync(
        IEnumerable<string> planCodes,
        CancellationToken ct)
    {
        var codes = planCodes.Distinct(StringComparer.Ordinal).ToList();
        if (codes.Count == 0)
            return new Dictionary<string, decimal>(StringComparer.Ordinal);

        var rows = await db.PurchaseOrders.AsNoTracking()
            .Where(o => codes.Contains(o.PlanCode)
                        && o.PaymentStatus == PurchaseOrderPaymentStatuses.Paid)
            .GroupBy(o => o.PlanCode)
            .Select(g => new { PlanCode = g.Key, Paid = g.Sum(o => o.TotalAmount) })
            .ToListAsync(ct);

        return rows.ToDictionary(
            r => r.PlanCode,
            r => Math.Round(r.Paid, 2),
            StringComparer.Ordinal);
    }

    private async Task<PurchasePlan?> LoadDetailAsync(string code, CancellationToken ct) =>
        await db.PurchasePlans
            .AsNoTracking()
            .Include(p => p.Project)
            .Include(p => p.Lines)
                .ThenInclude(l => l.Quotes)
            .FirstOrDefaultAsync(p => p.Code == code, ct);

    private async Task<Dictionary<int, decimal>> LoadPendingOrderedByLineIdAsync(
        IEnumerable<int> lineIds,
        CancellationToken ct)
    {
        var ids = lineIds.Distinct().ToList();
        if (ids.Count == 0) return [];

        var items = await db.PurchaseOrderItems.AsNoTracking()
            .Where(i => i.PlanLineId.HasValue && ids.Contains(i.PlanLineId.Value))
            .Where(i => i.PurchaseOrder.OrderPlacementStatus != PurchaseOrderPlacementStatuses.Placed)
            .GroupBy(i => i.PlanLineId!.Value)
            .Select(g => new { LineId = g.Key, Qty = g.Sum(x => x.Ordered) })
            .ToListAsync(ct);

        return items.ToDictionary(x => x.LineId, x => x.Qty);
    }

    private async Task<Dictionary<string, Material>> LoadMaterialsByCodesAsync(
        IEnumerable<string?> codes, CancellationToken ct)
    {
        var wanted = codes
            .Where(c => !string.IsNullOrWhiteSpace(c))
            .Select(c => c!.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (wanted.Count == 0)
            return new Dictionary<string, Material>(StringComparer.OrdinalIgnoreCase);

        var materials = await db.Materials.AsNoTracking()
            .Where(m => m.IsActive)
            .ToListAsync(ct);

        return materials
            .Where(m => !string.IsNullOrWhiteSpace(m.MaterialCode)
                        && wanted.Contains(m.MaterialCode.Trim(), StringComparer.OrdinalIgnoreCase))
            .GroupBy(m => m.MaterialCode.Trim(), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);
    }

    private async Task<IReadOnlyDictionary<string, ServiceExpenseItem>> LoadServiceExpenseItemsByCodesAsync(
        IEnumerable<string?> codes, CancellationToken ct)
    {
        var wanted = codes
            .Where(c => !string.IsNullOrWhiteSpace(c))
            .Select(c => c!.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (wanted.Count == 0)
            return new Dictionary<string, ServiceExpenseItem>(StringComparer.OrdinalIgnoreCase);

        var items = await db.ServiceExpenseItems.AsNoTracking()
            .Where(s => s.DeletedAt == null && s.IsActive && wanted.Contains(s.Code))
            .ToListAsync(ct);

        return items
            .GroupBy(s => s.Code.Trim(), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);
    }

    private static void ValidateMissingMaterials(
        IEnumerable<(string? Code, string? LineType, int Index)> lines,
        IReadOnlyDictionary<string, Material> materialsByCode,
        IReadOnlyDictionary<string, ServiceExpenseItem>? serviceItemsByCode = null)
    {
        var missingLines = new List<string>();
        foreach (var (rawCode, rawLineType, index) in lines)
        {
            var code = rawCode?.Trim();
            if (!string.IsNullOrEmpty(code) && serviceItemsByCode != null && serviceItemsByCode.ContainsKey(code))
            {
                // Hợp lệ theo Danh mục Dịch vụ & Chi phí ngoài
                continue;
            }

            var lineType = PurchasePlanLineTypes.IsValid(rawLineType)
                ? rawLineType!
                : PurchasePlanLineTypes.Material;
            if (lineType == PurchasePlanLineTypes.Material)
            {
                if (string.IsNullOrWhiteSpace(code) || !materialsByCode.ContainsKey(code))
                {
                    missingLines.Add($"Dòng {index} ('{code ?? "trống"}')");
                }
            }
        }
        if (missingLines.Count > 0)
        {
            var sample = string.Join(", ", missingLines.Take(10));
            var more = missingLines.Count > 10 ? $" và {missingLines.Count - 10} dòng khác" : "";
            throw new PurchasingConflictException($"Không thể tạo KHMH vì {missingLines.Count} dòng vật tư chưa có trong danh mục hệ thống: {sample}{more}. Vui lòng tạo danh mục vật tư trước.");
        }
    }

    private static Material RequireMaterial(
        string? materialCode,
        IReadOnlyDictionary<string, Material> byCode,
        int lineNo)
    {
        var code = materialCode?.Trim();
        if (string.IsNullOrWhiteSpace(code))
            throw new PurchasingConflictException($"Dòng {lineNo}: MaterialCode là bắt buộc.");

        if (!byCode.TryGetValue(code, out var material))
            throw new PurchasingConflictException(
                $"Dòng {lineNo}: không tìm thấy vật tư đang hoạt động với mã '{code}'.");

        return material;
    }

    private static PurchasePlanLine MapCreateLine(
        CreatePurchasePlanLineRequest src,
        string planCode,
        int lineNo,
        Material? material,
        string lineType,
        ServiceExpenseItem? serviceItem = null)
    {
        var materialCode = string.IsNullOrWhiteSpace(src.MaterialCode) ? null : src.MaterialCode.Trim();

        var productName = src.ProductName?.Trim();
        if (string.IsNullOrWhiteSpace(productName))
        {
            if (material != null) productName = material.MaterialName;
            else if (serviceItem != null) productName = serviceItem.Name;
        }

        var unit = src.Unit?.Trim();
        if (string.IsNullOrWhiteSpace(unit))
        {
            if (material != null) unit = material.Unit;
            else if (serviceItem != null) unit = serviceItem.Unit;
        }

        if (string.IsNullOrWhiteSpace(productName))
            throw new PurchasingConflictException($"Dòng {lineNo}: ProductName là bắt buộc.");

        if (string.IsNullOrWhiteSpace(unit))
            unit = lineType == PurchasePlanLineTypes.Expense ? "Gói" : "Hệ";

        var qty = src.Quantity;
        if (qty <= 0)
        {
            if (lineType != PurchasePlanLineTypes.Material)
                qty = 1m;
            else
                throw new PurchasingConflictException($"Dòng {lineNo}: Quantity phải > 0.");
        }

        if (src.BudgetUnitPrice is decimal budgetPrice)
        {
            if (budgetPrice < 0)
                throw new PurchasingConflictException("Đơn giá dự toán không được âm.");
            if (budgetPrice > PurchasingMoneyLimits.MaxUnitPrice)
                throw new PurchasingConflictException(
                    $"Đơn giá dự toán không được vượt {PurchasingMoneyLimits.MaxUnitPrice:N0} đ.");
        }

        var line = new PurchasePlanLine
        {
            PlanCode = planCode,
            LineNo = lineNo,
            LineType = lineType,
            SystemName = string.IsNullOrWhiteSpace(src.SystemName) ? null : src.SystemName.Trim(),
            Category = string.IsNullOrWhiteSpace(src.Category) ? null : src.Category.Trim(),
            MaterialCode = materialCode,
            ProductName = productName,
            Spec = string.IsNullOrWhiteSpace(src.Spec) ? null : src.Spec.Trim(),
            Description = string.IsNullOrWhiteSpace(src.Description) ? null : src.Description.Trim(),
            Unit = unit,
            Quantity = qty,
            KgPerUnit = src.KgPerUnit,
            LineNote = string.IsNullOrWhiteSpace(src.LineNote) ? null : src.LineNote.Trim(),
            PurchaseStatus = string.IsNullOrWhiteSpace(src.PurchaseStatus)
                ? PurchaseLineStatuses.NotPurchased
                : src.PurchaseStatus.Trim(),
            StatusNote = string.IsNullOrWhiteSpace(src.StatusNote) ? null : src.StatusNote.Trim(),
            BudgetUnitPrice = src.BudgetUnitPrice,
            BudgetAmount = src.BudgetAmount ?? (src.BudgetUnitPrice is decimal bup
                ? Math.Round(bup * qty, 2) : null),
            ProposalKgPerUnit = src.ProposalKgPerUnit,
            ProposalUnitPrice = src.ProposalUnitPrice,
            ProposalAmount = src.ProposalAmount ?? (src.ProposalUnitPrice is decimal pup
                ? Math.Round(pup * qty, 2) : null),
            ProposalSupplierName = string.IsNullOrWhiteSpace(src.ProposalSupplierName)
                ? null : src.ProposalSupplierName.Trim(),
            ProposalNote = string.IsNullOrWhiteSpace(src.ProposalNote) ? null : src.ProposalNote.Trim(),
            ProposalStatus = string.IsNullOrWhiteSpace(src.ProposalStatus) ? null : src.ProposalStatus.Trim(),
            RequiredDate = src.RequiredDate,
            OrderedDate = src.OrderedDate,
            ExpectedDeliveryDate = src.ExpectedDeliveryDate,
            ActualReceiptDate = src.ActualReceiptDate,
            DeliveryNote = string.IsNullOrWhiteSpace(src.DeliveryNote) ? null : src.DeliveryNote.Trim(),
            PurchaseKgPerUnit = src.PurchaseKgPerUnit,
            PurchaseUnitPrice = src.PurchaseUnitPrice,
            PurchaseAmount = src.PurchaseAmount ?? (src.PurchaseUnitPrice is decimal prup
                ? Math.Round(prup * qty, 2) : null),
            PurchaseSupplierName = string.IsNullOrWhiteSpace(src.PurchaseSupplierName)
                ? null : src.PurchaseSupplierName.Trim(),
            PurchaseNote = string.IsNullOrWhiteSpace(src.PurchaseNote) ? null : src.PurchaseNote.Trim(),
            SelectedUnitPrice = src.SelectedUnitPrice,
            SelectedAmount = src.SelectedAmount ?? (src.SelectedUnitPrice is decimal sup
                ? Math.Round(sup * qty, 2) : null),
            SelectedSupplierName = string.IsNullOrWhiteSpace(src.SelectedSupplierName)
                ? null : src.SelectedSupplierName.Trim(),
            SelectionReason = string.IsNullOrWhiteSpace(src.SelectionReason)
                ? null : src.SelectionReason.Trim(),
        };

        var sort = 0;
        foreach (var q in (src.Quotes ?? []).OrderBy(x => x.SortOrder))
        {
            if (string.IsNullOrWhiteSpace(q.SupplierName)
                && q.UnitPrice is null
                && q.Amount is null
                && q.KgPerUnit is null)
                continue;

            sort++;
            line.Quotes.Add(new PurchasePlanLineQuote
            {
                SortOrder = sort,
                KgPerUnit = q.KgPerUnit,
                UnitPrice = q.UnitPrice,
                Amount = q.Amount ?? (q.UnitPrice is decimal up
                    ? Math.Round(up * qty, 2) : null),
                SupplierName = string.IsNullOrWhiteSpace(q.SupplierName) ? null : q.SupplierName.Trim(),
                Note = string.IsNullOrWhiteSpace(q.Note) ? null : q.Note.Trim(),
                Status = string.IsNullOrWhiteSpace(q.Status) ? null : q.Status.Trim(),
            });
        }

        return line;
    }

    private static PurchasePlanLine MapImportLine(
        PurchasePlanImportLinePreviewDto src,
        string planCode,
        int lineNo,
        Material? material,
        string lineType,
        ServiceExpenseItem? serviceItem = null)
    {
        var materialCode = string.IsNullOrWhiteSpace(src.MaterialCode) ? null : src.MaterialCode.Trim();

        var productName = src.ProductName?.Trim();
        if (string.IsNullOrWhiteSpace(productName))
        {
            if (material != null) productName = material.MaterialName;
            else if (serviceItem != null) productName = serviceItem.Name;
        }

        var unit = src.Unit?.Trim();
        if (string.IsNullOrWhiteSpace(unit))
        {
            if (material != null) unit = material.Unit;
            else if (serviceItem != null) unit = serviceItem.Unit;
        }
            unit = material.Unit;

        if (string.IsNullOrWhiteSpace(unit))
            unit = lineType == PurchasePlanLineTypes.Expense ? "Gói" : "Hệ";

        var qty = src.Quantity;
        if (qty <= 0 && lineType != PurchasePlanLineTypes.Material)
            qty = 1m;

        var line = new PurchasePlanLine
        {
            PlanCode = planCode,
            LineNo = lineNo,
            LineType = lineType,
            SystemName = src.SystemName,
            Category = src.Category,
            MaterialCode = materialCode,
            ProductName = productName ?? string.Empty,
            Spec = src.Spec,
            Description = string.IsNullOrWhiteSpace(src.Description) ? null : src.Description.Trim(),
            Unit = unit ?? string.Empty,
            Quantity = qty,
            KgPerUnit = src.KgPerUnit,
            LineNote = src.LineNote,
            PurchaseStatus = string.IsNullOrWhiteSpace(src.PurchaseStatus)
                ? PurchaseLineStatuses.NotPurchased
                : src.PurchaseStatus,
            StatusNote = src.StatusNote,
            BudgetUnitPrice = src.BudgetUnitPrice,
            BudgetAmount = src.BudgetAmount ?? (src.BudgetUnitPrice is decimal bup
                ? Math.Round(bup * qty, 2) : null),
            ProposalKgPerUnit = src.ProposalKgPerUnit,
            ProposalUnitPrice = src.ProposalUnitPrice,
            ProposalAmount = src.ProposalAmount ?? (src.ProposalUnitPrice is decimal pup
                ? Math.Round(pup * qty, 2) : null),
            ProposalSupplierName = src.ProposalSupplierName,
            ProposalNote = src.ProposalNote,
            ProposalStatus = src.ProposalStatus,
            RequiredDate = src.RequiredDate,
            OrderedDate = src.OrderedDate,
            ExpectedDeliveryDate = src.ExpectedDeliveryDate,
            ActualReceiptDate = src.ActualReceiptDate,
            DeliveryNote = src.DeliveryNote,
            PurchaseKgPerUnit = src.PurchaseKgPerUnit,
            PurchaseUnitPrice = src.PurchaseUnitPrice,
            PurchaseAmount = src.PurchaseAmount ?? (src.PurchaseUnitPrice is decimal prup
                ? Math.Round(prup * qty, 2) : null),
            PurchaseSupplierName = src.PurchaseSupplierName,
            PurchaseNote = src.PurchaseNote,
            SelectedUnitPrice = src.SelectedUnitPrice,
            SelectedAmount = src.SelectedAmount ?? (src.SelectedUnitPrice is decimal sup
                ? Math.Round(sup * qty, 2) : null),
            SelectedSupplierName = src.SelectedSupplierName,
            SelectionReason = src.SelectionReason,
        };

        var sort = 0;
        foreach (var q in src.Quotes.OrderBy(x => x.SortOrder))
        {
            sort++;
            line.Quotes.Add(new PurchasePlanLineQuote
            {
                SortOrder = sort,
                KgPerUnit = q.KgPerUnit,
                UnitPrice = q.UnitPrice,
                Amount = q.Amount ?? (q.UnitPrice is decimal up ? Math.Round(up * qty, 2) : null),
                SupplierName = q.SupplierName,
                Note = q.Note,
                Status = q.Status,
            });
        }

        return line;
    }
}
