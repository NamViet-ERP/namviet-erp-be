using Microsoft.EntityFrameworkCore;
using NamViet.ERP.Api.Application.Dtos.Purchasing;
using NamViet.ERP.Api.Application.Mappers.Purchasing;
using NamViet.ERP.Api.Application.Services.Shared;
using NamViet.ERP.Modules.Warehouse.Domain.Entities;
using NamViet.ERP.Modules.Warehouse.Infrastructure.Persistence;
using NamViet.ERP.SharedKernel.Interfaces;

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
}

public sealed class PurchaseOrderService(
    WarehouseDbContext db,
    IPurchaseOrderCodeGenerator codeGenerator,
    IPurchasePlanService planService,
    ICurrentUser currentUser,
    IActivityEventWriter activity) : IPurchaseOrderService
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

        var planCode = request.PlanCode?.Trim()
            ?? throw new PurchasingConflictException("PlanCode là bắt buộc.");

        var plan = await db.PurchasePlans
            .Include(p => p.Lines)
            .FirstOrDefaultAsync(p => p.Code == planCode, ct)
            ?? throw new KeyNotFoundException($"Không tìm thấy KHMH {planCode}.");

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
            .Include(o => o.Plan)
                .ThenInclude(p => p!.Lines)
            .FirstOrDefaultAsync(o => o.Code == code, ct);
        if (order is null) return null;

        if (!string.Equals(order.ApprovalStatus, PurchaseOrderApprovalStatuses.Draft, StringComparison.OrdinalIgnoreCase))
            throw new PurchasingConflictException("Chỉ sửa được PO ở trạng thái draft.");

        if (request.Lines is null || request.Lines.Count == 0)
            throw new PurchasingConflictException("PO phải có ít nhất một dòng.");

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
            .FirstOrDefaultAsync(o => o.Code == code, ct);
}
