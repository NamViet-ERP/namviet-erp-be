using FluentValidation;
using NamViet.ERP.Api.Application.Dtos.Purchasing;

namespace NamViet.ERP.Api.Application.Validators;

public sealed class CreatePurchaseOrderRequestValidator : AbstractValidator<CreatePurchaseOrderRequest>
{
    public CreatePurchaseOrderRequestValidator()
    {
        RuleForEach(x => x.Lines).ChildRules(line =>
        {
            line.RuleFor(l => l.UnitPrice)
                .Must(MoneyValueLimits.IsValidNumeric18Scale2)
                .WithMessage($"Đơn giá phải từ 0 đến {MoneyValueLimits.Numeric18Scale2Max}.");
            line.RuleFor(l => l.Qty * l.UnitPrice)
                .Must(MoneyValueLimits.IsValidNumeric18Scale2)
                .WithMessage($"Thành tiền dòng phải từ 0 đến {MoneyValueLimits.Numeric18Scale2Max}.");
        });
    }
}

public sealed class UpdatePurchaseOrderRequestValidator : AbstractValidator<UpdatePurchaseOrderRequest>
{
    public UpdatePurchaseOrderRequestValidator()
    {
        RuleForEach(x => x.Lines).ChildRules(line =>
        {
            line.RuleFor(l => l.UnitPrice)
                .Must(MoneyValueLimits.IsValidNumeric18Scale2)
                .WithMessage($"Đơn giá phải từ 0 đến {MoneyValueLimits.Numeric18Scale2Max}.");
            line.RuleFor(l => l.Qty * l.UnitPrice)
                .Must(MoneyValueLimits.IsValidNumeric18Scale2)
                .WithMessage($"Thành tiền dòng phải từ 0 đến {MoneyValueLimits.Numeric18Scale2Max}.");
        });
    }
}

public sealed class CreatePurchasePaymentRequestValidator : AbstractValidator<CreatePurchasePaymentRequest>
{
    public CreatePurchasePaymentRequestValidator()
    {
        RuleFor(x => x.Amount)
            .Must(v => v is null || MoneyValueLimits.IsValidNumeric18Scale2(v.Value))
            .WithMessage($"Số tiền phải từ 0 đến {MoneyValueLimits.Numeric18Scale2Max}.");
    }
}

public sealed class CreatePurchasePlanRequestValidator : AbstractValidator<CreatePurchasePlanRequest>
{
    public CreatePurchasePlanRequestValidator()
    {
        When(x => x.Lines is { Count: > 0 }, () =>
        {
            RuleForEach(x => x.Lines!).ChildRules(line =>
            {
                AddPlanLineMoneyRules(line);
            });
        });
    }

    internal static void AddPlanLineMoneyRules(
        InlineValidator<CreatePurchasePlanLineRequest> line)
    {
        line.RuleFor(l => l.BudgetUnitPrice)
            .Must(v => v is null || MoneyValueLimits.IsValidNumeric18Scale2(v.Value))
            .WithMessage($"Đơn giá dự toán phải từ 0 đến {MoneyValueLimits.Numeric18Scale2Max}.");
        line.RuleFor(l => l.BudgetAmount)
            .Must(v => v is null || MoneyValueLimits.IsValidNumeric18Scale2(v.Value))
            .WithMessage($"Thành tiền dự toán phải từ 0 đến {MoneyValueLimits.Numeric18Scale2Max}.");
        line.RuleFor(l => l.ProposalUnitPrice)
            .Must(v => v is null || MoneyValueLimits.IsValidNumeric18Scale2(v.Value))
            .WithMessage($"Đơn giá đề xuất phải từ 0 đến {MoneyValueLimits.Numeric18Scale2Max}.");
        line.RuleFor(l => l.ProposalAmount)
            .Must(v => v is null || MoneyValueLimits.IsValidNumeric18Scale2(v.Value))
            .WithMessage($"Thành tiền đề xuất phải từ 0 đến {MoneyValueLimits.Numeric18Scale2Max}.");
        line.RuleFor(l => l.PurchaseUnitPrice)
            .Must(v => v is null || MoneyValueLimits.IsValidNumeric18Scale2(v.Value))
            .WithMessage($"Đơn giá mua phải từ 0 đến {MoneyValueLimits.Numeric18Scale2Max}.");
        line.RuleFor(l => l.PurchaseAmount)
            .Must(v => v is null || MoneyValueLimits.IsValidNumeric18Scale2(v.Value))
            .WithMessage($"Thành tiền mua phải từ 0 đến {MoneyValueLimits.Numeric18Scale2Max}.");
        line.RuleFor(l => l.SelectedUnitPrice)
            .Must(v => v is null || MoneyValueLimits.IsValidNumeric18Scale2(v.Value))
            .WithMessage($"Đơn giá chọn phải từ 0 đến {MoneyValueLimits.Numeric18Scale2Max}.");
        line.RuleFor(l => l.SelectedAmount)
            .Must(v => v is null || MoneyValueLimits.IsValidNumeric18Scale2(v.Value))
            .WithMessage($"Thành tiền chọn phải từ 0 đến {MoneyValueLimits.Numeric18Scale2Max}.");
    }
}

public sealed class UpdatePurchasePlanRequestValidator : AbstractValidator<UpdatePurchasePlanRequest>
{
    public UpdatePurchasePlanRequestValidator()
    {
        When(x => x.Lines is { Count: > 0 }, () =>
        {
            RuleForEach(x => x.Lines!).ChildRules(line =>
            {
                CreatePurchasePlanRequestValidator.AddPlanLineMoneyRules(line);
            });
        });
    }
}
