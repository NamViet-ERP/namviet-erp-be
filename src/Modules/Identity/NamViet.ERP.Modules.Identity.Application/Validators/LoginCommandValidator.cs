using FluentValidation;
using NamViet.ERP.Modules.Identity.Application.Commands;

namespace NamViet.ERP.Modules.Identity.Application.Validators;

public sealed class LoginCommandValidator : AbstractValidator<LoginCommand>
{
    public LoginCommandValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("Vui lòng nhập email hoặc tên đăng nhập.");

        RuleFor(x => x.Password)
            .NotEmpty().WithMessage("Vui lòng nhập mật khẩu.");
    }
}
