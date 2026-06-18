using FluentValidation;
using NamViet.ERP.Api.Application.Dtos.Hr;

namespace NamViet.ERP.Api.Application.Validators;

public sealed class CandidateCreateValidator : AbstractValidator<CandidateDto>
{
    public CandidateCreateValidator()
    {
        RuleFor(x => x.FullName)
            .NotEmpty().WithMessage("Họ tên không được để trống.")
            .MaximumLength(200);

        RuleFor(x => x.Phone)
            .NotEmpty().WithMessage("Số điện thoại không được để trống.")
            .MaximumLength(20);

        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("Email không được để trống.")
            .EmailAddress().WithMessage("Email không hợp lệ.");
    }
}
