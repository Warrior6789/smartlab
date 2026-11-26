using FluentValidation;
using SmartLab.BLL.DTOs.Reports;

namespace SmartLab.BLL.Validators.Reports;

public class WarrantyQueryParamsValidator : AbstractValidator<WarrantyQueryParams>
{
    public WarrantyQueryParamsValidator()
    {
        RuleFor(x => x.Days)
            .InclusiveBetween(1, 365).WithMessage("Số ngày phải từ 1 đến 365");
    }
}
