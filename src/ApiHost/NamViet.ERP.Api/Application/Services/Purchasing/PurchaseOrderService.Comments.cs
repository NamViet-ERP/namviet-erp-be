using Microsoft.EntityFrameworkCore;
using NamViet.ERP.Api.Application.Dtos.Purchasing;
using NamViet.ERP.Api.Application.Mappers.Purchasing;
using NamViet.ERP.Api.Application.Services.Shared;
using NamViet.ERP.Api.Services.Storage;
using NamViet.ERP.Modules.Warehouse.Domain.Entities;
using NamViet.ERP.Modules.Warehouse.Infrastructure.Persistence;
using NamViet.ERP.SharedKernel.Interfaces;
using System.Text.Json;
using NamViet.ERP.SharedKernel.Common;

namespace NamViet.ERP.Api.Application.Services.Purchasing;

public sealed partial class PurchaseOrderService
{
    public async Task<IReadOnlyList<PurchaseOrderCommentDto>?> ListCommentsAsync(
        string code, CancellationToken ct = default)
    {
        var poCode = code.Trim();
        var exists = await db.PurchaseOrders.AsNoTracking().AnyAsync(o => o.Code == poCode, ct);
        if (!exists) return null;

        var rows = await db.PurchaseOrderComments.AsNoTracking()
            .Where(c => c.PoCode == poCode)
            .OrderBy(c => c.CreatedAt)
            .ToListAsync(ct);

        return rows.Select(ToCommentDto).ToList();
    }

    public async Task<PurchaseOrderCommentDto?> CreateCommentAsync(
        string code, CreatePurchaseOrderCommentRequest request, CancellationToken ct = default)
    {
        var poCode = code.Trim();
        var order = await db.PurchaseOrders.FirstOrDefaultAsync(o => o.Code == poCode, ct);
        if (order is null) return null;

        var body = NormalizeCommentBody(request.Body);
        if (body is null)
            throw new DomainRuleException("Nội dung bình luận không được để trống.");

        if (!currentUser.IsAuthenticated)
            throw new DomainRuleException("Cần đăng nhập để bình luận.");

        var comment = new PurchaseOrderComment
        {
            PoCode = poCode,
            AuthorUserId = currentUser.Id,
            AuthorName = string.IsNullOrWhiteSpace(currentUser.Name) ? currentUser.Email : currentUser.Name.Trim(),
            Body = body,
            CreatedAt = DateTime.UtcNow,
        };
        db.PurchaseOrderComments.Add(comment);
        order.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);

        await activity.WriteAsync(new ActivityEventWriteRequest(
            ActivityModules.Purchasing,
            ActivityActions.PoCommented,
            $"Bình luận đơn đặt hàng [{poCode}]",
            ActivityCategories.Order,
            EntityType: "purchase_order",
            EntityCode: poCode), ct);

        return ToCommentDto(comment);
    }

    public async Task<PurchaseOrderCommentDto?> UpdateCommentAsync(
        string code, Guid commentId, UpdatePurchaseOrderCommentRequest request, CancellationToken ct = default)
    {
        var poCode = code.Trim();
        var order = await db.PurchaseOrders.FirstOrDefaultAsync(o => o.Code == poCode, ct);
        if (order is null) return null;

        var comment = await db.PurchaseOrderComments
            .FirstOrDefaultAsync(c => c.PoCode == poCode && c.Id == commentId, ct);
        if (comment is null) return null;

        if (!CanManageComment(comment))
            throw new UnauthorizedAccessException("Chỉ được sửa bình luận của chính mình.");

        var body = NormalizeCommentBody(request.Body);
        if (body is null)
            throw new DomainRuleException("Nội dung bình luận không được để trống.");

        comment.Body = body;
        comment.UpdatedAt = DateTime.UtcNow;
        order.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);

        return ToCommentDto(comment);
    }

    public async Task<bool> DeleteCommentAsync(string code, Guid commentId, CancellationToken ct = default)
    {
        var poCode = code.Trim();
        var order = await db.PurchaseOrders.FirstOrDefaultAsync(o => o.Code == poCode, ct);
        if (order is null) return false;

        var comment = await db.PurchaseOrderComments
            .FirstOrDefaultAsync(c => c.PoCode == poCode && c.Id == commentId, ct);
        if (comment is null) return false;

        var isAuthor = CanManageComment(comment);
        var canModerate = currentUser.HasPermission("purchasing.orders.update");
        if (!isAuthor && !canModerate)
            throw new UnauthorizedAccessException("Chỉ được xóa bình luận của chính mình.");

        comment.DeletedAt = DateTime.UtcNow;
        order.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        return true;
    }

    private bool CanManageComment(PurchaseOrderComment comment) =>
        currentUser.IsAuthenticated && comment.AuthorUserId == currentUser.Id;

    private static string? NormalizeCommentBody(string? raw)
    {
        var body = (raw ?? string.Empty).Trim();
        if (body.Length == 0) return null;
        if (body.Length > 4000) body = body[..4000];
        return body;
    }

    private static PurchaseOrderCommentDto ToCommentDto(PurchaseOrderComment c) =>
        new(c.Id, c.PoCode, c.AuthorUserId, c.AuthorName, c.Body, c.CreatedAt, c.UpdatedAt);
}
