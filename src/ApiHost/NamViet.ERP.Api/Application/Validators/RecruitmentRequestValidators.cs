using FluentValidation;
using NamViet.ERP.Api.Application.Dtos.Hr;

namespace NamViet.ERP.Api.Application.Validators;

public static class RecruitmentValidationLimits
{
    public const int MinimumYear = 2000;
    public const int MaximumYear = 2100;
    public const int MaximumDurationDays = 3660;
}

public sealed class CreateRecruitmentPlanRequestValidator
    : AbstractValidator<CreateRecruitmentPlanRequest>
{
    public CreateRecruitmentPlanRequestValidator() => AddPlanRules(this);

    internal static void AddPlanRules<T>(AbstractValidator<T> validator)
        where T : CreateRecruitmentPlanRequest
    {
        validator.RuleFor(x => x.PositionName).NotEmpty().MaximumLength(255);
        validator.RuleFor(x => x.DepartmentName).MaximumLength(255);
        validator.RuleFor(x => x)
            .Must(x => x.DepartmentId is > 0 || !string.IsNullOrWhiteSpace(x.DepartmentName))
            .WithMessage("Vui lòng chọn phòng ban (departmentId hoặc departmentName).");
        validator.RuleFor(x => x.Headcount).InclusiveBetween(1, 10_000);
        validator.RuleFor(x => x.Reason).MaximumLength(2_000);
        validator.RuleFor(x => x.Period).MaximumLength(100);
        validator.RuleFor(x => x.Status)
            .Must(status => status is "Draft" or "Active" or "Closed")
            .WithMessage("Trạng thái phải là Draft, Active hoặc Closed.");
        validator.RuleFor(x => x.OpenDate)
            .Must(BeEmptyOrValidDate).WithMessage("Ngày mở không hợp lệ.");
        validator.RuleFor(x => x.DeadlineDate)
            .Must(BeEmptyOrValidDate).WithMessage("Hạn tuyển không hợp lệ.");
        validator.RuleFor(x => x)
            .Must(HaveValidDateOrder)
            .WithMessage("Hạn tuyển không được trước ngày mở.");
    }

    private static bool BeEmptyOrValidDate(string? value) =>
        string.IsNullOrWhiteSpace(value) || RecruitmentRequestDate.TryParse(value, out _);

    private static bool HaveValidDateOrder(CreateRecruitmentPlanRequest request)
    {
        if (!RecruitmentRequestDate.TryParse(request.OpenDate, out var open)
            || !RecruitmentRequestDate.TryParse(request.DeadlineDate, out var deadline))
            return true;

        return deadline >= open;
    }
}

public sealed class UpdateRecruitmentPlanRequestValidator
    : AbstractValidator<UpdateRecruitmentPlanRequest>
{
    public UpdateRecruitmentPlanRequestValidator()
    {
        RuleFor(x => x.PositionName).NotEmpty().MaximumLength(255);
        RuleFor(x => x.DepartmentName).MaximumLength(255);
        RuleFor(x => x)
            .Must(x => x.DepartmentId is > 0 || !string.IsNullOrWhiteSpace(x.DepartmentName))
            .WithMessage("Vui lòng chọn phòng ban (departmentId hoặc departmentName).");
        RuleFor(x => x.Headcount).InclusiveBetween(1, 10_000);
        RuleFor(x => x.Reason).MaximumLength(2_000);
        RuleFor(x => x.Period).MaximumLength(100);
        RuleFor(x => x.Status)
            .Must(status => status is "Draft" or "Active" or "Closed")
            .WithMessage("Trạng thái phải là Draft, Active hoặc Closed.");
        RuleFor(x => x.OpenDate)
            .Must(BeEmptyOrValidDate).WithMessage("Ngày mở không hợp lệ.");
        RuleFor(x => x.DeadlineDate)
            .Must(BeEmptyOrValidDate).WithMessage("Hạn tuyển không hợp lệ.");
        RuleFor(x => x)
            .Must(HaveValidDateOrder)
            .WithMessage("Hạn tuyển không được trước ngày mở.");
    }

    private static bool BeEmptyOrValidDate(string? value) =>
        string.IsNullOrWhiteSpace(value) || RecruitmentRequestDate.TryParse(value, out _);

    private static bool HaveValidDateOrder(UpdateRecruitmentPlanRequest request)
    {
        if (!RecruitmentRequestDate.TryParse(request.OpenDate, out var open)
            || !RecruitmentRequestDate.TryParse(request.DeadlineDate, out var deadline))
            return true;

        return deadline >= open;
    }
}

public sealed class CreateRecruitmentCostRequestValidator
    : AbstractValidator<CreateRecruitmentCostRequest>
{
    public CreateRecruitmentCostRequestValidator()
    {
        RuleFor(x => x.Source).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Period).NotEmpty().MaximumLength(50);
        RuleFor(x => x.PositionName).MaximumLength(255);
        RuleFor(x => x.Note).MaximumLength(4_000);
        RuleFor(x => x.Amount)
            .Must(MoneyValueLimits.IsValidNumeric18Scale2)
            .WithMessage($"Số tiền phải từ 0 đến {MoneyValueLimits.Numeric18Scale2Max}.");
        AddOptionalPeriodRules(this);
    }

    internal static void AddOptionalPeriodRules<T>(AbstractValidator<T> validator)
        where T : CreateRecruitmentCostRequest
    {
        validator.RuleFor(x => x.DurationDays)
            .InclusiveBetween(1, RecruitmentValidationLimits.MaximumDurationDays)
            .When(x => x.DurationDays.HasValue);
        validator.RuleFor(x => x.PaymentYear)
            .InclusiveBetween(
                RecruitmentValidationLimits.MinimumYear,
                RecruitmentValidationLimits.MaximumYear)
            .When(x => x.PaymentYear.HasValue);
        validator.RuleFor(x => x.PaymentMonth)
            .InclusiveBetween(1, 12)
            .When(x => x.PaymentMonth.HasValue);
        validator.RuleFor(x => x.PaymentMonth)
            .Null()
            .When(x => !x.PaymentYear.HasValue)
            .WithMessage("Tháng thanh toán yêu cầu năm thanh toán.");
    }
}

public sealed class UpdateRecruitmentCostRequestValidator
    : AbstractValidator<UpdateRecruitmentCostRequest>
{
    public UpdateRecruitmentCostRequestValidator()
    {
        RuleFor(x => x.Source).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Period).NotEmpty().MaximumLength(50);
        RuleFor(x => x.PositionName).MaximumLength(255);
        RuleFor(x => x.Note).MaximumLength(4_000);
        RuleFor(x => x.Amount)
            .Must(MoneyValueLimits.IsValidNumeric18Scale2)
            .WithMessage($"Số tiền phải từ 0 đến {MoneyValueLimits.Numeric18Scale2Max}.");
        RuleFor(x => x.DurationDays)
            .InclusiveBetween(1, RecruitmentValidationLimits.MaximumDurationDays)
            .When(x => x.DurationDays.HasValue);
        RuleFor(x => x.PaymentYear)
            .InclusiveBetween(
                RecruitmentValidationLimits.MinimumYear,
                RecruitmentValidationLimits.MaximumYear)
            .When(x => x.PaymentYear.HasValue);
        RuleFor(x => x.PaymentMonth)
            .InclusiveBetween(1, 12)
            .When(x => x.PaymentMonth.HasValue);
        RuleFor(x => x.PaymentMonth)
            .Null()
            .When(x => !x.PaymentYear.HasValue)
            .WithMessage("Tháng thanh toán yêu cầu năm thanh toán.");
    }
}

public sealed class UpdateRecruitmentBudgetRequestValidator
    : AbstractValidator<UpdateRecruitmentBudgetRequest>
{
    public UpdateRecruitmentBudgetRequestValidator()
    {
        RuleFor(x => x.Year).InclusiveBetween(
            RecruitmentValidationLimits.MinimumYear,
            RecruitmentValidationLimits.MaximumYear);
        RuleFor(x => x.TotalAllocated)
            .Must(MoneyValueLimits.IsValidNumeric18Scale2)
            .WithMessage($"Ngân sách phải từ 0 đến {MoneyValueLimits.Numeric18Scale2Max}.");
    }
}

public sealed class RecruitmentDateRangeQueryValidator
    : AbstractValidator<RecruitmentDateRangeQuery>
{
    public RecruitmentDateRangeQueryValidator()
    {
        RuleFor(x => x)
            .Must(x => !x.DateFrom.HasValue || !x.DateTo.HasValue || x.DateFrom <= x.DateTo)
            .WithMessage("Ngày bắt đầu không được sau ngày kết thúc.");
    }
}

public sealed class RecruitmentTrendQueryValidator : AbstractValidator<RecruitmentTrendQuery>
{
    public RecruitmentTrendQueryValidator()
    {
        RuleFor(x => x.Months).InclusiveBetween(1, 24);
    }
}

public sealed class RecruitmentCostListQueryValidator : AbstractValidator<RecruitmentCostListQuery>
{
    public RecruitmentCostListQueryValidator()
    {
        RuleFor(x => x.Source).MaximumLength(100);
        RuleFor(x => x.Position).MaximumLength(255);
        RuleFor(x => x.Year)
            .InclusiveBetween(
                RecruitmentValidationLimits.MinimumYear,
                RecruitmentValidationLimits.MaximumYear)
            .When(x => x.Year.HasValue);
        RuleFor(x => x.Month)
            .InclusiveBetween(1, 12)
            .When(x => x.Month.HasValue);
    }
}

public sealed class RecruitmentYearQueryValidator : AbstractValidator<RecruitmentYearQuery>
{
    public RecruitmentYearQueryValidator()
    {
        RuleFor(x => x.Year)
            .InclusiveBetween(
                RecruitmentValidationLimits.MinimumYear,
                RecruitmentValidationLimits.MaximumYear)
            .When(x => x.Year.HasValue);
    }
}

internal static class RecruitmentRequestDate
{
    public static bool TryParse(string? value, out DateOnly date)
    {
        date = default;
        if (string.IsNullOrWhiteSpace(value)) return false;

        var formats = new[] { "yyyy-MM-dd", "dd/MM/yyyy" };
        return DateOnly.TryParseExact(
            value.Trim(),
            formats,
            System.Globalization.CultureInfo.InvariantCulture,
            System.Globalization.DateTimeStyles.None,
            out date);
    }
}
