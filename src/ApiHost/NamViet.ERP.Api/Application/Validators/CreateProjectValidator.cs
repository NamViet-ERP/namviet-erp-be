using FluentValidation;
using NamViet.ERP.Modules.Warehouse.Application.DTOs;

namespace NamViet.ERP.Api.Application.Validators;

public sealed class CreateProjectValidator : AbstractValidator<CreateProjectRequest>
{
    public CreateProjectValidator()
    {
        RuleFor(x => x.ProjectCode)
            .NotEmpty().WithMessage("Vui lòng nhập mã kho dự án")
            .MaximumLength(50).WithMessage("Mã kho dự án không được vượt quá 50 ký tự");

        RuleFor(x => x.ProjectName)
            .NotEmpty().WithMessage("Vui lòng nhập tên công trình")
            .MaximumLength(255).WithMessage("Tên công trình không được vượt quá 255 ký tự");

        RuleFor(x => x.StartDate)
            .Must(d => d != default).WithMessage("Vui lòng chọn ngày bắt đầu");

        RuleFor(x => x.MaterialBudget)
            .GreaterThan(0).WithMessage("Vui lòng nhập ngân sách vật tư")
            .LessThanOrEqualTo(WmsValueLimits.MaxMaterialBudget)
            .WithMessage("Ngân sách vật tư vượt quá giới hạn cho phép (tối đa 9.999.999.999.999.999 đ).");
    }
}
