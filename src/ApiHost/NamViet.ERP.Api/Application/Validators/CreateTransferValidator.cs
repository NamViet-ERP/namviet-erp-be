using FluentValidation;
using NamViet.ERP.Api.Application.Dtos.Wms;

namespace NamViet.ERP.Api.Application.Validators;

public sealed class CreateTransferValidator : AbstractValidator<CreateTransferDto>
{
    public CreateTransferValidator()
    {
        RuleFor(x => x.Project).NotEmpty().WithMessage("Vui lòng chọn kho dự án nguồn.");
        RuleFor(x => x.Items).NotEmpty().WithMessage("Phiếu chuyển phải có ít nhất một dòng vật tư.");
        RuleForEach(x => x.Items).ChildRules(item =>
        {
            item.RuleFor(i => i.Qty).GreaterThan(0).WithMessage("Số lượng phải lớn hơn 0.");
            item.RuleFor(i => i).Must(i =>
                !string.IsNullOrWhiteSpace(i.Sku) || !string.IsNullOrWhiteSpace(i.ProductId))
                .WithMessage("Mỗi dòng phải có mã vật tư.");
        });
    }
}
