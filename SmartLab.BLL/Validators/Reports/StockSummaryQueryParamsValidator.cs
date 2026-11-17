using FluentValidation;
using SmartLab.BLL.Constants;
using SmartLab.BLL.DTOs.Reports;

namespace SmartLab.BLL.Validators.Reports;

public class StockSummaryQueryParamsValidator : AbstractValidator<StockSummaryQueryParams>
{
    public StockSummaryQueryParamsValidator()
    {
        RuleFor(x => x.TrackingType)
            .Must(t => ComponentTrackingTypes.All.Contains(t!.Trim().ToUpper()))
            .When(x => !string.IsNullOrWhiteSpace(x.TrackingType))
            .WithMessage("Loại linh kiện phải là CONSUMABLE, REUSABLE hoặc HIGH_VALUE");
    }
}
