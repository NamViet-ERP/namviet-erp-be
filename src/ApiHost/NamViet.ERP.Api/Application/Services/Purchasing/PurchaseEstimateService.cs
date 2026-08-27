using Microsoft.EntityFrameworkCore;
using NamViet.ERP.Api.Application.Dtos.Purchasing;
using NamViet.ERP.Modules.Identity.Infrastructure.Persistence;
using NamViet.ERP.Modules.Warehouse.Domain.Entities;
using NamViet.ERP.Modules.Warehouse.Infrastructure.Persistence;
using NamViet.ERP.SharedKernel.Interfaces;

namespace NamViet.ERP.Api.Application.Services.Purchasing;

public interface IPurchaseEstimateService
{
    Task<PurchaseEstimatePagedResponse> ListAsync(
        int page, int pageSize, string? projectCode, string? search, CancellationToken ct = default);

    Task<PurchaseEstimateDetailDto?> GetByIdAsync(Guid id, int? version, CancellationToken ct = default);

    Task<PurchaseEstimateDetailDto> CreateAsync(CreatePurchaseEstimateRequest request, CancellationToken ct = default);

    Task<PurchaseEstimateDetailDto?> UpdateAsync(
        Guid id, UpdatePurchaseEstimateRequest request, CancellationToken ct = default);

    Task<PurchaseEstimateShareLinkDto?> ShareLinkAsync(Guid id, CancellationToken ct = default);

    Task<PurchaseEstimateDetailDto?> RevokeShareLinkAsync(Guid id, CancellationToken ct = default);

    Task<PurchaseEstimateDetailDto?> MarkReviewedAsync(Guid id, CancellationToken ct = default);

    Task<PurchaseEstimateDetailDto?> SubmitForApprovalAsync(Guid id, CancellationToken ct = default);

    Task<PurchaseEstimateDetailDto?> ApproveLevelAsync(
        Guid id, int level, string? note, CancellationToken ct = default);

    Task<PurchaseEstimateDetailDto?> RejectAsync(
        Guid id, RejectPurchaseEstimateRequest request, CancellationToken ct = default);

    Task<ConvertPurchaseEstimateResult?> ConvertToPlanAsync(Guid id, CancellationToken ct = default);

    Task<PublicPurchaseEstimateDto?> GetPublicAsync(Guid token, CancellationToken ct = default);

    Task<PublicPurchaseEstimateDto> SubmitPublicAsync(
        Guid token, SubmitPublicPurchaseEstimateRequest request, CancellationToken ct = default);
}

public sealed partial class PurchaseEstimateService(
    WarehouseDbContext db,
    IdentityDbContext identity,
    ICurrentUser currentUser,
    IPurchasePlanCodeGenerator planCodes,
    IPurchasePlanService plans) : IPurchaseEstimateService
{
    private static readonly (int Level, string Name)[] ApprovalTemplate =
    [
        (1, "Trưởng dự án"),
    ];

    private static readonly TimeSpan ShareLinkTtl = TimeSpan.FromDays(7);

    public async Task<PurchaseEstimatePagedResponse> ListAsync(
        int page, int pageSize, string? projectCode, string? search, CancellationToken ct = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var query = db.PurchaseEstimates.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(projectCode))
            query = query.Where(e => e.ProjectCode == projectCode.Trim());
        if (!string.IsNullOrWhiteSpace(search))
        {
            var q = search.Trim().ToLowerInvariant();
            query = query.Where(e =>
                e.Code.ToLower().Contains(q) || e.Title.ToLower().Contains(q));
        }

        var total = await query.CountAsync(ct);
        var totalPages = total == 0 ? 0 : (int)Math.Ceiling(total / (double)pageSize);
        var rows = await query
            .OrderByDescending(e => e.UpdatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(e => new
            {
                e.Id,
                e.Code,
                e.ProjectCode,
                e.Title,
                e.Status,
                e.CurrentVersion,
                e.PreparerName,
                e.PreparerDate,
                e.CreatedAt,
                e.CreatedByName,
                e.UpdatedAt,
                e.UpdatedByName,
                LineCount = e.Versions
                    .Where(v => v.VersionNumber == e.CurrentVersion)
                    .SelectMany(v => v.Lines)
                    .Count(),
                FilledLineCount = e.Versions
                    .Where(v => v.VersionNumber == e.CurrentVersion)
                    .SelectMany(v => v.Lines)
                    .Count(l => l.NewQty > 0),
            })
            .ToListAsync(ct);

        var items = rows.Select(e => new PurchaseEstimateListItemDto(
            e.Id, e.Code, e.ProjectCode, e.Title, e.Status, e.CurrentVersion,
            e.FilledLineCount, e.LineCount, e.PreparerName, e.PreparerDate,
            e.CreatedAt, e.CreatedByName, e.UpdatedAt, e.UpdatedByName)).ToList();

        return new PurchaseEstimatePagedResponse(items, total, page, pageSize, totalPages);
    }

    public async Task<PurchaseEstimateDetailDto?> GetByIdAsync(
        Guid id, int? version, CancellationToken ct = default)
    {
        var header = await db.PurchaseEstimates.AsNoTracking().FirstOrDefaultAsync(e => e.Id == id, ct);
        if (header is null) return null;
        return await MapDetailAsync(header, version, ct);
    }

    public async Task<PurchaseEstimateDetailDto> CreateAsync(
        CreatePurchaseEstimateRequest request, CancellationToken ct = default)
    {
        var projectCode = request.ProjectCode.Trim();
        if (string.IsNullOrWhiteSpace(projectCode))
            throw new PurchasingConflictException("Chọn dự án.");
        var project = await db.Projects.AsNoTracking()
            .FirstOrDefaultAsync(p => p.ProjectCode == projectCode, ct)
            ?? throw new PurchasingConflictException("Dự án không tồn tại.");
        if (request.Lines is null || request.Lines.Count == 0)
            throw new PurchasingConflictException("Thêm ít nhất một dòng vật tư.");
        if (request.Lines.Any(l => l.NewQty <= 0))
            throw new PurchasingConflictException("Số lượng mỗi dòng phải lớn hơn 0.");
        if (request.Lines.Any(l => l.EstimateUnitPrice <= 0))
            throw new PurchasingConflictException("Giá dự toán mỗi dòng phải lớn hơn 0.");

        var title = string.IsNullOrWhiteSpace(request.Title)
            ? project.ProjectName
            : request.Title.Trim();
        if (string.IsNullOrWhiteSpace(title))
            title = projectCode;

        var now = DateTime.UtcNow;
        var (userId, userName) = Actor();
        var seq = await NextEstimateSequenceAsync(ct);

        var header = new PurchaseEstimate
        {
            Code = PurchaseEstimate.GenerateCode(seq),
            ProjectCode = projectCode,
            Title = title,
            Status = PurchaseEstimateStatuses.Draft,
            CurrentVersion = 1,
            CreatedById = userId,
            CreatedByName = userName,
            UpdatedById = userId,
            UpdatedByName = userName,
            CreatedAt = now,
            UpdatedAt = now,
        };
        db.PurchaseEstimates.Add(header);

        var version = NewVersion(header.Id, 1, userId, userName, now);
        db.PurchaseEstimateVersions.Add(version);

        await AddLinesAsync(version.Id, previous: null, request.Lines, now, ct);
        await db.SaveChangesAsync(ct);
        return (await GetByIdAsync(header.Id, 1, ct))!;
    }

    public async Task<PurchaseEstimateDetailDto?> UpdateAsync(
        Guid id, UpdatePurchaseEstimateRequest request, CancellationToken ct = default)
    {
        var header = await db.PurchaseEstimates.FirstOrDefaultAsync(e => e.Id == id, ct);
        if (header is null) return null;
        if (!PurchaseEstimateStatuses.CanEditLines(header.Status))
            throw new PurchasingConflictException("Chỉ sửa dòng khi dự toán chưa gửi duyệt.");
        if (request.Lines is null || request.Lines.Count == 0)
            throw new PurchasingConflictException("Dự toán phải còn ít nhất một dòng vật tư.");
        if (request.Lines.Any(l => l.NewQty <= 0))
            throw new PurchasingConflictException("Số lượng mỗi dòng phải lớn hơn 0.");
        if (request.Lines.Any(l => l.EstimateUnitPrice <= 0))
            throw new PurchasingConflictException("Giá dự toán mỗi dòng phải lớn hơn 0.");

        if (string.Equals(header.Status, PurchaseEstimateStatuses.Reviewed, StringComparison.OrdinalIgnoreCase)
            || string.Equals(header.Status, PurchaseEstimateStatuses.Rejected, StringComparison.OrdinalIgnoreCase))
        {
            header.Status = PurchaseEstimateStatuses.Draft;
            header.ReviewedById = null;
            header.ReviewedByName = null;
            header.ReviewedAt = null;
        }

        await AppendVersionAsync(header, request.Title, request.Lines, ct);
        return await GetByIdAsync(id, header.CurrentVersion, ct);
    }

    public async Task<PurchaseEstimateShareLinkDto?> ShareLinkAsync(Guid id, CancellationToken ct = default)
    {
        var header = await db.PurchaseEstimates.FirstOrDefaultAsync(e => e.Id == id, ct);
        if (header is null) return null;

        /* Link xem Ver mới nhất — không đổi trạng thái phiếu */
        header.ShareToken = Guid.NewGuid();
        header.ShareTokenExpiresAt = DateTime.UtcNow.Add(ShareLinkTtl);
        Touch(header);
        await db.SaveChangesAsync(ct);
        return new PurchaseEstimateShareLinkDto(
            header.ShareToken.Value.ToString("N"),
            $"/estimates/fill/{header.ShareToken.Value:N}",
            header.ShareTokenExpiresAt.Value);
    }

    public async Task<PurchaseEstimateDetailDto?> RevokeShareLinkAsync(Guid id, CancellationToken ct = default)
    {
        var header = await db.PurchaseEstimates.FirstOrDefaultAsync(e => e.Id == id, ct);
        if (header is null) return null;
        header.ShareToken = null;
        header.ShareTokenExpiresAt = null;
        Touch(header);
        await db.SaveChangesAsync(ct);
        return await GetByIdAsync(id, null, ct);
    }

    public async Task<PurchaseEstimateDetailDto?> MarkReviewedAsync(Guid id, CancellationToken ct = default)
    {
        var header = await db.PurchaseEstimates.FirstOrDefaultAsync(e => e.Id == id, ct);
        if (header is null) return null;
        if (header.Status != PurchaseEstimateStatuses.Submitted)
            throw new PurchasingConflictException("Chỉ xác nhận kiểm tra khi đã nhận dự toán.");

        var (_, userName) = Actor();
        header.Status = PurchaseEstimateStatuses.Reviewed;
        header.ReviewedById = currentUser.Id;
        header.ReviewedByName = userName;
        header.ReviewedAt = DateTime.UtcNow;
        Touch(header);
        await db.SaveChangesAsync(ct);
        return await GetByIdAsync(id, null, ct);
    }

    public async Task<PurchaseEstimateDetailDto?> SubmitForApprovalAsync(Guid id, CancellationToken ct = default)
    {
        var header = await db.PurchaseEstimates.FirstOrDefaultAsync(e => e.Id == id, ct);
        if (header is null) return null;
        var canSubmit =
            header.Status is PurchaseEstimateStatuses.Draft
                or PurchaseEstimateStatuses.Rejected
                or PurchaseEstimateStatuses.Reviewed
                or PurchaseEstimateStatuses.Submitted
                or PurchaseEstimateStatuses.Shared;
        if (!canSubmit)
            throw new PurchasingConflictException("Chỉ gửi duyệt khi dự toán còn nháp hoặc bị từ chối.");

        var ver = await db.PurchaseEstimateVersions.AsNoTracking()
            .FirstOrDefaultAsync(v => v.EstimateId == id && v.VersionNumber == header.CurrentVersion, ct)
            ?? throw new PurchasingConflictException("Không tìm thấy version hiện hành.");
        var hasQty = await db.PurchaseEstimateLines.AsNoTracking()
            .AnyAsync(l => l.VersionId == ver.Id && l.NewQty > 0, ct);
        if (!hasQty)
            throw new PurchasingConflictException("Cần ít nhất một dòng có số lượng trước khi gửi duyệt.");
        var hasPrice = await db.PurchaseEstimateLines.AsNoTracking()
            .AnyAsync(l => l.VersionId == ver.Id && l.EstimateUnitPrice > 0, ct);
        if (!hasPrice)
            throw new PurchasingConflictException("Cần ít nhất một dòng có giá dự toán trước khi gửi duyệt.");
        var incomplete = await db.PurchaseEstimateLines.AsNoTracking()
            .AnyAsync(l => l.VersionId == ver.Id && (l.NewQty <= 0 || l.EstimateUnitPrice <= 0), ct);
        if (incomplete)
            throw new PurchasingConflictException("Mỗi dòng phải có số lượng và giá dự toán lớn hơn 0 trước khi gửi duyệt.");

        var now = DateTime.UtcNow;
        var existing = await db.PurchaseEstimateApprovals.Where(a => a.EstimateId == id).ToListAsync(ct);
        db.PurchaseEstimateApprovals.RemoveRange(existing);
        foreach (var (level, name) in ApprovalTemplate)
        {
            db.PurchaseEstimateApprovals.Add(new PurchaseEstimateApproval
            {
                EstimateId = id,
                Level = level,
                Name = name,
                Status = "pending",
                CreatedAt = now,
                UpdatedAt = now,
            });
        }

        header.Status = PurchaseEstimateStatuses.PendingApproval;
        header.ShareToken = null;
        header.ShareTokenExpiresAt = null;
        Touch(header);
        await db.SaveChangesAsync(ct);
        return await GetByIdAsync(id, null, ct);
    }

    public async Task<PurchaseEstimateDetailDto?> ApproveLevelAsync(
        Guid id, int level, string? note, CancellationToken ct = default)
    {
        EnsureCanDecide(level);
        return await DecideApprovalAsync(id, new DecidePurchaseEstimateApprovalRequest(level, "approved", note), ct);
    }

    public async Task<PurchaseEstimateDetailDto?> RejectAsync(
        Guid id, RejectPurchaseEstimateRequest request, CancellationToken ct = default)
    {
        var pending = await db.PurchaseEstimateApprovals.AsNoTracking()
            .Where(a => a.EstimateId == id && a.Status == "pending")
            .OrderBy(a => a.Level)
            .FirstOrDefaultAsync(ct)
            ?? throw new PurchasingConflictException("Không còn cấp nào chờ duyệt.");
        EnsureCanDecide(pending.Level);
        return await DecideApprovalAsync(
            id, new DecidePurchaseEstimateApprovalRequest(pending.Level, "rejected", request.Note), ct);
    }

    private void EnsureCanDecide(int level)
    {
        var key = level <= 1
            ? "purchasing.estimates.approve_l1"
            : "purchasing.estimates.approve_l2";
        if (!currentUser.HasPermission(key))
            throw new PurchasingConflictException("Bạn không có quyền duyệt cấp này.");
    }

    private async Task<PurchaseEstimateDetailDto?> DecideApprovalAsync(
        Guid id, DecidePurchaseEstimateApprovalRequest request, CancellationToken ct)
    {
        var header = await db.PurchaseEstimates.FirstOrDefaultAsync(e => e.Id == id, ct);
        if (header is null) return null;
        if (header.Status != PurchaseEstimateStatuses.PendingApproval)
            throw new PurchasingConflictException("Dự toán không đang chờ duyệt.");

        var decision = request.Decision.Trim().ToLowerInvariant();
        if (decision is not ("approved" or "rejected"))
            throw new PurchasingConflictException("Quyết định không hợp lệ.");

        var row = await db.PurchaseEstimateApprovals
            .FirstOrDefaultAsync(a => a.EstimateId == id && a.Level == request.Level, ct)
            ?? throw new PurchasingConflictException("Không tìm thấy cấp duyệt.");
        if (row.Status != "pending")
            throw new PurchasingConflictException("Cấp này đã xử lý.");

        var priorPending = await db.PurchaseEstimateApprovals
            .AnyAsync(a => a.EstimateId == id && a.Level < request.Level && a.Status == "pending", ct);
        if (priorPending)
            throw new PurchasingConflictException("Cần duyệt cấp trước đó trước.");

        var (_, userName) = Actor();
        var now = DateTime.UtcNow;
        row.Status = decision;
        row.ActionById = currentUser.Id;
        row.ActionByName = userName;
        row.ActionAt = now;
        row.Note = string.IsNullOrWhiteSpace(request.Note) ? null : request.Note.Trim();
        row.UpdatedAt = now;

        if (decision == "rejected")
            header.Status = PurchaseEstimateStatuses.Rejected;
        else if (!await db.PurchaseEstimateApprovals.AnyAsync(
                     a => a.EstimateId == id && a.Id != row.Id && a.Status != "approved", ct))
            header.Status = PurchaseEstimateStatuses.Approved;

        Touch(header);
        await db.SaveChangesAsync(ct);
        return await GetByIdAsync(id, null, ct);
    }

    public async Task<ConvertPurchaseEstimateResult?> ConvertToPlanAsync(Guid id, CancellationToken ct = default)
    {
        var header = await db.PurchaseEstimates.FirstOrDefaultAsync(e => e.Id == id, ct);
        if (header is null) return null;
        if (header.Status != PurchaseEstimateStatuses.Approved)
            throw new PurchasingConflictException("Chỉ tạo KHMH khi dự toán đã duyệt.");
        if (!string.IsNullOrWhiteSpace(header.ConvertedPlanCode))
            return new ConvertPurchaseEstimateResult(header.ConvertedPlanCode);

        var ver = await db.PurchaseEstimateVersions.AsNoTracking()
            .FirstOrDefaultAsync(v => v.EstimateId == id && v.VersionNumber == header.CurrentVersion, ct)
            ?? throw new PurchasingConflictException("Không tìm thấy version hiện hành.");
        var lines = await db.PurchaseEstimateLines.AsNoTracking()
            .Where(l => l.VersionId == ver.Id && l.NewQty > 0)
            .ToListAsync(ct);
        var proposals = await db.PurchaseEstimateProposals.AsNoTracking()
            .Where(p => p.EstimateId == id && p.Qty > 0)
            .ToListAsync(ct);
        if (lines.Count == 0 && proposals.Count == 0)
            throw new PurchasingConflictException("Dự toán chưa có dòng số lượng để chuyển KHMH.");

        var now = DateTime.UtcNow;
        var (userId, userName) = Actor();
        var code = await planCodes.NextAsync(PurchasePlanKinds.Plan, ct);
        var plan = new PurchasePlan
        {
            Code = code,
            ProjectCode = header.ProjectCode,
            Kind = PurchasePlanKinds.Plan,
            Status = PurchasePlanStatuses.Draft,
            Note = $"Từ dự toán {header.Code}",
            CreatedById = userId,
            CreatedByName = userName,
            CreatedAt = now,
            UpdatedAt = now,
        };

        var lineNo = 0;
        foreach (var line in lines)
        {
            lineNo++;
            plan.Lines.Add(new PurchasePlanLine
            {
                PlanCode = code,
                LineNo = lineNo,
                Category = line.CategoryName,
                MaterialCode = line.MaterialCode,
                ProductName = line.MaterialName,
                Unit = string.IsNullOrWhiteSpace(line.Unit) ? "cái" : line.Unit,
                Quantity = line.NewQty,
                BudgetUnitPrice = line.EstimateUnitPrice > 0 ? line.EstimateUnitPrice : null,
                PurchaseStatus = PurchaseLineStatuses.NotPurchased,
            });
        }

        foreach (var p in proposals)
        {
            lineNo++;
            plan.Lines.Add(new PurchasePlanLine
            {
                PlanCode = code,
                LineNo = lineNo,
                Category = "Đề xuất mới",
                MaterialCode = null,
                ProductName = p.Name,
                Spec = p.Spec,
                Unit = string.IsNullOrWhiteSpace(p.Unit) ? "cái" : p.Unit,
                Quantity = p.Qty,
                LineNote = p.Note,
                PurchaseStatus = PurchaseLineStatuses.NotPurchased,
            });
        }

        plans.RecalcTotals(plan);
        db.PurchasePlans.Add(plan);
        header.ConvertedPlanCode = code;
        Touch(header);
        await db.SaveChangesAsync(ct);
        return new ConvertPurchaseEstimateResult(code);
    }

    public async Task<PublicPurchaseEstimateDto?> GetPublicAsync(Guid token, CancellationToken ct = default)
    {
        var header = await db.PurchaseEstimates.AsNoTracking()
            .FirstOrDefaultAsync(e => e.ShareToken == token, ct);
        if (header is null) return null;
        EnsureShareTokenAlive(header);
        return await MapPublicAsync(header, ct);
    }

    public async Task<PublicPurchaseEstimateDto> SubmitPublicAsync(
        Guid token, SubmitPublicPurchaseEstimateRequest request, CancellationToken ct = default)
    {
        var header = await db.PurchaseEstimates.FirstOrDefaultAsync(e => e.ShareToken == token, ct)
            ?? throw new PurchasingConflictException("Không tìm thấy dự toán.");
        EnsureShareTokenAlive(header);
        if (!PurchaseEstimateStatuses.CanFill(header.Status))
            throw new PurchasingConflictException("Dự toán này đã được gửi hoặc đã khóa điền.");

        var rawName = request.PreparerName.Trim();
        if (string.IsNullOrWhiteSpace(rawName))
            throw new PurchasingConflictException("Chọn người lập dự toán.");
        var preparers = await ListPreparersAsync(ct);
        if (preparers.Count == 0)
            throw new PurchasingConflictException("Chưa có người được gán vai trò Phòng Dự án / Sản xuất.");
        var matched = preparers.FirstOrDefault(p =>
            string.Equals(FormatPreparerLabel(p), rawName, StringComparison.OrdinalIgnoreCase)
            || string.Equals(p.Name, rawName, StringComparison.OrdinalIgnoreCase)
            || string.Equals(p.Id.ToString(), rawName, StringComparison.OrdinalIgnoreCase));
        if (matched is null)
            throw new PurchasingConflictException("Người lập phải thuộc Phòng Dự án hoặc Phòng Sản xuất.");
        var name = FormatPreparerLabel(matched);

        var qtyByCode = request.Lines
            .Where(l => !string.IsNullOrWhiteSpace(l.MaterialCode))
            .GroupBy(l => l.MaterialCode.Trim(), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.Last().NewQty, StringComparer.OrdinalIgnoreCase);
        var proposals = (request.Proposals ?? [])
            .Where(p => !string.IsNullOrWhiteSpace(p.Name) && p.Qty > 0)
            .ToList();
        if (qtyByCode.Values.All(q => q <= 0) && proposals.Count == 0)
            throw new PurchasingConflictException("Chưa có mặt hàng nào được điền số lượng hoặc đề xuất.");

        var prev = await db.PurchaseEstimateVersions
            .Include(v => v.Lines)
            .FirstOrDefaultAsync(v => v.EstimateId == header.Id && v.VersionNumber == header.CurrentVersion, ct);
        var merged = (prev?.Lines ?? [])
            .Select(l => new PurchaseEstimateLineInput(
                l.MaterialCode,
                qtyByCode.TryGetValue(l.MaterialCode, out var qty) ? qty : l.NewQty,
                l.EstimateUnitPrice))
            .ToList();

        await AppendVersionAsync(header, null, merged, ct, actorName: name, actorId: Guid.Empty, persist: false);

        var now = DateTime.UtcNow;
        var oldProps = await db.PurchaseEstimateProposals.Where(p => p.EstimateId == header.Id).ToListAsync(ct);
        db.PurchaseEstimateProposals.RemoveRange(oldProps);
        foreach (var p in proposals)
        {
            db.PurchaseEstimateProposals.Add(new PurchaseEstimateProposal
            {
                EstimateId = header.Id,
                Name = p.Name.Trim(),
                Spec = string.IsNullOrWhiteSpace(p.Spec) ? null : p.Spec.Trim(),
                Unit = string.IsNullOrWhiteSpace(p.Unit) ? "cái" : p.Unit.Trim(),
                Qty = p.Qty,
                Note = string.IsNullOrWhiteSpace(p.Note) ? null : p.Note.Trim(),
                CreatedAt = now,
                UpdatedAt = now,
            });
        }

        header.Status = PurchaseEstimateStatuses.Submitted;
        header.PreparerName = name;
        header.PreparerDate = now;
        await db.SaveChangesAsync(ct);
        return (await GetPublicAsync(token, ct))!;
    }
}
