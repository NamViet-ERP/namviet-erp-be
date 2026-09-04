using Microsoft.AspNetCore.Identity;

namespace NamViet.ERP.Modules.Identity.Infrastructure.Services;

/// <summary>
/// Dịch lỗi ASP.NET Identity sang tiếng Việt để trả trực tiếp cho người dùng
/// (provision tài khoản, reset mật khẩu ở AdminUsersController...).
/// Giữ nguyên Code gốc để client / log vẫn đối chiếu được.
/// </summary>
public class VietnameseIdentityErrorDescriber : IdentityErrorDescriber
{
    public override IdentityError DefaultError() => new()
    {
        Code = nameof(DefaultError),
        Description = "Đã xảy ra lỗi không xác định. Vui lòng thử lại.",
    };

    public override IdentityError PasswordTooShort(int length) => new()
    {
        Code = nameof(PasswordTooShort),
        Description = $"Mật khẩu phải có ít nhất {length} ký tự.",
    };

    public override IdentityError PasswordRequiresUpper() => new()
    {
        Code = nameof(PasswordRequiresUpper),
        Description = "Mật khẩu phải có ít nhất một chữ hoa (A-Z).",
    };

    public override IdentityError PasswordRequiresLower() => new()
    {
        Code = nameof(PasswordRequiresLower),
        Description = "Mật khẩu phải có ít nhất một chữ thường (a-z).",
    };

    public override IdentityError PasswordRequiresDigit() => new()
    {
        Code = nameof(PasswordRequiresDigit),
        Description = "Mật khẩu phải có ít nhất một chữ số (0-9).",
    };

    public override IdentityError PasswordRequiresNonAlphanumeric() => new()
    {
        Code = nameof(PasswordRequiresNonAlphanumeric),
        Description = "Mật khẩu phải có ít nhất một ký tự đặc biệt.",
    };

    public override IdentityError PasswordMismatch() => new()
    {
        Code = nameof(PasswordMismatch),
        Description = "Mật khẩu hiện tại không đúng.",
    };

    public override IdentityError DuplicateUserName(string userName) => new()
    {
        Code = nameof(DuplicateUserName),
        Description = $"Tên đăng nhập '{userName}' đã được sử dụng.",
    };

    public override IdentityError DuplicateEmail(string email) => new()
    {
        Code = nameof(DuplicateEmail),
        Description = $"Email '{email}' đã được sử dụng.",
    };

    public override IdentityError InvalidUserName(string? userName) => new()
    {
        Code = nameof(InvalidUserName),
        Description = $"Tên đăng nhập '{userName}' không hợp lệ.",
    };

    public override IdentityError InvalidEmail(string? email) => new()
    {
        Code = nameof(InvalidEmail),
        Description = $"Email '{email}' không hợp lệ.",
    };
}
