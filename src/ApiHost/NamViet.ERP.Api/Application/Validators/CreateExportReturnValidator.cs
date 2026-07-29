using FluentValidation;
using NamViet.ERP.Api.Application.Dtos.Wms;

namespace NamViet.ERP.Api.Application.Validators;

public sealed class CreateExportReturnValidator : AbstractValidator<CreateExportReturnDto>
{
    public CreateExportReturnValidator()
    {
        RuleFor(x => x.SourceExportId).NotEmpty().WithMessage("Vui lòng chọn phiếu xuất nguồn.");
        RuleFor(x => x.Note)
            .NotEmpty().WithMessage("Vui lòng nhập lý do hoàn trả.")
            .MinimumLength(3).WithMessage("Lý do hoàn trả phải có ít nhất 3 ký tự.")
            .MaximumLength(1000).WithMessage("Lý do hoàn trả tối đa 1000 ký tự.");
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
