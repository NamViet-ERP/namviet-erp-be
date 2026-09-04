using Microsoft.EntityFrameworkCore;
using NamViet.ERP.Api.Application.Dtos.Purchasing;
using NamViet.ERP.Api.Application.Mappers.Purchasing;
using NamViet.ERP.Api.Application.Services.Shared;
using NamViet.ERP.Api.Services.Storage;
using NamViet.ERP.Modules.Warehouse.Domain.Entities;
using NamViet.ERP.Modules.Warehouse.Infrastructure.Helpers;
using NamViet.ERP.Modules.Warehouse.Infrastructure.Persistence;
using NamViet.ERP.SharedKernel.Interfaces;
using System.Text.Json;
using NamViet.ERP.SharedKernel.Common;
using NamViet.ERP.SharedKernel.Time;
using FluentValidation;

namespace NamViet.ERP.Api.Application.Services.Purchasing;

public sealed partial class PurchaseOrderService
{
    public async Task<PurchaseOrderDetailDto> CreateAsync(
        CreatePurchaseOrderRequest request, CancellationToken ct = default)
    {
        await createValidator.ValidateAndThrowAsync(request, ct);

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
        EnsureSupplierCanPlaceOrder(supplier);

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
        await TicketCodeAllocator.AcquireNamedLockAsync(db, "pur_po_code", ct);

        var code = await codeGenerator.NextAsync(ct);
        var orderDate = request.OrderDate ?? VietnamClock.Today;
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
                WarrantyMonths = Math.Clamp(reqLine.WarrantyMonths, 0, 120),
                WarrantyAnchorType = NormalizeWarrantyAnchor(reqLine.WarrantyAnchorType),
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
        await updateValidator.ValidateAndThrowAsync(request, ct);

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

        var plan = order.Plan ?? await RequirePlanAsync(order.PlanCode, ct);
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
                WarrantyMonths = Math.Clamp(reqLine.WarrantyMonths, 0, 120),
                WarrantyAnchorType = NormalizeWarrantyAnchor(reqLine.WarrantyAnchorType),
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

        var plan = await RequirePlanAsync(order.PlanCode, ct);
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
}
