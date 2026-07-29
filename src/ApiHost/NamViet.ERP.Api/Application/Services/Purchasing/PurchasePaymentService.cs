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
            .FirstOrDefaultAsync(o => o.Code == poCode, ct)
            ?? throw new PurchasingConflictException($"Không tìm thấy PO '{poCode}'.");

        var existing = await db.PurchasePayments
            .AnyAsync(p => p.PoCode == poCode, ct);
        if (existing)
            throw new PurchasingConflictException($"PO '{poCode}' đã có đề nghị thanh toán.");

        var amount = request.Amount ?? order.TotalAmount;
        if (amount <= 0)
            throw new PurchasingConflictException("Số tiền DNTT phải > 0.");

        var code = await codeGenerator.NextAsync(ct);
        var now = DateTime.UtcNow;
        var payment = new PurchasePayment
        {
            Code = code,
            PoCode = poCode,
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
            $"Tạo đề nghị thanh toán [{code}] cho {poCode}",
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
            $"Gửi đề nghị thanh toán [{payment.Code}]",
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
        payment.UpdatedAt = DateTime.UtcNow;

        var order = await db.PurchaseOrders.FirstOrDefaultAsync(o => o.Code == payment.PoCode, ct);
        if (order is not null)
        {
            // Một PO hiện chỉ có một DNTT; khi DNTT paid → PO paid.
            order.PaymentStatus = PurchaseOrderPaymentStatuses.Paid;
            order.PaymentWarning = false;
            order.UpdatedAt = DateTime.UtcNow;
        }

        await db.SaveChangesAsync(ct);

        await activity.WriteAsync(new ActivityEventWriteRequest(
            ActivityModules.Purchasing,
            ActivityActions.PaymentPaid,
            $"Xác nhận đã thanh toán [{payment.Code}]",
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
        await db.SaveChangesAsync(ct);

        await activity.WriteAsync(new ActivityEventWriteRequest(
            ActivityModules.Purchasing,
            ActivityActions.PaymentCancelled,
            $"Hủy đề nghị thanh toán [{payment.Code}]",
            ActivityCategories.Payment,
            EntityType: "purchase_payment",
            EntityCode: payment.Code), ct);

        return PurchasingMapper.ToPaymentDto(payment);
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
