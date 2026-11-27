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
            .WithMessage("Loại linh kiện phải là Tiêu hao, Tái sử dụng hoặc Giá trị cao");

        RuleFor(x => x.Status)
            .Must(s => StockStatusFilters.Normalize(s) != null)
            .When(x => !string.IsNullOrWhiteSpace(x.Status))
            .WithMessage("Trạng thái tồn phải là Stocked, OutOfStock hoặc LowStock");
    }
}
