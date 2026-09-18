using Microsoft.EntityFrameworkCore;
using NamViet.ERP.Api.Application.Dtos.Purchasing;
using NamViet.ERP.Api.Application.Mappers.Purchasing;
using NamViet.ERP.Api.Application.Services.Shared;
using NamViet.ERP.Api.Services.Storage;
using NamViet.ERP.Modules.Warehouse.Domain.Entities;
using NamViet.ERP.Modules.Warehouse.Infrastructure.Persistence;
using NamViet.ERP.SharedKernel.Interfaces;
using System.Text.Json;
using NamViet.ERP.SharedKernel.Common;
using FluentValidation;

namespace NamViet.ERP.Api.Application.Services.Purchasing;

public sealed partial class PurchaseOrderService(
    WarehouseDbContext db,
    IPurchaseOrderCodeGenerator codeGenerator,
    IPurchasePlanService planService,
    ICurrentUser currentUser,
    IActivityEventWriter activity,
    IFileStorage fileStorage,
    IValidator<CreatePurchaseOrderRequest> createValidator,
    IValidator<UpdatePurchaseOrderRequest> updateValidator) : IPurchaseOrderService
{
    public async Task<PurchaseOrderPagedResponse> ListAsync(
        int page,
        int pageSize,
        int? supplierId,
        string? planCode,
        string? projectCode,
        string? orderPlacementStatus,
        string? deliveryStatus,
        string? paymentStatus,
        bool? paymentWarning,
        string? search,
        CancellationToken ct = default)
    {
        (page, pageSize) = Paging.Normalize(page, pageSize);

        var q = db.PurchaseOrders.AsNoTracking()
            .Include(o => o.Project)
            .Include(o => o.Supplier)
            .AsQueryable();

        if (supplierId is not null)
            q = q.Where(o => o.SupplierId == supplierId);
        if (!string.IsNullOrWhiteSpace(planCode))
            q = q.Where(o => o.PlanCode == planCode.Trim());
        if (!string.IsNullOrWhiteSpace(projectCode))
            q = q.Where(o => o.ProjectCode == projectCode.Trim());
        if (!string.IsNullOrWhiteSpace(orderPlacementStatus))
            q = q.Where(o => o.OrderPlacementStatus == orderPlacementStatus.Trim());
        if (!string.IsNullOrWhiteSpace(deliveryStatus))
            q = q.Where(o => o.DeliveryStatus == deliveryStatus.Trim());
        if (!string.IsNullOrWhiteSpace(paymentStatus))
            q = q.Where(o => o.PaymentStatus == paymentStatus.Trim());
        if (paymentWarning is not null)
            q = q.Where(o => o.PaymentWarning == paymentWarning.Value);
        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.Trim();
            q = q.Where(o =>
                o.Code.Contains(s) ||
                o.PlanCode.Contains(s) ||
                (o.SupplierNameSnapshot != null && o.SupplierNameSnapshot.Contains(s)) ||
                (o.Note != null && o.Note.Contains(s)));
        }

        var total = await q.CountAsync(ct);
        var items = await q
            .OrderByDescending(o => o.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        var totalPages = Math.Max(1, (int)Math.Ceiling(total / (double)pageSize));
        return new PurchaseOrderPagedResponse(
            items.Select(PurchasingMapper.ToOrderDto).ToList(),
            total, page, pageSize, totalPages);
    }

    public async Task<PurchaseOrderDetailDto?> GetByCodeAsync(string code, CancellationToken ct = default)
    {
        var order = await LoadDetailAsync(code, ct);
        if (order is null) return null;

        await ReconcilePoPaymentStatusIfMismatchedAsync(order, ct);

        return PurchasingMapper.ToOrderDetailDto(order);
    }

    private async Task ReconcilePoPaymentStatusIfMismatchedAsync(PurchaseOrder order, CancellationToken ct)
    {
        var payments = await db.PurchasePayments
            .AsNoTracking()
            .Where(p => p.PoCode == order.Code && p.Status == PurchasePaymentStatuses.Paid)
            .ToListAsync(ct);

        var stageCount = order.Installments.Count;
        var paidStages = payments.Select(p => p.StageNumber).Distinct().Count();
        var paidAmount = payments.Sum(p => p.Amount);

        string expected;
        if (paidStages <= 0 && paidAmount <= 0)
            expected = PurchaseOrderPaymentStatuses.Unpaid;
        else if ((stageCount > 0 && paidStages >= stageCount)
                 || (order.TotalAmount > 0 && paidAmount >= order.TotalAmount - 0.01m))
            expected = PurchaseOrderPaymentStatuses.Paid;
        else
            expected = PurchaseOrderPaymentStatuses.Partial;

        if (!string.Equals(order.PaymentStatus, expected, StringComparison.OrdinalIgnoreCase))
        {
            order.PaymentStatus = expected;
            order.UpdatedAt = DateTime.UtcNow;
            await db.PurchaseOrders
                .Where(o => o.Code == order.Code)
                .ExecuteUpdateAsync(s => s
                    .SetProperty(o => o.PaymentStatus, expected)
                    .SetProperty(o => o.UpdatedAt, DateTime.UtcNow), ct);
        }
    }

    private static PurchaseOrderAttachmentDto ToAttachmentDto(PurchaseOrderAttachment a) => new(
        a.Id, a.PoCode, a.DocType, a.FileName, a.StorageKey, a.UploadedAt, a.UploadedByName);

    private async Task<PurchaseOrderInstallment?> FindInstallmentAsync(
        string code, int stageNumber, bool asTracking, CancellationToken ct)
    {
        var query = db.PurchaseOrderInstallments
            .Include(i => i.Documents)
            .Include(i => i.PurchaseOrder)
            .AsQueryable();
        if (!asTracking)
            query = query.AsNoTracking();
        return await query.FirstOrDefaultAsync(
            i => i.PoCode == code.Trim() && i.StageNumber == stageNumber, ct);
    }

    private static PurchaseOrderInstallmentDocumentDto ToInstallmentDocumentDto(
        PurchaseOrderInstallmentDocument document) => new(
        document.Id,
        document.InstallmentId,
        document.DocType,
        document.FileName,
        document.ContentType,
        document.StoragePath,
        document.UploadedAt,
        document.UploadedByName);

    private static void EnsureInstallmentEditable(PurchaseOrderInstallment installment)
    {
        if (!string.Equals(
                installment.Status,
                PurchaseOrderInstallmentStatuses.Open,
                StringComparison.OrdinalIgnoreCase))
            throw new PurchasingConflictException(
                $"Đợt thanh toán {installment.StageNumber} đang ở trạng thái '{installment.Status}', không thể chỉnh sửa.");
    }

    private static string NormalizeInstallmentDocType(string docType)
    {
        var normalized = (docType ?? string.Empty).Trim().ToUpperInvariant();
        if (string.IsNullOrWhiteSpace(normalized) || normalized.Length > 50)
            throw new PurchasingConflictException("DocType phải có từ 1 đến 50 ký tự.");
        return normalized;
    }

    private static string NormalizeDocType(string docType)
    {
        var t = (docType ?? string.Empty).Trim().ToLowerInvariant();
        // IV (shipping docs) → invoice
        if (t is "iv")
            t = PurchaseOrderAttachmentDocTypes.Invoice;
        if (!PurchaseOrderAttachmentDocTypes.Allowed.Contains(t))
            throw new PurchasingConflictException(
                $"DocType không hợp lệ: '{docType}'. Cho phép: contract, invoice, co, cq, bbgh, bbnt, cnxk, tokhai, pl, other.");
        return t;
    }

    private static void EnsureAllowedExtension(string fileName)
    {
        var ext = Path.GetExtension(fileName).ToLowerInvariant();
        // Ảnh + tài liệu văn phòng Microsoft / PDF thường dùng cho C/O, C/Q, HĐ, BBNT.
        string[] allowed =
        [
            ".jpg", ".jpeg", ".png", ".webp", ".gif",
            ".pdf",
            ".doc", ".docx", ".rtf",
            ".xls", ".xlsx", ".xlsm", ".xlsb", ".csv",
            ".ppt", ".pptx",
            ".txt",
        ];
        if (!allowed.Contains(ext))
            throw new PurchasingConflictException(
                $"Định dạng file không hỗ trợ: {ext}. Cho phép ảnh, PDF và file Microsoft Office (Word/Excel/PowerPoint).");
    }

    private static List<PurchaseOrderInstallmentRequest> BuildInstallmentSpecs(
        int? installmentCount,
        List<PurchaseOrderInstallmentRequest>? installments)
    {
        if (installmentCount is not null && installments is not null)
            throw new PurchasingConflictException(
                "Chỉ truyền installmentCount hoặc installments, không truyền đồng thời.");

        if (installments is null && installmentCount is null)
            throw new PurchasingConflictException("PO phải có ít nhất một đợt thanh toán.");

        if (installmentCount is int count)
        {
            if (count is < 1 or > 4)
                throw new PurchasingConflictException("InstallmentCount phải từ 1 đến 4.");
            throw new PurchasingConflictException(
                "Phải gửi installments kèm requiredDocTypes (mỗi đợt ≥ 1 loại chứng từ). Không dùng installmentCount thuần.");
        }

        if (installments is null || installments.Count is < 1 or > 4)
            throw new PurchasingConflictException("Installments phải có từ 1 đến 4 đợt.");

        var ordered = installments.OrderBy(i => i.StageNumber).ToList();
        var expectedStages = Enumerable.Range(1, ordered.Count).ToArray();
        if (!ordered.Select(i => i.StageNumber).SequenceEqual(expectedStages))
            throw new PurchasingConflictException(
                "StageNumber phải liên tục, không trùng, bắt đầu từ 1.");
        if (ordered.Any(i => i.Percent <= 0 || i.Percent > 100))
            throw new PurchasingConflictException("Percent của mỗi đợt phải lớn hơn 0 và không vượt quá 100.");
        if (Math.Abs(ordered.Sum(i => i.Percent) - 100m) > 0.01m)
            throw new PurchasingConflictException("Tổng Percent các đợt phải bằng 100.");

        var normalized = ordered.Select(i => new PurchaseOrderInstallmentRequest(
                i.StageNumber,
                i.Percent,
                (i.RequiredDocTypes ?? [])
                    .Select(NormalizeInstallmentDocType)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList()))
            .ToList();

        if (normalized.Any(i => i.RequiredDocTypes.Count == 0))
            throw new PurchasingConflictException(
                "Mỗi đợt thanh toán phải có ít nhất một loại chứng từ bắt buộc.");

        return normalized;
    }

    private static List<string> DeserializeRequiredDocTypes(string json)
    {
        try
        {
            return JsonSerializer.Deserialize<List<string>>(json) ?? [];
        }
        catch (JsonException)
        {
            throw new PurchasingConflictException("Danh sách tài liệu bắt buộc của đợt thanh toán không hợp lệ.");
        }
    }

    private static (Dictionary<string, Material> ByName, Dictionary<string, Material> ByCode) BuildMaterialLookups(
        IEnumerable<Material> materials)
    {
        var list = materials.ToList();
        var byName = list
            .GroupBy(m => NormalizeName(m.MaterialName))
            .Where(g => !string.IsNullOrEmpty(g.Key))
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.Ordinal);
        var byCode = list
            .Where(m => !string.IsNullOrWhiteSpace(m.MaterialCode))
            .GroupBy(m => m.MaterialCode.Trim(), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);
        return (byName, byCode);
    }

    private static Material ResolveMaterialFromPlanLine(
        PurchasePlanLine planLine,
        IReadOnlyDictionary<string, Material> byName,
        IReadOnlyDictionary<string, Material> byCode)
    {
        if (!string.IsNullOrWhiteSpace(planLine.MaterialCode))
        {
            var code = planLine.MaterialCode.Trim();
            if (!byCode.TryGetValue(code, out var byMatCode))
                throw new PurchasingConflictException(
                    $"Dòng {planLine.LineNo}: không tìm thấy vật tư mã '{code}'. Kiểm tra/sửa MaterialCode trên KHMH.");
            return byMatCode;
        }

        // Legacy rows without MaterialCode: fuzzy name match as fallback only.
        var material = ResolveMaterial(planLine.ProductName, byName, byCode);
        if (material is null)
            throw new PurchasingConflictException(
                $"Dòng {planLine.LineNo}: thiếu mã vật tư trên KHMH. Vui lòng cập nhật MaterialCode trên dòng kế hoạch rồi thử lại.");
        return material;
    }

    private static Material? ResolveMaterial(
        string? productName,
        IReadOnlyDictionary<string, Material> byName,
        IReadOnlyDictionary<string, Material> byCode)
    {
        var key = NormalizeName(productName);
        if (!string.IsNullOrEmpty(key) && byName.TryGetValue(key, out var byN))
            return byN;

        var raw = (productName ?? string.Empty).Trim();
        if (!string.IsNullOrEmpty(raw) && byCode.TryGetValue(raw, out var byC))
            return byC;

        return null;
    }

    private static void EnsureSupplierCanPlaceOrder(Supplier supplier)
    {
        if (supplier.IsBlacklisted)
            throw new PurchasingConflictException(
                "NCC đang blacklist, không thể tạo đơn đặt hàng.");
        if (!SupplierApprovalStatuses.CanPlaceOrder(supplier))
            throw new PurchasingConflictException(
                "NCC chưa được sếp duyệt, chưa đặt hàng được.");
    }

    private static void ValidateSupplierMatch(PurchasePlanLine planLine, Supplier supplier)
    {
        var supplierName = NormalizeName(supplier.SupplierName);
        var candidates = new[]
        {
            planLine.SelectedSupplierName,
            planLine.PurchaseSupplierName,
            planLine.ProposalSupplierName,
        };

        var hasAny = candidates.Any(n => !string.IsNullOrWhiteSpace(n));
        if (!hasAny)
            throw new PurchasingConflictException(
                $"Dòng {planLine.LineNo} chưa có tên NCC (đề xuất/mua/chọn) để đối chiếu.");

        var match = candidates.Any(n =>
            !string.IsNullOrWhiteSpace(n) &&
            string.Equals(NormalizeName(n), supplierName, StringComparison.Ordinal));

        if (!match)
            throw new PurchasingConflictException(
                $"Dòng {planLine.LineNo}: NCC '{supplier.SupplierName}' không khớp tên NCC trên dòng KHMH.");
    }

    private static string NormalizeName(string? name) =>
        (name ?? string.Empty).Trim().ToLowerInvariant();

    private static string NormalizeWarrantyAnchor(string? anchor) =>
        string.Equals(anchor, "acceptance", StringComparison.OrdinalIgnoreCase)
            ? "acceptance"
            : "delivery";

    private static string ResolvePurchaseStatus(PurchasePlanLine line)
    {
        if (line.ReceivedQty <= 0 && line.OrderedQty > 0)
            return PurchaseLineStatuses.Ordered;
        if (line.ReceivedQty > 0 && line.ReceivedQty < line.Quantity)
            return PurchaseLineStatuses.PartialDelivery;
        if (line.ReceivedQty >= line.Quantity && line.Quantity > 0)
            return PurchaseLineStatuses.Delivered;
        return line.PurchaseStatus ?? PurchaseLineStatuses.NotPurchased;
    }

    private async Task<PurchasePlan> RequirePlanAsync(string? planCode, CancellationToken ct)
    {
        var code = (planCode ?? string.Empty).Trim();
        if (string.IsNullOrEmpty(code))
            throw new PurchasingConflictException("PO thiếu mã kế hoạch mua hàng.");

        // Soft-deleted KHMH still owns live POs (cleanup scripts). Place/update must not FirstAsync.
        var plan = await db.PurchasePlans
            .IgnoreQueryFilters()
            .Include(p => p.Lines)
            .FirstOrDefaultAsync(p => p.Code == code, ct);

        if (plan is null)
            throw new PurchasingConflictException($"Không tìm thấy kế hoạch mua hàng {code}.");

        return plan;
    }

    private async Task<PurchaseOrder?> LoadDetailAsync(string code, CancellationToken ct) =>
        await db.PurchaseOrders
            .AsNoTracking()
            .Include(o => o.Project)
            .Include(o => o.Supplier)
            .Include(o => o.Items)
            .Include(o => o.Installments)
                .ThenInclude(i => i.Documents)
            .FirstOrDefaultAsync(o => o.Code == code, ct);
}
