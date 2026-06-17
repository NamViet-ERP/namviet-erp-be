using MediatR;
using NamViet.ERP.Modules.Identity.Application.DTOs;

namespace NamViet.ERP.Modules.Identity.Application.Commands;

/// <param name="RememberMe">
/// false (default): refresh token dùng Jwt:SessionRefreshTokenDays (phiên trình duyệt).
/// true: refresh token dùng Jwt:RefreshTokenDays (ghi nhớ đăng nhập).
/// </param>
public record LoginCommand(string Email, string Password, bool RememberMe = false) : IRequest<AuthResponse>;
