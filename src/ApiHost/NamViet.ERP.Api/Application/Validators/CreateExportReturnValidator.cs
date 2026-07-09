using FluentValidation;
using NamViet.ERP.Api.Application.Dtos.Wms;

namespace NamViet.ERP.Api.Application.Validators;

public sealed class CreateExportReturnValidator : AbstractValidator<CreateExportReturnDto>
{
    public CreateExportReturnValidator()
    {
        RuleFor(x => x.SourceExportId).NotEmpty().WithMessage("Vui lòng chọn phiếu xuất nguồn.");
        RuleFor(x => x.Items).NotEmpty().WithMessage("Phiếu hoàn trả phải có ít nhất một dòng vật tư.");
        RuleForEach(x => x.Items).ChildRules(item =>
        {
            item.RuleFor(i => i.Qty).GreaterThan(0).WithMessage("Số lượng phải lớn hơn 0.");
            item.RuleFor(i => i).Must(i =>
                !string.IsNullOrWhiteSpace(i.Sku) || !string.IsNullOrWhiteSpace(i.ProductId))
                .WithMessage("Mỗi dòng phải có mã vật tư.");
        });
    }
}
