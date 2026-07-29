using Microsoft.EntityFrameworkCore;
using NamViet.ERP.Api.Application.Dtos.Purchasing;
using NamViet.ERP.Api.Application.Mappers.Purchasing;
using NamViet.ERP.Api.Application.Services.Shared;
using NamViet.ERP.Modules.Warehouse.Domain.Entities;
using NamViet.ERP.Modules.Warehouse.Infrastructure.Persistence;
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
        CancellationToken ct = default);

    Task<PurchasePlanDetailDto?> GetByCodeAsync(string code, CancellationToken ct = default);
    Task<PurchasePlanDetailDto> CreateAsync(CreatePurchasePlanRequest request, CancellationToken ct = default);
    Task<PurchasePlanDetailDto?> UpdateAsync(string code, UpdatePurchasePlanRequest request, CancellationToken ct = default);
    Task<bool> DeleteAsync(string code, CancellationToken ct = default);
    Task<PurchasePlanDetailDto> ConfirmImportAsync(ConfirmPurchasePlanImportRequest request, CancellationToken ct = default);
    void RecalcTotals(PurchasePlan plan);
    /// <summary>Đồng bộ cột Tổng chi (PaidTotal) từ các PO đã thanh toán.</summary>
    Task SyncPaidTotalAsync(string planCode, CancellationToken ct = default);
    /// <summary>So sánh kế hoạch vs thực tế (đã đặt / đã nhận) để cảnh báo lệch.</summary>
    Task<IReadOnlyList<PurchasePlanVarianceDto>> GetVarianceAsync(
        string? planCode = null,
        string? projectCode = null,
        CancellationToken ct = default);
}

public sealed class PurchasePlanService(
    WarehouseDbContext db,
    IPurchasePlanCodeGenerator codeGenerator,
    ICurrentUser currentUser,
    IActivityEventWriter activity) : IPurchasePlanService
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
        CancellationToken ct = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 200);

        var q = db.PurchasePlans.AsNoTracking().Include(p => p.Project).AsQueryable();

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
        return PurchasingMapper.ToPlanDetailDto(plan, paid);
    }

    public async Task<PurchasePlanDetailDto> CreateAsync(CreatePurchasePlanRequest request, CancellationToken ct = default)
    {
        var projectCode = request.ProjectCode?.Trim()
            ?? throw new PurchasingConflictException("ProjectCode là bắt buộc.");

        var projectExists = await db.Projects.AnyAsync(p => p.ProjectCode == projectCode, ct);
        if (!projectExists)
            throw new KeyNotFoundException($"Không tìm thấy dự án {projectCode}.");

        var code = await codeGenerator.NextAsync(ct);
        var now = DateTime.UtcNow;
        var plan = new PurchasePlan
        {
            Code = code,
            ProjectCode = projectCode,
            Status = PurchasePlanStatuses.Draft,
            Note = string.IsNullOrWhiteSpace(request.Note) ? null : request.Note.Trim(),
            CreatedById = currentUser.IsAuthenticated ? currentUser.Id : null,
            CreatedByName = currentUser.IsAuthenticated ? currentUser.Name : null,
            CreatedAt = now,
            UpdatedAt = now,
        };

        if (request.Lines is { Count: > 0 })
        {
            var materialsByCode = await LoadMaterialsByCodesAsync(
                request.Lines.Select(l => l.MaterialCode), ct);

            var lineNo = 0;
            foreach (var src in request.Lines)
            {
                lineNo++;
                var material = RequireMaterial(src.MaterialCode, materialsByCode, lineNo);
                plan.Lines.Add(MapCreateLine(src, code, lineNo, material));
            }

            RecalcTotals(plan);
        }

        db.PurchasePlans.Add(plan);
        await db.SaveChangesAsync(ct);

        await activity.WriteAsync(new ActivityEventWriteRequest(
            ActivityModules.Purchasing,
            ActivityActions.PlanCreated,
            $"Tạo kế hoạch mua hàng [{code}]",
            ActivityCategories.Plan,
            EntityType: "purchase_plan",
            EntityCode: code), ct);

        return (await GetByCodeAsync(code, ct))!;
    }

    public async Task<PurchasePlanDetailDto?> UpdateAsync(
        string code, UpdatePurchasePlanRequest request, CancellationToken ct = default)
    {
        var plan = request.Lines is not null
            ? await db.PurchasePlans
                .Include(p => p.Lines)
                    .ThenInclude(l => l.Quotes)
                .FirstOrDefaultAsync(p => p.Code == code, ct)
            : await db.PurchasePlans.FirstOrDefaultAsync(p => p.Code == code, ct);
        if (plan is null) return null;

        if (!PurchasePlanStatuses.IsEditable(plan.Status))
            throw new PurchasingConflictException("Kế hoạch đã đóng, không thể chỉnh sửa.");

        if (request.Lines is not null)
        {
            if (!string.Equals(plan.Status, PurchasePlanStatuses.Draft, StringComparison.OrdinalIgnoreCase))
                throw new PurchasingConflictException("Chỉ thay thế dòng khi kế hoạch ở trạng thái draft.");

            db.PurchasePlanLines.RemoveRange(plan.Lines);
            plan.Lines.Clear();

            var materialsByCode = await LoadMaterialsByCodesAsync(
                request.Lines.Select(l => l.MaterialCode), ct);

            var lineNo = 0;
            foreach (var src in request.Lines)
            {
                lineNo++;
                var material = RequireMaterial(src.MaterialCode, materialsByCode, lineNo);
                plan.Lines.Add(MapCreateLine(src, code, lineNo, material));
            }

            RecalcTotals(plan);
        }

        if (request.Note is not null)
            plan.Note = string.IsNullOrWhiteSpace(request.Note) ? null : request.Note.Trim();

        if (!string.IsNullOrWhiteSpace(request.Status))
        {
            var status = request.Status.Trim();
            if (!PurchasePlanStatuses.All.Contains(status))
                throw new PurchasingConflictException($"Trạng thái không hợp lệ: {status}.");
            plan.Status = status;
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

        var materialsByCode = await LoadMaterialsByCodesAsync(
            validLines.Select(l => l.MaterialCode), ct);

        await using var tx = await db.Database.BeginTransactionAsync(ct);

        var code = await codeGenerator.NextAsync(ct);
        var now = DateTime.UtcNow;
        var plan = new PurchasePlan
        {
            Code = code,
            ProjectCode = projectCode,
            Status = PurchasePlanStatuses.Active,
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
            var material = RequireMaterial(src.MaterialCode, materialsByCode, lineNo);
            var line = MapImportLine(src, code, lineNo, material);
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
        CreatePurchasePlanLineRequest src, string planCode, int lineNo, Material material)
    {
        var materialCode = src.MaterialCode.Trim();

        var productName = src.ProductName?.Trim();
        if (string.IsNullOrWhiteSpace(productName))
            productName = material.MaterialName;

        var unit = src.Unit?.Trim();
        if (string.IsNullOrWhiteSpace(unit))
            unit = material.Unit;

        if (string.IsNullOrWhiteSpace(productName))
            throw new PurchasingConflictException("ProductName là bắt buộc.");

        if (string.IsNullOrWhiteSpace(unit))
            throw new PurchasingConflictException("Unit là bắt buộc.");

        if (src.Quantity <= 0)
            throw new PurchasingConflictException("Quantity phải > 0.");

        var line = new PurchasePlanLine
        {
            PlanCode = planCode,
            LineNo = lineNo,
            SystemName = string.IsNullOrWhiteSpace(src.SystemName) ? null : src.SystemName.Trim(),
            Category = string.IsNullOrWhiteSpace(src.Category) ? null : src.Category.Trim(),
            MaterialCode = materialCode,
            ProductName = productName,
            Spec = string.IsNullOrWhiteSpace(src.Spec) ? null : src.Spec.Trim(),
            Unit = unit,
            Quantity = src.Quantity,
            KgPerUnit = src.KgPerUnit,
            LineNote = string.IsNullOrWhiteSpace(src.LineNote) ? null : src.LineNote.Trim(),
            PurchaseStatus = string.IsNullOrWhiteSpace(src.PurchaseStatus)
                ? PurchaseLineStatuses.NotPurchased
                : src.PurchaseStatus.Trim(),
            StatusNote = string.IsNullOrWhiteSpace(src.StatusNote) ? null : src.StatusNote.Trim(),
            BudgetUnitPrice = src.BudgetUnitPrice,
            BudgetAmount = src.BudgetAmount ?? (src.BudgetUnitPrice is decimal bup
                ? Math.Round(bup * src.Quantity, 2) : null),
            ProposalKgPerUnit = src.ProposalKgPerUnit,
            ProposalUnitPrice = src.ProposalUnitPrice,
            ProposalAmount = src.ProposalAmount ?? (src.ProposalUnitPrice is decimal pup
                ? Math.Round(pup * src.Quantity, 2) : null),
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
                ? Math.Round(prup * src.Quantity, 2) : null),
            PurchaseSupplierName = string.IsNullOrWhiteSpace(src.PurchaseSupplierName)
                ? null : src.PurchaseSupplierName.Trim(),
            PurchaseNote = string.IsNullOrWhiteSpace(src.PurchaseNote) ? null : src.PurchaseNote.Trim(),
            SelectedUnitPrice = src.SelectedUnitPrice,
            SelectedAmount = src.SelectedAmount ?? (src.SelectedUnitPrice is decimal sup
                ? Math.Round(sup * src.Quantity, 2) : null),
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
                    ? Math.Round(up * src.Quantity, 2) : null),
                SupplierName = string.IsNullOrWhiteSpace(q.SupplierName) ? null : q.SupplierName.Trim(),
                Note = string.IsNullOrWhiteSpace(q.Note) ? null : q.Note.Trim(),
                Status = string.IsNullOrWhiteSpace(q.Status) ? null : q.Status.Trim(),
            });
        }

        return line;
    }

    private static PurchasePlanLine MapImportLine(
        PurchasePlanImportLinePreviewDto src, string planCode, int lineNo, Material material)
    {
        var materialCode = src.MaterialCode.Trim();

        var productName = src.ProductName?.Trim();
        if (string.IsNullOrWhiteSpace(productName))
            productName = material.MaterialName;

        var unit = src.Unit?.Trim();
        if (string.IsNullOrWhiteSpace(unit))
            unit = material.Unit ?? string.Empty;

        var line = new PurchasePlanLine
        {
            PlanCode = planCode,
            LineNo = lineNo,
            SystemName = src.SystemName,
            Category = src.Category,
            MaterialCode = materialCode,
            ProductName = productName,
            Spec = src.Spec,
            Unit = unit ?? string.Empty,
            Quantity = src.Quantity,
            KgPerUnit = src.KgPerUnit,
            LineNote = src.LineNote,
            PurchaseStatus = string.IsNullOrWhiteSpace(src.PurchaseStatus)
                ? PurchaseLineStatuses.NotPurchased
                : src.PurchaseStatus,
            StatusNote = src.StatusNote,
            BudgetUnitPrice = src.BudgetUnitPrice,
            BudgetAmount = src.BudgetAmount ?? (src.BudgetUnitPrice is decimal bup
                ? Math.Round(bup * src.Quantity, 2) : null),
            ProposalKgPerUnit = src.ProposalKgPerUnit,
            ProposalUnitPrice = src.ProposalUnitPrice,
            ProposalAmount = src.ProposalAmount ?? (src.ProposalUnitPrice is decimal pup
                ? Math.Round(pup * src.Quantity, 2) : null),
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
                ? Math.Round(prup * src.Quantity, 2) : null),
            PurchaseSupplierName = src.PurchaseSupplierName,
            PurchaseNote = src.PurchaseNote,
            SelectedUnitPrice = src.SelectedUnitPrice,
            SelectedAmount = src.SelectedAmount ?? (src.SelectedUnitPrice is decimal sup
                ? Math.Round(sup * src.Quantity, 2) : null),
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
                Amount = q.Amount ?? (q.UnitPrice is decimal up ? Math.Round(up * src.Quantity, 2) : null),
                SupplierName = q.SupplierName,
                Note = q.Note,
                Status = q.Status,
            });
        }

        return line;
    }
}
