using FluentValidation;
using SmartLab.BLL.DTOs.Reports;

namespace SmartLab.BLL.Validators.Reports;

public class TopComponentQueryParamsValidator : AbstractValidator<TopComponentQueryParams>
{
    public TopComponentQueryParamsValidator()
    {
        RuleFor(x => x.Top)
            .InclusiveBetween(1, 50).WithMessage("Top phải từ 1 đến 50");

        RuleFor(x => x.ToDate)
            .Must((x, toDate) => x.FromDate!.Value.Date <= toDate!.Value.Date)
            .When(x => x.FromDate.HasValue && x.ToDate.HasValue)
            .WithMessage("Đến ngày phải sau hoặc bằng từ ngày");
    }
}
