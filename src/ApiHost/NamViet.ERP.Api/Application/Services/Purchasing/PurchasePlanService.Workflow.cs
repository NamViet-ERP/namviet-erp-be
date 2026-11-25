using Microsoft.EntityFrameworkCore;
using NamViet.ERP.Api.Application.Dtos.Purchasing;
using NamViet.ERP.Api.Application.Services.Shared;
using NamViet.ERP.Modules.Warehouse.Domain.Entities;
using NamViet.ERP.Modules.Warehouse.Domain.SupplierEvaluation;

namespace NamViet.ERP.Api.Application.Services.Purchasing;

public sealed partial class PurchasePlanService
{
    public async Task<PurchasePlanDetailDto> ConfirmImportAsync(
        ConfirmPurchasePlanImportRequest request, CancellationToken ct = default)
    {
        var projectCode = request.ProjectCode?.Trim()
            ?? throw new PurchasingConflictException("ProjectCode là bắt buộc.");

        var projectExists = await db.Projects.AnyAsync(p => p.ProjectCode == projectCode, ct);
        if (!projectExists)
            throw new KeyNotFoundException($"Không tìm thấy dự án {projectCode}.");

        var validLines = request.Lines?.Where(l => l.IsValid).ToList()
            ?? throw new PurchasingConflictException("Danh sách dòng import trống.");
        if (validLines.Count == 0)
            throw new PurchasingConflictException("Không có dòng hợp lệ để import.");

        var allCodes = validLines
            .Select(l => l.MaterialCode)
            .Where(c => !string.IsNullOrWhiteSpace(c));
        var serviceItemsByCode = await LoadServiceExpenseItemsByCodesAsync(allCodes, ct);

        var materialCodes = validLines
            .Where(l =>
            {
                var c = l.MaterialCode?.Trim();
                if (!string.IsNullOrEmpty(c) && serviceItemsByCode.ContainsKey(c)) return false;
                return (l.LineType ?? PurchasePlanLineTypes.Material) == PurchasePlanLineTypes.Material;
            })
            .Select(l => l.MaterialCode);
        var materialsByCode = await LoadMaterialsByCodesAsync(materialCodes, ct);

        ValidateMissingMaterials(
            validLines.Select((l, i) => (l.MaterialCode, l.LineType, l.LineNo > 0 ? l.LineNo : i + 1)),
            materialsByCode,
            serviceItemsByCode);

        await using var tx = await db.Database.BeginTransactionAsync(ct);

        var code = await codeGenerator.NextAsync(PurchasePlanKinds.Plan, ct);
        var now = DateTime.UtcNow;
        var plan = new PurchasePlan
        {
            Code = code,
            ProjectCode = projectCode,
            Kind = PurchasePlanKinds.Plan,
            Status = PurchasePlanStatuses.Draft,
            Note = string.IsNullOrWhiteSpace(request.Note) ? null : request.Note.Trim(),
            CreatedById = currentUser.IsAuthenticated ? currentUser.Id : null,
            CreatedByName = currentUser.IsAuthenticated ? currentUser.Name : null,
            CreatedAt = now,
            UpdatedAt = now,
        };

        var lineNo = 0;
        foreach (var src in validLines.OrderBy(l => l.LineNo))
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
            var line = MapImportLine(src, code, lineNo, material, lineType, serviceItem);
            plan.Lines.Add(line);
        }

        RecalcTotals(plan);
        db.PurchasePlans.Add(plan);
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);

        await activity.WriteAsync(new ActivityEventWriteRequest(
            ActivityModules.Purchasing,
            ActivityActions.PlanCreated,
            $"Import Excel tạo kế hoạch mua hàng [{code}]",
            ActivityCategories.Plan,
            EntityType: "purchase_plan",
            EntityCode: code), ct);

        return (await GetByCodeAsync(code, ct))!;
    }

    public async Task<PurchasePlanDetailDto?> SubmitAsync(string code, CancellationToken ct = default)
    {
        var plan = await db.PurchasePlans.Include(p => p.Lines).FirstOrDefaultAsync(p => p.Code == code, ct);
        if (plan is null) return null;
        if (!string.Equals(plan.Status, PurchasePlanStatuses.Draft, StringComparison.OrdinalIgnoreCase))
            throw new PurchasingConflictException("Chỉ gửi duyệt được phiếu nháp.");
        if (plan.Lines.Count == 0)
            throw new PurchasingConflictException("Phiếu chưa có dòng vật tư, không thể gửi duyệt.");

        var now = DateTime.UtcNow;
        plan.Status = PurchasePlanStatuses.PendingL1;
        plan.SubmittedAt = now;
        plan.RejectedById = null;
        plan.RejectedByName = null;
        plan.RejectedAt = null;
        plan.RejectReason = null;
        plan.UpdatedAt = now;
        await db.SaveChangesAsync(ct);

        await activity.WriteAsync(new ActivityEventWriteRequest(
            ActivityModules.Purchasing,
            ActivityActions.PlanUpdated,
            $"Gửi duyệt [{code}]",
            ActivityCategories.Plan,
            EntityType: "purchase_plan",
            EntityCode: code), ct);

        return await GetByCodeAsync(code, ct);
    }

    public async Task<PurchasePlanDetailDto?> ApproveL1Async(string code, CancellationToken ct = default)
    {
        var plan = await db.PurchasePlans.FirstOrDefaultAsync(p => p.Code == code, ct);
        if (plan is null) return null;
        if (!string.Equals(plan.Status, PurchasePlanStatuses.PendingL1, StringComparison.OrdinalIgnoreCase))
            throw new PurchasingConflictException("Phiếu không đang chờ duyệt cấp 1.");

        var now = DateTime.UtcNow;
        plan.Status = PurchasePlanStatuses.PendingL2;
        plan.L1ApprovedById = currentUser.IsAuthenticated ? currentUser.Id : null;
        plan.L1ApprovedByName = currentUser.IsAuthenticated ? currentUser.Name : null;
        plan.L1ApprovedAt = now;
        plan.UpdatedAt = now;
        await db.SaveChangesAsync(ct);

        await activity.WriteAsync(new ActivityEventWriteRequest(
            ActivityModules.Purchasing,
            ActivityActions.PlanUpdated,
            $"Duyệt cấp 1 [{code}]",
            ActivityCategories.Plan,
            EntityType: "purchase_plan",
            EntityCode: code), ct);

        return await GetByCodeAsync(code, ct);
    }

    public async Task<PurchasePlanDetailDto?> ApproveL2Async(string code, CancellationToken ct = default)
    {
        var plan = await db.PurchasePlans.FirstOrDefaultAsync(p => p.Code == code, ct);
        if (plan is null) return null;
        if (!string.Equals(plan.Status, PurchasePlanStatuses.PendingL2, StringComparison.OrdinalIgnoreCase))
            throw new PurchasingConflictException("Phiếu không đang chờ duyệt cấp 2.");

        var now = DateTime.UtcNow;
        plan.Status = PurchasePlanStatuses.Approved;
        plan.L2ApprovedById = currentUser.IsAuthenticated ? currentUser.Id : null;
        plan.L2ApprovedByName = currentUser.IsAuthenticated ? currentUser.Name : null;
        plan.L2ApprovedAt = now;
        plan.UpdatedAt = now;
        await MaterialCeilingSync.ApplyFromPlanAsync(db, code, ct);
        await db.SaveChangesAsync(ct);

        await activity.WriteAsync(new ActivityEventWriteRequest(
            ActivityModules.Purchasing,
            ActivityActions.PlanUpdated,
            $"Duyệt cấp 2 [{code}]",
            ActivityCategories.Plan,
            EntityType: "purchase_plan",
            EntityCode: code), ct);

        return await GetByCodeAsync(code, ct);
    }

    public async Task<PurchasePlanDetailDto?> RejectAsync(
        string code, RejectPurchasePlanRequest request, CancellationToken ct = default)
    {
        var plan = await db.PurchasePlans.FirstOrDefaultAsync(p => p.Code == code, ct);
        if (plan is null) return null;
        if (!string.Equals(plan.Status, PurchasePlanStatuses.PendingL1, StringComparison.OrdinalIgnoreCase)
            && !string.Equals(plan.Status, PurchasePlanStatuses.PendingL2, StringComparison.OrdinalIgnoreCase))
            throw new PurchasingConflictException("Chỉ từ chối được phiếu đang chờ duyệt.");

        var now = DateTime.UtcNow;
        plan.Status = PurchasePlanStatuses.Rejected;
        plan.RejectedById = currentUser.IsAuthenticated ? currentUser.Id : null;
        plan.RejectedByName = currentUser.IsAuthenticated ? currentUser.Name : null;
        plan.RejectedAt = now;
        plan.RejectReason = string.IsNullOrWhiteSpace(request.Reason) ? null : request.Reason.Trim();
        plan.UpdatedAt = now;
        await db.SaveChangesAsync(ct);

        await activity.WriteAsync(new ActivityEventWriteRequest(
            ActivityModules.Purchasing,
            ActivityActions.PlanUpdated,
            $"Từ chối duyệt [{code}]",
            ActivityCategories.Plan,
            EntityType: "purchase_plan",
            EntityCode: code), ct);

        return await GetByCodeAsync(code, ct);
    }

    public async Task<PurchasePlanDetailDto> RemakeAsync(string code, CancellationToken ct = default)
    {
        var source = await db.PurchasePlans
            .Include(p => p.Lines)
                .ThenInclude(l => l.Quotes)
            .FirstOrDefaultAsync(p => p.Code == code, ct)
            ?? throw new KeyNotFoundException($"Không tìm thấy phiếu {code}.");

        if (!string.Equals(source.Status, PurchasePlanStatuses.Rejected, StringComparison.OrdinalIgnoreCase))
            throw new PurchasingConflictException("Chỉ lập phiếu mới từ phiếu bị từ chối.");

        var newCode = await codeGenerator.NextAsync(source.Kind, ct);
        var now = DateTime.UtcNow;
        var clone = new PurchasePlan
        {
            Code = newCode,
            ProjectCode = source.ProjectCode,
            Kind = source.Kind,
            Status = PurchasePlanStatuses.Draft,
            Note = source.Note,
            RemadeFromCode = source.Code,
            CreatedById = currentUser.IsAuthenticated ? currentUser.Id : null,
            CreatedByName = currentUser.IsAuthenticated ? currentUser.Name : null,
            CreatedAt = now,
            UpdatedAt = now,
            BudgetTotal = source.BudgetTotal,
            ProposalTotal = source.ProposalTotal,
            PurchaseTotal = source.PurchaseTotal,
            PaidTotal = 0,
        };

        foreach (var line in source.Lines.OrderBy(l => l.LineNo))
        {
            var clonedLine = CloneLine(line, newCode);
            clone.Lines.Add(clonedLine);
        }

        source.RemadeToCode = newCode;
        source.UpdatedAt = now;

        db.PurchasePlans.Add(clone);
        await db.SaveChangesAsync(ct);

        await activity.WriteAsync(new ActivityEventWriteRequest(
            ActivityModules.Purchasing,
            ActivityActions.PlanCreated,
            $"Lập phiếu mới [{newCode}] từ phiếu từ chối [{code}]",
            ActivityCategories.Plan,
            EntityType: "purchase_plan",
            EntityCode: newCode), ct);

        return (await GetByCodeAsync(newCode, ct))!;
    }

    public void RecalcTotals(PurchasePlan plan)
    {
        plan.BudgetTotal = Math.Round(plan.Lines.Sum(l => l.BudgetAmount ?? 0m), 2);
        plan.ProposalTotal = Math.Round(plan.Lines.Sum(l => l.ProposalAmount ?? 0m), 2);
        plan.PurchaseTotal = Math.Round(plan.Lines.Sum(l =>
            l.SelectedAmount ?? l.PurchaseAmount ?? 0m), 2);
        // PaidTotal (= Tổng chi): tổng TotalAmount các PO thuộc KHMH đã thanh toán đủ (paymentStatus=paid).
        // Tính live khi List/Get; giữ giá trị hiện có khi chỉ recalc dòng.
        plan.UpdatedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Tổng chi KHMH = tổng tiền các PO đã thanh toán (PaymentStatus = paid).
    /// </summary>
    public async Task SyncPaidTotalAsync(string planCode, CancellationToken ct = default)
    {
        var plan = await db.PurchasePlans.FirstOrDefaultAsync(p => p.Code == planCode, ct);
        if (plan is null) return;
        plan.PaidTotal = await SumPaidTotalForPlanAsync(planCode, ct);
        plan.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<PurchasePlanVarianceDto>> GetVarianceAsync(
        string? planCode = null,
        string? projectCode = null,
        CancellationToken ct = default)
    {
        var q = db.PurchasePlans.AsNoTracking()
            .Include(p => p.Lines)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(planCode))
            q = q.Where(p => p.Code == planCode.Trim());
        if (!string.IsNullOrWhiteSpace(projectCode))
            q = q.Where(p => p.ProjectCode == projectCode.Trim());

        var plans = await q
            .OrderByDescending(p => p.CreatedAt)
            .ToListAsync(ct);

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var result = new List<PurchasePlanVarianceDto>(plans.Count);

        foreach (var plan in plans)
        {
            var lines = plan.Lines
                .OrderBy(l => l.LineNo)
                .Select(l => ToVarianceLine(plan, l, today))
                .ToList();

            result.Add(new PurchasePlanVarianceDto(plan.Code, plan.ProjectCode, lines));
        }

        return result;
    }
}
