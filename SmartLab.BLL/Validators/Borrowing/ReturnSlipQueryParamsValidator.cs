using FluentValidation;
using SmartLab.BLL.Constants;
using SmartLab.BLL.DTOs.Borrowing;

namespace SmartLab.BLL.Validators.Borrowing;

public class ReturnSlipQueryParamsValidator : AbstractValidator<ReturnSlipQueryParams>
{
    public ReturnSlipQueryParamsValidator()
    {
        RuleFor(x => x.Status)
            .Must(s => ReturnSlipStatuses.Normalize(s) != null)
            .When(x => !string.IsNullOrWhiteSpace(x.Status))
            .WithMessage($"Trạng thái phải là một trong: {string.Join(", ", ReturnSlipStatuses.All)}");

        RuleFor(x => x.ToDate)
            .Must((x, toDate) => x.FromDate!.Value.Date <= toDate!.Value.Date)
            .When(x => x.FromDate.HasValue && x.ToDate.HasValue)
            .WithMessage("Đến ngày phải sau hoặc bằng từ ngày");
    }
}
