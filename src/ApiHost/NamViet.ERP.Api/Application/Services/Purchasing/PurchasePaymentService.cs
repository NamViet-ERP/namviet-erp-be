using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using NamViet.ERP.Api.Application.Dtos.Purchasing;
using NamViet.ERP.Api.Application.Mappers.Purchasing;
using NamViet.ERP.Api.Application.Services.Shared;
using NamViet.ERP.Modules.Warehouse.Domain.Entities;
using NamViet.ERP.Modules.Warehouse.Infrastructure.Persistence;
using NamViet.ERP.SharedKernel.Interfaces;

namespace NamViet.ERP.Api.Application.Services.Purchasing;

public interface IPurchasePaymentService
{
    Task<PurchasePaymentPagedResponse> ListAsync(
        int page,
        int pageSize,
        string? poCode,
        string? status,
        string? search,
        CancellationToken ct = default);

    Task<PurchasePaymentDto?> GetByCodeAsync(string code, CancellationToken ct = default);
    Task<PurchasePaymentNextStageDto?> GetNextStageAsync(string poCode, CancellationToken ct = default);
    Task<PurchasePaymentDto> CreateFromPoAsync(CreatePurchasePaymentRequest request, CancellationToken ct = default);
    Task<PurchasePaymentDto> SubmitAsync(string code, CancellationToken ct = default);
    Task<PurchasePaymentDto> MarkPaidAsync(string code, CancellationToken ct = default);
    Task<PurchasePaymentDto> CancelAsync(string code, CancellationToken ct = default);
}

public sealed class PurchasePaymentService(
    WarehouseDbContext db,
    IPurchasePaymentCodeGenerator codeGenerator,
    ICurrentUser currentUser,
    IActivityEventWriter activity) : IPurchasePaymentService
{
    public async Task<PurchasePaymentPagedResponse> ListAsync(
        int page,
        int pageSize,
        string? poCode,
        string? status,
        string? search,
        CancellationToken ct = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 200);

        var q = db.PurchasePayments.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(poCode))
            q = q.Where(p => p.PoCode == poCode.Trim());
        if (!string.IsNullOrWhiteSpace(status))
            q = q.Where(p => p.Status == status.Trim());
        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.Trim();
            q = q.Where(p =>
                p.Code.Contains(s) ||
                p.PoCode.Contains(s) ||
                (p.SupplierNameSnapshot != null && p.SupplierNameSnapshot.Contains(s)) ||
                (p.InvoiceNo != null && p.InvoiceNo.Contains(s)));
        }

        var total = await q.CountAsync(ct);
        var items = await q
            .OrderByDescending(p => p.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        var totalPages = Math.Max(1, (int)Math.Ceiling(total / (double)pageSize));
        return new PurchasePaymentPagedResponse(
            items.Select(PurchasingMapper.ToPaymentDto).ToList(),
            total, page, pageSize, totalPages);
    }

    public async Task<PurchasePaymentDto?> GetByCodeAsync(string code, CancellationToken ct = default)
    {
        var item = await db.PurchasePayments.AsNoTracking()
            .FirstOrDefaultAsync(p => p.Code == code, ct);
        return item is null ? null : PurchasingMapper.ToPaymentDto(item);
    }

    public async Task<PurchasePaymentNextStageDto?> GetNextStageAsync(string poCode, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(poCode))
            return null;

        var code = poCode.Trim();
        var order = await db.PurchaseOrders.AsNoTracking()
            .Include(o => o.Installments)
                .ThenInclude(i => i.Documents)
            .FirstOrDefaultAsync(o => o.Code == code, ct);
        if (order is null)
            return null;

        var next = await ResolveNextStageAsync(order, ct);
        if (next is null)
            return null;

        return new PurchasePaymentNextStageDto(
            order.Code,
            next.StageNumber,
            order.Installments.Count,
            next.Percent,
            StageAmount(order.TotalAmount, next.Percent),
            order.TotalAmount,
            GetMissingRequiredDocs(next));
    }

    public async Task<PurchasePaymentDto> CreateFromPoAsync(
        CreatePurchasePaymentRequest request, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.PoCode))
            throw new PurchasingConflictException("PoCode là bắt buộc.");
        if (request.DueDate == default)
            throw new PurchasingConflictException("Hạn thanh toán là bắt buộc.");

        var poCode = request.PoCode.Trim();
        var order = await db.PurchaseOrders
            .Include(o => o.Supplier)
            .Include(o => o.Installments)
                .ThenInclude(i => i.Documents)
            .FirstOrDefaultAsync(o => o.Code == poCode, ct)
            ?? throw new PurchasingConflictException($"Không tìm thấy PO '{poCode}'.");

        if (!string.Equals(order.ApprovalStatus, PurchaseOrderApprovalStatuses.Approved, StringComparison.OrdinalIgnoreCase))
            throw new PurchasingConflictException("Chỉ tạo DNTT cho PO đã duyệt.");

        if (order.Installments.Count == 0)
            throw new PurchasingConflictException("PO chưa có đợt thanh toán — không thể tạo DNTT.");

        var stage = await ResolveNextStageAsync(order, ct)
            ?? throw new PurchasingConflictException(
                $"PO '{poCode}' không còn đợt nào cần tạo DNTT (đã đủ hoặc đang chờ thanh toán đợt hiện tại).");

        var missingDocs = GetMissingRequiredDocs(stage);
        if (missingDocs.Count > 0)
            throw new PurchasingConflictException(
                $"Chưa đủ chứng từ bắt buộc cho đợt {stage.StageNumber}: {string.Join(", ", missingDocs)}.");

        var stageAmount = StageAmount(order.TotalAmount, stage.Percent);
        if (stageAmount <= 0)
            throw new PurchasingConflictException("Số tiền đợt thanh toán phải > 0.");
        // Luôn theo % đợt × tổng PO (không cho nhập lệch tổng PO).
        var amount = stageAmount;

        var code = await codeGenerator.NextAsync(ct);
        var now = DateTime.UtcNow;
        var payment = new PurchasePayment
        {
            Code = code,
            PoCode = poCode,
            StageNumber = stage.StageNumber,
            SupplierId = order.SupplierId,
            SupplierNameSnapshot = order.SupplierNameSnapshot ?? order.Supplier?.SupplierName,
            InvoiceNo = string.IsNullOrWhiteSpace(request.InvoiceNo) ? null : request.InvoiceNo.Trim(),
            Amount = Math.Round(amount, 2),
            DueDate = request.DueDate,
            Status = PurchasePaymentStatuses.Draft,
            Note = string.IsNullOrWhiteSpace(request.Note) ? null : request.Note.Trim(),
            CreatedById = currentUser.IsAuthenticated ? currentUser.Id : null,
            CreatedByName = currentUser.IsAuthenticated ? currentUser.Name : null,
            CreatedAt = now,
            UpdatedAt = now,
        };

        db.PurchasePayments.Add(payment);
        await db.SaveChangesAsync(ct);

        await activity.WriteAsync(new ActivityEventWriteRequest(
            ActivityModules.Purchasing,
            ActivityActions.PaymentCreated,
            $"Tạo đề nghị thanh toán [{code}] đợt {stage.StageNumber}/{order.Installments.Count} cho {poCode}",
            ActivityCategories.Payment,
            EntityType: "purchase_payment",
            EntityCode: code), ct);

        return PurchasingMapper.ToPaymentDto(payment);
    }

    public async Task<PurchasePaymentDto> SubmitAsync(string code, CancellationToken ct = default)
    {
        var payment = await FindTrackedAsync(code, ct);
        if (!string.Equals(payment.Status, PurchasePaymentStatuses.Draft, StringComparison.OrdinalIgnoreCase))
            throw new PurchasingConflictException("Chỉ DNTT ở trạng thái Nháp mới gửi được.");

        payment.Status = PurchasePaymentStatuses.Submitted;
        payment.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);

        await activity.WriteAsync(new ActivityEventWriteRequest(
            ActivityModules.Purchasing,
            ActivityActions.PaymentSubmitted,
            $"Gửi đề nghị thanh toán [{payment.Code}] đợt {payment.StageNumber}",
            ActivityCategories.Payment,
            EntityType: "purchase_payment",
            EntityCode: payment.Code), ct);

        return PurchasingMapper.ToPaymentDto(payment);
    }

    public async Task<PurchasePaymentDto> MarkPaidAsync(string code, CancellationToken ct = default)
    {
        var payment = await FindTrackedAsync(code, ct);
        if (!string.Equals(payment.Status, PurchasePaymentStatuses.Submitted, StringComparison.OrdinalIgnoreCase))
            throw new PurchasingConflictException("Chỉ DNTT đã gửi mới xác nhận thanh toán được.");

        payment.Status = PurchasePaymentStatuses.Paid;
        var now = DateTime.UtcNow;
        payment.UpdatedAt = now;
        payment.PaidAt = now;
        payment.PaidById = currentUser.IsAuthenticated && currentUser.Id != Guid.Empty
            ? currentUser.Id
            : null;
        payment.PaidByName = currentUser.IsAuthenticated
            ? (string.IsNullOrWhiteSpace(currentUser.Name) ? currentUser.Email : currentUser.Name)
            : null;

        var order = await db.PurchaseOrders
            .Include(o => o.Installments)
            .FirstOrDefaultAsync(o => o.Code == payment.PoCode, ct);
        if (order is not null)
        {
            await SyncPoPaymentStatusAsync(order, ct);
            order.UpdatedAt = DateTime.UtcNow;
        }

        await db.SaveChangesAsync(ct);

        await activity.WriteAsync(new ActivityEventWriteRequest(
            ActivityModules.Purchasing,
            ActivityActions.PaymentPaid,
            $"Xác nhận đã thanh toán [{payment.Code}] đợt {payment.StageNumber}",
            ActivityCategories.Payment,
            EntityType: "purchase_payment",
            EntityCode: payment.Code), ct);

        return PurchasingMapper.ToPaymentDto(payment);
    }

    public async Task<PurchasePaymentDto> CancelAsync(string code, CancellationToken ct = default)
    {
        var payment = await FindTrackedAsync(code, ct);
        if (string.Equals(payment.Status, PurchasePaymentStatuses.Paid, StringComparison.OrdinalIgnoreCase))
            throw new PurchasingConflictException("DNTT đã thanh toán không thể hủy.");
        if (string.Equals(payment.Status, PurchasePaymentStatuses.Cancelled, StringComparison.OrdinalIgnoreCase))
            throw new PurchasingConflictException("DNTT đã hủy trước đó.");

        payment.Status = PurchasePaymentStatuses.Cancelled;
        payment.UpdatedAt = DateTime.UtcNow;

        var order = await db.PurchaseOrders
            .Include(o => o.Installments)
            .FirstOrDefaultAsync(o => o.Code == payment.PoCode, ct);
        if (order is not null)
        {
            await SyncPoPaymentStatusAsync(order, ct);
            order.UpdatedAt = DateTime.UtcNow;
        }

        await db.SaveChangesAsync(ct);

        await activity.WriteAsync(new ActivityEventWriteRequest(
            ActivityModules.Purchasing,
            ActivityActions.PaymentCancelled,
            $"Hủy đề nghị thanh toán [{payment.Code}] đợt {payment.StageNumber}",
            ActivityCategories.Payment,
            EntityType: "purchase_payment",
            EntityCode: payment.Code), ct);

        return PurchasingMapper.ToPaymentDto(payment);
    }

    /// <summary>
    /// Đợt tiếp theo = đợt nhỏ nhất chưa có DNTT paid, và không có DNTT draft/submitted đang mở cho đợt đó.
    /// Tuần tự: mọi đợt trước phải đã paid.
    /// </summary>
    private async Task<PurchaseOrderInstallment?> ResolveNextStageAsync(PurchaseOrder order, CancellationToken ct)
    {
        var stages = order.Installments.OrderBy(i => i.StageNumber).ToList();
        if (stages.Count == 0)
            return null;

        var payments = await db.PurchasePayments.AsNoTracking()
            .Where(p => p.PoCode == order.Code
                        && !string.Equals(p.Status, PurchasePaymentStatuses.Cancelled))
            .ToListAsync(ct);

        foreach (var stage in stages)
        {
            var stagePayments = payments.Where(p => p.StageNumber == stage.StageNumber).ToList();
            if (stagePayments.Any(p => string.Equals(p.Status, PurchasePaymentStatuses.Paid, StringComparison.OrdinalIgnoreCase)))
                continue;

            if (stagePayments.Any(p =>
                    string.Equals(p.Status, PurchasePaymentStatuses.Draft, StringComparison.OrdinalIgnoreCase)
                    || string.Equals(p.Status, PurchasePaymentStatuses.Submitted, StringComparison.OrdinalIgnoreCase)))
                return null; // đang chờ xử lý DNTT đợt hiện tại

            // Mọi đợt trước phải paid
            var priorOk = stages
                .Where(s => s.StageNumber < stage.StageNumber)
                .All(s => payments.Any(p =>
                    p.StageNumber == s.StageNumber
                    && string.Equals(p.Status, PurchasePaymentStatuses.Paid, StringComparison.OrdinalIgnoreCase)));
            if (!priorOk)
                return null;

            return stage;
        }

        return null;
    }

    private async Task SyncPoPaymentStatusAsync(PurchaseOrder order, CancellationToken ct)
    {
        var stageCount = order.Installments.Count;
        if (stageCount == 0)
        {
            order.PaymentStatus = PurchaseOrderPaymentStatuses.Unpaid;
            order.PaymentWarning = false;
            return;
        }

        var paidStages = await db.PurchasePayments
            .AsNoTracking()
            .Where(p => p.PoCode == order.Code
                        && p.Status == PurchasePaymentStatuses.Paid)
            .Select(p => p.StageNumber)
            .Distinct()
            .CountAsync(ct);

        if (paidStages <= 0)
            order.PaymentStatus = PurchaseOrderPaymentStatuses.Unpaid;
        else if (paidStages >= stageCount)
            order.PaymentStatus = PurchaseOrderPaymentStatuses.Paid;
        else
            order.PaymentStatus = PurchaseOrderPaymentStatuses.Partial;

        order.PaymentWarning = false;
    }

    private static decimal StageAmount(decimal poTotal, decimal percent) =>
        Math.Round(poTotal * percent / 100m, 2);

    private static List<string> GetMissingRequiredDocs(PurchaseOrderInstallment stage)
    {
        List<string> required;
        try
        {
            required = JsonSerializer.Deserialize<List<string>>(stage.RequiredDocTypes) ?? [];
        }
        catch (JsonException)
        {
            return [];
        }

        var uploaded = stage.Documents
            .Select(d => d.DocType)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        return required.Where(t => !uploaded.Contains(t)).ToList();
    }

    private async Task<PurchasePayment> FindTrackedAsync(string code, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(code))
            throw new PurchasingConflictException("Mã DNTT không hợp lệ.");

        return await db.PurchasePayments
            .FirstOrDefaultAsync(p => p.Code == code.Trim(), ct)
            ?? throw new PurchasingConflictException($"Không tìm thấy DNTT '{code.Trim()}'.");
    }
}
