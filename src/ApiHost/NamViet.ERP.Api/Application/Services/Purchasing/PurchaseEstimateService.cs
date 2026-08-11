using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using NamViet.ERP.Api.Application.Dtos.Purchasing;
using NamViet.ERP.Api.Application.Mappers.Wms;
using NamViet.ERP.Modules.Identity.Infrastructure.Persistence;
using NamViet.ERP.Modules.Warehouse.Application.Helpers;
using NamViet.ERP.Modules.Warehouse.Domain.Entities;
using NamViet.ERP.Modules.Warehouse.Infrastructure.Persistence;
using NamViet.ERP.SharedKernel.Authorization;
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

    Task<PurchaseEstimateDetailDto?> MarkReviewedAsync(Guid id, CancellationToken ct = default);

    Task<PurchaseEstimateDetailDto?> SubmitForApprovalAsync(Guid id, CancellationToken ct = default);

    Task<PurchaseEstimateDetailDto?> DecideApprovalAsync(
        Guid id, DecidePurchaseEstimateApprovalRequest request, CancellationToken ct = default);

    Task<ConvertPurchaseEstimateResult?> ConvertToPlanAsync(Guid id, CancellationToken ct = default);

    Task<PublicPurchaseEstimateDto?> GetPublicAsync(Guid token, CancellationToken ct = default);

    Task<PublicPurchaseEstimateDto> SubmitPublicAsync(
        Guid token, SubmitPublicPurchaseEstimateRequest request, CancellationToken ct = default);
}

public sealed class PurchaseEstimateService(
    WarehouseDbContext db,
    IdentityDbContext identity,
    ICurrentUser currentUser,
    IPurchasePlanCodeGenerator planCodes,
    IPurchasePlanService plans) : IPurchaseEstimateService
{
    private static readonly (int Level, string Name)[] ApprovalTemplate =
    [
        (1, "Trưởng nhóm Mua hàng"),
        (2, "Trưởng phòng Cung ứng"),
    ];

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
        if (string.IsNullOrWhiteSpace(request.Title))
            throw new PurchasingConflictException("Nhập tiêu đề dự toán.");
        if (!await db.Projects.AnyAsync(p => p.ProjectCode == projectCode, ct))
            throw new PurchasingConflictException("Dự án không tồn tại.");

        var now = DateTime.UtcNow;
        var (userId, userName) = Actor();
        var seq = await NextEstimateSequenceAsync(ct);

        var header = new PurchaseEstimate
        {
            Code = PurchaseEstimate.GenerateCode(seq),
            ProjectCode = projectCode,
            Title = request.Title.Trim(),
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

        var inputs = request.Lines is { Count: > 0 }
            ? request.Lines
            : await CatalogInputsAsync(ct);
        await AddLinesAsync(version.Id, previous: null, inputs, now, ct);
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

        if (string.Equals(header.Status, PurchaseEstimateStatuses.Reviewed, StringComparison.OrdinalIgnoreCase))
        {
            header.Status = PurchaseEstimateStatuses.Submitted;
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
        if (!PurchaseEstimateStatuses.CanFill(header.Status)
            && header.Status != PurchaseEstimateStatuses.Submitted)
            throw new PurchasingConflictException("Không lấy link khi dự toán đã khóa điền.");

        header.ShareToken ??= Guid.NewGuid();
        if (header.Status == PurchaseEstimateStatuses.Draft)
            header.Status = PurchaseEstimateStatuses.Shared;
        Touch(header);
        await db.SaveChangesAsync(ct);
        return new PurchaseEstimateShareLinkDto(header.ShareToken.Value.ToString("N"), $"/estimates/fill/{header.ShareToken.Value:N}");
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
        if (header.Status != PurchaseEstimateStatuses.Reviewed)
            throw new PurchasingConflictException("Cần double check trước khi gửi duyệt.");

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
        Touch(header);
        await db.SaveChangesAsync(ct);
        return await GetByIdAsync(id, null, ct);
    }

    public async Task<PurchaseEstimateDetailDto?> DecideApprovalAsync(
        Guid id, DecidePurchaseEstimateApprovalRequest request, CancellationToken ct = default)
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
        return await MapPublicAsync(header, ct);
    }

    public async Task<PublicPurchaseEstimateDto> SubmitPublicAsync(
        Guid token, SubmitPublicPurchaseEstimateRequest request, CancellationToken ct = default)
    {
        var header = await db.PurchaseEstimates.FirstOrDefaultAsync(e => e.ShareToken == token, ct)
            ?? throw new PurchasingConflictException("Không tìm thấy dự toán.");
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
                qtyByCode.TryGetValue(l.MaterialCode, out var qty) ? qty : l.NewQty))
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

    private async Task AppendVersionAsync(
        PurchaseEstimate header,
        string? title,
        IReadOnlyList<PurchaseEstimateLineInput> lines,
        CancellationToken ct,
        string? actorName = null,
        Guid? actorId = null,
        bool persist = true)
    {
        var prev = await db.PurchaseEstimateVersions
            .Include(v => v.Lines)
            .FirstOrDefaultAsync(v => v.EstimateId == header.Id && v.VersionNumber == header.CurrentVersion, ct);

        var now = DateTime.UtcNow;
        var userId = actorId ?? Actor().Id;
        var userName = actorName ?? Actor().Name;
        var nextNo = header.CurrentVersion + 1;

        if (!string.IsNullOrWhiteSpace(title))
            header.Title = title.Trim();
        header.CurrentVersion = nextNo;
        header.UpdatedById = userId;
        header.UpdatedByName = userName;
        header.UpdatedAt = now;

        var version = NewVersion(header.Id, nextNo, userId, userName, now);
        db.PurchaseEstimateVersions.Add(version);
        await AddLinesAsync(version.Id, prev, lines, now, ct);
        if (persist)
            await db.SaveChangesAsync(ct);
    }

    private async Task<IReadOnlyList<PurchaseEstimateLineInput>> CatalogInputsAsync(CancellationToken ct)
    {
        var codes = await db.Materials.AsNoTracking()
            .Where(m => m.IsActive)
            .OrderBy(m => m.MaterialCode)
            .Select(m => m.MaterialCode)
            .ToListAsync(ct);
        if (codes.Count == 0)
            throw new PurchasingConflictException("Chưa có vật tư trong danh mục để tạo mẫu.");
        return codes.Select(c => new PurchaseEstimateLineInput(c, 0)).ToList();
    }

    private async Task AddLinesAsync(
        Guid versionId,
        PurchaseEstimateVersion? previous,
        IReadOnlyList<PurchaseEstimateLineInput> inputs,
        DateTime now,
        CancellationToken ct)
    {
        var codes = inputs
            .Select(l => l.MaterialCode.Trim())
            .Where(c => c.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        if (codes.Count == 0)
            throw new PurchasingConflictException("Chọn ít nhất một vật tư.");

        var materials = await db.Materials.AsNoTracking()
            .Where(m => codes.Contains(m.MaterialCode))
            .ToDictionaryAsync(m => m.MaterialCode, StringComparer.OrdinalIgnoreCase, ct);
        var cats = await db.MaterialCategories.AsNoTracking().ToDictionaryAsync(c => c.Id, ct);

        var prevByCode = previous?.Lines
            .GroupBy(l => l.MaterialCode, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First().NewQty, StringComparer.OrdinalIgnoreCase)
            ?? new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase);

        foreach (var input in inputs)
        {
            var code = input.MaterialCode.Trim();
            if (code.Length == 0) continue;
            if (!materials.TryGetValue(code, out var mat))
                throw new PurchasingConflictException($"Không tìm thấy vật tư {code}.");
            if (input.NewQty < 0)
                throw new PurchasingConflictException($"Số lượng mới của {code} không hợp lệ.");

            prevByCode.TryGetValue(code, out var oldQty);
            cats.TryGetValue(mat.CategoryId, out var leaf);
            var path = MaterialCategoryHelper.BuildPath(leaf, cats);
            var categoryName = path.NhomVatTu ?? path.DanhMuc ?? leaf?.Name;

            db.PurchaseEstimateLines.Add(new PurchaseEstimateLine
            {
                VersionId = versionId,
                MaterialCode = mat.MaterialCode,
                MaterialName = mat.MaterialName,
                CategoryName = categoryName,
                Unit = mat.Unit,
                OldQty = oldQty,
                NewQty = input.NewQty,
                CreatedAt = now,
                UpdatedAt = now,
            });
        }
    }

    private async Task<PurchaseEstimateDetailDto> MapDetailAsync(
        PurchaseEstimate header, int? version, CancellationToken ct)
    {
        var versions = await db.PurchaseEstimateVersions.AsNoTracking()
            .Where(v => v.EstimateId == header.Id)
            .OrderBy(v => v.VersionNumber)
            .Select(v => new PurchaseEstimateVersionSummaryDto(v.Id, v.VersionNumber, v.CreatedAt, v.UpdatedByName))
            .ToListAsync(ct);

        var viewed = version is > 0 ? version.Value : header.CurrentVersion;
        var ver = await db.PurchaseEstimateVersions.AsNoTracking()
            .FirstOrDefaultAsync(v => v.EstimateId == header.Id && v.VersionNumber == viewed, ct);
        var lines = ver is null
            ? []
            : await db.PurchaseEstimateLines.AsNoTracking()
                .Where(l => l.VersionId == ver.Id)
                .OrderBy(l => l.CategoryName).ThenBy(l => l.MaterialName)
                .Select(l => new PurchaseEstimateLineDto(
                    l.Id, l.MaterialCode, l.MaterialName, l.CategoryName, l.Unit, l.OldQty, l.NewQty))
                .ToListAsync(ct);

        var proposals = await db.PurchaseEstimateProposals.AsNoTracking()
            .Where(p => p.EstimateId == header.Id)
            .OrderBy(p => p.Name)
            .Select(p => new PurchaseEstimateProposalDto(p.Id, p.Name, p.Spec, p.Unit, p.Qty, p.Note))
            .ToListAsync(ct);
        var approvals = await db.PurchaseEstimateApprovals.AsNoTracking()
            .Where(a => a.EstimateId == header.Id)
            .OrderBy(a => a.Level)
            .Select(a => new PurchaseEstimateApprovalDto(
                a.Id, a.Level, a.Name, a.Status, a.ActionByName, a.ActionAt, a.Note))
            .ToListAsync(ct);

        return new PurchaseEstimateDetailDto(
            header.Id, header.Code, header.ProjectCode, header.Title, header.Status,
            header.CurrentVersion, viewed, null,
            header.PreparerName, header.PreparerDate, header.ReviewedByName, header.ReviewedAt,
            header.ConvertedPlanCode,
            header.CreatedAt, header.CreatedByName, header.UpdatedAt, header.UpdatedByName,
            versions, lines, proposals, approvals);
    }

    private async Task<PublicPurchaseEstimateDto> MapPublicAsync(PurchaseEstimate header, CancellationToken ct)
    {
        var projectName = await db.Projects.AsNoTracking()
            .Where(p => p.ProjectCode == header.ProjectCode)
            .Select(p => p.ProjectName)
            .FirstOrDefaultAsync(ct);
        var ver = await db.PurchaseEstimateVersions.AsNoTracking()
            .FirstOrDefaultAsync(v => v.EstimateId == header.Id && v.VersionNumber == header.CurrentVersion, ct);
        var lines = ver is null
            ? []
            : await db.PurchaseEstimateLines.AsNoTracking()
                .Where(l => l.VersionId == ver.Id)
                .OrderBy(l => l.CategoryName).ThenBy(l => l.MaterialName)
                .Select(l => new PublicPurchaseEstimateLineDto(
                    l.MaterialCode, l.MaterialName, l.CategoryName, l.Unit, l.NewQty))
                .ToListAsync(ct);
        var proposals = await db.PurchaseEstimateProposals.AsNoTracking()
            .Where(p => p.EstimateId == header.Id)
            .Select(p => new PurchaseEstimateProposalDto(p.Id, p.Name, p.Spec, p.Unit, p.Qty, p.Note))
            .ToListAsync(ct);
        var canFill = PurchaseEstimateStatuses.CanFill(header.Status);
        var preparers = canFill
            ? await ListPreparersAsync(ct)
            : Array.Empty<PublicPurchaseEstimatePreparerDto>();
        return new PublicPurchaseEstimateDto(
            header.Code, header.Title, header.ProjectCode, projectName, header.Status,
            canFill,
            header.PreparerName, header.PreparerDate, lines, proposals, preparers);
    }

    private async Task<IReadOnlyList<PublicPurchaseEstimatePreparerDto>> ListPreparersAsync(CancellationToken ct)
    {
        var roleNames = new[] { RolePermissionTemplates.ProjectRole, RolePermissionTemplates.ProductionRole };
        var rows = await (
            from u in identity.Users.AsNoTracking()
            join ur in identity.Set<IdentityUserRole<Guid>>().AsNoTracking() on u.Id equals ur.UserId
            join r in identity.Roles.AsNoTracking() on ur.RoleId equals r.Id
            where u.DeletedAt == null && u.IsActive
                  && r.Name != null && roleNames.Contains(r.Name)
            orderby r.Name, u.Name
            select new { u.Id, u.Name, Role = r.Name! }
        ).ToListAsync(ct);

        return rows
            .Select(x => new PublicPurchaseEstimatePreparerDto(
                x.Id, x.Name, x.Role, WmsMapper.ToClientRoleLabel(x.Role)))
            .ToList();
    }

    private static string FormatPreparerLabel(PublicPurchaseEstimatePreparerDto p) =>
        $"{p.Name} - {p.RoleLabel}";

    private async Task<int> NextEstimateSequenceAsync(CancellationToken ct)
    {
        var codes = await db.PurchaseEstimates.IgnoreQueryFilters()
            .Select(e => e.Code)
            .ToListAsync(ct);
        var next = 1;
        foreach (var code in codes)
        {
            if (code.Length > 3
                && code.StartsWith("DT-", StringComparison.OrdinalIgnoreCase)
                && int.TryParse(code.AsSpan(3), out var n))
                next = Math.Max(next, n + 1);
        }
        return next;
    }

    private static PurchaseEstimateVersion NewVersion(
        Guid estimateId, int number, Guid userId, string userName, DateTime now) =>
        new()
        {
            EstimateId = estimateId,
            VersionNumber = number,
            UpdatedById = userId,
            UpdatedByName = userName,
            CreatedAt = now,
            UpdatedAt = now,
        };

    private void Touch(PurchaseEstimate header)
    {
        var (userId, userName) = Actor();
        header.UpdatedById = userId;
        header.UpdatedByName = userName;
        header.UpdatedAt = DateTime.UtcNow;
    }

    private (Guid Id, string Name) Actor()
    {
        var name = string.IsNullOrWhiteSpace(currentUser.Name) ? currentUser.Email : currentUser.Name;
        if (string.IsNullOrWhiteSpace(name)) name = "Hệ thống";
        return (currentUser.IsAuthenticated ? currentUser.Id : Guid.Empty, name);
    }
}
