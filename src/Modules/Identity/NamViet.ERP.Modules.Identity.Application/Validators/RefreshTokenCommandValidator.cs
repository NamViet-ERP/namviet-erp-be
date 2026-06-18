using FluentValidation;
using NamViet.ERP.Modules.Identity.Application.Commands;

namespace NamViet.ERP.Modules.Identity.Application.Validators;

public sealed class RefreshTokenCommandValidator : AbstractValidator<RefreshTokenCommand>
{
    public RefreshTokenCommandValidator()
    {
        RuleFor(x => x.RefreshToken)
            .NotEmpty().WithMessage("Refresh token không hợp lệ.");
    }
}
