using FluentValidation;
using NamViet.ERP.Api.Application.Dtos.Hr;

namespace NamViet.ERP.Api.Application.Validators;

public sealed class EmployeeOnboardValidator : AbstractValidator<EmployeeDto>
{
    public EmployeeOnboardValidator()
    {
        RuleFor(x => x.FullName)
            .NotEmpty().WithMessage("Họ tên không được để trống.")
            .MaximumLength(200);

        RuleFor(x => x.PersonalPhone)
            .NotEmpty().WithMessage("Số điện thoại không được để trống.")
            .MaximumLength(20);

        RuleFor(x => x.PersonalEmail)
            .EmailAddress().When(x => !string.IsNullOrWhiteSpace(x.PersonalEmail))
            .WithMessage("Email không hợp lệ.");
    }
}
