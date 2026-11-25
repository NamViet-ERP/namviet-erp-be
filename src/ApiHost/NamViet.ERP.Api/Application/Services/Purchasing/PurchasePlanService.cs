using FluentValidation;
using Microsoft.EntityFrameworkCore;
using NamViet.ERP.Api.Application.Dtos.Purchasing;
using NamViet.ERP.Api.Application.Mappers.Purchasing;
using NamViet.ERP.Api.Application.Services.Shared;
using NamViet.ERP.Modules.Warehouse.Domain.Entities;
using NamViet.ERP.Modules.Warehouse.Infrastructure.Helpers;
using NamViet.ERP.Modules.Warehouse.Infrastructure.Persistence;
using NamViet.ERP.SharedKernel.Common;
using NamViet.ERP.SharedKernel.Interfaces;

namespace NamViet.ERP.Api.Application.Services.Purchasing;

public interface IPurchasePlanService
{
    Task<PurchasePlanPagedResponse> ListAsync(
        int page,
        int pageSize,
        string? code,
        string? projectCode,
        string? status,
        string? search,
        DateOnly? dateFrom,
        DateOnly? dateTo,
        string? kind = null,
        bool orderableOnly = false,
        CancellationToken ct = default);

    Task<PurchasePlanDetailDto?> GetByCodeAsync(string code, CancellationToken ct = default);
    Task<PurchasePlanDetailDto> CreateAsync(CreatePurchasePlanRequest request, CancellationToken ct = default);
    Task<PurchasePlanDetailDto?> UpdateAsync(string code, UpdatePurchasePlanRequest request, CancellationToken ct = default);
    Task<bool> DeleteAsync(string code, CancellationToken ct = default);
    Task<PurchasePlanDetailDto> ConfirmImportAsync(ConfirmPurchasePlanImportRequest request, CancellationToken ct = default);
    Task<PurchasePlanDetailDto?> SubmitAsync(string code, CancellationToken ct = default);
    Task<PurchasePlanDetailDto?> ApproveL1Async(string code, CancellationToken ct = default);
    Task<PurchasePlanDetailDto?> ApproveL2Async(string code, CancellationToken ct = default);
    Task<PurchasePlanDetailDto?> RejectAsync(string code, RejectPurchasePlanRequest request, CancellationToken ct = default);
    Task<PurchasePlanDetailDto> RemakeAsync(string code, CancellationToken ct = default);
    void RecalcTotals(PurchasePlan plan);
    /// <summary>Đồng bộ cột Tổng chi (PaidTotal) từ các PO đã thanh toán.</summary>
    Task SyncPaidTotalAsync(string planCode, CancellationToken ct = default);
    /// <summary>So sánh kế hoạch vs thực tế (đã đặt / đã nhận) để cảnh báo lệch.</summary>
    Task<IReadOnlyList<PurchasePlanVarianceDto>> GetVarianceAsync(
        string? planCode = null,
        string? projectCode = null,
        CancellationToken ct = default);
}

public sealed partial class PurchasePlanService(
    WarehouseDbContext db,
    IPurchasePlanCodeGenerator codeGenerator,
    ICurrentUser currentUser,
    IActivityEventWriter activity,
    IValidator<CreatePurchasePlanRequest> createValidator,
    IValidator<UpdatePurchasePlanRequest> updateValidator) : IPurchasePlanService
{
    public async Task<PurchasePlanPagedResponse> ListAsync(
        int page,
        int pageSize,
        string? code,
        string? projectCode,
        string? status,
        string? search,
        DateOnly? dateFrom,
        DateOnly? dateTo,
        string? kind = null,
        bool orderableOnly = false,
        CancellationToken ct = default)
    {
        (page, pageSize) = Paging.Normalize(page, pageSize);

        var q = db.PurchasePlans.AsNoTracking().Include(p => p.Project).AsQueryable();

        if (!string.IsNullOrWhiteSpace(kind))
        {
            var k = kind.Trim();
            if (!PurchasePlanKinds.IsValid(k))
                throw new PurchasingConflictException($"Loại phiếu không hợp lệ: {k}.");
            q = q.Where(p => p.Kind == k);
        }

        if (!string.IsNullOrWhiteSpace(code))
            q = q.Where(p => p.Code.Contains(code.Trim()));
        if (!string.IsNullOrWhiteSpace(projectCode))
            q = q.Where(p => p.ProjectCode == projectCode.Trim());
        if (!string.IsNullOrWhiteSpace(status))
            q = q.Where(p => p.Status == status.Trim());
        if (dateFrom is not null)
        {
            var from = dateFrom.Value.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
            q = q.Where(p => p.CreatedAt >= from);
        }

        if (dateTo is not null)
        {
            var to = dateTo.Value.ToDateTime(new TimeOnly(23, 59, 59), DateTimeKind.Utc);
            q = q.Where(p => p.CreatedAt <= to);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.Trim();
            q = q.Where(p =>
                p.Code.Contains(s) ||
                p.ProjectCode.Contains(s) ||
                (p.Note != null && p.Note.Contains(s)) ||
                (p.Project != null && p.Project.ProjectName.Contains(s)));
        }

        // Chỉ phiếu còn ít nhất 1 dòng Quantity > OrderedQty (còn đặt được).
        if (orderableOnly)
            q = q.Where(p => p.Lines.Any(l => l.Quantity > l.OrderedQty));

        var total = await q.CountAsync(ct);
        var items = await q
            .OrderByDescending(p => p.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        var paidByPlan = await LoadPaidTotalsByPlanCodesAsync(items.Select(p => p.Code), ct);

        var totalPages = Math.Max(1, (int)Math.Ceiling(total / (double)pageSize));
        return new PurchasePlanPagedResponse(
            items.Select(p => PurchasingMapper.ToPlanDto(
                p,
                paidByPlan.TryGetValue(p.Code, out var paid) ? paid : 0m)).ToList(),
            total, page, pageSize, totalPages);
    }

    public async Task<PurchasePlanDetailDto?> GetByCodeAsync(string code, CancellationToken ct = default)
    {
        var plan = await LoadDetailAsync(code, ct);
        if (plan is null) return null;
        var paid = await SumPaidTotalForPlanAsync(code, ct);
        var pendingByLineId = await LoadPendingOrderedByLineIdAsync(plan.Lines.Select(l => l.Id), ct);

        var relatedOrders = await db.PurchaseOrders.AsNoTracking()
            .Where(o => o.PlanCode == code)
            .OrderByDescending(o => o.CreatedAt)
            .Select(o => new PurchasePlanRelatedPoDto(
                o.Code,
                o.SupplierNameSnapshot ?? (o.Supplier != null ? o.Supplier.SupplierName : null),
                o.TotalAmount,
                o.ApprovalStatus,
                o.OrderPlacementStatus,
                o.DeliveryStatus,
                o.PaymentStatus,
                o.Items.Count,
                o.CreatedAt))
            .ToListAsync(ct);

        var lineIds = plan.Lines.Select(l => l.Id).Distinct().ToList();
        var poItems = await db.PurchaseOrderItems.AsNoTracking()
            .Where(i => i.PlanLineId.HasValue && lineIds.Contains(i.PlanLineId.Value))
            .Select(i => new
            {
                PlanLineId = i.PlanLineId!.Value,
                i.PoCode,
                i.Ordered,
                i.Received
            })
            .ToListAsync(ct);

        var posByLineId = poItems
            .GroupBy(i => i.PlanLineId)
            .ToDictionary(
                g => g.Key,
                g => g.Select(x => new PurchasePlanLinePoDto(x.PoCode, x.Ordered, x.Received)).ToList());

        return PurchasingMapper.ToPlanDetailDto(plan, paid, pendingByLineId, posByLineId, relatedOrders);
    }

    public async Task<PurchasePlanDetailDto> CreateAsync(CreatePurchasePlanRequest request, CancellationToken ct = default)
    {
        await createValidator.ValidateAndThrowAsync(request, ct);

        var projectCode = request.ProjectCode?.Trim()
            ?? throw new PurchasingConflictException("ProjectCode là bắt buộc.");

        var projectExists = await db.Projects.AnyAsync(p => p.ProjectCode == projectCode, ct);
        if (!projectExists)
            throw new KeyNotFoundException($"Không tìm thấy dự án {projectCode}.");

        var kind = string.IsNullOrWhiteSpace(request.Kind)
            ? PurchasePlanKinds.Plan
            : request.Kind.Trim();
        if (!PurchasePlanKinds.IsValid(kind))
            throw new PurchasingConflictException($"Loại phiếu không hợp lệ: {kind}.");

        return await SequentialCodeHelper.ExecuteWithUniqueRetryAsync(async () =>
        {
            db.ChangeTracker.Clear();

            await using var tx = await db.Database.BeginTransactionAsync(ct);
            await TicketCodeAllocator.AcquireNamedLockAsync(db, "pur_plan_code", ct);

            var code = await codeGenerator.NextAsync(kind, ct);
            var now = DateTime.UtcNow;
            var plan = new PurchasePlan
            {
                Code = code,
                ProjectCode = projectCode,
                Kind = kind,
                Status = PurchasePlanStatuses.Draft,
                Note = string.IsNullOrWhiteSpace(request.Note) ? null : request.Note.Trim(),
                CreatedById = currentUser.IsAuthenticated ? currentUser.Id : null,
                CreatedByName = currentUser.IsAuthenticated ? currentUser.Name : null,
                CreatedAt = now,
                UpdatedAt = now,
            };

            if (request.Lines is { Count: > 0 })
            {
                var allCodes = request.Lines
                    .Select(l => l.MaterialCode)
                    .Where(c => !string.IsNullOrWhiteSpace(c));
                var serviceItemsByCode = await LoadServiceExpenseItemsByCodesAsync(allCodes, ct);

                var materialCodes = request.Lines
                    .Where(l =>
                    {
                        var c = l.MaterialCode?.Trim();
                        if (!string.IsNullOrEmpty(c) && serviceItemsByCode.ContainsKey(c)) return false;
                        return (l.LineType ?? PurchasePlanLineTypes.Material) == PurchasePlanLineTypes.Material;
                    })
                    .Select(l => l.MaterialCode);
                var materialsByCode = await LoadMaterialsByCodesAsync(materialCodes, ct);

                ValidateMissingMaterials(
                    request.Lines.Select((l, i) => (l.MaterialCode, l.LineType, i + 1)),
                    materialsByCode,
                    serviceItemsByCode);

                var lineNo = 0;
                foreach (var src in request.Lines)
                {
                    lineNo++;
                    var rawCode = src.MaterialCode?.Trim();
                    ServiceExpenseItem? serviceItem = null;
                    var isServiceItem = !string.IsNullOrEmpty(rawCode) && serviceItemsByCode.TryGetValue(rawCode, out serviceItem);
                    var lineType = isServiceItem
                        ? (serviceItem!.Type == "expense" ? PurchasePlanLineTypes.Expense : PurchasePlanLineTypes.Service)
                        : (PurchasePlanLineTypes.IsValid(src.LineType) ? src.LineType! : PurchasePlanLineTypes.Material);

                    Material? material = null;
                    if (lineType == PurchasePlanLineTypes.Material && !isServiceItem)
                    {
                        material = RequireMaterial(src.MaterialCode, materialsByCode, lineNo);
                    }
                    plan.Lines.Add(MapCreateLine(src, code, lineNo, material, lineType, serviceItem));
                }

                RecalcTotals(plan);
            }

            db.PurchasePlans.Add(plan);
            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);

            await activity.WriteAsync(new ActivityEventWriteRequest(
                ActivityModules.Purchasing,
                ActivityActions.PlanCreated,
                $"Tạo kế hoạch mua hàng [{code}]",
                ActivityCategories.Plan,
                EntityType: "purchase_plan",
                EntityCode: code), ct);

            return await GetByCodeAsync(code, ct)
                ?? throw new DomainRuleException($"Không tải được KHMH {code} sau khi tạo.");
        }, ct: ct);
    }

    public async Task<PurchasePlanDetailDto?> UpdateAsync(
        string code, UpdatePurchasePlanRequest request, CancellationToken ct = default)
    {
        await updateValidator.ValidateAndThrowAsync(request, ct);

        var plan = request.Lines is not null
            ? await db.PurchasePlans
                .Include(p => p.Lines)
                    .ThenInclude(l => l.Quotes)
                .FirstOrDefaultAsync(p => p.Code == code, ct)
            : await db.PurchasePlans.FirstOrDefaultAsync(p => p.Code == code, ct);
        if (plan is null) return null;

        var isStatusOnly =
            request.Lines is null
            && request.Note is null
            && !string.IsNullOrWhiteSpace(request.Status);

        if (!isStatusOnly && !PurchasePlanStatuses.IsEditable(plan.Status))
            throw new PurchasingConflictException("Chỉ chỉnh sửa nội dung được khi phiếu ở trạng thái nháp.");

        if (request.Lines is not null)
        {
            if (!string.Equals(plan.Status, PurchasePlanStatuses.Draft, StringComparison.OrdinalIgnoreCase))
                throw new PurchasingConflictException("Chỉ thay thế dòng khi phiếu ở trạng thái nháp.");

            db.PurchasePlanLines.RemoveRange(plan.Lines);
            plan.Lines.Clear();

            var allCodes = request.Lines
                .Select(l => l.MaterialCode)
                .Where(c => !string.IsNullOrWhiteSpace(c));
            var serviceItemsByCode = await LoadServiceExpenseItemsByCodesAsync(allCodes, ct);

            var materialCodes = request.Lines
                .Where(l =>
                {
                    var c = l.MaterialCode?.Trim();
                    if (!string.IsNullOrEmpty(c) && serviceItemsByCode.ContainsKey(c)) return false;
                    return (l.LineType ?? PurchasePlanLineTypes.Material) == PurchasePlanLineTypes.Material;
                })
                .Select(l => l.MaterialCode);
            var materialsByCode = await LoadMaterialsByCodesAsync(materialCodes, ct);

            ValidateMissingMaterials(
                request.Lines.Select((l, i) => (l.MaterialCode, l.LineType, i + 1)),
                materialsByCode,
                serviceItemsByCode);

            var lineNo = 0;
            foreach (var src in request.Lines)
            {
                lineNo++;
                var rawCode = src.MaterialCode?.Trim();
                ServiceExpenseItem? serviceItem = null;
                var isServiceItem = !string.IsNullOrEmpty(rawCode) && serviceItemsByCode.TryGetValue(rawCode, out serviceItem);
                var lineType = isServiceItem
                    ? (serviceItem!.Type == "expense" ? PurchasePlanLineTypes.Expense : PurchasePlanLineTypes.Service)
                    : (PurchasePlanLineTypes.IsValid(src.LineType) ? src.LineType! : PurchasePlanLineTypes.Material);

                Material? material = null;
                if (lineType == PurchasePlanLineTypes.Material && !isServiceItem)
                {
                    material = RequireMaterial(src.MaterialCode, materialsByCode, lineNo);
                }
                plan.Lines.Add(MapCreateLine(src, code, lineNo, material, lineType, serviceItem));
            }

            RecalcTotals(plan);
        }

        if (request.Note is not null)
        {
            if (!PurchasePlanStatuses.IsEditable(plan.Status))
                throw new PurchasingConflictException("Chỉ sửa ghi chú khi phiếu ở trạng thái nháp.");
            plan.Note = string.IsNullOrWhiteSpace(request.Note) ? null : request.Note.Trim();
        }

        if (!string.IsNullOrWhiteSpace(request.Status))
        {
            var status = request.Status.Trim();
            // Chỉ cho đóng / mở lại từ approved ↔ closed qua update status.
            if (string.Equals(status, PurchasePlanStatuses.Closed, StringComparison.OrdinalIgnoreCase))
            {
                if (!PurchasePlanStatuses.CanClose(plan.Status)
                    && !string.Equals(plan.Status, PurchasePlanStatuses.Closed, StringComparison.OrdinalIgnoreCase))
                    throw new PurchasingConflictException("Chỉ đóng được phiếu đã duyệt.");
                plan.Status = PurchasePlanStatuses.Closed;
            }
            else if (string.Equals(status, PurchasePlanStatuses.Approved, StringComparison.OrdinalIgnoreCase))
            {
                if (!string.Equals(plan.Status, PurchasePlanStatuses.Closed, StringComparison.OrdinalIgnoreCase))
                    throw new PurchasingConflictException("Chỉ mở lại từ trạng thái đã đóng.");
                plan.Status = PurchasePlanStatuses.Approved;
            }
            else
            {
                throw new PurchasingConflictException(
                    "Không đổi trạng thái duyệt qua API sửa. Dùng gửi duyệt / duyệt cấp 1 / cấp 2 / từ chối / lập phiếu mới.");
            }
        }

        plan.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);

        await activity.WriteAsync(new ActivityEventWriteRequest(
            ActivityModules.Purchasing,
            ActivityActions.PlanUpdated,
            $"Cập nhật kế hoạch mua hàng [{code}]",
            ActivityCategories.Plan,
            EntityType: "purchase_plan",
            EntityCode: code), ct);

        return await GetByCodeAsync(code, ct);
    }

    public async Task<bool> DeleteAsync(string code, CancellationToken ct = default)
    {
        var plan = await db.PurchasePlans.FirstOrDefaultAsync(p => p.Code == code, ct);
        if (plan is null) return false;

        if (!string.Equals(plan.Status, PurchasePlanStatuses.Draft, StringComparison.OrdinalIgnoreCase))
            throw new PurchasingConflictException("Chỉ xóa được kế hoạch ở trạng thái draft.");

        var hasOrders = await db.PurchaseOrders.AnyAsync(o => o.PlanCode == code, ct);
        if (hasOrders)
            throw new PurchasingConflictException("Không thể xóa kế hoạch đã có đơn mua hàng.");

        plan.DeletedAt = DateTime.UtcNow;
        plan.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        return true;
    }
}
