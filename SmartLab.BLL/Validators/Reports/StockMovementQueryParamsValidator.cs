using FluentValidation;
using SmartLab.BLL.DTOs.Reports;

namespace SmartLab.BLL.Validators.Reports;

public class StockMovementQueryParamsValidator : AbstractValidator<StockMovementQueryParams>
{
    public StockMovementQueryParamsValidator()
    {
        RuleFor(x => x.ComponentId)
            .NotEmpty().WithMessage("Phải chọn linh kiện (componentId)");

        RuleFor(x => x.ToDate)
            .Must((x, toDate) => x.FromDate!.Value.Date <= toDate!.Value.Date)
            .When(x => x.FromDate.HasValue && x.ToDate.HasValue)
            .WithMessage("Đến ngày phải sau hoặc bằng từ ngày");
    }
}
