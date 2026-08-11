using FluentValidation;
using NamViet.ERP.Api.Application.Dtos.Wms;

namespace NamViet.ERP.Api.Application.Validators;

public sealed class CreateImportValidator : AbstractValidator<CreateImportDto>
{
    public CreateImportValidator()
    {
        RuleFor(x => x.Project).NotEmpty().WithMessage("Vui lòng chọn kho dự án.");
        RuleFor(x => x.Items).NotEmpty().WithMessage("Phiếu nhập phải có ít nhất một dòng vật tư.");
        RuleForEach(x => x.Items).ChildRules(item =>
        {
            item.RuleFor(i => i.Qty).GreaterThan(0).WithMessage("Số lượng phải lớn hơn 0.");
            // Allow 0 on the wire for thủ kho (BE preserves DB prices). Create/update services enforce > 0 when required.
            item.RuleFor(i => i.UnitPrice).GreaterThanOrEqualTo(0).WithMessage("Đơn giá không được âm.");
            item.RuleFor(i => i).Must(i =>
                !string.IsNullOrWhiteSpace(i.Sku) || !string.IsNullOrWhiteSpace(i.ProductId))
                .WithMessage("Mỗi dòng phải có mã vật tư.");
        });
    }
}
