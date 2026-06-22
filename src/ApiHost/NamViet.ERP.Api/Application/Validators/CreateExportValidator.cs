using FluentValidation;
using NamViet.ERP.Api.Application.Dtos.Wms;

namespace NamViet.ERP.Api.Application.Validators;

public sealed class CreateExportValidator : AbstractValidator<CreateExportDto>
{
    public CreateExportValidator()
    {
        RuleFor(x => x.Project).NotEmpty().WithMessage("Vui lòng chọn kho dự án xuất vật tư.");
        RuleFor(x => x.Items).NotEmpty().WithMessage("Phiếu xuất phải có ít nhất một dòng vật tư.");
        RuleForEach(x => x.Items).ChildRules(item =>
        {
            item.RuleFor(i => i.Qty).GreaterThan(0).WithMessage("Số lượng phải lớn hơn 0.");
            item.RuleFor(i => i).Must(i =>
                !string.IsNullOrWhiteSpace(i.Sku) || !string.IsNullOrWhiteSpace(i.ProductId))
                .WithMessage("Mỗi dòng phải có mã vật tư.");
        });
    }
}
