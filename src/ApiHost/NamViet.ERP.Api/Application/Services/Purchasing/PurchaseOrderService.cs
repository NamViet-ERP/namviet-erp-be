using Microsoft.EntityFrameworkCore;
using NamViet.ERP.Api.Application.Dtos.Purchasing;
using NamViet.ERP.Api.Application.Mappers.Purchasing;
using NamViet.ERP.Api.Application.Services.Shared;
using NamViet.ERP.Api.Services.Storage;
using NamViet.ERP.Modules.Warehouse.Domain.Entities;
using NamViet.ERP.Modules.Warehouse.Infrastructure.Persistence;
using NamViet.ERP.SharedKernel.Interfaces;
using System.Text.Json;

namespace NamViet.ERP.Api.Application.Services.Purchasing;

public interface IPurchaseOrderService
{
    Task<PurchaseOrderPagedResponse> ListAsync(
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
        CancellationToken ct = default);

    Task<PurchaseOrderDetailDto?> GetByCodeAsync(string code, CancellationToken ct = default);
    Task<PurchaseOrderDetailDto> CreateAsync(CreatePurchaseOrderRequest request, CancellationToken ct = default);
    Task<PurchaseOrderDetailDto?> UpdateAsync(string code, UpdatePurchaseOrderRequest request, CancellationToken ct = default);
    Task<PurchaseOrderDetailDto?> ApproveAsync(string code, CancellationToken ct = default);
    Task<PurchaseOrderDetailDto?> UnapproveAsync(string code, CancellationToken ct = default);
    Task<PurchaseOrderDetailDto?> PlaceAsync(string code, CancellationToken ct = default);

    Task<IReadOnlyList<PurchaseOrderAttachmentDto>?> ListAttachmentsAsync(string code, CancellationToken ct = default);
    Task<PurchaseOrderAttachmentDto?> UploadAttachmentAsync(
        string code,
        string docType,
        string fileName,
        string contentType,
        Stream content,
        CancellationToken ct = default);
    Task<PurchaseOrderAttachmentDto?> GetAttachmentAsync(string code, Guid attachmentId, CancellationToken ct = default);
    Task<bool> DeleteAttachmentAsync(string code, Guid attachmentId, CancellationToken ct = default);
    Task<IReadOnlyList<PurchaseOrderInstallmentDocumentDto>?> ListInstallmentDocumentsAsync(
        string code, int stageNumber, CancellationToken ct = default);
    Task<PurchaseOrderInstallmentDocumentDto?> UploadInstallmentDocumentAsync(
        string code, int stageNumber, string docType, string fileName, string contentType,
        Stream content, CancellationToken ct = default);
    Task<PurchaseOrderInstallmentDocumentDto?> GetInstallmentDocumentAsync(
        string code, int stageNumber, int documentId, CancellationToken ct = default);
    Task<bool> DeleteInstallmentDocumentAsync(
        string code, int stageNumber, int documentId, CancellationToken ct = default);
    Task<PurchaseOrderInstallmentDto?> CompleteInstallmentAsync(
        string code, int stageNumber, CancellationToken ct = default);
}

public sealed class PurchaseOrderService(
    WarehouseDbContext db,
    IPurchaseOrderCodeGenerator codeGenerator,
    IPurchasePlanService planService,
    ICurrentUser currentUser,
    IActivityEventWriter activity,
    IFileStorage fileStorage) : IPurchaseOrderService
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
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 200);

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
        return order is null ? null : PurchasingMapper.ToOrderDetailDto(order);
    }

    public async Task<PurchaseOrderDetailDto> CreateAsync(
        CreatePurchaseOrderRequest request, CancellationToken ct = default)
    {
        if (request.Lines is null || request.Lines.Count == 0)
            throw new PurchasingConflictException("PO phải có ít nhất một dòng.");
        var installmentSpecs = BuildInstallmentSpecs(request.InstallmentCount, request.Installments);

        var planCode = request.PlanCode?.Trim()
            ?? throw new PurchasingConflictException("PlanCode là bắt buộc.");

        var plan = await db.PurchasePlans
            .Include(p => p.Lines)
            .FirstOrDefaultAsync(p => p.Code == planCode, ct)
            ?? throw new KeyNotFoundException($"Không tìm thấy KHMH {planCode}.");

        if (!PurchasePlanStatuses.CanCreatePurchaseOrder(plan.Status))
            throw new PurchasingConflictException(
                "Chỉ tạo PO từ phiếu đã duyệt đủ 2 cấp (Trưởng cung ứng → Sếp).");

        if (string.Equals(plan.Status, PurchasePlanStatuses.Closed, StringComparison.OrdinalIgnoreCase))
            throw new PurchasingConflictException("KHMH đã đóng, không thể tạo PO.");

        var supplier = await db.Suppliers.FirstOrDefaultAsync(s => s.Id == request.SupplierId, ct)
            ?? throw new KeyNotFoundException($"Không tìm thấy nhà cung cấp #{request.SupplierId}.");

        var lineIds = request.Lines.Select(l => l.PlanLineId).Distinct().ToList();
        var planLines = plan.Lines.Where(l => lineIds.Contains(l.Id)).ToDictionary(l => l.Id);
        if (planLines.Count != lineIds.Count)
            throw new PurchasingConflictException("Một hoặc nhiều dòng không thuộc KHMH đã chọn.");

        foreach (var reqLine in request.Lines)
        {
            var planLine = planLines[reqLine.PlanLineId];
            ValidateSupplierMatch(planLine, supplier);
            if (reqLine.Qty <= 0)
                throw new PurchasingConflictException($"Số lượng dòng {planLine.LineNo} phải > 0.");
            if (reqLine.Qty > planLine.RemainingOrderableQty)
                throw new PurchasingConflictException(
                    $"Số lượng dòng {planLine.LineNo} vượt phần còn lại ({planLine.RemainingOrderableQty}).");
            if (reqLine.UnitPrice < 0)
                throw new PurchasingConflictException($"Đơn giá dòng {planLine.LineNo} không hợp lệ.");
        }

        await using var tx = await db.Database.BeginTransactionAsync(ct);

        var code = await codeGenerator.NextAsync(ct);
        var orderDate = request.OrderDate ?? DateOnly.FromDateTime(DateTime.UtcNow);
        var terms = supplier.MaxDebtDays > 0 ? supplier.MaxDebtDays : 30;
        var now = DateTime.UtcNow;

        var order = new PurchaseOrder
        {
            Code = code,
            PlanCode = plan.Code,
            ProjectCode = plan.ProjectCode,
            SupplierId = supplier.Id,
            OrderDate = orderDate,
            ApprovalStatus = PurchaseOrderApprovalStatuses.Draft,
            OrderPlacementStatus = PurchaseOrderPlacementStatuses.NotPlaced,
            DeliveryStatus = PurchaseOrderDeliveryStatuses.None,
            PaymentStatus = PurchaseOrderPaymentStatuses.Unpaid,
            PaymentWarning = false,
            Note = string.IsNullOrWhiteSpace(request.Note) ? null : request.Note.Trim(),
            SupplierPaymentTermsDays = terms,
            SupplierNameSnapshot = supplier.SupplierName,
            PaymentDueDate = orderDate.AddDays(terms),
            CreatedById = currentUser.IsAuthenticated ? currentUser.Id : null,
            CreatedByName = currentUser.IsAuthenticated ? currentUser.Name : null,
            CreatedAt = now,
            UpdatedAt = now,
        };

        var materialsByName = await db.Materials.AsNoTracking()
            .Where(m => m.IsActive)
            .ToListAsync(ct);
        var (byName, byCode) = BuildMaterialLookups(materialsByName);

        foreach (var reqLine in request.Lines)
        {
            var planLine = planLines[reqLine.PlanLineId];
            var material = ResolveMaterialFromPlanLine(planLine, byName, byCode);
            order.Items.Add(new PurchaseOrderItem
            {
                PoCode = code,
                PlanLineId = planLine.Id,
                MaterialCode = material.MaterialCode,
                Sku = material.MaterialCode,
                Name = planLine.ProductName,
                Spec = planLine.Spec,
                Unit = planLine.Unit,
                Ordered = reqLine.Qty,
                Received = 0,
                PoPrice = reqLine.UnitPrice,
                EstPrice = planLine.BudgetUnitPrice ?? planLine.ProposalUnitPrice ?? 0,
                ExpectedDeliveryDate = reqLine.ExpectedDeliveryDate,
                Note = string.IsNullOrWhiteSpace(reqLine.Note) ? null : reqLine.Note.Trim(),
            });
        }

        foreach (var spec in installmentSpecs)
        {
            order.Installments.Add(new PurchaseOrderInstallment
            {
                PoCode = code,
                StageNumber = spec.StageNumber,
                Percent = spec.Percent,
                Status = spec.StageNumber == 1
                    ? PurchaseOrderInstallmentStatuses.Open
                    : PurchaseOrderInstallmentStatuses.Locked,
                RequiredDocTypes = JsonSerializer.Serialize(spec.RequiredDocTypes),
            });
        }

        order.TotalAmount = Math.Round(order.Items.Sum(i => i.Ordered * i.PoPrice), 2);
        db.PurchaseOrders.Add(order);
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);

        await activity.WriteAsync(new ActivityEventWriteRequest(
            ActivityModules.Purchasing,
            ActivityActions.PoCreated,
            $"Tạo đơn đặt hàng [{code}]",
            ActivityCategories.Order,
            EntityType: "purchase_order",
            EntityCode: code), ct);

        return (await GetByCodeAsync(code, ct))!;
    }

    public async Task<PurchaseOrderDetailDto?> UpdateAsync(
        string code, UpdatePurchaseOrderRequest request, CancellationToken ct = default)
    {
        var order = await db.PurchaseOrders
            .Include(o => o.Items)
            .Include(o => o.Installments)
                .ThenInclude(i => i.Documents)
            .Include(o => o.Plan)
                .ThenInclude(p => p!.Lines)
            .FirstOrDefaultAsync(o => o.Code == code, ct);
        if (order is null) return null;

        if (!string.Equals(order.ApprovalStatus, PurchaseOrderApprovalStatuses.Draft, StringComparison.OrdinalIgnoreCase))
            throw new PurchasingConflictException("Chỉ sửa được PO ở trạng thái draft.");

        if (request.Lines is null || request.Lines.Count == 0)
            throw new PurchasingConflictException("PO phải có ít nhất một dòng.");
        var replaceInstallments = request.InstallmentCount is not null || request.Installments is not null;
        var installmentSpecs = replaceInstallments
            ? BuildInstallmentSpecs(request.InstallmentCount, request.Installments)
            : [];
        if (replaceInstallments && order.Installments.Any(i =>
                i.Documents.Count > 0 ||
                string.Equals(i.Status, PurchaseOrderInstallmentStatuses.Completed, StringComparison.OrdinalIgnoreCase)))
            throw new PurchasingConflictException(
                "Không thể thay đổi lịch thanh toán đã có tài liệu hoặc đợt đã hoàn tất.");

        var supplier = order.SupplierId is int sid
            ? await db.Suppliers.FirstOrDefaultAsync(s => s.Id == sid, ct)
            : null;
        if (supplier is null)
            throw new PurchasingConflictException("PO thiếu nhà cung cấp.");

        var plan = order.Plan
            ?? await db.PurchasePlans.Include(p => p.Lines).FirstAsync(p => p.Code == order.PlanCode, ct);
        var planLines = plan.Lines.ToDictionary(l => l.Id);

        // Remaining must ignore current PO quantities (draft not yet placed).
        foreach (var reqLine in request.Lines)
        {
            if (!planLines.TryGetValue(reqLine.PlanLineId, out var planLine))
                throw new PurchasingConflictException("Một hoặc nhiều dòng không thuộc KHMH đã chọn.");
            ValidateSupplierMatch(planLine, supplier);
            if (reqLine.Qty <= 0)
                throw new PurchasingConflictException($"Số lượng dòng {planLine.LineNo} phải > 0.");
            if (reqLine.Qty > planLine.RemainingOrderableQty)
                throw new PurchasingConflictException(
                    $"Số lượng dòng {planLine.LineNo} vượt phần còn lại ({planLine.RemainingOrderableQty}).");
        }

        await using var tx = await db.Database.BeginTransactionAsync(ct);

        if (request.Note is not null)
            order.Note = string.IsNullOrWhiteSpace(request.Note) ? null : request.Note.Trim();
        if (request.OrderDate is not null)
        {
            order.OrderDate = request.OrderDate.Value;
            order.PaymentDueDate = order.OrderDate.AddDays(order.SupplierPaymentTermsDays);
        }

        db.PurchaseOrderItems.RemoveRange(order.Items);
        order.Items.Clear();
        var materialsByName = await db.Materials.AsNoTracking()
            .Where(m => m.IsActive)
            .ToListAsync(ct);
        var (byName, byCode) = BuildMaterialLookups(materialsByName);

        foreach (var reqLine in request.Lines)
        {
            var planLine = planLines[reqLine.PlanLineId];
            var material = ResolveMaterialFromPlanLine(planLine, byName, byCode);
            order.Items.Add(new PurchaseOrderItem
            {
                PoCode = order.Code,
                PlanLineId = planLine.Id,
                MaterialCode = material.MaterialCode,
                Sku = material.MaterialCode,
                Name = planLine.ProductName,
                Spec = planLine.Spec,
                Unit = planLine.Unit,
                Ordered = reqLine.Qty,
                Received = 0,
                PoPrice = reqLine.UnitPrice,
                EstPrice = planLine.BudgetUnitPrice ?? planLine.ProposalUnitPrice ?? 0,
                ExpectedDeliveryDate = reqLine.ExpectedDeliveryDate,
                Note = string.IsNullOrWhiteSpace(reqLine.Note) ? null : reqLine.Note.Trim(),
            });
        }

        if (replaceInstallments)
        {
            db.PurchaseOrderInstallments.RemoveRange(order.Installments);
            order.Installments.Clear();
            foreach (var spec in installmentSpecs)
            {
                order.Installments.Add(new PurchaseOrderInstallment
                {
                    PoCode = order.Code,
                    StageNumber = spec.StageNumber,
                    Percent = spec.Percent,
                    Status = spec.StageNumber == 1
                        ? PurchaseOrderInstallmentStatuses.Open
                        : PurchaseOrderInstallmentStatuses.Locked,
                    RequiredDocTypes = JsonSerializer.Serialize(spec.RequiredDocTypes),
                });
            }
        }

        order.TotalAmount = Math.Round(order.Items.Sum(i => i.Ordered * i.PoPrice), 2);
        order.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);

        await activity.WriteAsync(new ActivityEventWriteRequest(
            ActivityModules.Purchasing,
            ActivityActions.PoUpdated,
            $"Cập nhật đơn đặt hàng [{code}]",
            ActivityCategories.Order,
            EntityType: "purchase_order",
            EntityCode: code), ct);

        return await GetByCodeAsync(code, ct);
    }

    public async Task<PurchaseOrderDetailDto?> ApproveAsync(string code, CancellationToken ct = default)
    {
        var order = await db.PurchaseOrders.FirstOrDefaultAsync(o => o.Code == code, ct);
        if (order is null) return null;

        if (string.Equals(order.ApprovalStatus, PurchaseOrderApprovalStatuses.Approved, StringComparison.OrdinalIgnoreCase))
            return await GetByCodeAsync(code, ct);

        order.ApprovalStatus = PurchaseOrderApprovalStatuses.Approved;
        order.ApprovedById = currentUser.IsAuthenticated ? currentUser.Id : null;
        order.ApprovedByName = currentUser.IsAuthenticated ? currentUser.Name : null;
        order.ApprovedAt = DateTime.UtcNow;
        order.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);

        await activity.WriteAsync(new ActivityEventWriteRequest(
            ActivityModules.Purchasing,
            ActivityActions.PoApproved,
            $"Duyệt đơn đặt hàng [{code}]",
            ActivityCategories.Order,
            EntityType: "purchase_order",
            EntityCode: code), ct);

        return await GetByCodeAsync(code, ct);
    }

    public async Task<PurchaseOrderDetailDto?> UnapproveAsync(string code, CancellationToken ct = default)
    {
        var order = await db.PurchaseOrders
            .Include(o => o.Items)
            .FirstOrDefaultAsync(o => o.Code == code, ct);
        if (order is null) return null;

        if (order.Items.Any(i => i.Received > 0))
            throw new PurchasingConflictException("Không thể bỏ duyệt PO đã có số lượng nhận.");

        if (string.Equals(order.OrderPlacementStatus, PurchaseOrderPlacementStatuses.Placed, StringComparison.OrdinalIgnoreCase))
            throw new PurchasingConflictException("Không thể bỏ duyệt PO đã đặt hàng.");

        order.ApprovalStatus = PurchaseOrderApprovalStatuses.Draft;
        order.ApprovedById = null;
        order.ApprovedByName = null;
        order.ApprovedAt = null;
        order.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);

        await activity.WriteAsync(new ActivityEventWriteRequest(
            ActivityModules.Purchasing,
            ActivityActions.PoUnapproved,
            $"Hủy duyệt đơn đặt hàng [{code}]",
            ActivityCategories.Order,
            EntityType: "purchase_order",
            EntityCode: code), ct);

        return await GetByCodeAsync(code, ct);
    }

    public async Task<PurchaseOrderDetailDto?> PlaceAsync(string code, CancellationToken ct = default)
    {
        var order = await db.PurchaseOrders
            .Include(o => o.Items)
            .FirstOrDefaultAsync(o => o.Code == code, ct);
        if (order is null) return null;

        if (!string.Equals(order.ApprovalStatus, PurchaseOrderApprovalStatuses.Approved, StringComparison.OrdinalIgnoreCase))
            throw new PurchasingConflictException("Chỉ đặt hàng được PO đã duyệt.");

        if (string.Equals(order.OrderPlacementStatus, PurchaseOrderPlacementStatuses.Placed, StringComparison.OrdinalIgnoreCase))
            return await GetByCodeAsync(code, ct);

        await using var tx = await db.Database.BeginTransactionAsync(ct);

        var planLineIds = order.Items
            .Where(i => i.PlanLineId.HasValue)
            .Select(i => i.PlanLineId!.Value)
            .Distinct()
            .ToList();

        var planLines = await db.PurchasePlanLines
            .Where(l => planLineIds.Contains(l.Id))
            .ToDictionaryAsync(l => l.Id, ct);

        foreach (var item in order.Items)
        {
            if (item.PlanLineId is not int lineId || !planLines.TryGetValue(lineId, out var planLine))
                throw new PurchasingConflictException("PO có dòng không gắn KHMH.");

            if (item.Ordered > planLine.RemainingOrderableQty)
                throw new PurchasingConflictException(
                    $"Dòng {planLine.LineNo}: số lượng vượt phần còn lại ({planLine.RemainingOrderableQty}).");

            planLine.OrderedQty += item.Ordered;
            planLine.OrderedDate ??= order.OrderDate;
            planLine.PurchaseStatus = ResolvePurchaseStatus(planLine);
            if (string.IsNullOrWhiteSpace(planLine.PurchaseSupplierName))
                planLine.PurchaseSupplierName = order.SupplierNameSnapshot;
            if (planLine.PurchaseUnitPrice is null)
                planLine.PurchaseUnitPrice = item.PoPrice;
            if (planLine.PurchaseAmount is null)
                planLine.PurchaseAmount = Math.Round(item.Ordered * item.PoPrice, 2);
        }

        // Backfill MaterialCode nếu lúc tạo PO chưa khớp VT (không xóa Name/Sku hiện có).
        var materials = await db.Materials.AsNoTracking().Where(m => m.IsActive).ToListAsync(ct);
        var (byName, byCode) = BuildMaterialLookups(materials);
        foreach (var item in order.Items)
        {
            if (!string.IsNullOrWhiteSpace(item.MaterialCode))
                continue;
            var material = ResolveMaterial(item.Name, byName, byCode);
            if (material is null)
                continue;
            item.MaterialCode = material.MaterialCode;
            if (string.IsNullOrWhiteSpace(item.Sku))
                item.Sku = material.MaterialCode;
        }

        var plan = await db.PurchasePlans
            .Include(p => p.Lines)
            .FirstAsync(p => p.Code == order.PlanCode, ct);
        planService.RecalcTotals(plan);

        order.OrderPlacementStatus = PurchaseOrderPlacementStatuses.Placed;
        order.PlacedById = currentUser.IsAuthenticated ? currentUser.Id : null;
        order.PlacedByName = currentUser.IsAuthenticated ? currentUser.Name : null;
        order.PlacedAt = DateTime.UtcNow;
        order.UpdatedAt = DateTime.UtcNow;

        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);

        await activity.WriteAsync(new ActivityEventWriteRequest(
            ActivityModules.Purchasing,
            ActivityActions.PoPlaced,
            $"Đặt hàng NCC đơn đặt hàng [{code}]",
            ActivityCategories.Order,
            EntityType: "purchase_order",
            EntityCode: code), ct);

        return await GetByCodeAsync(code, ct);
    }

    public async Task<IReadOnlyList<PurchaseOrderAttachmentDto>?> ListAttachmentsAsync(
        string code, CancellationToken ct = default)
    {
        var poCode = code.Trim();
        var exists = await db.PurchaseOrders.AsNoTracking().AnyAsync(o => o.Code == poCode, ct);
        if (!exists) return null;

        var rows = await db.PurchaseOrderAttachments.AsNoTracking()
            .Where(a => a.PoCode == poCode)
            .OrderByDescending(a => a.UploadedAt)
            .ToListAsync(ct);

        return rows.Select(ToAttachmentDto).ToList();
    }

    public async Task<PurchaseOrderAttachmentDto?> UploadAttachmentAsync(
        string code,
        string docType,
        string fileName,
        string contentType,
        Stream content,
        CancellationToken ct = default)
    {
        var poCode = code.Trim();
        var order = await db.PurchaseOrders.FirstOrDefaultAsync(o => o.Code == poCode, ct);
        if (order is null) return null;

        var normalizedDocType = NormalizeDocType(docType);
        EnsureAllowedExtension(fileName);

        var ext = Path.GetExtension(fileName);
        var relative = $"purchasing/orders/{poCode}/{normalizedDocType}/{Guid.NewGuid():N}{ext}";
        var stored = await fileStorage.SaveAsync(content, relative, contentType, ct);

        var attachment = new PurchaseOrderAttachment
        {
            PoCode = poCode,
            DocType = normalizedDocType,
            StorageKey = stored.StorageKey,
            FileName = Path.GetFileName(fileName),
            UploadedAt = DateTime.UtcNow,
            UploadedByName = currentUser.IsAuthenticated ? currentUser.Name : null,
        };
        db.PurchaseOrderAttachments.Add(attachment);
        order.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);

        return ToAttachmentDto(attachment);
    }

    public async Task<PurchaseOrderAttachmentDto?> GetAttachmentAsync(
        string code, Guid attachmentId, CancellationToken ct = default)
    {
        var poCode = code.Trim();
        var att = await db.PurchaseOrderAttachments.AsNoTracking()
            .FirstOrDefaultAsync(a => a.PoCode == poCode && a.Id == attachmentId, ct);
        return att is null ? null : ToAttachmentDto(att);
    }

    public async Task<bool> DeleteAttachmentAsync(string code, Guid attachmentId, CancellationToken ct = default)
    {
        var poCode = code.Trim();
        var order = await db.PurchaseOrders.FirstOrDefaultAsync(o => o.Code == poCode, ct);
        if (order is null) return false;

        var att = await db.PurchaseOrderAttachments
            .FirstOrDefaultAsync(a => a.PoCode == poCode && a.Id == attachmentId, ct);
        if (att is null) return false;

        db.PurchaseOrderAttachments.Remove(att);
        order.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<IReadOnlyList<PurchaseOrderInstallmentDocumentDto>?> ListInstallmentDocumentsAsync(
        string code, int stageNumber, CancellationToken ct = default)
    {
        var installment = await FindInstallmentAsync(code, stageNumber, asTracking: false, ct);
        if (installment is null) return null;
        return installment.Documents
            .OrderByDescending(d => d.UploadedAt)
            .Select(ToInstallmentDocumentDto)
            .ToList();
    }

    public async Task<PurchaseOrderInstallmentDocumentDto?> UploadInstallmentDocumentAsync(
        string code,
        int stageNumber,
        string docType,
        string fileName,
        string contentType,
        Stream content,
        CancellationToken ct = default)
    {
        var installment = await FindInstallmentAsync(code, stageNumber, asTracking: true, ct);
        if (installment is null) return null;
        EnsureInstallmentEditable(installment);

        var normalizedDocType = NormalizeInstallmentDocType(docType);
        var required = DeserializeRequiredDocTypes(installment.RequiredDocTypes);
        if (required.Count > 0
            && !required.Contains(normalizedDocType, StringComparer.OrdinalIgnoreCase))
            throw new PurchasingConflictException(
                $"Loại chứng từ '{normalizedDocType}' không thuộc danh sách bắt buộc của đợt {stageNumber}.");
        EnsureAllowedExtension(fileName);
        var ext = Path.GetExtension(fileName);
        var relative =
            $"purchasing/orders/{installment.PoCode}/installments/{stageNumber}/{normalizedDocType}/{Guid.NewGuid():N}{ext}";
        var stored = await fileStorage.SaveAsync(content, relative, contentType, ct);
        var document = new PurchaseOrderInstallmentDocument
        {
            InstallmentId = installment.Id,
            DocType = normalizedDocType,
            FileName = Path.GetFileName(fileName),
            ContentType = string.IsNullOrWhiteSpace(contentType)
                ? "application/octet-stream"
                : contentType.Trim(),
            StoragePath = stored.StorageKey,
            UploadedAt = DateTime.UtcNow,
            UploadedByName = currentUser.IsAuthenticated ? currentUser.Name : null,
        };
        installment.Documents.Add(document);
        installment.PurchaseOrder.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        return ToInstallmentDocumentDto(document);
    }

    public async Task<PurchaseOrderInstallmentDocumentDto?> GetInstallmentDocumentAsync(
        string code, int stageNumber, int documentId, CancellationToken ct = default)
    {
        var poCode = code.Trim();
        var document = await db.PurchaseOrderInstallmentDocuments.AsNoTracking()
            .Include(d => d.Installment)
            .FirstOrDefaultAsync(d =>
                d.Id == documentId &&
                d.Installment.PoCode == poCode &&
                d.Installment.StageNumber == stageNumber, ct);
        return document is null ? null : ToInstallmentDocumentDto(document);
    }

    public async Task<bool> DeleteInstallmentDocumentAsync(
        string code, int stageNumber, int documentId, CancellationToken ct = default)
    {
        var installment = await FindInstallmentAsync(code, stageNumber, asTracking: true, ct);
        if (installment is null) return false;
        EnsureInstallmentEditable(installment);
        var document = installment.Documents.FirstOrDefault(d => d.Id == documentId);
        if (document is null) return false;

        db.PurchaseOrderInstallmentDocuments.Remove(document);
        installment.PurchaseOrder.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<PurchaseOrderInstallmentDto?> CompleteInstallmentAsync(
        string code, int stageNumber, CancellationToken ct = default)
    {
        var poCode = code.Trim();
        var installments = await db.PurchaseOrderInstallments
            .Include(i => i.Documents)
            .Include(i => i.PurchaseOrder)
            .Where(i => i.PoCode == poCode)
            .OrderBy(i => i.StageNumber)
            .ToListAsync(ct);
        var installment = installments.FirstOrDefault(i => i.StageNumber == stageNumber);
        if (installment is null) return null;
        EnsureInstallmentEditable(installment);

        var required = DeserializeRequiredDocTypes(installment.RequiredDocTypes);
        var uploaded = installment.Documents
            .Select(d => d.DocType)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var missing = required.Where(t => !uploaded.Contains(t)).ToList();
        if (missing.Count > 0)
            throw new PurchasingConflictException(
                $"Chưa đủ tài liệu bắt buộc cho đợt {stageNumber}: {string.Join(", ", missing)}.");

        installment.Status = PurchaseOrderInstallmentStatuses.Completed;
        var next = installments.FirstOrDefault(i => i.StageNumber == stageNumber + 1);
        if (next is not null)
            next.Status = PurchaseOrderInstallmentStatuses.Open;
        installment.PurchaseOrder.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        return PurchasingMapper.ToInstallmentDto(installment);
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
        if (!PurchaseOrderAttachmentDocTypes.Allowed.Contains(t))
            throw new PurchasingConflictException(
                $"DocType không hợp lệ: '{docType}'. Cho phép: contract, invoice, co, cq, bbgh, bbnt, other.");
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
