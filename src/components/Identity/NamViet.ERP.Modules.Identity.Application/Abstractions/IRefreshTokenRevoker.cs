namespace NamViet.ERP.Modules.Identity.Application.Abstractions;

/// <summary>
/// Thu hồi refresh token khi tài khoản bị reset mật khẩu / khóa,
/// để phiên cũ không thể tiếp tục gia hạn access token.
/// </summary>
public interface IRefreshTokenRevoker
{
    /// <summary>Revoke mọi refresh token còn hiệu lực của user. Trả về số token bị thu hồi.</summary>
    Task<int> RevokeAllForUserAsync(Guid userId, CancellationToken ct = default);
}
